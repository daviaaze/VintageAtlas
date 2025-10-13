#if DEBUG
#define SINGEL_THREAD
#endif
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SkiaSharp;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Common.Database;
using Vintagestory.GameContent;
using Vintagestory.Server;
using WebCartographer.GeoJson;
using WebCartographer.GeoJson.Sign;
using WebCartographer.GeoJson.SignPost;
using WebCartographer.GeoJson.Trader;
using WebCartographer.GeoJson.Translocator;

namespace WebCartographer;

public class Extractor
{
    private readonly SavegameDataLoader _savegameDataLoader;

    private readonly ServerMain _server;

    private readonly int _chunkSize;

    private readonly Dictionary<int, List<uint>> _blockColor;

    private readonly Config _config;

    private readonly ILogger _logger;

    private readonly int _mapXHalf;
    private readonly int _mapZHalf;
    private readonly int _mapYHalf;
    private readonly int _mapSizeY;

    private byte[] _block2Color = null!;
    private bool[] _blockIsLake = null!;
    private readonly uint[] _colors;

    public Extractor(ServerMain server, Config config, ILogger modLogger)
    {
        _logger = modLogger;
        _server = server;
        _config = config;
        _savegameDataLoader = new SavegameDataLoader(_server, _config.MaxDegreeOfParallelism, modLogger);
        _blockColor = new Dictionary<int, List<uint>>();
        _chunkSize = MagicNum.ServerChunkSize;
        _colors = new uint[MapColors.ColorsByCode.Count];
        _mapXHalf = _server.WorldMap.MapSizeX / 2;
        _mapZHalf = _server.WorldMap.MapSizeZ / 2;
        _mapYHalf = _server.WorldMap.MapSizeY / 2;
        _mapSizeY = _server.WorldMap.MapSizeY;
    }

    public void Run()
    {
        try
        {
            _server.Api.Logger.Notification("WebCartographer is now starting...");
            if (_config.FixWhiteLines || _config.ExtractWorldMap)
            {
                _server.Api.Logger.Notification("WebCartographer is now preparing for FixWhiteLines or ExtractWorldMap ...");
                LoadBlockColorsJson();
            }

            if (_config.FixWhiteLines)
            {
                _server.Api.Logger.Notification("WebCartographer is now running FixWhiteLines...");
                FixWhiteLines();
            }

            if (_config.ExtractWorldMap)
            {
                _server.Api.Logger.Notification("WebCartographer is now running ExtractWorldMap...");
                ExtractWorldMap();
            }

            if (_config.CreateZoomLevels)
            {
                _server.Api.Logger.Notification("WebCartographer is now running CreateZoomLevels...");
                CreateZoomLevels();
            }

            if (_config.ExtractStructures)
            {
                _server.Api.Logger.Notification("WebCartographer is now running ExtractStructures...");
                ExtractStructures();
            }

            if (_config.ExportChunkVersionMap)
            {
                _server.Api.Logger.Notification("WebCartographer is now running ExtractChunkVersions...");
                ExtractChunkVersions();
            }

            _server.Api.Logger.Notification("WebCartographer did finish");
        }
        catch (Exception e)
        {
            _logger.Error(e);
        }

        _savegameDataLoader.Dispose();
    }

