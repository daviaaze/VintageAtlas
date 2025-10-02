# Coordinate Systems Guide

**Last Updated:** 2025-10-02  
**Version:** 1.0.0

## Overview

VintageAtlas uses three different coordinate systems that must be properly transformed between each other:
1. **Game Coordinates** - Vintage Story world positions
2. **Map Coordinates** - OpenLayers display coordinates
3. **Tile Coordinates** - XYZ tile addressing

This document explains each system and how to transform between them.

## Game Coordinates (Vintage Story)

### Coordinate System

```
      North (Z-)
           ↑
           |
West (X-) ← Spawn → East (X+)
           |
           ↓
      South (Z+)
```

**Properties:**
- Origin: World spawn position
- X-axis: East (+) / West (-)
- Y-axis: Height (up/down)
- Z-axis: South (+) / North (-)
- Units: Blocks (1 block = 1 unit)

**Example:**
```
Spawn at: [512000, 512000] (absolute world coordinates)
Player at: [512100, 511900]
  → 100 blocks East of spawn
  → 100 blocks North of spawn
```

### Absolute vs Relative Positions

VintageAtlas supports two modes controlled by `AbsolutePositions` in config:

#### Relative Mode (Default: `false`)

Spawn becomes origin [0, 0]:

```
World Spawn: [512000, 512000]  (absolute)
Display as:  [0, 0]             (relative)

Player at [512100, 511900]  (absolute)
Display as [100, -100]      (relative: 100E, 100N)
```

**Why use relative:**
- Players think relative to spawn ("500 north of spawn")
- Matches in-game F3 coordinate display
- Easier for navigation

#### Absolute Mode (`true`)

Uses raw world coordinates:

```
World Spawn: [512000, 512000]
Display as:  [512000, 512000]

Player at [512100, 511900]
Display as [512100, 511900]
```

**Why use absolute:**
- Debugging and development
- Integration with external tools
- Raw world data analysis

### Chunk Coordinates

Chunks are 32×32 blocks:

```csharp
// Block position to chunk position
int chunkX = blockX / 32;
int chunkZ = blockZ / 32;

// Chunk position to block position (chunk origin)
int blockX = chunkX * 32;
int blockZ = chunkZ * 32;

// Block within chunk (0-31)
int localX = blockX % 32;
int localZ = blockZ % 32;
```

**Example:**
```
Block [512100, 511900]
Chunk [16003, 15996]  (512100/32, 511900/32)
Local [4, 20]         (512100%32, 511900%32)
```

## Map Coordinates (OpenLayers)

### Coordinate System

OpenLayers uses different conventions:

```
     North (Y+)
          ↑
          |
West (X-) ← Center → East (X+)
          |
          ↓
     South (Y-)
```

**Properties:**
- Origin: Map center (configurable)
- X-axis: East (+) / West (-)
- Y-axis: North (+) / South (-)  [FLIPPED from game Z]
- Projection: Simple CRS or Web Mercator (EPSG:3857)
- Units: Pixels or projected units

### Game to Map Transformation

#### When `AbsolutePositions: false` (Default)

```typescript
// Game coordinates (relative to spawn)
const gameX = 100;   // 100 blocks East
const gameZ = -50;   // 50 blocks North (negative Z)

// Map coordinates
const mapX = gameX;          // 100 (same direction)
const mapY = -gameZ;         // 50 (flip Z to Y)

// Result: [100, 50] on map
```

**Formula:**
```typescript
mapX = gameX
mapY = -gameZ  // Note the negation!
```

#### When `AbsolutePositions: true`

```typescript
// Game coordinates (absolute)
const gameX = 512100;
const gameZ = 511900;
const spawnX = 512000;
const spawnZ = 512000;

// Map coordinates (still relative to spawn)
const mapX = gameX - spawnX;           // 100
const mapY = -(gameZ - spawnZ);        // -(-100) = 100

// Result: [100, 100] on map
```

**Formula:**
```typescript
mapX = gameX - spawnX
mapY = -(gameZ - spawnZ)
```

### Map to Game Transformation

#### When `AbsolutePositions: false`

