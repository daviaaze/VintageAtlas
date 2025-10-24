using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common;
using Vintagestory.Common.Database;
using Vintagestory.Server;

namespace VintageAtlas.Export.DataSources.Repositories;

/// <summary>
/// Thread-safe repository for accessing savegame database.
/// Uses connection pooling and encapsulates all locking internally.
/// </summary>
public sealed class SavegameRepository : ISavegameRepository, IDisposable
{
    private readonly SqliteConnectionPool _pool;
    private readonly ChunkDataPool _chunkDataPool;
    private readonly ServerMain _server;
    private readonly ILogger _logger;
    private bool _disposed;

    public SavegameRepository(ServerMain server, int poolSize, ILogger logger)
    {
        _server = server;
        _logger = logger;
        _chunkDataPool = new ChunkDataPool(MagicNum.ServerChunkSize, server);

        var connectionString = BuildConnectionString(server.Config.WorldConfig.SaveFileLocation);
        _pool = new SqliteConnectionPool(connectionString, poolSize, logger);
    }

    private static string BuildConnectionString(string saveFileLocation)
    {
        return $"Data Source={saveFileLocation};Pooling=false;Mode=ReadOnly";
    }

    public IEnumerable<ChunkPos> GetAllMapChunkPositions()
    {
        using var lease = _pool.AcquireConnection();
        
        return lease.Execute(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT position FROM mapchunk";
            
            using var reader = cmd.ExecuteReader();
            var positions = new List<ChunkPos>();
            
            while (reader.Read())
            {
                var pos = (long)reader["position"];
                positions.Add(ChunkPos.FromChunkIndex_saveGamev2((ulong)pos));
            }
            
            return positions;
        });
    }

    public IEnumerable<Vec2i> GetAllMapRegionPositions()
    {
        using var lease = _pool.AcquireConnection();
        
        return lease.Execute(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT position FROM mapregion";
            
            using var reader = cmd.ExecuteReader();
            var positions = new List<Vec2i>();
            
            while (reader.Read())
            {
                var pos = (long)reader["position"];
                var vec2i = new Vec2i((int)(pos % 16), (int)(pos / 16));
                positions.Add(vec2i);
            }
            
            return positions;
        });
    }

    public ServerMapChunk? GetMapChunk(ChunkPos position)
    {
        var positionIndex = ChunkPos.ToChunkIndex(position.X, position.Y, position.Z);
        return GetMapChunk(positionIndex);
    }

    public ServerMapChunk? GetMapChunk(ulong positionIndex)
    {
        using var lease = _pool.AcquireConnection();
        
        return lease.Execute(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT data FROM mapchunk WHERE position = @position";
            cmd.Parameters.AddWithValue("@position", positionIndex);
            
            using var reader = cmd.ExecuteReader();
            
            if (!reader.Read())
                return null;

            try
            {
                var bytes = reader["data"] as byte[];
                return ServerMapChunk.FromBytes(bytes);
            }
            catch (Exception ex)
            {
                _logger.Error($"[VintageAtlas] Error reading ServerMapChunk: {ex.Message}");
                return null;
            }
        });
    }

    public ServerChunk? GetChunk(ChunkPos position)
    {
        var positionIndex = ChunkPos.ToChunkIndex(position.X, position.Y, position.Z);
        return GetChunk(positionIndex);
    }

    public ServerChunk? GetChunk(Vec3i position)
    {
        var positionIndex = ChunkPos.ToChunkIndex(position.X, position.Y, position.Z);
        return GetChunk(positionIndex);
    }

    public ServerChunk? GetChunk(ulong positionIndex)
    {
        using var lease = _pool.AcquireConnection();
        
        return lease.Execute(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT data FROM chunk WHERE position = @position";
            cmd.Parameters.AddWithValue("@position", positionIndex);
            
            using var reader = cmd.ExecuteReader();
            
            if (!reader.Read())
                return null;

            try
            {
                var bytes = reader["data"] as byte[];
                return ServerChunk.FromBytes(bytes, _chunkDataPool, _server);
            }
            catch (Exception ex)
            {
                _logger.Error($"[VintageAtlas] Error reading ServerChunk: {ex.Message}");
                return null;
            }
        });
    }

    public ServerMapRegion? GetMapRegion(ulong positionIndex)
    {
        using var lease = _pool.AcquireConnection();
        
        return lease.Execute(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT data FROM mapregion WHERE position = @position";
            cmd.Parameters.AddWithValue("@position", positionIndex);
            
            using var reader = cmd.ExecuteReader();
            
            if (!reader.Read())
                return null;

            try
            {
                var bytes = reader["data"] as byte[];
                return ServerMapRegion.FromBytes(bytes);
            }
            catch (Exception ex)
            {
                _logger.Error($"[VintageAtlas] Error reading ServerMapRegion: {ex.Message}");
                return null;
            }
        });
    }

    public SaveGame? GetGameData()
    {
        using var lease = _pool.AcquireConnection();
        
        return lease.Execute(conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT data FROM gamedata LIMIT 1";
            
            using var reader = cmd.ExecuteReader();
            
            if (!reader.Read())
                return null;

            try
            {
                if (reader["data"] is not byte[] data)
                    return null;

                return Serializer.Deserialize<SaveGame>(new MemoryStream(data));
            }
            catch (Exception ex)
            {
                _logger.Error($"[VintageAtlas] Error reading game data: {ex.Message}");
                return null;
            }
        });
    }

    public void SaveMapChunks(IDictionary<long, ServerMapChunk> chunks)
    {
        using var lease = _pool.AcquireConnection();
        
        lease.Execute(conn =>
        {
            using var transaction = conn.BeginTransaction();
            
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT OR REPLACE INTO mapchunk (position, data) VALUES (@position, @data)";
                
                var positionParam = cmd.Parameters.Add("@position", SqliteType.Integer);
                var dataParam = cmd.Parameters.Add("@data", SqliteType.Blob);
                
                foreach (var kvp in chunks)
                {
                    positionParam.Value = kvp.Key;
                    dataParam.Value = kvp.Value.ToBytes();
                    cmd.ExecuteNonQuery();
                }
                
                transaction.Commit();
            }
            catch (Exception ex)
            {
                _logger.Error($"[VintageAtlas] Error saving map chunks: {ex.Message}");
                transaction.Rollback();
                throw;
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _pool?.Dispose();
        _chunkDataPool?.FreeAll();
    }
}