    private void ExtractStructures()
    {
        var traderGeoJson = new TraderGeoJson();
        var translocatorGeoJson = new TranslocatorGeoJson();
        IEnumerable<DblChunk<ServerMapRegion>> allServerMapRegions;

        var sqliteConnRegions = _savegameDataLoader.SqliteThreadConn;
        lock (sqliteConnRegions.Con)
        {
            allServerMapRegions = SavegameDataLoader.GetAllServerMapRegions(sqliteConnRegions);
        }

        var processedTranslocators = new Dictionary<string, TranslocatorFeature>();
        var itr = 0;
        var loc = new object();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _config.MaxDegreeOfParallelism
        };
#if SINGEL_THREAD
        foreach (var serverMapRegion in allServerMapRegions)
#else
        Parallel.ForEach(allServerMapRegions, parallelOptions, serverMapRegion =>
#endif
            {
                var sqliteConn = _savegameDataLoader.SqliteThreadConn;
                lock (sqliteConn.Con)
                {
                    lock (loc)
                    {
                        itr++;
                        if (itr % 100 == 0)
                        {
                            _logger.Notification($"mapregion: {itr}");
                        }
                    }

                    // find traders
                    foreach (var structure in serverMapRegion.Data.GeneratedStructures.Where(s =>
                                 s.Group is not null && s.Group.Contains("trader")))
                    {
                        var chunksToLoad = new HashSet<Vec3i>
                        {
                            new(structure.Location.X1 / _chunkSize, structure.Location.Y1 / _chunkSize,
                                structure.Location.Z1 / _chunkSize),
                            new(structure.Location.X2 / _chunkSize, structure.Location.Y1 / _chunkSize,
                                structure.Location.Z1 / _chunkSize),
                            new(structure.Location.X1 / _chunkSize, structure.Location.Y2 / _chunkSize,
                                structure.Location.Z1 / _chunkSize),
                            new(structure.Location.X2 / _chunkSize, structure.Location.Y2 / _chunkSize,
                                structure.Location.Z1 / _chunkSize),
                            new(structure.Location.X1 / _chunkSize, structure.Location.Y1 / _chunkSize,
                                structure.Location.Z2 / _chunkSize),
                            new(structure.Location.X2 / _chunkSize, structure.Location.Y1 / _chunkSize,
                                structure.Location.Z2 / _chunkSize),
                            new(structure.Location.X1 / _chunkSize, structure.Location.Y2 / _chunkSize,
                                structure.Location.Z2 / _chunkSize),
                            new(structure.Location.X2 / _chunkSize, structure.Location.Y2 / _chunkSize,
                                structure.Location.Z2 / _chunkSize)
                        };

                        foreach (var serverChunk in chunksToLoad.Select(pos => _savegameDataLoader.GetServerChunk(sqliteConn, pos)))
                        {
                            serverChunk?.Unpack_ReadOnly();
                            var enumerable = serverChunk?.Entities.Where(e => e is EntityTrader);
                            if (enumerable == null) continue;
                            foreach (var humanoid in enumerable)
                            {
                                if (humanoid is not EntityTrader trader) continue;
                                var entityBehaviorName =
                                    trader.WatchedAttributes.GetTreeAttribute("nametag").GetString("name");
                                // item-creature-humanoid-trader-commodities
                                var wares = Lang.Get("item-creature-" + trader.Code.Path);
                                var feature = new TraderFeature(
                                    new TraderProperties(entityBehaviorName, wares, trader.Pos.AsBlockPos.Y),
                                    new PointGeometry(GetGeoJsonCoordinates(trader.Pos.AsBlockPos)));
                                traderGeoJson.Features.Add(feature);
                            }

                            serverChunk?.Dispose();
                        }
                    }


                    foreach (var structure in serverMapRegion.Data.GeneratedStructures.Where(s => s.Code.Contains("gates")))
                    {
                        var chunksToLoad = new HashSet<Vec3i>
                        {
                            new(structure.Location.X1 / _chunkSize, structure.Location.Y1 / _chunkSize,
                                structure.Location.Z1 / _chunkSize),
                            new(structure.Location.X2 / _chunkSize, structure.Location.Y1 / _chunkSize,
                                structure.Location.Z1 / _chunkSize),
                            new(structure.Location.X1 / _chunkSize, structure.Location.Y2 / _chunkSize,
                                structure.Location.Z1 / _chunkSize),
                            new(structure.Location.X2 / _chunkSize, structure.Location.Y2 / _chunkSize,
                                structure.Location.Z1 / _chunkSize),
                            new(structure.Location.X1 / _chunkSize, structure.Location.Y1 / _chunkSize,
                                structure.Location.Z2 / _chunkSize),
                            new(structure.Location.X2 / _chunkSize, structure.Location.Y1 / _chunkSize,
                                structure.Location.Z2 / _chunkSize),
                            new(structure.Location.X1 / _chunkSize, structure.Location.Y2 / _chunkSize,
                                structure.Location.Z2 / _chunkSize),
                            new(structure.Location.X2 / _chunkSize, structure.Location.Y2 / _chunkSize,
                                structure.Location.Z2 / _chunkSize)
                        };

                        var chunksToLoadTarget = new HashSet<Vec3i>();
                        foreach (var serverChunk in chunksToLoad.Select(pos => _savegameDataLoader.GetServerChunk(sqliteConn, pos)))
                        {
                            serverChunk?.Unpack_ReadOnly();
                            var translocators = serverChunk?.BlockEntities.Values.Where(e =>
                                (e as BlockEntityStaticTranslocator)?.TargetLocation != null);

                            if (translocators == null) continue;
                            foreach (var translocator in translocators)
                            {
                                if (translocator is not BlockEntityStaticTranslocator tlEntity) continue;
                                chunksToLoadTarget.Add(new Vec3i(tlEntity.TargetLocation.X / _chunkSize,
                                    tlEntity.TargetLocation.Y / _chunkSize,
                                    tlEntity.TargetLocation.Z / _chunkSize));

                                if (serverChunk != null)
                                    ExtractTranslocators(serverChunk, tlEntity, processedTranslocators, translocatorGeoJson);
                            }

                            serverChunk?.Dispose();
                        }

                        foreach (var serverChunk in chunksToLoadTarget.Select(pos => _savegameDataLoader.GetServerChunk(sqliteConn, pos)))
                        {
                            serverChunk?.Unpack_ReadOnly();
                            var translocators = serverChunk?.BlockEntities.Values.Where(e =>
                                (e as BlockEntityStaticTranslocator)?.TargetLocation != null);
                            if (translocators == null) continue;
                            foreach (var translocator in translocators)
                            {
                                var tlEntity = translocator as BlockEntityStaticTranslocator;
                                if (serverChunk == null || tlEntity == null) continue;
                                ExtractTranslocators(serverChunk, tlEntity, processedTranslocators,
                                    translocatorGeoJson);
                            }

                            serverChunk?.Dispose();
                        }
                    }
                }

                sqliteConn.Free();
            }
#if !SINGEL_THREAD
        );
#endif
        var teleporterManager = _server.Api.ModLoader.GetModSystem<TeleporterManager>();

        var teleporters = typeof(TeleporterManager).GetField("Locations", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(teleporterManager);
        if (teleporters is Dictionary<BlockPos, TeleporterLocation> locations)
        {
            var processedLocations = new HashSet<BlockPos>();
            foreach (var (pos, location) in locations)
            {
                if(processedLocations.Contains(location.SourcePos)) continue;
                if(location.SourcePos == null || location.TargetPos == null) continue;
                processedLocations.Add(location.TargetPos);
                var a = new List<List<int>>
                {
                    GetGeoJsonCoordinates(location.SourcePos),
                    GetGeoJsonCoordinates(location.TargetPos)
                };
               var feature = new TranslocatorFeature(new TranslocatorProperties(location.SourcePos.Y, location.TargetPos.Y),
                    new LineGeometry(a), "Feature");
               feature.Properties.Tag = "TP";
               feature.Properties.Label = $"{location.SourceName} <-> {location.TargetName}";
               translocatorGeoJson.Features.Add(feature);
            }
        }
        
        sqliteConnRegions.Free();
        SaveJsonToFile(traderGeoJson, "traders.geojson");
        SaveJsonToFile(translocatorGeoJson, "translocators.geojson");
        _logger.Notification("Finished Exporting traders and translocators");
    }

