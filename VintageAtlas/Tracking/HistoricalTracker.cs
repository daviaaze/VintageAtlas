using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using VintageAtlas.Core;
using VintageAtlas.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace VintageAtlas.Tracking
{
    /// <summary>
    /// Historical data tracker using SQLite for persistence
    /// Tracks player positions, entity census, server stats, and events
    /// </summary>
    public class HistoricalTracker(ICoreServerAPI sapi) : IHistoricalTracker, IDisposable
    {
        private SqliteConnection? _metricsDb;
        private long _lastPlayerSnapshot;
        private long _lastCensusSnapshot;
        private long _lastStatsSnapshot;
        private const int PlayerSnapshotIntervalMs = 15000; // 15 seconds
        private const int CensusSnapshotIntervalMs = 60000; // 1 minute
        private const int StatsSnapshotIntervalMs = 30000;  // 30 seconds
        private const int MaxPositionsPerPlayer = 10000; // Limit history per player

        public void Initialize()
        {
            try
            {
                var dataPath = Path.Combine(sapi.DataBasePath, "ModData", "VintageAtlas");
                Directory.CreateDirectory(dataPath);
                
                var dbPath = Path.Combine(dataPath, "metrics.db");
                _metricsDb = new SqliteConnection($"Data Source={dbPath}");
                _metricsDb.Open();
                
                CreateSchema();
                
                sapi.Logger.Notification("[VintageAtlas] Historical tracker initialized at: " + dbPath);
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[VintageAtlas] Failed to initialize historical tracker: {ex.Message}");
                sapi.Logger.Error(ex.StackTrace ?? "");
            }
        }

        private void CreateSchema()
        {
            if (_metricsDb == null) return;

            var cmd = _metricsDb.CreateCommand();
            cmd.CommandText = """

                                              -- Player position history
                                              CREATE TABLE IF NOT EXISTS player_positions (
                                                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                  timestamp INTEGER NOT NULL,
                                                  player_uid TEXT NOT NULL,
                                                  player_name TEXT NOT NULL,
                                                  x REAL NOT NULL,
                                                  y REAL NOT NULL,
                                                  z REAL NOT NULL,
                                                  health REAL,
                                                  max_health REAL,
                                                  hunger REAL,
                                                  max_hunger REAL,
                                                  temperature REAL,
                                                  body_temp REAL
                                              );
                                              
                                              CREATE INDEX IF NOT EXISTS idx_player_positions_uid_time 
                                                  ON player_positions(player_uid, timestamp);
                                              CREATE INDEX IF NOT EXISTS idx_player_positions_time 
                                                  ON player_positions(timestamp);
                                              
                                              -- Entity census
                                              CREATE TABLE IF NOT EXISTS entity_census (
                                                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                  timestamp INTEGER NOT NULL,
                                                  entity_type TEXT NOT NULL,
                                                  count INTEGER NOT NULL,
                                                  avg_health REAL,
                                                  min_x REAL,
                                                  max_x REAL,
                                                  min_z REAL,
                                                  max_z REAL
                                              );
                                              
                                              CREATE INDEX IF NOT EXISTS idx_entity_census_time 
                                                  ON entity_census(timestamp);
                                              CREATE INDEX IF NOT EXISTS idx_entity_census_type_time 
                                                  ON entity_census(entity_type, timestamp);
                                              
                                              -- Server statistics
                                              CREATE TABLE IF NOT EXISTS server_stats (
                                                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                  timestamp INTEGER NOT NULL,
                                                  players_online INTEGER NOT NULL,
                                                  entities_loaded INTEGER NOT NULL,
                                                  chunks_loaded INTEGER NOT NULL,
                                                  memory_mb REAL,
                                                  server_uptime_seconds REAL NOT NULL
                                              );
                                              
                                              CREATE INDEX IF NOT EXISTS idx_server_stats_time 
                                                  ON server_stats(timestamp);
                                              
                                              -- Player death events
                                              CREATE TABLE IF NOT EXISTS player_deaths (
                                                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                  timestamp INTEGER NOT NULL,
                                                  player_uid TEXT NOT NULL,
                                                  player_name TEXT NOT NULL,
                                                  x REAL NOT NULL,
                                                  y REAL NOT NULL,
                                                  z REAL NOT NULL,
                                                  damage_source TEXT
                                              );
                                              
                                              CREATE INDEX IF NOT EXISTS idx_player_deaths_uid 
                                                  ON player_deaths(player_uid);
                                              CREATE INDEX IF NOT EXISTS idx_player_deaths_time 
                                                  ON player_deaths(timestamp);
                                          
                              """;
            cmd.ExecuteNonQuery();
            
            sapi.Logger.Debug("[VintageAtlas] Database schema created/verified");
        }

        /// <summary>
        /// Main tick handler - call this from mod's game tick listener
        /// </summary>
        public void OnGameTick(float dt)
        {
            if (_metricsDb == null) return;

            var now = sapi.World.ElapsedMilliseconds;

            try
            {
                // Player position snapshots
                if (now - _lastPlayerSnapshot > PlayerSnapshotIntervalMs)
                {
                    RecordPlayerPositions();
                    _lastPlayerSnapshot = now;
                    CleanupOldPlayerPositions(); // Prevent unbounded growth
                }

                // Entity census
                if (now - _lastCensusSnapshot > CensusSnapshotIntervalMs)
                {
                    RecordEntityCensus();
                    _lastCensusSnapshot = now;
                }

                // Server stats
                if (now - _lastStatsSnapshot > StatsSnapshotIntervalMs)
                {
                    RecordServerStats();
                    _lastStatsSnapshot = now;
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning($"[VintageAtlas] Error in historical tracker tick: {ex.Message}");
            }
        }

        private void RecordPlayerPositions()
        {
            if (_metricsDb == null) return;

            var timestamp = sapi.World.ElapsedMilliseconds;
            
            using var transaction = _metricsDb.BeginTransaction();
            try
            {
                var cmd = _metricsDb.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = """

                                                      INSERT INTO player_positions 
                                                      (timestamp, player_uid, player_name, x, y, z, health, max_health, hunger, max_hunger, temperature, body_temp)
                                                      VALUES (@ts, @uid, @name, @x, @y, @z, @health, @maxHealth, @hunger, @maxHunger, @temp, @bodyTemp)
                                                  
                                  """;

                var recorded = 0;
                foreach (var player in sapi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;

                    try
                    {
                        var pos = player.Entity.ServerPos ?? player.Entity.Pos;
                        var attrs = player.Entity.WatchedAttributes as TreeAttribute;
                        
                        var healthTree = attrs?.GetTreeAttribute("health");
                        var hungerTree = attrs?.GetTreeAttribute("hunger");
                        var bodyTempTree = attrs?.GetTreeAttribute("bodyTemp");
                        
                        var blockPos = pos.AsBlockPos;
                        var climate = sapi.World.BlockAccessor?.GetClimateAt(blockPos);

                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@ts", timestamp);
                        cmd.Parameters.AddWithValue("@uid", player.PlayerUID);
                        cmd.Parameters.AddWithValue("@name", player.PlayerName);
                        cmd.Parameters.AddWithValue("@x", pos.X);
                        cmd.Parameters.AddWithValue("@y", pos.Y);
                        cmd.Parameters.AddWithValue("@z", pos.Z);
                        cmd.Parameters.AddWithValue("@health", (object?)healthTree?.GetFloat("currenthealth") ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@maxHealth", (object?)healthTree?.GetFloat("maxhealth", 20f) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@hunger", (object?)hungerTree?.GetFloat("currentsaturation") ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@maxHunger", (object?)hungerTree?.GetFloat("maxsaturation", 1500f) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@temp", (object?)climate?.Temperature ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@bodyTemp", (object?)bodyTempTree?.GetFloat("bodytemp") ?? DBNull.Value);

                        cmd.ExecuteNonQuery();
                        recorded++;
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Debug($"[VintageAtlas] Failed to record position for {player.PlayerName}: {ex.Message}");
                    }
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                sapi.Logger.Warning($"[VintageAtlas] Failed to record player positions: {ex.Message}");
            }
        }

        private void RecordEntityCensus()
        {
            if (_metricsDb == null) return;

            var timestamp = sapi.World.ElapsedMilliseconds;
            var entityGroups = new Dictionary<string, List<Entity>>();

            // Group entities by type
            foreach (var entity in sapi.World.LoadedEntities.Values)
            {
                if (entity is not { Alive: true }) continue;
                if (entity is EntityPlayer) continue;
                if (entity is not EntityAgent) continue;

                var typeCode = entity.Code?.ToString() ?? "unknown";
                if (!entityGroups.TryGetValue(typeCode, out var value))
                {
                    value = [];
                    entityGroups[typeCode] = value;
                }

                value.Add(entity);
            }

            using var transaction = _metricsDb.BeginTransaction();
            try
            {
                var cmd = _metricsDb.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = """

                                                      INSERT INTO entity_census 
                                                      (timestamp, entity_type, count, avg_health, min_x, max_x, min_z, max_z)
                                                      VALUES (@ts, @type, @count, @avgHealth, @minX, @maxX, @minZ, @maxZ)
                                                  
                                  """;

                foreach (var (key, entities) in entityGroups)
                {
                    var healthValues = new List<double>();
                    double minX = double.MaxValue, maxX = double.MinValue;
                    double minZ = double.MaxValue, maxZ = double.MinValue;

                    foreach (var entity in entities)
                    {
                        var health = entity.WatchedAttributes
                            ?.GetTreeAttribute("health")
                            ?.GetFloat("currenthealth") ?? 0f;
                        if (health > 0) healthValues.Add(health);

                        var pos = entity.ServerPos ?? entity.Pos;
                        minX = Math.Min(minX, pos.X);
                        maxX = Math.Max(maxX, pos.X);
                        minZ = Math.Min(minZ, pos.Z);
                        maxZ = Math.Max(maxZ, pos.Z);
                    }

                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@ts", timestamp);
                    cmd.Parameters.AddWithValue("@type", key);
                    cmd.Parameters.AddWithValue("@count", entities.Count);
                    cmd.Parameters.AddWithValue("@avgHealth", healthValues.Count != 0 ? healthValues.Average() : DBNull.Value);
                    cmd.Parameters.AddWithValue("@minX", minX != double.MaxValue ? minX : DBNull.Value);
                    cmd.Parameters.AddWithValue("@maxX", maxX != double.MinValue ? maxX : DBNull.Value);
                    cmd.Parameters.AddWithValue("@minZ", minZ != double.MaxValue ? minZ : DBNull.Value);
                    cmd.Parameters.AddWithValue("@maxZ", maxZ != double.MinValue ? maxZ : DBNull.Value);

                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                sapi.Logger.Warning($"[VintageAtlas] Failed to record entity census: {ex.Message}");
            }
        }

        private void RecordServerStats()
        {
            if (_metricsDb == null) return;

            try
            {
                var timestamp = sapi.World.ElapsedMilliseconds;
                var cmd = _metricsDb.CreateCommand();
                cmd.CommandText = """

                                                      INSERT INTO server_stats 
                                                      (timestamp, players_online, entities_loaded, chunks_loaded, memory_mb, server_uptime_seconds)
                                                      VALUES (@ts, @players, @entities, @chunks, @memory, @uptime)
                                                  
                                  """;

                var memoryMb = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
                
                // Get actual loaded chunk count from Vintage Story API
                cmd.Parameters.AddWithValue("@ts", timestamp);
                cmd.Parameters.AddWithValue("@players", sapi.World.AllOnlinePlayers.Length);
                cmd.Parameters.AddWithValue("@entities", sapi.World.LoadedEntities.Count);
                cmd.Parameters.AddWithValue("@chunks", sapi.World.LoadedMapChunkIndices.Length);
                cmd.Parameters.AddWithValue("@memory", memoryMb);
                cmd.Parameters.AddWithValue("@uptime", sapi.World.ElapsedMilliseconds / 1000);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning($"[VintageAtlas] Failed to record server stats: {ex.Message}");
            }
        }

        public void RecordPlayerDeath(IServerPlayer player, string? damageSource = null)
        {
            if (_metricsDb == null) return;

            try
            {
                var pos = player.Entity?.ServerPos ?? player.Entity?.Pos;
                if (pos == null) return;

                var cmd = _metricsDb.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO player_deaths 
                    (timestamp, player_uid, player_name, x, y, z, damage_source)
                    VALUES (@ts, @uid, @name, @x, @y, @z, @source)
                ";

                cmd.Parameters.AddWithValue("@ts", sapi.World.ElapsedMilliseconds);
                cmd.Parameters.AddWithValue("@uid", player.PlayerUID);
                cmd.Parameters.AddWithValue("@name", player.PlayerName);
                cmd.Parameters.AddWithValue("@x", pos.X);
                cmd.Parameters.AddWithValue("@y", pos.Y);
                cmd.Parameters.AddWithValue("@z", pos.Z);
                cmd.Parameters.AddWithValue("@source", (object?)damageSource ?? DBNull.Value);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning($"[VintageAtlas] Failed to record player death: {ex.Message}");
            }
        }

        private void CleanupOldPlayerPositions()
        {
            if (_metricsDb == null) return;

            try
            {
                // Keep only the last MAX_POSITIONS_PER_PLAYER for each player
                var cmd = _metricsDb.CreateCommand();
                cmd.CommandText = @"
                    DELETE FROM player_positions
                    WHERE id IN (
                        SELECT id FROM (
                            SELECT id, player_uid,
                                   ROW_NUMBER() OVER (PARTITION BY player_uid ORDER BY timestamp DESC) as rn
                            FROM player_positions
                        ) WHERE rn > @limit
                    )
                ";
                cmd.Parameters.AddWithValue("@limit", MaxPositionsPerPlayer);
                var deleted = cmd.ExecuteNonQuery();
                
                if (deleted > 0)
                {
                    sapi.Logger.Debug($"[VintageAtlas] Cleaned up {deleted} old position records");
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning($"[VintageAtlas] Failed to cleanup old positions: {ex.Message}");
            }
        }

        #region Query Methods

        /// <summary>
        /// Get heatmap data for a player or all players
        /// </summary>
        public List<HeatmapPoint> GetHeatmap(HistoricalQueryParams queryParams)
        {
            if (_metricsDb == null) return new List<HeatmapPoint>();

            try
            {
                var gridSize = queryParams.GridSize ?? 32;
                var hoursBack = queryParams.Hours ?? 24;
                var cutoffTime = sapi.World.ElapsedMilliseconds - hoursBack * 3600000;

                var cmd = _metricsDb.CreateCommand();
                var whereClauses = new List<string> { "timestamp >= @cutoff" };
                
                if (!string.IsNullOrEmpty(queryParams.PlayerUid))
                {
                    whereClauses.Add("player_uid = @playerUid");
                    cmd.Parameters.AddWithValue("@playerUid", queryParams.PlayerUid);
                }

                cmd.CommandText = $@"
                    SELECT 
                        CAST(x / @gridSize AS INTEGER) * @gridSize as grid_x,
                        CAST(z / @gridSize AS INTEGER) * @gridSize as grid_z,
                        COUNT(*) as count
                    FROM player_positions
                    WHERE {string.Join(" AND ", whereClauses)}
                    GROUP BY grid_x, grid_z
                    ORDER BY count DESC
                    LIMIT 1000
                ";
                cmd.Parameters.AddWithValue("@gridSize", gridSize);
                cmd.Parameters.AddWithValue("@cutoff", cutoffTime);

                var heatmap = new List<HeatmapPoint>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    heatmap.Add(new HeatmapPoint
                    {
                        X = reader.GetDouble(0),
                        Z = reader.GetDouble(1),
                        Count = reader.GetInt32(2)
                    });
                }

                sapi.Logger.Debug($"[VintageAtlas] Retrieved {heatmap.Count} heatmap points");
                return heatmap;
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[VintageAtlas] Failed to get heatmap: {ex.Message}");
                return new List<HeatmapPoint>();
            }
        }

        /// <summary>
        /// Get player movement path
        /// </summary>
        public List<PlayerPathPoint> GetPlayerPath(HistoricalQueryParams queryParams)
        {
            if (_metricsDb == null) return new List<PlayerPathPoint>();
            if (string.IsNullOrEmpty(queryParams.PlayerUid)) return new List<PlayerPathPoint>();

            try
            {
                var cmd = _metricsDb.CreateCommand();
                cmd.CommandText = @"
                    SELECT timestamp, x, y, z, health
                    FROM player_positions
                    WHERE player_uid = @uid 
                      AND timestamp BETWEEN @from AND @to
                    ORDER BY timestamp ASC
                    LIMIT 10000
                ";

                var hoursBack = queryParams.Hours ?? 1;
                var toTime = queryParams.ToTimestamp ?? sapi.World.ElapsedMilliseconds;
                var fromTime = queryParams.FromTimestamp ?? toTime - hoursBack * 3600000;

                cmd.Parameters.AddWithValue("@uid", queryParams.PlayerUid);
                cmd.Parameters.AddWithValue("@from", fromTime);
                cmd.Parameters.AddWithValue("@to", toTime);

                var path = new List<PlayerPathPoint>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    path.Add(new PlayerPathPoint
                    {
                        Timestamp = reader.GetInt64(0),
                        X = reader.GetDouble(1),
                        Y = reader.GetDouble(2),
                        Z = reader.GetDouble(3),
                        Health = reader.IsDBNull(4) ? null : reader.GetDouble(4)
                    });
                }

                sapi.Logger.Debug($"[VintageAtlas] Retrieved path with {path.Count} points for player {queryParams.PlayerUid}");
                return path;
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[VintageAtlas] Failed to get player path: {ex.Message}");
                return new List<PlayerPathPoint>();
            }
        }

        /// <summary>
        /// Get entity census data
        /// </summary>
        public List<EntityCensusSnapshot> GetEntityCensus(HistoricalQueryParams queryParams)
        {
            if (_metricsDb == null) return new List<EntityCensusSnapshot>();

            try
            {
                var hoursBack = queryParams.Hours ?? 24;
                var cutoffTime = sapi.World.ElapsedMilliseconds - hoursBack * 3600000;

                var cmd = _metricsDb.CreateCommand();
                var whereClauses = new List<string> { "timestamp >= @cutoff" };

                if (!string.IsNullOrEmpty(queryParams.EntityType))
                {
                    whereClauses.Add("entity_type LIKE @entityType");
                    cmd.Parameters.AddWithValue("@entityType", $"%{queryParams.EntityType}%");
                }

                cmd.CommandText = $@"
                    SELECT id, timestamp, entity_type, count, avg_health, min_x, max_x, min_z, max_z
                    FROM entity_census
                    WHERE {string.Join(" AND ", whereClauses)}
                    ORDER BY timestamp DESC
                    LIMIT 10000
                ";
                cmd.Parameters.AddWithValue("@cutoff", cutoffTime);

                var census = new List<EntityCensusSnapshot>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    census.Add(new EntityCensusSnapshot
                    {
                        Id = reader.GetInt64(0),
                        Timestamp = reader.GetInt64(1),
                        EntityType = reader.GetString(2),
                        Count = reader.GetInt32(3),
                        AvgHealth = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                        MinX = reader.IsDBNull(5) ? null : reader.GetDouble(5),
                        MaxX = reader.IsDBNull(6) ? null : reader.GetDouble(6),
                        MinZ = reader.IsDBNull(7) ? null : reader.GetDouble(7),
                        MaxZ = reader.IsDBNull(8) ? null : reader.GetDouble(8)
                    });
                }

                sapi.Logger.Debug($"[VintageAtlas] Retrieved {census.Count} census records");
                return census;
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[VintageAtlas] Failed to get entity census: {ex.Message}");
                return new List<EntityCensusSnapshot>();
            }
        }

        /// <summary>
        /// Get server statistics
        /// </summary>
        public ServerStatistics GetServerStatistics()
        {
            var stats = new ServerStatistics
            {
                CurrentTimestamp = sapi.World.ElapsedMilliseconds
            };

            if (_metricsDb == null) return stats;

            try
            {
                // Total players tracked
                var cmd = _metricsDb.CreateCommand();
                cmd.CommandText = "SELECT COUNT(DISTINCT player_uid) FROM player_positions";
                stats.TotalPlayersTracked = Convert.ToInt32(cmd.ExecuteScalar());

                // Total positions recorded
                cmd.CommandText = "SELECT COUNT(*) FROM player_positions";
                stats.TotalPositionsRecorded = Convert.ToInt32(cmd.ExecuteScalar());

                // Total deaths
                cmd.CommandText = "SELECT COUNT(*) FROM player_deaths";
                stats.TotalDeaths = Convert.ToInt32(cmd.ExecuteScalar());

                // Oldest data timestamp
                cmd.CommandText = "SELECT MIN(timestamp) FROM player_positions";
                var oldestObj = cmd.ExecuteScalar();
                stats.OldestDataTimestamp = oldestObj != null && oldestObj != DBNull.Value 
                    ? Convert.ToInt64(oldestObj) 
                    : stats.CurrentTimestamp;

                // Database size
                var dbPath = ((SqliteConnection)_metricsDb).DataSource;
                if (File.Exists(dbPath))
                {
                    stats.DatabaseSizeMb = new FileInfo(dbPath).Length / 1024.0 / 1024.0;
                }

                // Entity type counts
                cmd.CommandText = @"
                    SELECT entity_type, SUM(count) as total_sightings, MAX(count) as max_count
                    FROM entity_census
                    GROUP BY entity_type
                    ORDER BY total_sightings DESC
                    LIMIT 20
                ";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        stats.EntityTypeCounts.Add(new EntityTypeCount
                        {
                            EntityType = reader.GetString(0),
                            TotalSightings = reader.GetInt32(1),
                            CurrentCount = reader.GetInt32(2)
                        });
                    }
                }

                // Top players by activity
                cmd.CommandText = @"
                    SELECT 
                        player_uid,
                        player_name,
                        COUNT(*) as snapshots,
                        MIN(timestamp) as first_seen,
                        MAX(timestamp) as last_seen
                    FROM player_positions
                    GROUP BY player_uid
                    ORDER BY snapshots DESC
                    LIMIT 10
                ";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var playerUid = reader.GetString(0);
                        
                        // Calculate distance traveled
                        var distCmd = _metricsDb.CreateCommand();
                        distCmd.CommandText = @"
                            WITH positions AS (
                                SELECT x, z, LAG(x) OVER (ORDER BY timestamp) as prev_x,
                                       LAG(z) OVER (ORDER BY timestamp) as prev_z
                                FROM player_positions
                                WHERE player_uid = @uid
                                ORDER BY timestamp
                            )
                            SELECT SUM(
                                SQRT(
                                    POWER(x - COALESCE(prev_x, x), 2) + 
                                    POWER(z - COALESCE(prev_z, z), 2)
                                )
                            )
                            FROM positions
                            WHERE prev_x IS NOT NULL
                        ";
                        distCmd.Parameters.AddWithValue("@uid", playerUid);
                        var distObj = distCmd.ExecuteScalar();
                        var distance = distObj != null && distObj != DBNull.Value ? Convert.ToDouble(distObj) : 0;

                        // Count deaths
                        var deathCmd = _metricsDb.CreateCommand();
                        deathCmd.CommandText = "SELECT COUNT(*) FROM player_deaths WHERE player_uid = @uid";
                        deathCmd.Parameters.AddWithValue("@uid", playerUid);
                        var deaths = Convert.ToInt32(deathCmd.ExecuteScalar());

                        stats.TopPlayers.Add(new PlayerActivitySummary
                        {
                            PlayerUid = playerUid,
                            PlayerName = reader.GetString(1),
                            TotalSnapshots = reader.GetInt32(2),
                            FirstSeenTimestamp = reader.GetInt64(3),
                            LastSeenTimestamp = reader.GetInt64(4),
                            TotalDistanceTraveled = distance,
                            Deaths = deaths
                        });
                    }
                }

                sapi.Logger.Debug("[VintageAtlas] Retrieved server statistics");
                return stats;
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[VintageAtlas] Failed to get server statistics: {ex.Message}");
                return stats;
            }
        }

        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing) 
                return;
            if (_metricsDb is null)
                return;
            
            sapi.Logger.Notification("[VintageAtlas] Shutting down historical tracker");
            _metricsDb.Close();
            _metricsDb.Dispose();
            _metricsDb = null;
        }
    }
}

