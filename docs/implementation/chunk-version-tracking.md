# Chunk Version Tracking Implementation

**Date:** October 6, 2025  
**Status:** 🟡 In Progress  
**Priority:** Enhancement

---

## Overview

Implement chunk version tracking to visualize which parts of the world were generated with which game versions. This helps server admins:

- Track world exploration history over time
- Identify chunks from specific game versions
- Debug chunk-related issues
- Visualize world growth patterns

## Architecture

```
SavegameDatabase
    ↓
ChunkVersionExtractor (NEW)
    → Reads chunk metadata
    → Groups by version
    ↓
GroupChunks (EXISTING - needs integration)
    → Groups adjacent chunks with same version
    → Creates concave hull polygons
    → Generates gradient colors
    ↓
GeoJsonController (ENHANCE)
    → New endpoint: /api/geojson/chunk-versions
    → Serves ChunkversionGeoJson
    ↓
Frontend (NEW LAYER)
    → Chunk Version Layer (toggleable)
    → Shows colored regions by version
    → Legend with version → color mapping
```

## Implementation Phases

### Phase 1: Data Extraction ⏳

**Goal:** Read chunk version data from savegame

**Tasks:**
- [ ] Enhance SavegameDataSource to read chunk metadata
- [ ] Extract game version from ServerChunk or ServerMapChunk
- [ ] Create ChunkVersionData model
- [ ] Test with real savegame data

**Key Question:** Where is version stored in ServerChunk/ServerMapChunk?
```csharp
// Need to investigate:
ServerChunk.??? // Version field?
ServerMapChunk.??? // Metadata?
```

### Phase 2: Grouping & Hull Generation ⏳

**Goal:** Use GroupChunks to create polygon regions