    private void ExtractTranslocators(ServerChunk serverChunk, BlockEntityStaticTranslocator tlEntity,
        IDictionary<string, TranslocatorFeature> processedTranslocators, TranslocatorGeoJson translocatorGeoJson)
    {
        var signs = serverChunk.BlockEntities.Values.Where(sign =>
                sign is BlockEntitySign entitySign && entitySign.text.Contains("<AM:TL>"))
            .Select(s => (BlockEntitySign)s)
            .ToList();

        signs.Sort((s1, s2) =>
        {
            var sDist = Math.Abs(s1.Pos.X - tlEntity.Pos.X) + Math.Abs(s1.Pos.Y - tlEntity.Pos.Y) +
                        Math.Abs(s1.Pos.Z - tlEntity.Pos.Z);
            var s2Dist = Math.Abs(s2.Pos.X - tlEntity.Pos.X) + Math.Abs(s2.Pos.Y - tlEntity.Pos.Y) +
                         Math.Abs(s2.Pos.Z - tlEntity.Pos.Z);
            return sDist - s2Dist;
        });

        var sign = signs.FirstOrDefault();
        if (processedTranslocators.TryGetValue($"{tlEntity.Pos}{tlEntity.TargetLocation}", out var feature))
        {
            if (sign != null)
            {
                feature.SetTexts(sign.text);
            }

            return;
        }

        feature = new TranslocatorFeature(new TranslocatorProperties(tlEntity.Pos.Y, tlEntity.TargetLocation.Y),
            new LineGeometry(GetTranslocatorCoordinates(tlEntity)), "Feature");

        if (sign != null)
        {
            feature.SetTexts(sign.text);
        }

        var p2t = $"{tlEntity.Pos}{tlEntity.TargetLocation}";
        var t2p = $"{tlEntity.TargetLocation}{tlEntity.Pos}";
        processedTranslocators.TryAdd(p2t, feature);

        processedTranslocators.TryAdd(t2p, feature);

        translocatorGeoJson.Features.Add(feature);
    }

    private List<int> GetGeoJsonCoordinates(BlockPos pos)
    {
        var x = pos.X;
        var z = pos.Z;

        var spawnX = (int)(_server.World.DefaultSpawnPosition?.X ?? _mapXHalf);
        var spawnZ = (int)(_server.World.DefaultSpawnPosition?.Z ?? _mapZHalf);

        if (!_config.AbsolutePositions)
        {
            x = x - spawnX;
            z = (z - spawnZ) * -1;
        }

        return new List<int>
        {
            x, z
        };
    }

    private List<List<int>> GetTranslocatorCoordinates(BlockEntityStaticTranslocator tlEntity)
    {
        return new List<List<int>>
        {
            GetGeoJsonCoordinates(tlEntity.Pos),
            GetGeoJsonCoordinates(tlEntity.TargetLocation)
        };
    }

    private void SaveJsonToFile<T>(T data, string filename)
    {
        using var file = File.CreateText(Path.Combine(_config.OutputDirectoryGeojson, filename));
        var serializer = new JsonSerializer();
        serializer.Serialize(file, data);
    }

    private void FixWhiteLines()
    {
        var sqliteConn = _savegameDataLoader.SqliteThreadConn;
        IEnumerable<ChunkPos> mapChunkPositions;
        lock (sqliteConn.Con)
        {
            mapChunkPositions = _savegameDataLoader.GetAllMapChunkPositions(sqliteConn);
        }

        var toSave = new ConcurrentDictionary<long, ServerMapChunk>();
        sqliteConn.Free();

        var itr = 1;
        var loc = new object();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _config.MaxDegreeOfParallelism
        };
#if SINGEL_THREAD
        foreach (var chunkPos in mapChunkPositions)
#else
        Parallel.ForEach(mapChunkPositions, parallelOptions, chunkPos =>
#endif
            {
                sqliteConn = _savegameDataLoader.SqliteThreadConn;
                lock (sqliteConn.Con)
                {
                    lock (loc)
                    {
                        itr++;
                        if (itr % 2000 == 0)
                        {
                            _logger.Notification($"Working on {itr} chunks...");
                        }
                    }

                    var serverChunk = _savegameDataLoader.GetServerChunkT(sqliteConn, chunkPos);
                    if (serverChunk is null || !serverChunk.GameVersionCreated.StartsWith("1.18"))
                    {
#if SINGEL_THREAD
                        sqliteConn.Free();
                        continue;
#else
                        sqliteConn.Free();
                        return;
#endif
                    }

                    var serverMapChunk = _savegameDataLoader.GetServerMapChunkT(sqliteConn, chunkPos);
                    if (serverMapChunk is null)
                    {
#if SINGEL_THREAD
                    sqliteConn.Free();
                    continue;
#else
                    sqliteConn.Free();
                    return;
#endif
                    }

                    var pos = new ChunkPos();
                    var chunksToLoad = new List<int>();
                    var serverChunks = new ServerChunk?[_mapSizeY / _chunkSize];

                    // check which chunk need to be loaded to get the top surface block
                    CheckChunksToLoad(serverMapChunk, chunksToLoad);

                    // load chunks from database
                    foreach (var y in chunksToLoad)
                    {
                        pos.Set(chunkPos.X, y, chunkPos.Z);
                        serverChunks[y] = _savegameDataLoader.GetServerChunk(sqliteConn, pos.ToChunkIndex());

                        serverChunks[y]?.Unpack();
                    }

                    var chunkIndex = (long)ChunkPos.ToChunkIndex(chunkPos.X, 0, chunkPos.Z);
                    for (var dx = 0; dx < _chunkSize; dx++)
                    {
                        for (var dz = 0; dz < _chunkSize; dz++)
                        {
                            for (var dy = _mapSizeY - 1; dy >= 0; dy--)
                            {
                                var chunk = serverChunks[dy / _chunkSize];
                                if (chunk is null) continue;
                                var index = ((dy % _chunkSize) * _chunkSize + dz) * _chunkSize + dx;
                                var block =
                                    _server.World.Blocks[chunk.Data.GetBlockId(index, BlockLayersAccess.FluidOrSolid)];

                                if (!block.RainPermeable || dy == 0)
                                {
                                    serverMapChunk.RainHeightMap[dz * _chunkSize + dx] = (ushort)dy;
                                    if (!toSave.ContainsKey(chunkIndex))
                                    {
                                        toSave.TryAdd(chunkIndex, serverMapChunk);
                                        serverMapChunk.MarkDirty();
                                    }

                                    break;
                                }
                            }
                        }
                    }

                    if (toSave.Count >= 10000)
                    {
                        if (toSave.Count >= 10000)
                        {
                            _logger.Notification("Saving 10000 chunks...");
                            _savegameDataLoader.SaveMapChunks(toSave);
                            toSave.Clear();
                        }
                    }
                }

                sqliteConn.Free();
            }
#if !SINGEL_THREAD
        );
#endif

