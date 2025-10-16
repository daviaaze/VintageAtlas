using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VintageAtlas.Models;
using VintageAtlas.Core;

namespace VintageAtlas.Tracking;

/// <summary>
/// Collects live game data for API responses (based on ServerstatusQuery patterns)
/// THREAD-SAFE: UpdateCache() called from game tick, CollectData() called from HTTP threads
/// </summary>
public class DataCollector(ICoreServerAPI sapi) : IDataCollector
{
    // Animal caching (like ServerstatusQuery does)
    private List<AnimalData>? _animalsCache;
    private DateTime _animalsCacheUntil = DateTime.MinValue;
    private const int AnimalsCacheSeconds = 3;
    private const int AnimalsMax = 200;
    private const int AnimalTrackingRadius = 64; // Only scan 64 blocks around players

    // Pre-computed data cache (updated on game tick, read by HTTP threads)
    private ServerStatusData? _cachedData;
    private volatile bool _dataReady;
    private long _lastUpdate;
    private const int CacheUpdateIntervalMs = 1000; // Update every 1 second
    private readonly object _cacheLock = new();

    /// <summary>
    /// CALLED FROM GAME TICK (MAIN THREAD) - Updates cache safely
    /// This is the ONLY method that accesses game state
    /// </summary>
    public void UpdateCache(float deltaTime)
    {
        var now = sapi.World.ElapsedMilliseconds;

        // Only update if the cache expired
        if (_dataReady && now - _lastUpdate < CacheUpdateIntervalMs)
        {
            return;
        }

        try
        {
            // Collect all data ON MAIN THREAD (safe!)
            var data = new ServerStatusData
            {
                SpawnPoint = GetSpawnPoint(),
                Date = GetGameDate(),
                Weather = GetWeatherInfo(),
                Players = GetPlayersData(),
                Animals = GetAnimalsData()
            };

            // Add spawn point climate data
            var spawnPos = new BlockPos((int)data.SpawnPoint.X, (int)data.SpawnPoint.Y, (int)data.SpawnPoint.Z);
            var climate = sapi.World.BlockAccessor.GetClimateAt(spawnPos);
            if (climate != null)
            {
                data.SpawnTemperature = FiniteOrNull(climate.Temperature);
                data.SpawnRainfall = FiniteOrNull(climate.Rainfall);
            }

            // Atomically update cache
            lock (_cacheLock)
            {
                _cachedData = data;
                _lastUpdate = now;
                _dataReady = true;
            }
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error updating data cache: {ex.Message}");
        }
    }

    /// <summary>
    /// CALLED FROM HTTP THREADS - Returns cached data only
    /// NEVER accesses game state directly!
    /// </summary>
    public ServerStatusData CollectData()
    {
        lock (_cacheLock)
        {
            if (_cachedData != null && _dataReady)
            {
                return _cachedData;
            }

            // Fallback if cache not ready yet (server just started)
            sapi.Logger.Debug("[VintageAtlas] Data cache not ready yet, returning empty data");
            return new ServerStatusData
            {
                Players = new List<PlayerData>(),
                Animals = new List<AnimalData>(),
                SpawnPoint = new SpawnPoint { X = 0, Y = sapi.World.SeaLevel, Z = 0 },
                Date = new DateInfo { Year = 1, Month = 1, Day = 1, Hour = 0, Minute = 0 },
                Weather = new WeatherInfo { Temperature = 20, Rainfall = 0, WindSpeed = 0 }
            };
        }
    }

    private SpawnPoint GetSpawnPoint()
    {
        try
        {
            var spawnPos = sapi.World.DefaultSpawnPosition;
            if (spawnPos == null) return new SpawnPoint { X = 0, Y = sapi.World.SeaLevel, Z = 0 };

            var pos = spawnPos.AsBlockPos;
            return new SpawnPoint
            {
                X = pos.X,
                Y = pos.Y,
                Z = pos.Z
            };
        }
        catch
        {
            return new SpawnPoint { X = 0, Y = sapi.World.SeaLevel, Z = 0 };
        }
    }

