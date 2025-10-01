using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VintageAtlas.Models;
using VintageAtlas.Core;

namespace VintageAtlas.Tracking;

/// <summary>
/// Collects live game data for API responses (based on ServerstatusQuery patterns)
/// </summary>
public class DataCollector : IDataCollector
{
        private readonly ICoreServerAPI _sapi;
        
        // Animal caching (like ServerstatusQuery does)
        private List<AnimalData>? _animalsCache;
        private DateTime _animalsCacheUntil = DateTime.MinValue;
        private const int AnimalsCacheSeconds = 3;
        private const int AnimalsMax = 200;
        
        // Full data caching for thread safety and performance
        private ServerStatusData? _fullDataCache;
        private long _fullDataCacheExpiry;
        private const int CACHE_DURATION_MS = 1000; // 1 second cache
        private readonly object _cacheLock = new object();

        public DataCollector(ICoreServerAPI sapi)
        {
            _sapi = sapi;
        }

        public ServerStatusData CollectData()
        {
            var now = _sapi.World.ElapsedMilliseconds;
            
            // Check cache first (thread-safe)
            lock (_cacheLock)
            {
                if (_fullDataCache != null && now < _fullDataCacheExpiry)
                {
                    return _fullDataCache;
                }
            }
            
            // Cache miss - collect fresh data
            var data = new ServerStatusData
            {
                SpawnPoint = GetSpawnPoint(),
                Date = GetGameDate(),
                Weather = GetWeatherInfo(),
                Players = GetPlayersData(),
                Animals = GetAnimalsData()
            };

            // Add spawn point climate data
            if (data.SpawnPoint != null)
            {
                var spawnPos = new BlockPos((int)data.SpawnPoint.X, (int)data.SpawnPoint.Y, (int)data.SpawnPoint.Z);
                var climate = _sapi.World.BlockAccessor.GetClimateAt(spawnPos, EnumGetClimateMode.NowValues);
                if (climate != null)
                {
                    data.SpawnTemperature = FiniteOrNull(climate.Temperature);
                    data.SpawnRainfall = FiniteOrNull(climate.Rainfall);
                }
            }
            
            // Update cache (thread-safe)
            lock (_cacheLock)
            {
                _fullDataCache = data;
                _fullDataCacheExpiry = now + CACHE_DURATION_MS;
            }

            return data;
        }

        private SpawnPoint GetSpawnPoint()
        {
            try
            {
                var spawnPos = _sapi.World.DefaultSpawnPosition;
                if (spawnPos == null) return new SpawnPoint { X = 0, Y = _sapi.World.SeaLevel, Z = 0 };

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
                return new SpawnPoint { X = 0, Y = _sapi.World.SeaLevel, Z = 0 };
            }
        }

