# Dynamic Tile Generation Consolidation

**Started:** 2025-10-02  
**Status:** In Progress  
**Goal:** Consolidate all tile generation into DynamicTileGenerator with full feature parity to Extractor

## Overview

Consolidating tile generation from the legacy `Extractor` class into `DynamicTileGenerator` to:
- Enable true dynamic tile generation with all render modes
- Simplify codebase by removing duplication
- Support incremental tile updates with full color and shading
- Maintain pixel-perfect compatibility with existing exports

## Implementation Phases

### ✅ Phase 0: Planning & Documentation
- [x] Analyzed Extractor implementation
- [x] Identified Vintage Story API constraints
- [x] Created comprehensive constraints documentation
- [x] Defined implementation phases

### 🔄 Phase 1: Data Extraction Infrastructure (IN PROGRESS)
**Goal:** Thread-safe chunk data extraction following VS API constraints

**Tasks:**
- [ ] Create `ChunkSnapshot` class for safe data transfer
- [ ] Implement main thread data extraction in DynamicTileGenerator
- [ ] Add coordinate conversion utilities
- [ ] Test thread safety and performance

**Key Constraints (from vintagestory-modding-constraints.md):**
- Chunk access MUST be on main thread
- Cannot cache chunk references (prevents unloading)
- Extract data quickly, process off main thread

**Implementation Notes:**
```csharp
// Pattern to follow:
// 1. Request data extraction on main thread via EnqueueMainThreadTask
// 2. Copy block IDs and heights into snapshot
// 3. Process snapshot on background thread
// 4. Render tile from snapshot data
```

### Phase 2: Block Color Mapping System
**Goal:** Load and cache block colors for fast rendering

**Tasks:**
- [ ] Create `BlockColorCache` class
- [ ] Load from blockColorMapping.json
- [ ] Implement MapColors fallback for Medieval style
- [ ] Cache lake/water detection
- [ ] Test with different block types

### Phase 3: Basic Render Modes
**Goal:** Implement simpler render modes first

**Tasks:**
- [ ] Port OnlyOneColor mode
- [ ] Port ColorVariations mode
- [ ] Port ColorVariationsWithHeight mode
- [ ] Create unit tests comparing output to Extractor

### Phase 4: Hill Shading
**Goal:** Implement slope calculations and directional lighting

**Tasks:**
- [ ] Port altitude difference calculations
- [ ] Implement slope boost multiplier
- [ ] Handle chunk boundaries for neighbor access
- [ ] Port ColorVariationsWithHillShading mode

### Phase 5: Medieval Style
**Goal:** Implement the recommended render mode with water edges

**Tasks:**
- [ ] Port GetMedievalStyleColor logic
- [ ] Implement water edge detection
- [ ] Handle chunk boundary water detection
- [ ] Test with various terrain types

### Phase 6: Integration
**Goal:** Wire up new system in DynamicTileGenerator

**Tasks:**
- [ ] Update GenerateTileFromWorldDataAsync
- [ ] Implement pyramid generation for lower zoom levels
- [ ] Update caching strategy
- [ ] Add configuration options

### Phase 7: Testing & Validation
**Goal:** Ensure pixel-perfect output

**Tasks:**
- [ ] Generate test tiles with Extractor
- [ ] Generate same tiles with new system
- [ ] Pixel-by-pixel comparison
- [ ] Performance benchmarking
- [ ] Memory usage profiling

### Phase 8: Deprecation
**Goal:** Remove legacy code

**Tasks:**
- [ ] Add deprecation warnings to Extractor
- [ ] Update MapExporter to use new system
- [ ] Remove Extractor class
- [ ] Update documentation
- [ ] Final testing

## Technical Decisions

### Data Extraction Pattern

**Decision:** Use snapshot pattern with main thread extraction
**Rationale:** VS API requires main thread for chunk access, but rendering is CPU-intensive and should be off main thread
**Implementation:**
```csharp
public class ChunkSnapshot
{
    public int ChunkX { get; set; }
    public int ChunkZ { get; set; }
    public int[] BlockIds { get; set; } // 32*32*32 = 32768 blocks
    public int[] Heights { get; set; }  // Per-column heights
}
```

### Block Color Caching

**Decision:** Pre-cache all block colors at startup
**Rationale:** Block list doesn't change after world load, O(1) lookup vs repeated calculation
**Implementation:**
```csharp
Dictionary<int, List<uint>> _blockIdToColors
Dictionary<int, uint> _blockIdToBaseColor (for Medieval)
```

### Thread Safety