    private DateInfo GetGameDate()
    {
        var calendar = sapi.World.Calendar;
        if (calendar == null)
        {
            return new DateInfo { Year = 1, Month = 1, Day = 1, Hour = 0, Minute = 0 };
        }

        // ServerstatusQuery approach: use DayOfMonth and Month directly
        var daysPerMonth = Math.Max(1, calendar.DaysPerMonth);
        var daysPerYear = Math.Max(daysPerMonth, calendar.DaysPerYear);

        var dayOfYear = calendar.DayOfYear;
        if (dayOfYear < 0) dayOfYear = 0;
        if (dayOfYear >= daysPerYear) dayOfYear = daysPerYear - 1;

        var day = dayOfYear % daysPerMonth + 1;
        var fullHour = calendar.FullHourOfDay;
        var minute = (int)Math.Floor((calendar.HourOfDay - fullHour) * 60.0 + 0.0000001);

        if (minute < 0) minute = 0;
        if (minute > 59) minute = 59;

        return new DateInfo
        {
            Year = calendar.Year,
            Month = calendar.Month,
            Day = day,
            Hour = fullHour,
            Minute = minute
        };
    }

    private WeatherInfo GetWeatherInfo()
    {
        try
        {
            // Check if world is ready (can be null during early startup)
            if (sapi.World?.BlockAccessor == null)
            {
                return new WeatherInfo
                {
                    Temperature = 20,
                    Rainfall = 0,
                    WindSpeed = 0
                };
            }

            BlockPos pos;
            if (sapi.World.DefaultSpawnPosition != null)
            {
                pos = sapi.World.DefaultSpawnPosition.AsBlockPos;
            }
            else
            {
                // Fallback to first online player position
                var firstPlayer = System.Linq.Enumerable.FirstOrDefault(sapi.World.AllOnlinePlayers, p => p?.Entity != null);
                if (firstPlayer != null)
                {
                    var playerPos = firstPlayer.Entity.Pos.AsBlockPos;
                    pos = new BlockPos(playerPos.X, playerPos.Y, playerPos.Z);
                }
                else
                {
                    pos = new BlockPos(0, sapi.World.SeaLevel, 0);
                }
            }

            var climate = sapi.World.BlockAccessor.GetClimateAt(pos);
            if (climate != null)
            {
                var windSpeed = GetWindSpeed(pos);

                return new WeatherInfo
                {
                    Temperature = FiniteOrNull(climate.Temperature) ?? 20,
                    Rainfall = FiniteOrNull(climate.Rainfall) ?? 0,
                    WindSpeed = windSpeed
                };
            }
        }
        catch (Exception ex)
        {
            sapi.Logger.Warning($"Failed to get weather info: {ex.Message}");
        }

        return new WeatherInfo
        {
            Temperature = 20,
            Rainfall = 0,
            WindSpeed = 0
        };
    }