```typescript
// Map coordinates
const mapX = 100;
const mapY = 50;

// Game coordinates (relative)
const gameX = mapX;          // 100
const gameZ = -mapY;         // -50

// Result: [100, -50] in game
```

#### When `AbsolutePositions: true`

```typescript
// Map coordinates
const mapX = 100;
const mapY = 50;
const spawnX = 512000;
const spawnZ = 512000;

// Game coordinates (absolute)
const gameX = mapX + spawnX;         // 512100
const gameZ = spawnZ - mapY;         // 511950

// Result: [512100, 511950] in game
```

### OpenLayers Configuration

```typescript
import { Projection } from 'ol/proj';
import { View } from 'ol';

// Define custom projection
const mapSize = config.mapSizeX;
const extent = [-mapSize/2, -mapSize/2, mapSize/2, mapSize/2];

const projection = new Projection({
  code: 'SIMPLE',
  units: 'pixels',
  extent: extent,
  global: false
});

// Create map view
const view = new View({
  projection: projection,
  center: config.defaultCenter,  // [0, 0] for spawn-relative
  zoom: config.defaultZoom,
  minZoom: 0,
  maxZoom: config.maxZoom,
  extent: config.worldExtent
});
```

## Tile Coordinates (XYZ)

### Tile Addressing

Tiles use standard XYZ addressing:

```
/tiles/{zoom}/{x}_{z}.png
```

**Properties:**
- Zoom: 0 (furthest) to N (closest)
- X: Tile column (East +)
- Z: Tile row (South +)
- Size: 256×256 pixels (configurable)

### Tile Coordinate System

```
(0,0) ────────────→ X (East)
  │
  │   Tile (5, 3)
  │   ┌─────┐
  │   │     │
  │   └─────┘
  ↓
  Z (South)
```

### Zoom Levels

Each zoom level doubles resolution:

| Zoom | World Units/Tile | Pixels/Block | Coverage |
|------|------------------|--------------|----------|
| 0    | 65536           | 0.0039       | Very far |
| 1    | 32768           | 0.0078       | Far      |
| 2    | 16384           | 0.0156       | Medium   |
| ...  | ...             | ...          | ...      |
| 7    | 512             | 0.5          | Near     |
| 8    | 256             | 1.0          | Close    |
| 9    | 128             | 2.0          | Closest  |

**Formula:**
```
worldUnitsPerTile = tileSize * (2^(maxZoom - zoom))
```

**Example (tileSize=256, maxZoom=9):**
```
Zoom 9: 256 * (2^0) = 256 blocks/tile  (1px = 1 block)
Zoom 8: 256 * (2^1) = 512 blocks/tile  (1px = 2 blocks)
Zoom 7: 256 * (2^2) = 1024 blocks/tile (1px = 4 blocks)
```

### Block to Tile Transformation

```csharp
// Block position to tile position
int worldUnitsPerTile = tileSize * (1 << (maxZoom - zoom));

int tileX = blockX / worldUnitsPerTile;
int tileZ = blockZ / worldUnitsPerTile;

// Tile position to block position (tile origin)
int blockX = tileX * worldUnitsPerTile;
int blockZ = tileZ * worldUnitsPerTile;

// Block within tile
int localX = blockX % worldUnitsPerTile;
int localZ = blockZ % worldUnitsPerTile;
```

**Example (zoom=9, tileSize=256):**
```csharp
// Block [512100, 511900]
int worldUnitsPerTile = 256;  // zoom 9

int tileX = 512100 / 256 = 2000;
int tileZ = 511900 / 256 = 1999;

// Tile (2000, 1999) at zoom 9
// URL: /tiles/9/2000_1999.png
```

### Tile Pyramid

Lower zoom tiles are generated by downsampling:

```
Zoom 9:  [TILE]
           ↓
Zoom 8:  4 tiles → 1 tile (2×2 grid)
           ↓
Zoom 7:  16 tiles → 1 tile (4×4 grid)
           ↓
         ... etc
```

**Downsampling:**
```
Zoom N+1 (higher detail):
┌─────┬─────┐
│ TL  │ TR  │  4 tiles
├─────┼─────┤
│ BL  │ BR  │
└─────┴─────┘
      ↓ (combine)
Zoom N (lower detail):
┌───────────┐
│   Single  │  1 tile
│   Tile    │
└───────────┘
```