        if (toSave.Count > 0)
        {
            _logger.Notification("Saving rest of chunks...");
            _savegameDataLoader.SaveMapChunks(toSave);
            toSave.Clear();
        }
    }

    private void ExtractChunkVersions()
    {
        var sqliteConn = _savegameDataLoader.SqliteThreadConn;
        lock (sqliteConn.Con)
        {
            var allChunks = new Dictionary<ChunkPos, string>();
            var mapChunkPositions = _savegameDataLoader.GetAllMapChunkPositions(sqliteConn);
            var itr = 0;
            foreach (var chunksPosition in mapChunkPositions)
            {
                ServerMapChunk? chunk;
                try
                {
                    chunk = _savegameDataLoader.GetServerMapChunk(sqliteConn, chunksPosition);
                }
                catch (Exception)
                {
                    _logger.Notification(
                        $"chunk: {chunksPosition.X * _chunkSize} {chunksPosition.Z * _chunkSize} seems broken (skipped). Try running in repairmode to fix it.");
                    continue;
                }

                var pos = new ChunkPos();

                if (chunk is null)
                {
                    _logger.Notification($"chunk: {chunksPosition} was null skipping");
                    continue;
                }

                itr++;
                if (itr % 10000 == 0)
                {
                    _logger.Notification($"mapchunk: {itr}");
                }

                pos.Set(chunksPosition.X, 0, chunksPosition.Z);
                var serverChunk = _savegameDataLoader.GetServerChunk(sqliteConn, pos.ToChunkIndex());

                if (serverChunk != null)
                    allChunks.Add(new ChunkPos(pos.X, 0, pos.Z), serverChunk.GameVersionCreated);
                serverChunk?.Dispose();
            }

            var chunkversionGeoJson = new ChunkversionGeoJson();
            var groupedPosition = new GroupChunks(allChunks, _server);
            _logger.Notification("grouping chunks by version...");
            var groupedPositions = groupedPosition.GroupPositions();

            groupedPosition.GenerateGradient(groupedPositions);

            itr = 0;
            foreach (var position in groupedPositions)
            {
                itr++;
                if (itr % 100 == 0)
                {
                    _logger.Notification($"genChunkVersion: {itr} / {groupedPositions.Count}");
                }

                var chunkVersionFeature = groupedPosition.GetShape(position);
                chunkversionGeoJson.Features.Add(chunkVersionFeature);
            }

            SaveJsonToFile(chunkversionGeoJson, "chunk.geojson");
        }

        sqliteConn.Free();
    }

    private unsafe void ExtractWorldMap()
    {
        _logger.Notification("Exporting world data and signs...");

        var microBlocks = _server.World.Blocks
            .Where(b => b.Code?.Path.StartsWith("chiseledblock") == true ||
                        b.Code?.Path.StartsWith("microblock") == true)
            .Select(x => x.Id)
            .ToHashSet();

        var size = _config.TileSize;
        var divisor = size / 32;

        var chunkGroups = PrepareChunkGroups(divisor);

        var itr = 0;
        var lck = new object();

        var landmarks = new SingGeoJson();
        var signPosts = new SignPostGeoJson();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _config.MaxDegreeOfParallelism
        };

#if SINGEL_THREAD
         foreach (var chunkGroup in chunkGroups)