    private List<PlayerData> GetPlayersData()
    {
        var players = new List<PlayerData>();

        try
        {
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;

                try
                {
                    // Use ServerstatusQuery approach: directly access position
                    var entityPos = player.Entity.Pos?.XYZ ?? new Vec3d(0, sapi.World.SeaLevel, 0);
                    var pos = new BlockPos((int)entityPos.X, (int)entityPos.Y, (int)entityPos.Z);

                    var climate = sapi.World.BlockAccessor?.GetClimateAt(pos);
                    var watchedAttributes = player.Entity.WatchedAttributes;

                    // Health - using ITreeAttribute like ServerstatusQuery
                    var healthTree = watchedAttributes?.GetTreeAttribute("health");
                    var currentHealth = FiniteOrNull(healthTree?.GetFloat("currenthealth")) ?? 0;
                    var maxHealth = FiniteOrNull(healthTree?.GetFloat("maxhealth")) ?? 20;

                    // Hunger
                    var hungerTree = watchedAttributes?.GetTreeAttribute("hunger");
                    var currentSaturation = FiniteOrNull(hungerTree?.GetFloat("currentsaturation")) ?? 0;
                    var maxSaturation = FiniteOrNull(hungerTree?.GetFloat("maxsaturation")) ?? 1500;

                    // Body temperature
                    var bodyTempTree = watchedAttributes?.GetTreeAttribute("bodyTemp");
                    var bodyTemp = FiniteOrNull(bodyTempTree?.GetFloat("bodytemp"));

                    players.Add(new PlayerData
                    {
                        Name = player.PlayerName,
                        Uid = player.PlayerUID,
                        Coordinates = new CoordinateData
                        {
                            X = pos.X,
                            Y = pos.Y,
                            Z = pos.Z
                        },
                        Health = new HealthData
                        {
                            Current = currentHealth,
                            Max = maxHealth
                        },
                        Hunger = new HealthData
                        {
                            Current = currentSaturation,
                            Max = maxSaturation
                        },
                        Temperature = FiniteOrNull(climate?.Temperature),
                        BodyTemp = bodyTemp
                    });
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning($"Failed to collect data for player {player.PlayerName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            sapi.Logger.Warning($"Failed to collect players data: {ex.Message}");
        }

        return players;
    }

    private List<AnimalData> GetAnimalsData()
    {
        // Use cached data if still valid (like ServerstatusQuery)
        if (_animalsCache != null && DateTime.UtcNow < _animalsCacheUntil)
        {
            return _animalsCache;
        }

        var animals = CollectAnimalsFromPlayers();
        CacheAnimalsData(animals);

        return animals;
    }

    private List<AnimalData> CollectAnimalsFromPlayers()
    {
        var animals = new List<AnimalData>();

        try
        {
            var allPlayers = sapi.World.AllOnlinePlayers;

            if (allPlayers == null || allPlayers.Length == 0)
            {
                return animals;
            }

            // OPTIMIZATION: Only scan around players (spatial query)
            var seenEntities = new HashSet<long>();

            foreach (var player in allPlayers)
            {
                if (player is not IServerPlayer serverPlayer || !IsValidPlayer(serverPlayer)) 
                    continue;

                CollectAnimalsNearPlayer(serverPlayer, animals, seenEntities);

                if (animals.Count >= AnimalsMax) break;
            }

            sapi.Logger.Debug($"[VintageAtlas] Spatial animal scan: {animals.Count} animals found within {AnimalTrackingRadius} blocks of players");
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Failed to collect animals data: {ex.Message}");
        }

        return animals;
    }

    private void CollectAnimalsNearPlayer(IServerPlayer player, List<AnimalData> animals, HashSet<long> seenEntities)
    {
        var playerPos = player.Entity.Pos.AsBlockPos;

        var nearbyEntities = sapi.World.GetEntitiesAround(
            playerPos.ToVec3d(),
            AnimalTrackingRadius,
            AnimalTrackingRadius,
            entity => IsTrackableAnimal(entity, seenEntities)
        );

        foreach (var entity in nearbyEntities)
        {
            if (!IsValidEntity(entity)) continue;

            seenEntities.Add(entity.EntityId);

            var animalData = TryCreateAnimalData(entity);
            if (animalData != null)
            {
                animals.Add(animalData);
            }

            if (animals.Count >= AnimalsMax) break;
        }
    }

    private AnimalData? TryCreateAnimalData(Vintagestory.API.Common.Entities.Entity entity)
    {
        try
        {
            return CreateAnimalData(entity);
        }
        catch (Exception ex)
        {
            sapi.Logger.Warning($"[VintageAtlas] Failed to process entity {entity.Code}: {ex.Message}");
            return null;
        }
    }

    private AnimalData CreateAnimalData(Vintagestory.API.Common.Entities.Entity entity)
    {
        var typeCode = entity.Code.ToString();
        var name = entity.GetName() ?? typeCode;
        var (currentHealth, maxHealth) = GetEntityHealth(entity);
        var position = GetEntityPosition(entity);
        var (temperature, rainfall) = GetClimateData(position);
        var windPercent = GetWindPercent(position);

        return new AnimalData
        {
            Type = typeCode,
            Name = string.IsNullOrWhiteSpace(name) ? typeCode : name,
            Coordinates = new CoordinateData { X = position.X, Y = position.Y, Z = position.Z },
            Health = new HealthData
            {
                Current = currentHealth,
                Max = maxHealth
            },
            Temperature = temperature,
            Rainfall = rainfall ?? 0,
            Wind = new WindData { Percent = windPercent }
        };
    }

    private static (double currentHealth, double maxHealth) GetEntityHealth(Vintagestory.API.Common.Entities.Entity entity)
    {
        var healthTree = entity.WatchedAttributes?.GetTreeAttribute("health");
        var currentHealth = FiniteOrNull(healthTree?.GetFloat("currenthealth"));
        var maxHealth = FiniteOrNull(healthTree?.GetFloat("maxhealth"));

        // Fallback health values
        if (maxHealth is null or <= 0)
        {
            maxHealth = 20;
            currentHealth = entity.Alive ? 20 : 0;
        }

        return (currentHealth ?? 0, (double)maxHealth);
    }

    private BlockPos GetEntityPosition(Vintagestory.API.Common.Entities.Entity entity)
    {
        var x = entity.ServerPos != null ? (int)entity.ServerPos.X : 0;
        var y = entity.ServerPos != null ? (int)entity.ServerPos.Y : sapi.World.SeaLevel;
        var z = entity.ServerPos != null ? (int)entity.ServerPos.Z : 0;

        return new BlockPos(x, y, z);
    }

    private (double? temperature, double? rainfall) GetClimateData(BlockPos pos)
    {
        if (sapi.World.BlockAccessor == null)
        {
            return (null, null);
        }

        var climate = sapi.World.BlockAccessor.GetClimateAt(pos);
        if (climate == null)
        {
            return (null, null);
        }

        return (FiniteOrNull(climate.Temperature), FiniteOrNull(climate.Rainfall));
    }

    private static bool IsValidPlayer(IServerPlayer player)
    {
        return player?.Entity?.Pos != null;
    }

    private static bool IsValidEntity(Vintagestory.API.Common.Entities.Entity entity)
    {
        return entity?.Pos != null && entity.Code != null;
    }

    private static bool IsTrackableAnimal(Vintagestory.API.Common.Entities.Entity entity, HashSet<long> seenEntities)
    {
        return entity is EntityAgent &&
               !(entity is EntityPlayer) &&
               entity.Alive &&
               !seenEntities.Contains(entity.EntityId);
    }

    private void CacheAnimalsData(List<AnimalData> animals)
    {
        _animalsCache = animals;
        _animalsCacheUntil = DateTime.UtcNow.AddSeconds(AnimalsCacheSeconds);
    }

    // Helper method from ServerstatusQuery
    private static float? FiniteOrNull(float? f)
    {
        if (!f.HasValue) return null;
        var value = f.Value;
        return float.IsNaN(value) || float.IsInfinity(value) ? null : value;
    }

    private static double? FiniteOrNull(double? d)
    {
        if (!d.HasValue) return null;
        var value = d.Value;
        return double.IsNaN(value) || double.IsInfinity(value) ? null : value;
    }

    private double GetWindSpeed(BlockPos pos)
    {
        try
        {
            if (sapi.World.BlockAccessor == null) return 0;

            var windVec = sapi.World.BlockAccessor.GetWindSpeedAt(pos);
            if (windVec == null) return 0;

            // Calculate magnitude
            var speed = Math.Sqrt(windVec.X * windVec.X + windVec.Y * windVec.Y + windVec.Z * windVec.Z);
            return FiniteOrNull(speed) ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private double GetWindPercent(BlockPos pos)
    {
        try
        {
            if (sapi.World.BlockAccessor == null) return 0;

            var windVec = sapi.World.BlockAccessor.GetWindSpeedAt(pos);
            if (windVec == null) return 0;

            // Calculate magnitude and convert to percentage
            var speed = Math.Sqrt(windVec.X * windVec.X + windVec.Y * windVec.Y + windVec.Z * windVec.Z);
            var percent = (int)Math.Round(speed * 100.0);

            return FiniteOrNull(percent) ?? 0;
        }
        catch
        {
            return 0;
        }
    }
}
