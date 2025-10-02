# Tile Generation System

**Last Updated:** 2025-10-02  
**Version:** 1.0.0

## Overview

VintageAtlas uses a sophisticated tile generation system that supports both full exports and dynamic on-demand generation. This document details the tile generation pipeline, caching strategies, and optimization techniques.

## Architecture

### Components

```
┌─────────────────────────────────────────────────────────┐
│ MapExporter                                             │
│ - Full map export orchestration                         │
│ - Multi-threaded chunk processing                       │
│ - Zoom level generation                                 │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│ DynamicTileGenerator                                    │
│ - On-demand tile generation                             │
│ - Incremental updates for modified chunks               │
│ - Tile metadata caching with ETags                      │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│ Extractor                                               │
│ - Core terrain rendering engine                         │
│ - Block color mapping                                   │
│ - Height-based shading                                  │
│ - Multiple render modes                                 │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│ PyramidTileDownsampler                                  │
│ - Lower zoom level generation                           │
│ - 4-to-1 tile downsampling                             │
│ - Image smoothing and filtering                         │
└─────────────────────────────────────────────────────────┘
```

## Full Map Export

### Export Process

**Triggered by:** `/atlas export` command or `AutoExportMap` config

**Steps:**

1. **Preparation**
   ```csharp
   - Load configuration
   - Validate output directory
   - Initialize thread pool
   - Clear old tiles (optional)
   ```

2. **Chunk Loading**
   ```csharp
   - Access savegame files
   - Load all world chunks
   - Parse chunk data
   - Build chunk index
   ```

3. **Base Tile Generation (Zoom N)**
   ```csharp
   - For each tile coordinate:
     ├─ Query overlapping chunks
     ├─ Render terrain colors
     ├─ Apply height shading
     ├─ Apply blur/smoothing
     └─ Save as PNG
   
   - Parallel processing:
     └─ MaxDegreeOfParallelism = ProcessorCount
   ```

4. **Pyramid Generation (Zoom N-1 to 0)**
   ```csharp
   - For each lower zoom level:
     ├─ For each tile position:
     │  ├─ Load 4 tiles from higher zoom
     │  ├─ Downsample to single tile
     │  ├─ Apply smoothing filter
     │  └─ Save as PNG
     └─ Continue to next zoom level
   ```

5. **Structure Export**
   ```csharp
   - Scan loaded chunks for:
     ├─ Traders
     ├─ Signs
     ├─ Signposts
     └─ Translocators
   
   - Generate GeoJSON files
   ```

### Performance Characteristics

**Test Case:** 35GB savegame, 8-core CPU

| Phase | Time | Memory | Disk I/O |
|-------|------|--------|----------|
| Chunk Loading | ~90s | 2GB | Read |
| Base Tiles (Zoom 9) | ~15min | 4GB | Write |
| Pyramid (Zoom 8-0) | ~5min | 2GB | Read+Write |
| Structure Export | ~2min | 1GB | Read |
| **Total** | **~22min** | **Peak 4GB** | **Heavy** |

### Code Example

```csharp
public class MapExporter
{
    public async Task<bool> ExportMap()
    {
        _logger.Notification("Starting full map export...");
        
        try
        {
            // Step 1: Load chunk data
            var chunks = await LoadChunkData();
            
            // Step 2: Generate base tiles
            await GenerateBaseTiles(chunks);
            
            // Step 3: Generate pyramid
            await GeneratePyramid();
            
            // Step 4: Export structures
            await ExportStructures(chunks);
            
            _logger.Notification("Map export complete!");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Map export failed: {0}", ex.Message);
            return false;
        }
    }
    
    private async Task GenerateBaseTiles(ChunkData[] chunks)
    {
        var tiles = CalculateTileCoordinates(chunks);
        
        await Parallel.ForEachAsync(tiles,
            new ParallelOptions 
            { 
                MaxDegreeOfParallelism = Environment.ProcessorCount 
            },
            async (tile, ct) =>
            {
                var image = await RenderTile(tile.X, tile.Z, chunks);
                await SaveTile(image, tile.X, tile.Z, _config.BaseZoomLevel);
            });
    }
}
```

## Dynamic Tile Generation

### On-Demand Generation

**Triggered by:** HTTP request for missing tile

**Flow:**

```
1. Browser requests: /tiles/9/2000_1999.png
   ↓
2. TileController checks disk cache
   ↓ [miss]
3. DynamicTileGenerator.GenerateTileAsync(9, 2000, 1999)
   ↓
4. Check if tile can be generated
   ├─ Loaded chunks in area?
   ├─ Yes: Generate from world data
   └─ No: Generate placeholder
   ↓
5. Save to disk
   ↓
6. Return PNG with ETag
   ↓
7. Browser caches (1 hour)
```