#else
        Parallel.ForEach(chunkGroups, parallelOptions, chunkGroup =>
#endif
            {
                var sqliteConn = _savegameDataLoader.SqliteThreadConn;
                lock (sqliteConn.Con)
                {
                    var img = new SKBitmap(size, size);
                    var rand = new Random(DateTime.Now.Millisecond);

                    SKBitmap? imgHeight = null;
                    byte* imgHeightPtr = null;
                    var imgHeightRowBytes = 0;
                    if (_config.ExportHeightmap)
                    {
                        imgHeight = new SKBitmap(new SKImageInfo(size, size, SKColorType.Gray8));
                        imgHeightPtr = (byte*)imgHeight.GetPixels().ToPointer();
                        imgHeightRowBytes = imgHeight.RowBytes;
                    }

                    var imgPtr = (byte*)img.GetPixels().ToPointer();
                    var imgRowBytes = img.RowBytes;

                    lock (lck)
                    {
                        itr++;
                        if (itr % 100 == 0)
                        {
                            _logger.Notification($"chunk: {itr} / {chunkGroups.Count}");
                        }
                    }

                    Span<byte> shadowMap = null;
                    if (_config.Mode is ImageMode.ColorVariationsWithHillShading or ImageMode.MedievalStyleWithHillShading)
                    {
                        shadowMap = new byte[size * size];
                        for (var i = 0; i < shadowMap.Length; i++) shadowMap[i] = 128;
                    }

                    foreach (var chunksPosition in chunkGroup.Value)
                    {
                        ServerMapChunk? chunk;
                        try
                        {
                            chunk = _savegameDataLoader.GetServerMapChunk(sqliteConn, chunksPosition);
                        }
                        catch (Exception)
                        {
                            _logger.Notification(
                                $"chunk: {chunksPosition.X * _chunkSize} {chunksPosition.Z * _chunkSize} seems broken (skipped). Try running in repairmode to fix it.");
                            continue;
                        }

                        int blockId, chunkIndex, heighmapIndex, chunkY, chunkHeight;
                        ushort height;
                        var pos = new ChunkPos();
                        var chunksToLoad = new List<int>();
                        var serverChunks = new ServerChunk?[_mapSizeY / _chunkSize];
                        uint pixelColor = 0;
                        var color = new List<uint>();
                        int imgX, imgZ;

                        if (chunk is null)
                        {
                            _logger.Notification($"chunk: {chunksPosition} was null skipping");
                            continue;
                        }

                        // check which chunk need to be loaded to get the top surface block
                        CheckChunksToLoad(chunk, chunksToLoad);

                        // load chunks from the database
                        LoadChunks(sqliteConn ,chunksToLoad, pos, chunksPosition, serverChunks);

                        if (_config.ExportSigns)
                        {
                            ExtractSigns(serverChunks, landmarks, signPosts);
                        }

                        // get the topmost block
                        for (var x = 0; x < _chunkSize; x++)
                        {
                            for (var z = 0; z < _chunkSize; z++)
                            {
                                var isOff = 0;
                                heighmapIndex = z % _chunkSize * _chunkSize + x % _chunkSize;
                                height = (ushort)GameMath.Clamp(chunk.RainHeightMap[heighmapIndex], 0, _mapSizeY - 1);
                                chunkY = height / _chunkSize;

                                chunkHeight = height % _chunkSize;
                                chunkIndex = (chunkHeight * _chunkSize + z) * _chunkSize + x;
                                if (serverChunks[chunkY] == null)
                                {
                                    continue;
                                }

                                blockId = serverChunks[chunkY]!.Data[chunkIndex];

                                if (_server.World.Blocks[blockId].BlockMaterial == EnumBlockMaterial.Snow)
                                {
                                    isOff = 1;
                                    height--;
                                    chunkY = height / _chunkSize;
                                    if (serverChunks[chunkY] is null)
                                    {
                                        pos.Set(chunksPosition.X, chunkY, chunksPosition.Z);
                                        serverChunks[chunkY] =
                                            _savegameDataLoader.GetServerChunk(sqliteConn, pos.ToChunkIndex());
                                        serverChunks[chunkY]?.Unpack_ReadOnly();
                                    }

                                    chunkHeight = height % _chunkSize;
                                    chunkIndex = (chunkHeight * _chunkSize + z) * _chunkSize + x;
                                    // maybe null here if the chunk could not be loaded form db
                                    blockId = serverChunks[chunkY]?.Data[chunkIndex] ?? 0;
                                }

                                imgX = x + chunksPosition.X % divisor * _chunkSize;
                                imgZ = z + chunksPosition.Z % divisor * _chunkSize;

                                if (microBlocks.Contains(blockId))
                                {
                                    var blockPos = new BlockPos(chunksPosition.X * _chunkSize + x, height, chunksPosition.Z * _chunkSize + z, 0);
                                    BlockEntity? blockEntity = null;
                                    serverChunks[chunkY]?.BlockEntities.TryGetValue(blockPos, out blockEntity);

                                    if (blockEntity is BlockEntityMicroBlock blockEntityChisel)
                                    {
                                        if (_config.Mode != ImageMode.MedievalStyleWithHillShading)
                                        {
                                            if (!_blockColor.TryGetValue(blockEntityChisel.BlockIds[0], out color))
                                            {
                                                _logger.Error($"no color found for chiseledblock material: {blockId} @ {blockPos}");
                                            }
                                        }
                                        else
                                        {
                                            pixelColor = GetColor(blockEntityChisel.BlockIds[0]);
                                        }
                                    }
                                    else
                                    {
                                        if (_config.Mode != ImageMode.MedievalStyleWithHillShading)
                                        {
                                            color = new List<uint> { (uint)SKColors.Green };
                                        }
                                        else
                                        {
                                            // default to land for invalid chiselblock
                                            pixelColor = MapColors.ColorsByCode["land"];
                                        }
                                    }
                                }
                                else
                                {
                                    if (_config.Mode != ImageMode.MedievalStyleWithHillShading)
                                    {
                                        _blockColor.TryGetValue(blockId, out color);
                                    }
                                    else
                                    {
                                        pixelColor = GetMedievalStyleColor(blockId, z, x, chunkHeight, serverChunks, chunkY);
                                    }
                                }

                                try
                                {
                                    if (color is null)
                                    {
                                        var blockPos = new BlockPos(chunksPosition.X * _chunkSize + x, height, chunksPosition.Z * _chunkSize + z, 0);
                                        _logger.Error(
                                            $"Could not find color for {blockId} : {_server.World.Blocks[blockId].Code} @ {blockPos}. If you are using mode: 0,1,2,3 try see if a color reexport (.exportcolors using the color exporter mod) helps else report it as a bug");
                                        continue;
                                    }

                                    pixelColor = GetTilePixelColorAndHeight(rand, color, pixelColor, height + isOff, x, z, chunk,
                                        shadowMap, imgZ, size, imgX, blockId);

                                    var row = (uint*)(imgPtr + imgZ * imgRowBytes);
                                    row[imgX] = pixelColor;

                                    if (_config.ExportHeightmap)
                                    {
                                        var rowHeight = imgHeightPtr + imgZ * imgHeightRowBytes;
                                        rowHeight[imgX] = (byte)height;
                                    }
                                }
                                catch (Exception e)
                                {
                                    _logger.Error(
                                        $"imgX: {imgX} imgZ: {imgZ} col: {color} | {e.Message}");
                                }
                            }
                        }

                        foreach (var serverChunk in serverChunks)
                        {
                            serverChunk?.Dispose();
                        }
                    }

                    if (_config.Mode is ImageMode.ColorVariationsWithHillShading or ImageMode.MedievalStyleWithHillShading)
                    {
                        Span<byte> bla = new byte[shadowMap.Length];
                        shadowMap.CopyTo(bla);

                        BlurTool.Blur(shadowMap, size, size, 2);
                        const float sharpen = 1.4f;

                        for (var i = 0; i < shadowMap.Length; i++)
                        {
                            var b = (int)((shadowMap[i] / 128f - 1f) * 5) / 5f;
                            b += (bla[i] / 128f - 1f) * 5 % 1 / 5f;

                            var imgX = i % size;
                            var imgZ = i / size;
                            var row = (uint*)(imgPtr + imgZ * imgRowBytes);
                            if (b == 0) continue;
                            row[imgX] = (uint)(ColorUtil.ColorMultiply3Clamped((int)row[imgX], b * sharpen + 1f) | 255 << 24);
                        }
                    }

                    var file = Path.Combine(_config.OutputDirectoryWorld, _config.BaseZoomLevel.ToString(),
                        $"{chunkGroup.Key.X}_{chunkGroup.Key.Y}.png");
                    using Stream fileStream = File.OpenWrite(file);
                    img.Encode(SKEncodedImageFormat.Png, 100).SaveTo(fileStream);
                    img.Dispose();

                    if (_config.ExportHeightmap && imgHeight is not null)
                    {
                        var heightFile = Path.Combine(_config.OutputDirectory, "heightmap", $"{chunkGroup.Key.X}_{chunkGroup.Key.Y}.png");
                        using Stream fileStreamHeighmap = File.OpenWrite(heightFile);
                        imgHeight.Encode(SKEncodedImageFormat.Png, 100).SaveTo(fileStreamHeighmap);
                        imgHeight.Dispose();
                    }
                }

                sqliteConn.Free();
            }
#if !SINGEL_THREAD
        );
