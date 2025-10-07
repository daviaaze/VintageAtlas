# VintageAtlas Coordinate System - Quick Reference

## TL;DR

**Backend handles all coordinate transformations. Frontend just passes OpenLayers grid coords.**

## For Developers

### Adding Tile Features

When adding new tile-related features, remember:

1. **Frontend**: Use OpenLayers grid coordinates (x, y, z) directly
2. **Backend**: Transform grid → storage before database lookup
3. **Never** do coordinate math in frontend

### Tile URL Pattern

```
/tiles/{zoom}/{gridX}_{gridY}.png
```

Where `gridX` and `gridY` are OpenLayers grid coordinates (0-based from origin).

### Transformation Formula

```csharp
// Backend only!
originTileX = worldOrigin[0] / blocksPerTile
originTileY = worldOrigin[1] / blocksPerTile

storageTileX = originTileX + gridX
storageTileY = originTileY + gridY
```

## For Troubleshooting

### Map Not Loading?

1. **Check backend logs** for transformation:
   ```
   [VintageAtlas] Tile request: z=7, grid=(7,8) → storage=(497,506)
   ```

2. **Verify extent** in browser console:
   ```javascript
   World extent (blocks): [501760, 509952, 517120, 527360]
   World origin (blocks): [501760, 509952]
   ```

3. **Check tile exists** in database:
   ```bash
   sqlite3 test_server/ModData/VintageAtlas/maptiles.db \
     "SELECT COUNT(*) FROM map WHERE zoom_level=7 AND tile_column=497 AND tile_row=506;"
   ```

### Tiles at Wrong Location?

- **Problem**: Origin misconfigured
- **Check**: Origin should be at bottom-left (minX, minY)
- **Fix**: Verify `MapConfigController.GenerateMapConfig()`

### Some Tiles Missing (404)?

- **Normal**: Sparse worlds have gaps
- **Cause**: Tiles skipped during pyramid downsampling
- **Future Fix**: Return transparent PNG instead of 404

## Coordinate Systems At-a-Glance

| System | Format | Example | Used By |
|--------|--------|---------|---------|
| **World Blocks** | Absolute blocks | `[509440, 518144]` | Map config, extent |
| **Storage Tiles** | Absolute tile # | `[497, 506]` | Database, MBTiles |
| **Grid Coords** | Relative to origin | `[7, 8]` | OpenLayers, frontend |

## Architecture Diagram

```
┌─────────────────────────────────────────────┐
│           OpenLayers (Frontend)             │
│  ┌──────────────────────────────────────┐   │
│  │ Request: /tiles/7/7_8.png            │   │
│  │ Grid coords: (7, 8) at zoom 7        │   │
│  └──────────────┬───────────────────────┘   │
└─────────────────┼───────────────────────────┘
                  │ HTTP GET
                  ▼
┌─────────────────────────────────────────────┐
│         TileController (Backend)            │
│  ┌──────────────────────────────────────┐   │
│  │ Parse: gridX=7, gridY=8, zoom=7      │   │
│  │ Transform: grid → storage            │   │
│  │   origin = (490, 498)                │   │
│  │   storage = (490+7, 498+8)           │   │
│  │   storage = (497, 506) ✅            │   │
│  └──────────────┬───────────────────────┘   │
└─────────────────┼───────────────────────────┘
                  │ GetTile(497, 506)
                  ▼
┌─────────────────────────────────────────────┐
│         MBTiles Database                    │
│  ┌──────────────────────────────────────┐   │
│  │ Query: z=7, x=497, y=506             │   │
│  │ Return: PNG bytes                     │   │
│  └──────────────────────────────────────┘   │
└─────────────────────────────────────────────┘
```

## Key Files

- **Backend Logic**: `VintageAtlas/Web/API/TileController.cs`
- **Config Generation**: `VintageAtlas/Web/API/MapConfigController.cs`
- **Frontend Display**: `VintageAtlas/frontend/src/utils/olMapConfig.ts`
- **Full Docs**: `COORDINATE_SYSTEM_SUMMARY.md`
- **Session Notes**: `SESSION_SUMMARY.md`

## Common Mistakes to Avoid

❌ **Don't** transform coordinates in frontend  
❌ **Don't** assume tile numbers = block coordinates  
❌ **Don't** use top-left origin explicitly  
✅ **Do** use grid coordinates in tile URLs  
✅ **Do** let backend handle transformations  
✅ **Do** let OpenLayers use default bottom-left origin  

## Status

✅ **Production Ready** - Map working, code clean, fully documented

---

*Last Updated: October 6, 2025*  
*For detailed technical information, see COORDINATE_SYSTEM_SUMMARY.md*