        private DateInfo GetGameDate()
        {
            var calendar = _sapi.World.Calendar;
            if (calendar == null)
            {
                return new DateInfo { Year = 1, Month = 1, Day = 1, Hour = 0, Minute = 0 };
            }

            // ServerstatusQuery approach: use DayOfMonth and Month directly
            int daysPerMonth = Math.Max(1, calendar.DaysPerMonth);
            int daysPerYear = Math.Max(daysPerMonth, calendar.DaysPerYear);
            
            int dayOfYear = calendar.DayOfYear;
            if (dayOfYear < 0) dayOfYear = 0;
            if (dayOfYear >= daysPerYear) dayOfYear = daysPerYear - 1;
            
            int day = (dayOfYear % daysPerMonth) + 1;
            int fullHour = calendar.FullHourOfDay;
            int minute = (int)Math.Floor((calendar.HourOfDay - fullHour) * 60.0 + 0.0000001);
            
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
                BlockPos pos;
                if (_sapi.World.DefaultSpawnPosition != null)
                {
                    pos = _sapi.World.DefaultSpawnPosition.AsBlockPos;
                }
                else
                {
                    // Fallback to first online player position
                    var firstPlayer = System.Linq.Enumerable.FirstOrDefault(_sapi.World.AllOnlinePlayers, p => p?.Entity != null);
                    if (firstPlayer != null)
                    {
                        var playerPos = firstPlayer.Entity.Pos.AsBlockPos;
                        pos = new BlockPos(playerPos.X, playerPos.Y, playerPos.Z);
                    }
                    else
                    {
                        pos = new BlockPos(0, _sapi.World.SeaLevel, 0);
                    }
                }

                var climate = _sapi.World.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues);
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
                _sapi.Logger.Warning($"Failed to get weather info: {ex.Message}");
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
                foreach (var player in _sapi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;

                    try
                    {
                        // Use ServerstatusQuery approach: directly access position
                        var entityPos = player.Entity.Pos?.XYZ ?? new Vec3d(0, _sapi.World.SeaLevel, 0);
                        var pos = new BlockPos((int)entityPos.X, (int)entityPos.Y, (int)entityPos.Z);
                        
                        var climate = _sapi.World.BlockAccessor?.GetClimateAt(pos, EnumGetClimateMode.NowValues);
                        var watchedAttributes = player.Entity.WatchedAttributes;

                        // Health - using ITreeAttribute like ServerstatusQuery
                        var healthTree = (watchedAttributes as TreeAttribute)?.GetTreeAttribute("health");
                        var currentHealth = FiniteOrNull(healthTree?.GetFloat("currenthealth", 0f)) ?? 0;
                        var maxHealth = FiniteOrNull(healthTree?.GetFloat("maxhealth", 0f)) ?? 20;

                        // Hunger
                        var hungerTree = (watchedAttributes as TreeAttribute)?.GetTreeAttribute("hunger");
                        var currentSaturation = FiniteOrNull(hungerTree?.GetFloat("currentsaturation", 0f)) ?? 0;
                        var maxSaturation = FiniteOrNull(hungerTree?.GetFloat("maxsaturation", 0f)) ?? 1500;

                        // Body temperature
                        var bodyTempTree = (watchedAttributes as TreeAttribute)?.GetTreeAttribute("bodyTemp");
                        var bodyTemp = FiniteOrNull(bodyTempTree?.GetFloat("bodytemp", 0f));

                        players.Add(new PlayerData
                        {
                            Name = player.PlayerName,
                            UID = player.PlayerUID,
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
                        _sapi.Logger.Warning($"Failed to collect data for player {player.PlayerName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _sapi.Logger.Warning($"Failed to collect players data: {ex.Message}");
            }

            return players;
        }

        private List<AnimalData> GetAnimalsData()
        {
            // Use cached data if still valid (like ServerstatusQuery)
            if (_animalsCache != null && DateTime.UtcNow < _animalsCacheUntil)
            {
                _sapi.Logger.Debug($"[WebCartographer] Using cached animal data ({_animalsCache.Count} animals)");
                return _animalsCache;
            }

            var animals = new List<AnimalData>();
            int totalEntities = 0;
            int processedAgents = 0;

            try
            {
                var loadedEntities = _sapi.World.LoadedEntities;
                if (loadedEntities == null) return animals;

                foreach (var entity in loadedEntities.Values)
                {
                    totalEntities++;
                    
                    try
                    {
                        // ServerstatusQuery checks
                        if (entity == null || entity.World == null) continue;
                        if (entity is EntityPlayer) continue;
                        if (!(entity is EntityAgent entityAgent)) continue;
                        if (!entity.Alive) continue;

                        processedAgents++;

                        // Use ServerstatusQuery approach: check Code first, then GetName()
                        string? typeCode = entity.Code?.ToString();
                        string name = entity.GetName() ?? typeCode ?? "unknown";

                        // Get health using ITreeAttribute
                        var healthTree = (entity.WatchedAttributes as TreeAttribute)?.GetTreeAttribute("health");
                        var currentHealth = FiniteOrNull(healthTree?.GetFloat("currenthealth", 0f));
                        var maxHealth = FiniteOrNull(healthTree?.GetFloat("maxhealth", 0f));

                        // Fallback health values
                        if (!maxHealth.HasValue || maxHealth <= 0)
                        {
                            maxHealth = 20;
                            currentHealth = entity.Alive ? 20 : 0;
                        }

                        // Use ServerPos like ServerstatusQuery
                        int x = entity.ServerPos != null ? (int)entity.ServerPos.X : 0;
                        int y = entity.ServerPos != null ? (int)entity.ServerPos.Y : _sapi.World.SeaLevel;
                        int z = entity.ServerPos != null ? (int)entity.ServerPos.Z : 0;

                        // Get climate data
                        double? temperature = null;
                        double? rainfall = null;
                        
                        if (_sapi.World.BlockAccessor != null)
                        {
                            var climate = _sapi.World.BlockAccessor.GetClimateAt(new BlockPos(x, y, z), EnumGetClimateMode.NowValues);
                            if (climate != null)
                            {
                                temperature = FiniteOrNull(climate.Temperature);
                                rainfall = FiniteOrNull(climate.Rainfall);
                            }
                        }

                        // Get wind data
                        var windPercent = GetWindPercent(new BlockPos(x, y, z));

                        animals.Add(new AnimalData
                        {
                            Type = typeCode ?? "unknown",
                            Name = string.IsNullOrWhiteSpace(name) ? typeCode ?? "unknown" : name,
                            Coordinates = new CoordinateData { X = x, Y = y, Z = z },
                            Health = new HealthData
                            {
                                Current = currentHealth ?? 0,
                                Max = maxHealth ?? 20
                            },
                            Temperature = temperature,
                            Rainfall = rainfall ?? 0,
                            Wind = new WindData { Percent = windPercent }
                        });

                        // Limit like ServerstatusQuery
                        if (animals.Count >= AnimalsMax)
                            break;
                    }
                    catch (Exception ex)
                    {
                        _sapi.Logger.Warning($"Failed to process entity: {ex.Message}");
                    }
                }

                _sapi.Logger.Debug($"[WebCartographer] Animal scan: {totalEntities} total entities, {processedAgents} alive agents, {animals.Count} animals added");
            }
            catch (Exception ex)
            {
                _sapi.Logger.Warning($"Failed to collect animals data: {ex.Message}");
            }

            // Cache the results
            _animalsCache = animals;
            _animalsCacheUntil = DateTime.UtcNow.AddSeconds(AnimalsCacheSeconds);

            return animals;
        }

        // Helper method from ServerstatusQuery
        private static float? FiniteOrNull(float? f)
        {
            if (!f.HasValue) return null;
            float value = f.Value;
            return float.IsNaN(value) || float.IsInfinity(value) ? null : value;
        }

        private static double? FiniteOrNull(double? d)
        {
            if (!d.HasValue) return null;
            double value = d.Value;
            return double.IsNaN(value) || double.IsInfinity(value) ? null : value;
        }

        private double GetWindSpeed(BlockPos pos)
        {
            try
            {
                if (_sapi.World.BlockAccessor == null || pos == null) return 0;
                
                var windVec = _sapi.World.BlockAccessor.GetWindSpeedAt(pos);
                if (windVec == null) return 0;

                // Calculate magnitude
                double speed = Math.Sqrt(windVec.X * windVec.X + windVec.Y * windVec.Y + windVec.Z * windVec.Z);
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
                if (_sapi.World.BlockAccessor == null || pos == null) return 0;
                
                var windVec = _sapi.World.BlockAccessor.GetWindSpeedAt(pos);
                if (windVec == null) return 0;

                // Calculate magnitude and convert to percentage
                double speed = Math.Sqrt(windVec.X * windVec.X + windVec.Y * windVec.Y + windVec.Z * windVec.Z);
                int percent = (int)Math.Round(speed * 100.0);
                
                return FiniteOrNull(percent) ?? 0;
            }
            catch
            {
                return 0;
            }
        }
}