**Tasks:**
- [ ] Wire GroupChunks into MapExporter or new service
- [ ] Feed chunk position + version data to GroupChunks
- [ ] Generate concave hulls for each version group
- [ ] Apply gradient colors based on version age
- [ ] Cache results (chunks don't change versions)

**Integration Point:**
```csharp
// In GeoJsonController or new ChunkVersionService
public async Task<ChunkversionGeoJson> GetChunkVersions()
{
    var chunkData = await _dataSource.GetChunkVersionsAsync();
    var grouper = new GroupChunks(chunkData, _server);
    var grouped = grouper.GroupPositions();
    
    grouper.GenerateGradient(grouped);
    
    var features = grouped.Select(g => grouper.GetShape(g)).ToList();
    
    return new ChunkversionGeoJson { Features = features };
}
```

### Phase 3: API Endpoint ⏳

**Goal:** Expose chunk versions via HTTP API

**Tasks:**
- [ ] Add `/api/geojson/chunk-versions` endpoint to GeoJsonController
- [ ] Implement caching (versions don't change often)
- [ ] Add ETag support for efficient updates
- [ ] Handle empty worlds gracefully

**API Response:**
```json
{
  "type": "FeatureCollection",
  "name": "chunk_versions",
  "features": [
    {
      "type": "Feature",
      "properties": {
        "color": "#FF6A00",
        "version": "1.19.8"
      },
      "geometry": {
        "type": "Polygon",
        "coordinates": [[[x1, z1], [x2, z2], ...]]
      }
    }
  ]
}
```

### Phase 4: Frontend Integration ⏳

**Goal:** Add toggleable chunk version layer to map

**Tasks:**
- [ ] Create `ChunkVersionLayer` component
- [ ] Add layer toggle in settings/sidebar
- [ ] Implement color legend showing version → color mapping
- [ ] Add opacity control for layer
- [ ] Ensure it works with other layers

**Frontend Files:**
- `frontend/src/components/map/ChunkVersionLayer.vue`
- `frontend/src/stores/mapStore.ts` (add version layer state)
- `frontend/src/utils/layerFactory.ts` (add createChunkVersionLayer)

### Phase 5: Testing & Polish ⏳

**Goal:** Validate and optimize

**Tasks:**
- [ ] Test with worlds generated across multiple versions
- [ ] Test performance with large worlds (10,000+ chunks)
- [ ] Verify concave hull looks good
- [ ] Test gradient colors are distinguishable
- [ ] Add documentation

---

## Technical Details

### Chunk Version Data Structure

```csharp
public class ChunkVersionData
{
    public Dictionary<ChunkPos, string> ChunkVersions { get; set; }
    // ChunkPos → Game version string (e.g., "1.19.8")
}
```

### GroupChunks Integration

**Existing code is good, just needs wiring:**

```csharp
// GroupChunks.cs (already implemented!)
public class GroupChunks
{
    // ✅ Already has: DFS grouping algorithm
    // ✅ Already has: Concave hull generation
    // ✅ Already has: Gradient color generation
    // ✅ Already has: GeoJSON feature creation
    
    // Just needs: Data input from SavegameDataSource
}
```

### Concave Hull Parameters

```csharp
var concaveHull = new ConcaveHull(multiPoint)
{
    MaximumEdgeLength = 45  // Good default for 32-block chunks
};
```

This creates smooth boundaries around chunk groups without being too aggressive.

### Color Gradient

```csharp
// Orange (oldest) → Blue (newest)
var startColor = new Vector3(255, 106, 0);  // Orange
var endColor = new Vector3(0, 78, 255);     // Blue
```

Versions are sorted by semantic version, gradient applied oldest → newest.

---

## API Integration

### New Endpoint

```csharp
// In GeoJsonController.cs
public async Task ServeChunkVersions(HttpListenerContext context)
{
    try
    {
        var ifNoneMatch = context.Request.Headers["If-None-Match"];
        var geoJson = await GetChunkVersionsGeoJsonAsync();
        
        var json = JsonConvert.SerializeObject(geoJson, _jsonSettings);
        var etag = GenerateETag(json);
        
        if (ifNoneMatch == etag)
        {
            context.Response.StatusCode = 304;
            context.Response.Headers.Add("ETag", etag);
            context.Response.Close();
            return;
        }
        
        await ServeGeoJson(context, json, etag);
    }
    catch (Exception ex)
    {
        sapi.Logger.Error($"[VintageAtlas] Error serving chunk versions: {ex.Message}");
        await ServeError(context, "Failed to generate chunk version data");
    }
}
```

### Caching Strategy

```csharp
// Cache for 5 minutes (chunks rarely change versions)
private ChunkversionGeoJson? _cachedChunkVersions;
private long _lastChunkVersionUpdate;
private const long ChunkVersionCacheMs = 5 * 60 * 1000; // 5 minutes
```

---

## Frontend Components

### Layer Creation

```typescript
// frontend/src/utils/layerFactory.ts
export function createChunkVersionLayer(visible = false, projection?: any) {
  const source = new VectorSource({
    url: '/api/geojson/chunk-versions',
    format: new GeoJSON({
      dataProjection: projection || 'EPSG:3857',
      featureProjection: projection || 'EPSG:3857'
    })
  });

  return new VectorLayer({
    source,
    visible,
    zIndex: 150, // Above tiles, below entities
    style: (feature) => {
      const color = feature.get('color') || 'rgba(100, 149, 237, 0.3)';
      return new Style({
        fill: new Fill({ color }),
        stroke: new Stroke({ 
          color: color.replace(/[\d.]+\)/, '0.8)'), // More opaque border
          width: 2 
        })
      });
    }
  });
}
```

### UI Controls

```vue
<!-- frontend/src/components/map/LayerControls.vue -->
<template>
  <div class="layer-controls">
    <label>
      <input 
        type="checkbox" 
        v-model="showChunkVersions"
        @change="toggleChunkVersionLayer"
      />
      Show Chunk Versions
    </label>
    
    <div v-if="showChunkVersions" class="version-legend">
      <h4>Game Versions</h4>
      <div v-for="version in versionColors" :key="version.version">
        <span 
          class="color-box" 
          :style="{ backgroundColor: version.color }"
        ></span>
        {{ version.version }}
      </div>
    </div>
  </div>
</template>
```

---

## Performance Considerations

### Database Query Optimization

```csharp
// Load chunk versions in batches
const int BatchSize = 1000;

var allVersions = new Dictionary<ChunkPos, string>();

await foreach (var batch in GetChunkVersionsBatched(BatchSize))
{
    foreach (var (pos, version) in batch)
    {
        allVersions[pos] = version;
    }
}
```

### Concave Hull Optimization

- **MaximumEdgeLength = 45**: Good balance between smooth boundaries and performance
- **Point density**: Use chunk corners (4 points per chunk) instead of all blocks
- **Grouping**: DFS algorithm is O(N) where N = chunk count, very efficient

### Caching

- **Backend**: Cache GeoJSON for 5 minutes (chunks rarely change versions)
- **Frontend**: OpenLayers caches vector features automatically
- **Invalidation**: Clear cache on `/atlas export` completion

---

## Data Investigation Needed

### Question: Where is chunk version stored?

Need to investigate ServerChunk/ServerMapChunk structure:

```csharp
// Possible locations:
1. ServerChunk.GameVersion? // Property?
2. ServerMapChunk.Metadata? // JSON field?
3. SaveGame.WorldVersion? // Global version?
4. Chunk file metadata? // File system?

// Alternative: Use chunk modification time as proxy
// Chunks created in v1.19 → mtime from that era
```

**Action:** Examine Vintage Story source or test with debugger

---

## Testing Plan

### Unit Tests

```csharp
[Fact]
public void GroupChunks_GroupsAdjacentChunks()
{
    var positions = new Dictionary<ChunkPos, string>
    {
        [new ChunkPos(0, 0, 0)] = "1.19.8",
        [new ChunkPos(1, 0, 0)] = "1.19.8",
        [new ChunkPos(2, 0, 0)] = "1.19.9",
    };
    
    var grouper = new GroupChunks(positions, _server);
    var groups = grouper.GroupPositions();
    
    Assert.Equal(2, groups.Count); // Two version groups
}
```

### Integration Tests

1. Load real savegame with multiple versions
2. Extract chunk versions
3. Generate GeoJSON
4. Verify polygon validity
5. Check gradient colors

### Visual Tests

1. View on map with different world sizes
2. Verify colors are distinguishable
3. Test with 2-10 different versions
4. Check performance with 10,000+ chunks

---

## Migration Path

### Step 1: Investigation (Day 1)
- [ ] Determine where chunk version is stored in savegame
- [ ] Write test extractor to read version data
- [ ] Validate data availability

### Step 2: Backend Integration (Days 2-3)
- [ ] Enhance SavegameDataSource
- [ ] Wire up GroupChunks
- [ ] Add API endpoint
- [ ] Test with real data

### Step 3: Frontend Integration (Day 4)
- [ ] Create chunk version layer
- [ ] Add UI controls
- [ ] Implement legend
- [ ] Test visualization

### Step 4: Polish (Day 5)
- [ ] Performance optimization
- [ ] Documentation
- [ ] User guide
- [ ] Release

---

## Success Criteria

- [ ] API endpoint returns valid GeoJSON with version data
- [ ] Frontend displays colored regions for different versions
- [ ] Concave hulls look smooth and natural
- [ ] Colors are distinguishable (2-10 versions)
- [ ] Performance acceptable (<3s for 10,000 chunks)
- [ ] Cache reduces repeated API calls
- [ ] Documentation complete

---

## Open Questions

1. **Where is chunk version stored in Vintage Story savegame?**
   - Need to investigate ServerChunk/ServerMapChunk structure
   - May need to use modification time as proxy

2. **Should we track world generation version or last modified version?**
   - Generation version = when chunk was first created
   - Modified version = last game version that touched it

3. **How many versions should we support in the gradient?**
   - 2-10 versions: Use full gradient
   - 10+ versions: Group into ranges or use different colors

4. **Should this be cached in a separate table?**
   - Pro: Faster access, no repeated savegame scans
   - Con: Extra storage, needs invalidation logic

---

## References

- GroupChunks.cs (already implemented!)
- NetTopologySuite ConcaveHull algorithm
- GeoJSON Feature Collection spec
- OpenLayers VectorLayer documentation

---

**Status:** Ready to start investigation phase  
**Next Step:** Determine chunk version storage location in Vintage Story savegame  
**Estimated Time:** 1 week to full integration

---

**Maintained by:** daviaaze  
**Created:** October 6, 2025
