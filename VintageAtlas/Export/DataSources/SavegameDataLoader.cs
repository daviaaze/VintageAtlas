using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common;
using Vintagestory.Common.Database;
using Vintagestory.Server;

namespace VintageAtlas.Export;

public sealed class SavegameDataLoader : IDisposable
{
    private readonly SqliteThreadCon[] _sqliteConnections;

    private readonly ChunkDataPool _chunkDataPool;

    private readonly ServerMain _server;
    private readonly object _chunkTable = new();
    private readonly ILogger _logger;

    public SavegameDataLoader(ServerMain server, int workers, ILogger modLogger)
    {
        _logger = modLogger;
        _chunkDataPool = new ChunkDataPool(MagicNum.ServerChunkSize, server);
        _server = server;
        _sqliteConnections = GetSqlite(_server, workers);
    }

    internal SqliteThreadCon SqliteThreadConn
    {
        get
        {
            lock (_sqliteConnections)
            {
                while (true)
                {
                    foreach (var conn in _sqliteConnections)
                    {
                        if (conn.InUse) continue;
                        conn.InUse = true;

                        return conn;
                    }
                    Thread.Sleep(500);
                    _logger.Notification("Could not find a free sqlite connection for a worker thread. Waiting...");
                }
            }
        }
    }

    private SqliteThreadCon[] GetSqlite(ServerMain server, int workers)
    {
        // If workers is -1, use processor count
        if (workers == -1)
        {
            workers = Environment.ProcessorCount;
            _logger.Notification($"Auto-detected {workers} processor cores, using that for worker count.");
        }

        var connectionString = new DbConnectionStringBuilder
        {
            { "Data Source", server.Config.WorldConfig.SaveFileLocation },
            { "Pooling", "false" },
            { "Mode", "ReadOnly" }
        }.ToString();

        // + 1 for extract structures where SavegameDataLoader.GetAllServerMapRegions needs one since it does yield return the entire time
        workers++;

        if (workers <= 0)
        {
            _logger.Error($"Invalid worker count: {workers}. Must be at least 1.");
            throw new ArgumentException($"Invalid worker count: {workers}. Must be at least 1.", nameof(workers));
        }

        var sqliteConnections = new SqliteThreadCon[workers];
        for (var i = 0; i < sqliteConnections.Length; i++)
        {
            try
            {
                var sqliteConnection = new SqliteConnection(connectionString);
                sqliteConnection.Open();
                sqliteConnections[i] = new SqliteThreadCon(sqliteConnection);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to create SQLite connection {i + 1}/{workers}: {ex.Message}");
                _logger.Error($"Connection string: {connectionString}");
                _logger.Error($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        _logger.Notification($"Created {sqliteConnections.Length} sqlite connections.");
        return sqliteConnections;
    }

    public IEnumerable<ChunkPos> GetAllMapChunkPositions(SqliteThreadCon sqliteConn)
    {
        using var cmd = sqliteConn.Con.CreateCommand();

        cmd.CommandText = "SELECT position FROM mapchunk";
        using var sqliteDataReader = cmd.ExecuteReader();
        var posList = new List<ChunkPos>();
        while (sqliteDataReader.Read())
        {
            var pos = (long)sqliteDataReader["position"];
            posList.Add(ChunkPos.FromChunkIndex_saveGamev2((ulong)pos));
        }

        return posList;
    }

    private static SqliteDataReader? ReadReaderSafely(SqliteCommand cmd)
    {
        try
        {
            return cmd.ExecuteReader();
        }
        catch (Exception e)
        {
            ServerMain.Logger.Error(e);
            return null;
        }
    }

    private static bool ExecuteReaderSafely(SqliteDataReader reader)
    {
        try
        {
            return reader.Read();
        }
        catch (Exception e)
        {
            ServerMain.Logger.Error(e);
            return false;
        }
    }

    public void SaveMapChunks(IDictionary<long, ServerMapChunk> toSave)
    {
        var sqliteConn = SqliteThreadConn;
        lock (sqliteConn.Con)
        {
            try
            {
                using var transaction = sqliteConn.Con.BeginTransaction();
                sqliteConn.SaveChunksCmd.Transaction = transaction;
                foreach (var c in toSave)
                {
                    sqliteConn.SaveChunksCmd.Parameters["position"].Value = c.Key;
                    sqliteConn.SaveChunksCmd.Parameters["data"].Value = c.Value.ToBytes();
                    sqliteConn.SaveChunksCmd.ExecuteNonQuery();
                }

                transaction.Commit();
                sqliteConn.Free();
            }
            catch (Exception e)
            {
                _logger.Error("VintageAtlas error while saving MapChunks: ");
                _logger.Error(e.Message);
                sqliteConn.Free();
            }
        }
    }

    private ServerMapChunk? GetServerMapChunk(SqliteThreadCon sqliteConn, ulong position)
    {
        sqliteConn.GetMapChunk.Parameters["position"].Value = position;
        using var dataReader = sqliteConn.GetMapChunk.ExecuteReader();
        try
        {
            if (dataReader.Read())
            {
                var bytes = dataReader["data"] as byte[];

                var serverMapChunk = ServerMapChunk.FromBytes(bytes);
                return serverMapChunk;
            }
        }
        catch (Exception e)
        {
            _logger.Error("VintageAtlas error while reading ServerMapChunk: ");
            _logger.Error(e);
        }

        return null;
    }

    public ServerMapChunk? GetServerMapChunk(SqliteThreadCon sqliteConn, ChunkPos position)
    {
        var pos = ChunkPos.ToChunkIndex(position.X, position.Y, position.Z);
        return GetServerMapChunk(sqliteConn, pos);
    }

    public ServerChunk? GetServerChunk(SqliteThreadCon sqliteConn, ulong position)
    {
        try
        {
            sqliteConn.GetChunk.Parameters["position"].Value = position;
            using var dataReader = sqliteConn.GetChunk.ExecuteReader();

            if (dataReader.Read())
            {
                var bytes = dataReader["data"] as byte[];
                var serverMapChunk = ServerChunk.FromBytes(bytes, _chunkDataPool, _server);

                return serverMapChunk;
            }
        }
        catch (Exception e)
        {
            _logger.Error("VintageAtlas error while saving ServerChunk: ");
            _logger.Error(e.Message);
        }

        return null;
    }

    public ServerChunk? GetServerChunk(SqliteThreadCon sqliteConn, ChunkPos position)
    {
        var pos = ChunkPos.ToChunkIndex(position.X, position.Y, position.Z);
        lock (_chunkTable)
        {
            return GetServerChunk(sqliteConn, pos);
        }
    }

    public ServerChunk? GetServerChunk(SqliteThreadCon sqliteConn, Vec3i position)
    {
        var pos = ChunkPos.ToChunkIndex(position.X, position.Y, position.Z);
        return GetServerChunk(sqliteConn, pos);
    }

    public SaveGame? GetGameData(SqliteThreadCon sqliteConn)
    {
        try
        {
            using var cmd = sqliteConn.Con.CreateCommand();
            cmd.CommandText = "SELECT data FROM gamedata LIMIT 1";
            using var sqliteDataReader = cmd.ExecuteReader();
            if (!sqliteDataReader.Read())
                return null;

            if (sqliteDataReader["data"] is not byte[] data)
                return null;

            var saveGame = Serializer.Deserialize<SaveGame>(new MemoryStream(data));
            return saveGame;

        }
        catch (Exception e)
        {
            _logger.Error("Exception thrown on GetGameData: ");
            _logger.Error(e.Message);
            return null;
        }
    }

    public static DbParameter CreateParameter(string parameterName, DbType dbType, object? value, DbCommand command)
    {
        var dbParameter = command.CreateParameter();
        dbParameter.ParameterName = parameterName;
        dbParameter.DbType = dbType;
        dbParameter.Value = value;
        return dbParameter;
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (!disposing) 
            return;
        
        foreach (var con in _sqliteConnections)
        {
            con.Con.Dispose();
        }

        _chunkDataPool.FreeAll();
    }
}