# API Documentation with OpenAPI/Swagger

**Created:** October 6, 2025  
**Status:** Planning  
**Goal:** Generate OpenAPI 3.0 spec for all API endpoints

---

## Current State

**Status:** No API documentation  
**Endpoints:** 20+ across 6 controllers  
**Format:** None  
**Target:** OpenAPI 3.0 specification

---

## Why OpenAPI/Swagger?

### Benefits

1. **Auto-generated documentation** - Interactive API explorer
2. **Type safety** - Generate TypeScript clients automatically
3. **Testing** - Use spec for API testing
4. **Standards** - Industry-standard format
5. **Discovery** - Easy for developers to explore API

### Tools

- **NSwag** - .NET OpenAPI generator
- **Swashbuckle** - Alternative .NET generator
- **Swagger UI** - Interactive documentation
- **openapi-typescript** - Generate TypeScript types

---

## Implementation Approach

### Option 1: NSwag (Recommended)

**Pros:**

- Generates C# and TypeScript clients
- Excellent .NET integration
- Can generate from code or spec

**Cons:**

- Adds build-time dependency

### Option 2: Manual OpenAPI Spec

**Pros:**

- No code changes
- Full control
- No dependencies

**Cons:**

- Manual maintenance
- Can get out of sync

**Recommendation:** Start with **manual spec** (faster), migrate to NSwag later if needed.

---

## API Endpoint Inventory

### Status Controller (`/api/status`)

```yaml
GET /api/status
  Summary: Get current server status
  Response: ServerStatusData
  
GET /api/status/players
  Summary: Get online players
  Response: Player[]
```

### Historical Controller (`/api/historical`)

```yaml
GET /api/historical/heatmap
  Summary: Get player activity heatmap
  Query: startTime, endTime, playerId (optional)
  Response: HeatmapPoint[]

GET /api/historical/player-path
  Summary: Get player movement path
  Query: startTime, endTime, playerId
  Response: PlayerPathPoint[]

GET /api/historical/entity-census
  Summary: Get entity population over time
  Query: startTime, endTime
  Response: EntityCensusSnapshot[]

GET /api/historical/statistics
  Summary: Get server statistics
  Response: ServerStatistics
```

### Live Controller (`/api/live`)

```yaml
GET /api/live/players
  Summary: Get current player positions
  Response: LivePlayerData[]

GET /api/live/animals
  Summary: Get animal positions
  Response: LiveAnimalData[]

GET /api/live/combined
  Summary: Get all live data
  Response: LiveMapData
```

### Config Controller (`/api/config`)

```yaml
GET /api/config/map
  Summary: Get map configuration
  Response: MapConfigData

POST /api/config/export
  Summary: Trigger map export
  Body: ExportOptions (optional)
  Response: { success: bool, message: string }
```

### GeoJSON Controller (`/api/geojson`)

```yaml
GET /api/geojson/{category}
  Summary: Get GeoJSON features
  Path: category (traders, translocators, signs, etc.)
  Response: FeatureCollection
```

### Tile Controller (`/tiles`)

```yaml
GET /tiles/{z}/{x}/{y}.png
  Summary: Get map tile
  Path: z (zoom), x, y (tile coordinates)
  Response: image/png
  Headers: ETag, Last-Modified
```

---

## OpenAPI Spec Structure

### Basic Info

```yaml
openapi: 3.0.0
info:
  title: VintageAtlas API
  version: 1.0.0
  description: |
    REST API for VintageAtlas mod - Interactive map and data visualization
    for Vintage Story game servers.
    
    ## Features
    - Real-time player and entity tracking
    - Historical data analysis
    - Map tile serving
    - GeoJSON feature export
    
  contact:
    name: daviaaze
    url: https://github.com/daviaaze/VintageAtlas
  license:
    name: MIT
    url: https://opensource.org/licenses/MIT

servers:
  - url: http://localhost:42422
    description: Local development server
  - url: http://localhost:42423
    description: Production server (typical port)
```

### Common Components

```yaml
components:
  schemas:
    ServerStatus:
      type: object
      properties:
        serverName:
          type: string
          example: "My Vintage Story Server"
        gameVersion:
          type: string
          example: "1.19.8"
        modVersion:
          type: string
          example: "1.0.0"
        currentPlayers:
          type: integer
          example: 3
        maxPlayers:
          type: integer
          example: 10
        uptime:
          type: integer
          format: int64
          description: Server uptime in seconds
        
    Player:
      type: object
      properties:
        name:
          type: string
          example: "PlayerName"
        uid:
          type: string
          example: "uuid-here"
        position:
          $ref: '#/components/schemas/Position'
        online:
          type: boolean
        
    Position:
      type: object
      properties:
        x:
          type: number
          format: double
        y:
          type: number
          format: double
        z:
          type: number
          format: double
          
    HeatmapPoint:
      type: object
      properties:
        x:
          type: number
        z:
          type: number
        intensity:
          type: integer
          description: Activity intensity (0-100)
        timestamp:
          type: string
          format: date-time
          
    Error:
      type: object
      properties:
        error:
          type: string
          example: "Resource not found"
        code:
          type: integer
          example: 404
```

---

## Implementation Steps

### Step 1: Create Base Spec (1 hour)

```bash
mkdir -p docs/api
touch docs/api/openapi.yaml
```

Write base structure:

- Info section
- Servers
- Common schemas

### Step 2: Document Each Controller (3-4 hours)