## Complete Transformation Examples

### Example 1: Player Position to Map

**Scenario:** Player at game coordinates, display on map

**Given:**
- Config: `AbsolutePositions: false`
- Spawn: [512000, 512000] (absolute)
- Player: [512100, 511900] (absolute)

**Step 1: Convert to relative**
```
playerRelX = 512100 - 512000 = 100
playerRelZ = 511900 - 512000 = -100
```

**Step 2: Convert to map coordinates**
```
mapX = playerRelX = 100
mapY = -playerRelZ = -(-100) = 100
```

**Result:** Display player at [100, 100] on map

### Example 2: Map Click to Game Coordinate

**Scenario:** User clicks map, get game coordinates

**Given:**
- Config: `AbsolutePositions: false`
- Spawn: [512000, 512000]
- Click: [150, -75] (map coordinates)

**Step 1: Convert to game relative**
```
gameRelX = mapX = 150
gameRelZ = -mapY = -(-75) = 75
```

**Step 2: Convert to absolute (if needed)**
```
gameAbsX = 512000 + 150 = 512150
gameAbsZ = 512000 + 75 = 512075
```

**Result:** Click at [512150, 512075] in world

### Example 3: Block Position to Tile URL

**Scenario:** Generate tile for block position

**Given:**
- Block: [512100, 511900]
- Zoom: 9
- TileSize: 256

**Step 1: Calculate world units per tile**
```
worldUnitsPerTile = 256 * (2^(9-9)) = 256
```

**Step 2: Calculate tile coordinates**
```
tileX = 512100 / 256 = 2000
tileZ = 511900 / 256 = 1999
```

**Result:** `/tiles/9/2000_1999.png`

### Example 4: Chunk Change to Tile Regeneration

**Scenario:** Block placed, determine which tiles to regenerate

**Given:**
- Chunk: [16003, 15996] (chunk coordinates)
- Zoom: 9
- TileSize: 256
- ChunkSize: 32

**Step 1: Convert chunk to block coordinates**
```
blockX = 16003 * 32 = 512096
blockZ = 15996 * 32 = 511872
```

**Step 2: Calculate affected tiles**
```
Chunk covers blocks [512096-512127, 511872-511903]

At zoom 9 (256 blocks/tile):
  tileXmin = 512096 / 256 = 2000
  tileXmax = 512127 / 256 = 2000  (same tile)
  tileZmin = 511872 / 256 = 1999
  tileZmax = 511903 / 256 = 1999  (same tile)
```

**Result:** Regenerate tile (2000, 1999) at zoom 9

## Implementation Code Examples

### Backend (C#)

#### Game to Map Coordinate
```csharp
public class CoordinateTransform
{
    private readonly bool _absolutePositions;
    private readonly Vec3d _spawnPos;
    
    public (int mapX, int mapY) GameToMap(int gameX, int gameZ)
    {
        int relX = _absolutePositions ? gameX - (int)_spawnPos.X : gameX;
        int relZ = _absolutePositions ? gameZ - (int)_spawnPos.Z : gameZ;
        
        return (relX, -relZ);  // Note Z flip
    }
    
    public (int gameX, int gameZ) MapToGame(int mapX, int mapY)
    {
        int gameZ = -mapY;  // Flip back
        int gameX = mapX;
        
        if (_absolutePositions)
        {
            gameX += (int)_spawnPos.X;
            gameZ += (int)_spawnPos.Z;
        }
        
        return (gameX, gameZ);
    }
}
```

#### Block to Tile
```csharp
public (int tileX, int tileZ) BlockToTile(int blockX, int blockZ, int zoom)
{
    int worldUnitsPerTile = _config.TileSize * (1 << (_config.BaseZoomLevel - zoom));
    
    return (
        blockX / worldUnitsPerTile,
        blockZ / worldUnitsPerTile
    );
}
```

### Frontend (TypeScript)