### Incremental Updates

**Triggered by:** Chunk modifications detected

**Flow:**

```
1. Player places/breaks blocks
   ↓
2. ChunkChangeTracker.OnBlockPlaced()
   ↓
3. Add to _modifiedChunks dictionary
   ↓
4. Every 30 seconds: OnGameTick()
   ↓
5. Query modified chunks
   ↓
6. DynamicTileGenerator.RegenerateTilesForChunksAsync()
   ↓
7. Calculate affected tiles:
   ├─ Convert chunk coords to tiles
   ├─ For each zoom level
   └─ Mark tiles for regeneration
   ↓
8. Regenerate tiles (parallel)
   ↓
9. Clear modified chunks
```

### Code Example

```csharp
public class DynamicTileGenerator
{
    private readonly ConcurrentDictionary<string, TileMetadata> _tileCache 
        = new ConcurrentDictionary<string, TileMetadata>();
    
    public async Task<TileResult> GenerateTileAsync(
        int zoom, 
        int tileX, 
        int tileZ, 
        string? ifNoneMatch)
    {
        var key = $"{zoom}_{tileX}_{tileZ}";
        
        // Check ETag
        if (_tileCache.TryGetValue(key, out var meta) 
            && meta.ETag == ifNoneMatch)
        {
            return TileResult.NotModified();
        }
        
        // Check disk cache
        var filePath = GetTilePath(zoom, tileX, tileZ);
        if (File.Exists(filePath))
        {
            var data = await File.ReadAllBytesAsync(filePath);
            var etag = GenerateETag(data);
            
            _tileCache[key] = new TileMetadata { ETag = etag };
            
            return TileResult.Success(data, etag);
        }
        
        // Generate tile
        var image = await GenerateTileInternal(zoom, tileX, tileZ);
        if (image != null)
        {
            await SaveTileAsync(image, filePath);
            var etag = GenerateETag(image);
            
            _tileCache[key] = new TileMetadata { ETag = etag };
            
            return TileResult.Success(image, etag);
        }
        
        // Fallback: placeholder
        var placeholder = await GeneratePlaceholderTile(zoom, tileX, tileZ);
        return TileResult.Success(placeholder, "placeholder");
    }
    
    public async Task RegenerateTilesForChunksAsync(List<Vec2i> modifiedChunks)
    {
        var tilesToRegenerate = new HashSet<(int zoom, int x, int z)>();
        
        // Calculate affected tiles for each zoom level
        foreach (var chunk in modifiedChunks)
        {
            for (int zoom = _config.BaseZoomLevel; zoom >= 0; zoom--)
            {
                var tiles = ChunkToTiles(chunk, zoom);
                foreach (var tile in tiles)
                {
                    tilesToRegenerate.Add(tile);
                }
            }
        }
        
        _logger.Debug("Regenerating {0} tiles for {1} modified chunks", 
            tilesToRegenerate.Count, modifiedChunks.Count);
        
        // Regenerate in parallel
        await Parallel.ForEachAsync(tilesToRegenerate,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            async (tile, ct) =>
            {
                await GenerateTileAsync(tile.zoom, tile.x, tile.z, null);
            });
    }
}
```

## Terrain Rendering

### Extractor Engine

The `Extractor` class handles core terrain rendering.

#### Render Modes

**Mode 4 (Medieval with Hill Shading)** - Recommended:
```csharp
- Base terrain colors from block mapping
- Height-based shading:
  └─ Lighter for higher elevation
  └─ Darker for lower elevation
- Hill shading:
  └─ Calculate slope from neighboring blocks
  └─ Apply directional lighting
- Medieval style color palette
```

**Other Modes:**
- Mode 0: Flat colors (no shading)
- Mode 1: Basic height shading
- Mode 2: Hill shading only
- Mode 3: Combined (deprecated)

#### Color Mapping

**Block to Color:**
```csharp
// 1. Load color map
var colorMap = LoadBlockColorMap();

// 2. For each block in tile:
var blockId = chunk.GetBlockId(x, y, z);
var blockCode = GetBlockCode(blockId);
var color = colorMap.GetColor(blockCode);

// 3. Apply height shading
var heightFactor = y / 256.0f;
var shadedColor = AdjustBrightness(color, heightFactor);

// 4. Apply hill shading
var slope = CalculateSlope(x, y, z);
var finalColor = ApplyHillShading(shadedColor, slope);

// 5. Set pixel
image.SetPixel(pixelX, pixelZ, finalColor);
```