For each controller:

1. List all endpoints
2. Define request parameters
3. Define response schemas
4. Add examples

### Step 3: Add to Build Process (1 hour)

```csharp
// Add to VintageAtlas.csproj
<ItemGroup>
  <None Include="../../docs/api/openapi.yaml">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

Serve spec at `/api/openapi.yaml`

### Step 4: Add Swagger UI (2 hours)

Embed Swagger UI in `html/` directory:

```html
<!-- html/api-docs.html -->
<!DOCTYPE html>
<html>
<head>
    <title>VintageAtlas API</title>
    <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5/swagger-ui.css">
</head>
<body>
    <div id="swagger-ui"></div>
    <script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
    <script>
        SwaggerUIBundle({
            url: '/api/openapi.yaml',
            dom_id: '#swagger-ui',
        });
    </script>
</body>
</html>
```

Access at: `http://localhost:42422/api-docs.html`

### Step 5: Generate TypeScript Types (Optional, 1 hour)

```bash
cd VintageAtlas/frontend
npm install --save-dev openapi-typescript
npx openapi-typescript ../../docs/api/openapi.yaml -o src/types/api-schema.ts
```

Use generated types:

```typescript
import type { components } from '@/types/api-schema';

type ServerStatus = components['schemas']['ServerStatus'];
type Player = components['schemas']['Player'];
```

---

## Minimal OpenAPI Spec (MVP)

```yaml
openapi: 3.0.0
info:
  title: VintageAtlas API
  version: 1.0.0
  description: Interactive map API for Vintage Story servers

servers:
  - url: http://localhost:42422

paths:
  /api/status:
    get:
      summary: Get server status
      operationId: getStatus
      tags:
        - Status
      responses:
        '200':
          description: Server status
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ServerStatus'

  /api/live/combined:
    get:
      summary: Get all live map data
      operationId: getLiveData
      tags:
        - Live
      responses:
        '200':
          description: Live data
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/LiveMapData'

  /tiles/{z}/{x}/{y}.png:
    get:
      summary: Get map tile
      operationId: getTile
      tags:
        - Tiles
      parameters:
        - name: z
          in: path
          required: true
          schema:
            type: integer
          description: Zoom level
        - name: x
          in: path
          required: true
          schema:
            type: integer
          description: Tile X coordinate
        - name: y
          in: path
          required: true
          schema:
            type: integer
          description: Tile Y coordinate
      responses:
        '200':
          description: PNG tile image
          content:
            image/png:
              schema:
                type: string
                format: binary
        '404':
          description: Tile not found

components:
  schemas:
    ServerStatus:
      type: object
      required:
        - serverName
        - gameVersion
      properties:
        serverName:
          type: string
        gameVersion:
          type: string
        modVersion:
          type: string
        currentPlayers:
          type: integer
        maxPlayers:
          type: integer
        uptime:
          type: integer
          format: int64

    LiveMapData:
      type: object
      properties:
        players:
          type: array
          items:
            $ref: '#/components/schemas/LivePlayer'
        animals:
          type: array
          items:
            $ref: '#/components/schemas/LiveAnimal'
        spawn:
          $ref: '#/components/schemas/Position'

    LivePlayer:
      type: object
      properties:
        name:
          type: string
        position:
          $ref: '#/components/schemas/Position'

    LiveAnimal:
      type: object
      properties:
        type:
          type: string
        position:
          $ref: '#/components/schemas/Position'

    Position:
      type: object
      properties:
        x:
          type: number
        y:
          type: number
        z:
          type: number
```

---

## Testing the Spec

### Validation

```bash
# Install validator
npm install -g @apidevtools/swagger-cli

# Validate spec
swagger-cli validate docs/api/openapi.yaml
```

### Mock Server

```bash
# Install prism (mock server)
npm install -g @stoplight/prism-cli

# Run mock server
prism mock docs/api/openapi.yaml
```

---

## Timeline

| Task | Time | Status |
|------|------|--------|
| Create base spec structure | 1h | ⏳ |
| Document Status API | 30m | ⏳ |
| Document Historical API | 1h | ⏳ |
| Document Live API | 30m | ⏳ |
| Document Config API | 30m | ⏳ |
| Document GeoJSON API | 30m | ⏳ |
| Document Tile API | 30m | ⏳ |
| Add Swagger UI | 2h | ⏳ |
| Generate TypeScript types | 1h | ⏳ |
| **Total** | **~8 hours** | |

---

## Success Criteria

- [ ] Complete OpenAPI 3.0 spec covering all endpoints
- [ ] Swagger UI accessible at `/api-docs.html`
- [ ] Spec validates without errors
- [ ] Examples for all major endpoints
- [ ] TypeScript types generated (optional)
- [ ] Documentation linked from main README

---

## Next Steps

1. ✅ Review this plan
2. 🟡 Create minimal MVP spec (3 endpoints)
3. 🟡 Set up Swagger UI serving
4. ⏳ Expand to all endpoints
5. ⏳ Generate TypeScript types
6. ⏳ Add to CI/CD validation

---

## References

- [OpenAPI 3.0 Specification](https://swagger.io/specification/)
- [Swagger UI](https://swagger.io/tools/swagger-ui/)
- [NSwag Documentation](https://github.com/RicoSuter/NSwag)
- [openapi-typescript](https://github.com/drwpow/openapi-typescript)

---

**Maintained by:** daviaaze  
**Last Updated:** October 6, 2025
