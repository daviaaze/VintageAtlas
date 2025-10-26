using System;
using System.Threading;
using Microsoft.Data.Sqlite;
using Vintagestory.API.Common;

namespace VintageAtlas.Export.DataSources;

/// <summary>
/// Thread-safe connection pool for SQLite database access.
/// Manages a pool of connections and provides IDisposable lease objects for automatic cleanup.
/// </summary>
public sealed class SqliteConnectionPool : IDisposable
{
    private readonly SqliteConnectionLease[] _connections;
    private readonly object _poolLock = new();
    private readonly ILogger _logger;
    private bool _disposed;

    /// <summary>
    /// Creates a new connection pool with the specified number of connections.
    /// </summary>
    /// <param name="connectionString">SQLite connection string</param>
    /// <param name="poolSize">Number of connections in the pool</param>
    /// <param name="logger">Logger for diagnostics</param>
    public SqliteConnectionPool(string connectionString, int poolSize, ILogger logger)
    {
        if (poolSize <= 0)
        {
            throw new ArgumentException($"Invalid pool size: {poolSize}. Must be at least 1.", nameof(poolSize));
        }

        _logger = logger;
        _connections = new SqliteConnectionLease[poolSize];

        for (var i = 0; i < poolSize; i++)
        {
            try
            {
                var connection = new SqliteConnection(connectionString);
                connection.Open();
                _connections[i] = new SqliteConnectionLease(connection, this);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to create SQLite connection {i + 1}/{poolSize}: {ex.Message}");

                // Clean up any connections we've already created
                for (var j = 0; j < i; j++)
                {
                    _connections[j]?.Dispose();
                }

                throw;
            }
        }

        _logger.Notification($"[VintageAtlas] Created SQLite connection pool with {poolSize} connections");
    }

    /// <summary>
    /// Acquires a connection from the pool. Blocks until one is available.
    /// The connection is automatically returned to the pool when disposed.
    /// </summary>
    /// <returns>A disposable connection lease</returns>
    public SqliteConnectionLease AcquireConnection()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_poolLock)
        {
            while (true)
            {
                foreach (var lease in _connections)
                {
                    if (!lease.InUse)
                    {
                        lease.InUse = true;
                        return lease;
                    }
                }

                // No free connections, wait and retry
                _logger.Debug("[VintageAtlas] Waiting for free SQLite connection...");
                Monitor.Wait(_poolLock, TimeSpan.FromSeconds(5));
            }
        }
    }

    /// <summary>
    /// Internal method called by SqliteConnectionLease to return the connection to the pool.
    /// </summary>
    internal void ReleaseConnection()
    {
        lock (_poolLock)
        {
            Monitor.Pulse(_poolLock); // Wake up any waiting threads
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_poolLock)
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var lease in _connections)
            {
                lease?.DisposeConnection();
            }
        }
    }
}

/// <summary>
/// Represents a leased connection from the pool.
/// Automatically returns the connection to the pool when disposed.
/// Thread-safe: all operations on the underlying connection are protected by a lock.
/// </summary>
public sealed class SqliteConnectionLease : IDisposable
{
    private readonly SqliteConnectionPool _pool;
    private readonly object _connectionLock = new();
    private bool _disposed;

    internal bool InUse { get; set; }

    /// <summary>
    /// The underlying SQLite connection.
    /// All operations should be performed within a lock on this lease.
    /// </summary>
    public SqliteConnection Connection { get; }

    internal SqliteConnectionLease(SqliteConnection connection, SqliteConnectionPool pool)
    {
        Connection = connection;
        _pool = pool;
        InUse = false;
    }

    /// <summary>
    /// Executes an action with the connection, protected by a lock.
    /// </summary>
    public T Execute<T>(System.Func<SqliteConnection, T> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_connectionLock)
        {
            return action(Connection);
        }
    }

    /// <summary>
    /// Executes an action with the connection, protected by a lock.
    /// </summary>
    public void Execute(System.Action<SqliteConnection> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_connectionLock)
        {
            action(Connection);
        }
    }

    /// <summary>
    /// Returns the connection to the pool (for reuse).
    /// This does NOT close the underlying connection.
    /// </summary>
    public void Dispose()
    {
        // Note: We don't set _disposed here because the lease is reused
        // The InUse flag is what matters for the pool
        lock (_connectionLock)
        {
            if (!InUse) return; // Already returned

            InUse = false;
            _pool?.ReleaseConnection();
        }
    }

    /// <summary>
    /// Called by the pool when disposing - actually closes the underlying connection.
    /// </summary>
    internal void DisposeConnection()
    {
        lock (_connectionLock)
        {
            _disposed = true;
            Connection?.Dispose();
        }
    }
}