#### Game to Map Coordinate
```typescript
export function gameToMap(
  gameX: number,
  gameZ: number,
  config: MapConfigData
): [number, number] {
  let relX = gameX;
  let relZ = gameZ;
  
  if (config.absolutePositions) {
    relX = gameX - config.spawnPosition[0];
    relZ = gameZ - config.spawnPosition[1];
  }
  
  return [relX, -relZ];  // Note Z flip
}
```

#### Map to Game Coordinate
```typescript
export function mapToGame(
  mapX: number,
  mapY: number,
  config: MapConfigData
): [number, number] {
  let gameX = mapX;
  let gameZ = -mapY;  // Flip back
  
  if (config.absolutePositions) {
    gameX += config.spawnPosition[0];
    gameZ += config.spawnPosition[1];
  }
  
  return [gameX, gameZ];
}
```

#### Format Coordinates for Display
```typescript
export function formatCoordinates(
  x: number,
  z: number,
  config: MapConfigData
): string {
  if (config.absolutePositions) {
    return `[${x}, ${z}]`;
  } else {
    const ew = x >= 0 ? 'E' : 'W';
    const ns = z <= 0 ? 'N' : 'S';  // Remember: negative Z is North
    return `${Math.abs(x)}${ew}, ${Math.abs(z)}${ns}`;
  }
}
```

## Common Pitfalls

### 1. Forgetting Z-Axis Flip

❌ **Wrong:**
```typescript
const mapY = gameZ;  // Wrong direction!
```

✅ **Correct:**
```typescript
const mapY = -gameZ;  // Flip the axis
```

### 2. Confusing Relative and Absolute

❌ **Wrong:**
```csharp
// Assuming always relative
var mapX = gameX;
var mapY = -gameZ;
```

✅ **Correct:**
```csharp
// Check mode first
if (_config.AbsolutePositions) {
    gameX -= spawnX;
    gameZ -= spawnZ;
}
var mapX = gameX;
var mapY = -gameZ;
```

### 3. Integer Division Errors

❌ **Wrong:**
```csharp
int tileX = blockX / worldUnitsPerTile;  // Rounds toward zero
// Block -100 / 256 = 0 (wrong!)
```

✅ **Correct:**
```csharp
int tileX = (int)Math.Floor((double)blockX / worldUnitsPerTile);
// Block -100 / 256 = -1 (correct)
```

### 4. Off-by-One in Tile Ranges

❌ **Wrong:**
```csharp
// Chunk covers blocks [0-31]
int maxTileX = 31 / 256;  // 0 (wrong!)
```

✅ **Correct:**
```csharp
// Chunk covers blocks [0-31]
int maxTileX = (31 + 1) / 256;  // Account for inclusive range
```

## Testing Coordinate Transformations

### Unit Test Examples

```csharp
[Test]
public void TestGameToMapCoordinate()
{
    // Relative mode
    var result = GameToMap(100, -50, absolutePositions: false);
    Assert.AreEqual((100, 50), result);
    
    // Absolute mode
    var spawn = new Vec3d(512000, 0, 512000);
    var result2 = GameToMap(512100, 511950, absolutePositions: true, spawn);
    Assert.AreEqual((100, 50), result2);
}

[Test]
public void TestBlockToTile()
{
    var result = BlockToTile(512100, 511900, zoom: 9, tileSize: 256, maxZoom: 9);
    Assert.AreEqual((2000, 1999), result);
}
```

### Visual Testing

1. **Place marker at spawn** - Should appear at map center (0, 0)
2. **Walk 100 blocks East** - Marker should move 100 units right
3. **Walk 100 blocks North** - Marker should move 100 units up
4. **Check tile boundaries** - Tiles should align with grid

## Resources

- [OpenLayers Coordinate Systems](https://openlayers.org/en/latest/apidoc/module-ol_proj.html)
- [Web Mercator Projection](https://en.wikipedia.org/wiki/Web_Mercator_projection)
- [Tile Map Service Specification](https://wiki.osgeo.org/wiki/Tile_Map_Service_Specification)

## See Also

- [Architecture Overview](architecture-overview.md)
- [API Integration](api-integration.md)
- [Tile Generation](../implementation/tile-generation.md)
- [Map Configuration API](../api/map-config.md)

---

**Maintained by:** daviaaze  
**Last Reviewed:** 2025-10-02