**Custom Block Colors:**
- Use WebCartographerColorExporter mod on client
- Run `/exportcolors` command in-game
- Colors sent to server automatically
- Saved to `ModConfig/blockColorMapping.json`

### Blur and Smoothing

**BlurTool** applies post-processing:

```csharp
public class BlurTool
{
    public Bitmap ApplyBlur(Bitmap image, int radius)
    {
        // Gaussian blur for smoother appearance
        // Radius typically 1-2 pixels
        
        var result = new Bitmap(image.Width, image.Height);
        
        for (int x = 0; x < image.Width; x++)
        {
            for (int y = 0; y < image.Height; y++)
            {
                var color = GaussianBlur(image, x, y, radius);
                result.SetPixel(x, y, color);
            }
        }
        
        return result;
    }
}
```

## Pyramid Generation

### Downsampling Algorithm

**PyramidTileDownsampler** generates lower zoom levels:

```
Zoom N (base):        Zoom N-1:
┌───┬───┐             ┌───────┐
│ 0 │ 1 │             │       │
├───┼───┤  ────────>  │   D   │
│ 2 │ 3 │             │       │
└───┴───┘             └───────┘

4 tiles → 1 tile (50% scale)
```

### Implementation

```csharp
public class PyramidTileDownsampler
{
    public async Task<bool> GenerateZoomLevel(int targetZoom)
    {
        var sourceZoom = targetZoom + 1;
        
        // Get all tiles at source zoom
        var sourceTiles = GetTilesForZoom(sourceZoom);
        
        // Group into 2×2 grids
        var groups = GroupTilesBy2x2(sourceTiles);
        
        await Parallel.ForEachAsync(groups,
            async (group, ct) =>
            {
                // Load 4 source tiles
                var tiles = await LoadTiles(group);
                
                // Downsample to single tile
                var downsampled = Downsample2x2(tiles);
                
                // Apply smoothing
                var smoothed = ApplyBilinearFilter(downsampled);
                
                // Save result
                var outputPath = GetTilePath(
                    targetZoom, 
                    group.TargetX, 
                    group.TargetZ
                );
                await SaveTileAsync(smoothed, outputPath);
            });
        
        return true;
    }
    
    private Bitmap Downsample2x2(Bitmap[] tiles)
    {
        var tileSize = tiles[0].Width;
        var result = new Bitmap(tileSize, tileSize);
        
        // Combine 4 tiles into quadrants
        var halfSize = tileSize / 2;
        
        // Top-left
        CopyAndScale(tiles[0], result, 0, 0, halfSize);
        // Top-right
        CopyAndScale(tiles[1], result, halfSize, 0, halfSize);
        // Bottom-left
        CopyAndScale(tiles[2], result, 0, halfSize, halfSize);
        // Bottom-right
        CopyAndScale(tiles[3], result, halfSize, halfSize, halfSize);
        
        return result;
    }
}
```

## Caching Strategy

### Multi-Level Caching

```
┌─────────────────────┐
│  Browser Cache      │  60 minutes (Cache-Control header)
└──────────┬──────────┘
           │ [miss]
           ▼
┌─────────────────────┐
│  Memory Cache       │  Tile metadata with ETags
│  (in-process)       │  Session lifetime
└──────────┬──────────┘
           │ [miss]
           ▼
┌─────────────────────┐
│  Disk Cache         │  PNG files on disk
│  (persistent)       │  Until regenerated
└──────────┬──────────┘
           │ [miss]
           ▼
┌─────────────────────┐
│  Dynamic Generation │  Render from world data
│  (compute)          │  50-500ms
└─────────────────────┘
```

### Cache Invalidation

**Event-Driven:**
```csharp
// Chunk modified
OnChunkModified(chunkX, chunkZ) =>
  ├─ Mark affected tiles for regeneration
  ├─ Clear memory cache entries
  └─ Schedule background regeneration

// Entity spawned
OnEntitySpawn(entity) =>
  └─ Invalidate GeoJSON cache
```

**Time-Based:**
```csharp
// Full export every N minutes (configurable)
AutoExportMap: true
MapExportIntervalMs: 300000  // 5 minutes
```

**Manual:**
```csharp
// Player command
/atlas export  → Full regeneration
```

### ETag Support

**Generation:**
```csharp
string GenerateETag(byte[] data)
{
    using var sha = SHA256.Create();
    var hash = sha.ComputeHash(data);
    return Convert.ToBase64String(hash).Substring(0, 16);
}
```

**Usage:**
```csharp
// Client sends
GET /tiles/9/2000_1999.png
If-None-Match: "abc123def456"

// Server responds
HTTP/1.1 304 Not Modified
ETag: "abc123def456"
Cache-Control: max-age=3600

// Or if modified
HTTP/1.1 200 OK
ETag: "xyz789uvw012"
Content-Type: image/png
Cache-Control: max-age=3600
[PNG data]
```