#endif
        if (_config.ExportSigns)
        {
            SaveJsonToFile(landmarks, "landmarks.geojson");
            SaveJsonToFile(signPosts, "signPosts.geojson");
        }
        _logger.Notification("Finished Exporting world data and signs");
    }

    private uint GetTilePixelColorAndHeight(Random rand, List<uint> color, uint pixelColor, int height, int x, int z,
        ServerMapChunk chunk, Span<byte> shadowMap, int imgZ, int size, int imgX, int blockId)
    {
        switch (_config.Mode)
        {
            case ImageMode.OnlyOneColor:
            {
                pixelColor = color[0];
                break;
            }
            case ImageMode.ColorVariations:
            {
                var next = rand.Next(color.Count);
                pixelColor = color[next];
                break;
            }
            case ImageMode.ColorVariationsWithHeight:
            {
                var next = rand.Next(color.Count);
                pixelColor = (uint)ColorUtil.ColorMultiply3Clamped((int)color[next], height / (float)_mapYHalf);
                break;
            }
            case ImageMode.ColorVariationsWithHillShading:
            {
                var next = rand.Next(color.Count);
                var (northWestDelta, northDelta, westDelta) = CalculateAltitudeDiffOptimized(x, height, z, chunk);
                var boostMultiplier = CalculateSlopeBoost(northWestDelta, northDelta, westDelta);

                shadowMap[imgZ * size + imgX] = (byte)(shadowMap[imgZ * size + imgX] * boostMultiplier);
                pixelColor = color[next];
                break;
            }
            case ImageMode.MedievalStyleWithHillShading:
            {
                if (!_blockIsLake[blockId])
                {
                    var (northWestDelta, northDelta, westDelta) = CalculateAltitudeDiffOptimized(x, height, z, chunk);
                    var boostMultiplier = CalculateSlopeBoost(northWestDelta, northDelta, westDelta);

                    shadowMap[imgZ * size + imgX] = (byte)(shadowMap[imgZ * size + imgX] * boostMultiplier);
                }

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }

        return pixelColor;
    }

    private uint GetMedievalStyleColor(int blockId, int z, int x, int chunkHeight, ServerChunk?[] serverChunks,
        int chunkY)
    {
        uint pixelColor;
        if (_blockIsLake[blockId])
        {
            var n = z - 1 % 32;
            var s = z + 1 % 32;
            var e = x + 1 % 32;
            var w = x - 1 % 32;

            var chunkIndexN = (chunkHeight * _chunkSize + n) * _chunkSize + x;
            var chunkIndexS = (chunkHeight * _chunkSize + s) * _chunkSize + x;
            var chunkIndexE = (chunkHeight * _chunkSize + z) * _chunkSize + e;
            var chunkIndexW = (chunkHeight * _chunkSize + z) * _chunkSize + w;

            var blockIdN = serverChunks[chunkY]!.Data[chunkIndexN];
            var blockIdS = serverChunks[chunkY]!.Data[chunkIndexS];
            var blockIdE = serverChunks[chunkY]!.Data[chunkIndexE];
            var blockIdW = serverChunks[chunkY]!.Data[chunkIndexW];

            if (_blockIsLake[blockIdN] && _blockIsLake[blockIdS] && _blockIsLake[blockIdE] && _blockIsLake[blockIdW])
            {
                pixelColor = GetColor(blockId);
            }
            else if (x == 31 || x == 0 || z == 31 || z == 0)
            {
                pixelColor = GetColor(blockId);
            }
            else
            {
                pixelColor = MapColors.ColorsByCode["wateredge"];
            }
        }
        else
        {
            pixelColor = GetColor(blockId);
        }

        return pixelColor;
    }

    private static bool IsLake(Block block)
    {
        return block.BlockMaterial == EnumBlockMaterial.Liquid ||
               (block.BlockMaterial == EnumBlockMaterial.Ice && block.Code.Path != "glacierice");
    }

    private record ImageData(string Path, int X, int Z);

    private void CreateZoomLevels()
    {
        // Define the tile size
        var tileSizeHalf = _config.TileSize / 2;
        using var outputImage = new SKBitmap(new SKImageInfo(_config.TileSize, _config.TileSize));
        using var canvas = new SKCanvas(outputImage);

        for (var i = _config.BaseZoomLevel; i >= 2; i--)
        {
            _logger.Notification("Generating zoom level: " + (i - 1));
            var path = Path.Combine(_config.OutputDirectoryWorld, i.ToString());
            var imagePaths = Directory.GetFiles(path);

            // deconstruct image urls to coords
            // /img/dir/1234_1234.png
            var imageCoords = imagePaths.Select(i =>
            {
                var indexUnderscore = i.LastIndexOf("_", StringComparison.InvariantCulture) + 1;
                var indexStart = i.LastIndexOf(Path.DirectorySeparatorChar.ToString(), StringComparison.InvariantCulture) + 1;
                var x = i.Substring(indexStart, indexUnderscore - indexStart - 1);
                var z = i.Substring(indexUnderscore, i.Length - indexUnderscore - 4);
                return new ImageData(i, int.Parse(x), int.Parse(z));
            }).ToList();

            var imageGroups = PrepareImageGroups(imageCoords);
            var itr = 0;
            foreach (var imageGroup in imageGroups)
            {
                canvas.Clear();
                outputImage.Erase(SKColor.Empty);

                itr++;
                if (itr % 100 == 0)
                {
                    _logger.Notification($"chunk: {itr} / {imageGroups.Count}");
                }

                foreach (var imageData in imageGroup.Value)
                {
                    using var image = SKBitmap.Decode(imageData.Path);
                    // Scale the image down by a factor of 4 each time
                    var left = tileSizeHalf * (imageData.X % 2);
                    var top = tileSizeHalf * (imageData.Z % 2);
                    canvas.DrawBitmap(image, new SKRect(left, top, left + tileSizeHalf, top + tileSizeHalf));
                }

                // image.ScalePixels(scaledImage, SKFilterQuality.Medium);
                var filename = $"{imageGroup.Key.X / 2}_{imageGroup.Key.Y / 2}.png";
                var file = Path.Combine(_config.OutputDirectoryWorld, (i - 1).ToString(), filename);

                using Stream fileStream = File.OpenWrite(file);
                outputImage.Encode(SKEncodedImageFormat.Png, 100).SaveTo(fileStream);
            }
        }
    }

    private float CalculateSlopeBoost(int northWestDelta, int northDelta, int westDelta)
    {
        var direction = Math.Sign(northWestDelta) + Math.Sign(northDelta) + Math.Sign(westDelta);
        float steepness = Math.Max(Math.Max(Math.Abs(northWestDelta), Math.Abs(northDelta)), Math.Abs(westDelta));
        var slopeFactor = Math.Min(0.5f, steepness / 10f) / 1.25f;

        if (direction > 0)
            return 1.08f + slopeFactor;
        if (direction < 0)
            return 0.92f - slopeFactor;
        return 1;
    }

    private (int northWestDelta, int northDelta, int westDelta) CalculateAltitudeDiffOptimized(int x, int y, int z,
        IMapChunk chunk)
    {
        var westernX = x - 1;
        var northernZ = z - 1;

        // optimised and less accurate but should be alright
        // go south and east if we are at the boarder to stay in same chunk so we dont have to load others which gets complicated with caching and is slower
        if (westernX < 0)
        {
            westernX++;
        }

        if (northernZ < 0)
        {
            northernZ++;
        }

        westernX = GameMath.Mod(westernX, _chunkSize);
        northernZ = GameMath.Mod(northernZ, _chunkSize);

        var northWestBlockIndex = MapUtil.Index2d(westernX, northernZ, _chunkSize);
        var northWestDelta = y - chunk.RainHeightMap[northWestBlockIndex];

        var northBlockIndex = MapUtil.Index2d(x, northernZ, _chunkSize);
        var northDelta = y - chunk.RainHeightMap[northBlockIndex];

        var westBlockIndex = MapUtil.Index2d(westernX, z, _chunkSize);
        var westDelta = y - chunk.RainHeightMap[westBlockIndex];

        return (northWestDelta, northDelta, westDelta);
    }

    private void ExtractSigns(IEnumerable<ServerChunk?> serverChunks, SingGeoJson signs, SignPostGeoJson signPosts)
    {
        foreach (var serverChunk in serverChunks.Where(s => s is not null))
        {
            var blockSigns = serverChunk?.BlockEntities.Where(be => be.Value is BlockEntitySign)
                .Select(be =>
                    new KeyValuePair<BlockPos, BlockEntitySign>(be.Key,
                        be.Value as BlockEntitySign ?? throw new UnreachableException())).ToList();
            if (blockSigns is null) continue;
            foreach (var blockSign in blockSigns)
            {
                var match = Regex.Match(blockSign.Value.text, "^<AM:(.*)>\n(.*)", RegexOptions.Singleline);
                if (match.Success)
                {
                    var feature = new SignFeature
                    (
                        new SignProperties(
                            match.Groups[2].Value.Trim(),
                            blockSign.Value.Pos.Y),
                        new PointGeometry(GetGeoJsonCoordinates(blockSign.Key))
                    );
                    switch (match.Groups[1].Value.ToLowerInvariant())
                    {
                        case "base":
                        case "misc":
                        case "server":
                        {
                            var name = match.Groups[1].Value.Substring(0,1).ToUpperInvariant() + 
                                       match.Groups[1].Value[1..].ToLowerInvariant();
                            feature.Properties.Type = name;
                            lock (signs)
                            {
                                signs.Features.Add(feature);
                            }

                            break;
                        }
                        case "tl":
                        {
                            // TL signs are handled directly along with the tl finding
                            break;
                        }
                        default:
                        {
                            if (_config.ExportCustomTaggedSigns)
                            {
                                var name = match.Groups[1].Value.Substring(0,1).ToUpperInvariant() + 
                                           match.Groups[1].Value[1..].ToLowerInvariant();
                                feature.Properties.Type = name;
                                lock (signs)
                                {
                                    signs.Features.Add(feature);
                                }
                            }

                            break;
                        }
                    }
                }
                else if (_config.ExportUntaggedSigns)
                {
                    var feature = new SignFeature(
                        new SignProperties(blockSign.Value.text.Trim(), blockSign.Value.Pos.Y, "default"),
                        new PointGeometry(GetGeoJsonCoordinates(blockSign.Key)));
                    lock (signs)
                    {
                        signs.Features.Add(feature);
                    }
                }
            }

            var blockSignPosts = serverChunk?.BlockEntities
                .Where(be => be.Value is BlockEntitySignPost).Select(be =>
                    new KeyValuePair<BlockPos, BlockEntitySignPost>(be.Key,
                        be.Value as BlockEntitySignPost ?? throw new UnreachableException())).ToList();
            if (blockSignPosts == null) continue;
            foreach (var blockSignPost in blockSignPosts)
            {
                for (var i = 0; i < blockSignPost.Value.textByCardinalDirection.Length; i++)
                {
                    if (blockSignPost.Value.textByCardinalDirection[i] == string.Empty) continue;
                    var label = blockSignPost.Value.textByCardinalDirection[i].Trim();
                    var feature =
                        new SignPostFeature(new SignPostProperties("SignPost", label, blockSignPost.Value.Pos.Y, i),
                            new PointGeometry(GetGeoJsonCoordinates(blockSignPost.Key)));
                    lock (signs)
                    {
                        signPosts.Features.Add(feature);
                    }
                }
            }
        }
    }

    private Dictionary<Vec2i, List<ChunkPos>> PrepareChunkGroups(int divisor)
    {
        _logger.Notification("preparing chunk group data...");
        var chunkGroups = new Dictionary<Vec2i, List<ChunkPos>>();
        var sqliteConn = _savegameDataLoader.SqliteThreadConn;
        lock (sqliteConn.Con)
        {
            var mapChunkPositions = _savegameDataLoader.GetAllMapChunkPositions(sqliteConn);
            foreach (var chunkPosition in mapChunkPositions)
            {
                var imgChunkPos = new Vec2i(chunkPosition.X / divisor, chunkPosition.Z / divisor);
                if (chunkGroups.TryGetValue(imgChunkPos, out var chunkPositions))
                {
                    chunkPositions.Add(chunkPosition);
                }
                else
                {
                    chunkGroups.Add(imgChunkPos, new List<ChunkPos> { chunkPosition });
                }
            }
        }

        sqliteConn.Free();

        _logger.Notification("Done preparing chunk group data");
        return chunkGroups;
    }

    private static Dictionary<Vec2i, List<ImageData>> PrepareImageGroups(IEnumerable<ImageData> positions)
    {
        var chunkGroups = new Dictionary<Vec2i, List<ImageData>>();
        foreach (var chunkPosition in positions)
        {
            var imgChunkPos = new Vec2i(chunkPosition.X - chunkPosition.X % 2,
                chunkPosition.Z - chunkPosition.Z % 2);
            if (chunkGroups.TryGetValue(imgChunkPos, out var chunkPositions))
            {
                chunkPositions.Add(chunkPosition);
            }
            else
            {
                chunkGroups.Add(imgChunkPos, new List<ImageData> { chunkPosition });
            }
        }

        return chunkGroups;
    }

    private void LoadChunks(SqliteThreadCon sqliteConn, List<int> chunksToLoad, ChunkPos pos, ChunkPos chunksPosition,
        IList<ServerChunk?> serverChunks)
    {
        foreach (var y in chunksToLoad)
        {
            pos.Set(chunksPosition.X, y, chunksPosition.Z);
            serverChunks[y] = _savegameDataLoader.GetServerChunk(sqliteConn, pos.ToChunkIndex());
            serverChunks[y]?.Unpack_ReadOnly();
        }
    }

    private void CheckChunksToLoad(ServerMapChunk chunk, ICollection<int> chunksToLoad)
    {
        int heightmapIndex, height, chunkY;
        for (var x = 0; x < _chunkSize; x++)
        {
            for (var z = 0; z < _chunkSize; z++)
            {
                heightmapIndex = z % _chunkSize * _chunkSize + x % _chunkSize;
                // seems sometimes the rain heightmap has values > mapSizeY
                height = (ushort)GameMath.Clamp(chunk.RainHeightMap[heightmapIndex], 0, _mapSizeY - 1);
                chunkY = height / _chunkSize;
                if (!chunksToLoad.Contains(chunkY))
                {
                    chunksToLoad.Add(chunkY);
                }
            }
        }
    }

    private void LoadBlockColorsJson()
    {
        var loadModConfig = _server.Api.LoadModConfig<ExportData>("blockColorMapping.json");
        if (_config.Mode != ImageMode.MedievalStyleWithHillShading && loadModConfig == null)
        {
            throw new Exception("blockColorMapping.json is missing! Stopping WebCartographer");
        }

        for (var i = 0; i < _colors.Length; i++)
        {
            _colors[i] = MapColors.ColorsByCode.GetValueAtIndex(i);
        }

        var max = _server.World.Blocks.Count;
        _block2Color = new byte[max + 1];
        _blockIsLake = new bool[max + 1];

        for (var id = 0; id < max; id++)
        {
            var block = _server.World.Blocks[id];
            if (block == null)
            {
                _block2Color[id] = (byte)MapColors.ColorsByCode.IndexOfKey("land");
                _blockIsLake[id] = false;
                continue;
            }

            if (block.BlockMaterial == EnumBlockMaterial.Snow && block.Code.Path.Contains("snowblock"))
            {
                _block2Color[id] = (byte)MapColors.ColorsByCode.IndexOfKey("glacier");
                _blockIsLake[id] = false;
                continue;
            }

            var colorCode = "land";
            if (block.Attributes != null)
            {
                colorCode = block.Attributes["mapColorCode"].AsString() ??
                            MapColors.GetDefaultMapColorCode(block.BlockMaterial);
            }

            _block2Color[id] = (byte)MapColors.ColorsByCode.IndexOfKey(colorCode);
            _blockIsLake[id] = IsLake(block);

            if (MapColors.ColorsByCode.IndexOfKey(colorCode) < 0)
            {
                throw new Exception("No color exists for color code " + colorCode);
            }
        }

        if (_config.Mode != ImageMode.MedievalStyleWithHillShading)
        {
            foreach (var block in loadModConfig.Blocks)
            {
                var colors = block.Value.Select(color => (uint)(color | 0xff000000)).ToList();
                var blockNow = _server.World.GetBlock(new AssetLocation(block.Key));
                if (blockNow != null)
                {
                    _blockColor.TryAdd(blockNow.Id, colors);
                }
            }
        }
    }

    private uint GetColor(int block)
    {
        var colorIndex = _block2Color[block];
        var color = _colors[colorIndex];
        return color;
    }
}