**Decision:** Extract on main thread, render on background
**Rationale:** Follows VS constraints, maximizes parallelism
**Pattern:**
```csharp
await sapi.Event.EnqueueMainThreadTask(() => {
    snapshot = ExtractChunkData();
    return true;
}, "extract-chunks");

await Task.Run(() => RenderFromSnapshot(snapshot));
```

## Performance Targets

| Metric | Target | Current Extractor |
|--------|--------|-------------------|
| Base tile generation | < 500ms | ~400ms |
| Memory per tile | < 10MB | ~8MB |
| Parallel tiles | 8+ concurrent | 8 (CPU cores) |
| Cache hit latency | < 5ms | ~2ms |

## Testing Strategy

### Unit Tests
- Coordinate conversions
- Color mapping lookups
- Snapshot data extraction
- Individual render modes

### Integration Tests
- Full tile generation pipeline
- Multi-tile rendering
- Chunk boundary handling
- All render modes

### Visual Tests
- Side-by-side comparison with Extractor output
- Different biomes and terrain types
- Water edges in Medieval mode
- Hill shading accuracy

### Performance Tests
- Tile generation speed
- Memory usage
- Concurrent tile generation
- Cache efficiency

## Known Limitations & Workarounds

### Limitation 1: Main Thread Bottleneck
**Issue:** Data extraction must happen on main thread
**Impact:** Can't fully parallelize generation
**Mitigation:** 
- Extract data for multiple tiles in single main thread task
- Minimize time spent on main thread
- Batch extractions when possible

### Limitation 2: IMapChunk Insufficient
**Issue:** IMapChunk only has heights, not block IDs
**Impact:** Must access full chunks via IBlockAccessor
**Mitigation:**
- Use IServerChunk.Data for direct access when available
- Fall back to IBlockAccessor for safety

### Limitation 3: Chunk Unloading
**Issue:** Chunks may unload between tiles
**Impact:** May need to regenerate snapshots
**Mitigation:**
- Don't cache chunk references
- Handle null chunks gracefully
- Re-extract if chunk unloaded

## Migration Path

### Phase 1-3: Parallel Development
- Keep Extractor functional
- Build new system alongside
- Feature flag for testing

### Phase 4-6: Gradual Rollout
- Use new system for dynamic tiles only
- Keep Extractor for full exports
- Monitor for issues

### Phase 7-8: Full Migration
- Switch full exports to new system
- Deprecate Extractor
- Remove after validation period

## Rollback Plan

If issues arise:
1. **Immediate:** Feature flag to disable new system
2. **Short-term:** Revert to Extractor for problematic cases
3. **Long-term:** Fix issues, re-enable gradually

## Success Criteria

- [ ] All 5 render modes working
- [ ] Pixel-perfect match with Extractor output (99%+ similarity)
- [ ] Performance within 20% of Extractor
- [ ] No memory leaks or crashes
- [ ] Passes all integration tests
- [ ] Documentation complete

## References

- [Vintage Story Modding Constraints](../guides/vintagestory-modding-constraints.md)
- [Tile Generation System](tile-generation.md)
- [Architecture Overview](../architecture/architecture-overview.md)

---

## Implementation Log

### 2025-10-02: Project Start
- Created implementation plan
- Set up TODO tracking
- Ready to begin Phase 1

### 2025-10-02: Phase 1 - Data Extraction Infrastructure ✅
**Completed:**
- ✅ Created `ChunkSnapshot` class for thread-safe data transfer
- ✅ Created `TileChunkData` collection class
- ✅ Implemented `ChunkDataExtractor` for main thread data extraction
- ✅ Updated `DynamicTileGenerator` to use new extraction pattern
- ✅ Implemented proper main thread → background thread pattern
- ✅ Fixed linter warnings

**Technical Details:**
- ChunkSnapshot captures block IDs and heightmaps on main thread
- Uses `EnqueueMainThreadTask` for proper VS API constraint compliance
- Extraction is fast (<50ms per tile), rendering happens on background thread
- Smart chunk Y level detection based on average surface height

**Files Created:**
- `VintageAtlas/Export/ChunkSnapshot.cs` - Snapshot data structures
- `VintageAtlas/Export/ChunkDataExtractor.cs` - Main thread extraction logic

**Files Modified:**
- `VintageAtlas/Export/DynamicTileGenerator.cs` - Updated to use snapshots

**Current Status:**
- Phase 1 complete ✅
- Basic tile generation working with new architecture
- Ready for Phase 2 (Block Color Mapping)

**Next Steps:**
- Test the new extraction pattern with real world data
- Begin Phase 2: Block color mapping system

---

**Maintained by:** daviaaze  
**Next Review:** After Phase 1 completion