## Performance Optimization

### Parallelization

```csharp
// Use all CPU cores
var options = new ParallelOptions
{
    MaxDegreeOfParallelism = Environment.ProcessorCount
};

await Parallel.ForEachAsync(tiles, options, async (tile, ct) =>
{
    await GenerateTileAsync(tile.X, tile.Z);
});
```

### Memory Management

```csharp
// Dispose images promptly
using (var image = RenderTile(x, z))
{
    await SaveTileAsync(image, path);
}  // Image disposed here

// Limit concurrent operations
var semaphore = new SemaphoreSlim(maxConcurrent);
await semaphore.WaitAsync();
try
{
    await GenerateTileAsync(x, z);
}
finally
{
    semaphore.Release();
}
```

### Disk I/O

```csharp
// Use async I/O
await File.WriteAllBytesAsync(path, data);

// Buffer writes
using var stream = new FileStream(path, FileMode.Create, 
    FileAccess.Write, FileShare.None, 
    bufferSize: 81920, useAsync: true);
await stream.WriteAsync(data);
```

## Troubleshooting

### Issue: Tiles not generating

**Causes:**
- Output directory not writable
- Insufficient disk space
- Chunk data not loaded

**Solutions:**
```csharp
// Check permissions
Directory.CreateDirectory(outputDir);
File.WriteAllText(Path.Combine(outputDir, "test.txt"), "test");

// Check disk space
var drive = new DriveInfo(Path.GetPathRoot(outputDir));
if (drive.AvailableFreeSpace < requiredSpace)
{
    _logger.Error("Insufficient disk space");
}

// Verify chunk loading
var chunk = _sapi.World.BlockAccessor.GetChunk(chunkX, 0, chunkZ);
if (chunk == null)
{
    _logger.Warning("Chunk {0},{1} not loaded", chunkX, chunkZ);
}
```

### Issue: Slow generation

**Causes:**
- Too many concurrent operations
- Disk I/O bottleneck
- CPU thermal throttling

**Solutions:**
```csharp
// Reduce parallelism
MaxDegreeOfParallelism = Environment.ProcessorCount / 2;

// Use SSD for output
// Monitor CPU temperature
// Increase export interval
```

### Issue: Out of memory

**Causes:**
- Too many images in memory
- Large tile size
- Memory leaks

**Solutions:**
```csharp
// Dispose images
using var image = RenderTile(...);

// Reduce concurrent operations
MaxDegreeOfParallelism = 2;

// Force GC periodically
if (tilesProcessed % 1000 == 0)
{
    GC.Collect();
}
```

## Alternative Storage: MBTiles

VintageAtlas supports optional MBTiles (SQLite) storage:

**Advantages:**
- Single file for all tiles
- Better for network storage (NFS, SMB)
- GIS tool compatibility

**Disadvantages:**
- Slower than disk files (2-10ms vs 1-5ms)
- Database locking issues
- More complex to maintain

**Implementation:**
```csharp
public class MBTilesStorage
{
    public async Task SaveTileAsync(byte[] data, int zoom, int x, int z)
    {
        using var connection = new SQLiteConnection(_connectionString);
        await connection.OpenAsync();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO tiles (zoom_level, tile_column, tile_row, tile_data)
            VALUES (@zoom, @x, @z, @data)";
        
        cmd.Parameters.AddWithValue("@zoom", zoom);
        cmd.Parameters.AddWithValue("@x", x);
        cmd.Parameters.AddWithValue("@z", z);
        cmd.Parameters.AddWithValue("@data", data);
        
        await cmd.ExecuteNonQueryAsync();
    }
}
```

## Future Enhancements

1. **Vector Tiles for POIs** - Use vector tiles for markers instead of GeoJSON
2. **Incremental Base Tile Updates** - Update individual pixels instead of full tiles
3. **Progressive Loading** - Stream tiles in multiple quality passes
4. **Tile Versioning** - Track tile versions for cache invalidation
5. **Distributed Generation** - Multi-server tile generation
6. **GPU Acceleration** - Use GPU for terrain rendering

## Related Documentation

- [Architecture Overview](../architecture/architecture-overview.md)
- [Vintage Story Modding Constraints](../guides/vintagestory-modding-constraints.md) - API constraints and threading
- [Coordinate Systems](../architecture/coordinate-systems.md)
- [Background Services](background-services.md)
- [REST API Reference](../api/rest-api.md)

---

**Maintained by:** daviaaze  
**Last Reviewed:** 2025-10-02

