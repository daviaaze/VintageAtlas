# VintageAtlas API Examples

Complete examples for using the new VintageAtlas API endpoints.

## 🌐 Base URL

Replace `localhost:42421` with your server address and port (default is game port + 1).

```
http://localhost:42421
```

## 📍 Map Configuration

### Get Full Map Configuration

```bash
curl http://localhost:42421/api/map-config
```

**Response:**
```json
{
  "worldExtent": [-256000, -256000, 256000, 256000],
  "worldOrigin": [-256000, 256000],
  "defaultCenter": [0, -5000],
  "defaultZoom": 7,
  "minZoom": 0,
  "maxZoom": 9,
  "baseZoomLevel": 9,
  "tileSize": 256,
  "tileResolutions": [512, 256, 128, 64, 32, 16, 8, 4, 2, 1],
  "viewResolutions": [256, 128, 64, 32, 16, 8, 4, 2, 1, 0.5, 0.25, 0.125],
  "spawnPosition": [0, 0],
  "mapSizeX": 1024000,
  "mapSizeZ": 1024000,
  "mapSizeY": 256,
  "tileStats": {
    "totalTiles": 4523,
    "totalSizeBytes": 125829374,
    "zoomLevels": {
      "9": { "tileCount": 2134, "totalSizeBytes": 89374829 },
      "8": { "tileCount": 534, "totalSizeBytes": 23847293 }
    }
  },
  "serverName": "My Awesome Server",
  "worldName": "world_20251002"
}
```

### JavaScript Usage

```javascript
// Fetch map configuration
const config = await fetch('http://localhost:42421/api/map-config')
  .then(res => res.json());

// Use in OpenLayers
import Map from 'ol/Map';
import View from 'ol/View';
import TileLayer from 'ol/layer/Tile';
import XYZ from 'ol/source/XYZ';
import TileGrid from 'ol/tilegrid/TileGrid';

const tileGrid = new TileGrid({
  extent: config.worldExtent,
  origin: config.worldOrigin,
  resolutions: config.tileResolutions,
  tileSize: [config.tileSize, config.tileSize]
});

const map = new Map({
  target: 'map',
  layers: [
    new TileLayer({
      source: new XYZ({
        url: 'http://localhost:42421/tiles/{z}/{x}_{y}.png',
        tileGrid: tileGrid,
        wrapX: false
      })
    })
  ],
  view: new View({
    center: config.defaultCenter,
    zoom: config.defaultZoom,
    minZoom: config.minZoom,
    maxZoom: config.maxZoom,
    resolutions: config.viewResolutions,
    extent: config.worldExtent
  })
});
```

### Python Usage

```python
import requests

# Fetch configuration
response = requests.get('http://localhost:42421/api/map-config')
config = response.json()

# Extract useful info
world_extent = config['worldExtent']
spawn = config['spawnPosition']
total_tiles = config['tileStats']['totalTiles']

print(f"World extends from {world_extent[0]},{world_extent[1]} to {world_extent[2]},{world_extent[3]}")
print(f"Spawn at: {spawn}")
print(f"Total tiles: {total_tiles}")
```

## 🗺️ Map Tiles

### Fetch a Specific Tile

```bash
# Zoom level 9, tile coordinates (1000, 1000)
curl http://localhost:42421/tiles/9/1000_1000.png -o tile.png
```

### With Caching (ETag)

```bash
# First request - get ETag
curl -I http://localhost:42421/tiles/9/1000_1000.png

# Response includes:
# ETag: "12345-67890"
# Cache-Control: public, max-age=3600

# Subsequent request with ETag
curl -H 'If-None-Match: "12345-67890"' \
  http://localhost:42421/tiles/9/1000_1000.png

# Returns 304 Not Modified if unchanged
```

### JavaScript with Caching

```javascript
// OpenLayers automatically handles ETags via browser cache
const tileSource = new XYZ({
  url: 'http://localhost:42421/tiles/{z}/{x}_{y}.png',
  tileGrid: tileGrid
});

// Manual fetch with cache handling
async function fetchTile(zoom, x, y, etag = null) {
  const headers = {};
  if (etag) {
    headers['If-None-Match'] = etag;
  }

  const response = await fetch(
    `http://localhost:42421/tiles/${zoom}/${x}_${y}.png`,
    { headers }
  );

  if (response.status === 304) {
    console.log('Using cached tile');
    return null; // Use cached version
  }

  const newEtag = response.headers.get('ETag');
  const blob = await response.blob();
  
  return { blob, etag: newEtag };
}
```

## 📍 GeoJSON Layers

### Fetch All GeoJSON Layers

```bash
# Signs/Landmarks
curl http://localhost:42421/api/geojson/signs

# Signposts
curl http://localhost:42421/api/geojson/signposts

# Traders
curl http://localhost:42421/api/geojson/traders

# Translocators
curl http://localhost:42421/api/geojson/translocators
```

### JavaScript Usage

```javascript
// Fetch signs layer
const signsData = await fetch('http://localhost:42421/api/geojson/signs')
  .then(res => res.json());

// Use with OpenLayers
import VectorLayer from 'ol/layer/Vector';
import VectorSource from 'ol/source/Vector';
import GeoJSON from 'ol/format/GeoJSON';
import { Style, Icon, Text, Fill, Stroke } from 'ol/style';

const signsLayer = new VectorLayer({
  source: new VectorSource({
    features: new GeoJSON().readFeatures(signsData)
  }),
  style: feature => {
    const type = feature.get('type') || 'default';
    return new Style({
      image: new Icon({
        src: `/assets/icons/${type}.svg`,
        scale: 0.5
      }),
      text: new Text({
        text: feature.get('name'),
        offsetY: 20,
        fill: new Fill({ color: '#000' }),
        stroke: new Stroke({ color: '#fff', width: 2 })
      })
    });
  }
});

map.addLayer(signsLayer);
```

### Python Usage

```python
import requests
import json

# Fetch all GeoJSON layers
endpoints = {
    'signs': 'http://localhost:42421/api/geojson/signs',
    'traders': 'http://localhost:42421/api/geojson/traders',
    'translocators': 'http://localhost:42421/api/geojson/translocators'
}

geojson_data = {}
for name, url in endpoints.items():
    response = requests.get(url)
    geojson_data[name] = response.json()
    
    print(f"{name}: {len(geojson_data[name]['features'])} features")

# Process trader locations
traders = geojson_data['traders']['features']
for trader in traders:
    coords = trader['geometry']['coordinates']
    name = trader['properties'].get('name', 'Unknown')
    wares = trader['properties'].get('wares', 'Unknown')
    print(f"{name} at [{coords[0]}, {coords[1]}] sells: {wares}")
```

### With Caching

```javascript
// Fetch with ETag support
async function fetchGeoJsonWithCache(url, etag = null) {
  const headers = { 'Accept': 'application/geo+json' };
  if (etag) {
    headers['If-None-Match'] = etag;
  }

  const response = await fetch(url, { headers });

  if (response.status === 304) {
    return { cached: true };
  }

  const data = await response.json();
  const newEtag = response.headers.get('ETag');
  
  return { data, etag: newEtag, cached: false };
}

// Usage
let cachedEtag = localStorage.getItem('signs-etag');
const result = await fetchGeoJsonWithCache(
  'http://localhost:42421/api/geojson/signs',
  cachedEtag
);

if (result.cached) {
  console.log('Using cached GeoJSON');
  // Load from local storage
} else {
  console.log('Received new GeoJSON data');
  localStorage.setItem('signs-etag', result.etag);
  localStorage.setItem('signs-data', JSON.stringify(result.data));
}
```

## 📊 Server Status

### Get Live Server Status

```bash
curl http://localhost:42421/api/status
```

**Response:**
```json
{
  "serverName": "My Server",
  "worldName": "world_20251002",
  "gameTime": 1234567890,
  "serverUptime": 123456,
  "playersOnline": 5,
  "players": [
    {
      "name": "PlayerName",
      "x": 1234,
      "y": 128,
      "z": 5678,
      "health": 20,
      "saturation": 1500
    }
  ],
  "animals": {
    "game-chicken": 45,
    "game-pig": 23
  },
  "weather": {
    "precipitation": 0.5,
    "temperature": 15
  }
}
```

### JavaScript Polling

```javascript
// Poll server status every 5 seconds
async function pollServerStatus() {
  const status = await fetch('http://localhost:42421/api/status')
    .then(res => res.json());

  // Update UI with player positions
  status.players.forEach(player => {
    updatePlayerMarker(player.name, [player.x, player.z]);
  });

  // Schedule next poll
  setTimeout(pollServerStatus, 5000);
}

pollServerStatus();
```

## ⚙️ Configuration Management

### Get Runtime Configuration

```bash
curl http://localhost:42421/api/config
```

**Response:**
```json
{
  "autoExportMap": true,
  "historicalTracking": true,
  "exportIntervalMs": 300000,
  "isExporting": false,
  "enableLiveServer": true,
  "maxConcurrentRequests": 50
}
```

### Update Configuration

```bash
curl -X POST http://localhost:42421/api/config \
  -H 'Content-Type: application/json' \
  -d '{
    "autoExportMap": false,
    "saveToDisk": true
  }'
```

### Trigger Manual Export

```bash
curl -X POST http://localhost:42421/api/export
```

**Response:**
```json
{
  "success": true,
  "message": "Export started"
}
```

## 📈 Historical Data

### Get Player Heatmap

```bash
curl 'http://localhost:42421/api/heatmap?player=PlayerName&hours=24'
```

### Get Player Path

```bash
curl 'http://localhost:42421/api/player-path?player=PlayerName&hours=6'
```

### Get Entity Census

```bash
curl 'http://localhost:42421/api/census'
```

## 🔐 CORS Configuration

### Enable CORS for External Apps

In `VintageAtlasConfig.json`:
```json
{
  "EnableCORS": true
}
```

### JavaScript from External Origin

```javascript
// Fetch from external website/app
fetch('http://game-server.com:42421/api/map-config', {
  mode: 'cors'
})
.then(res => res.json())
.then(config => {
  console.log('Map config:', config);
});
```

## 🐍 Complete Python Example

```python
import requests
import time
from typing import Dict, List

class VintageAtlasClient:
    def __init__(self, base_url: str):
        self.base_url = base_url.rstrip('/')
        self._etags = {}
    
    def get_map_config(self) -> Dict:
        """Fetch map configuration"""
        response = requests.get(f'{self.base_url}/api/map-config')
        response.raise_for_status()
        return response.json()
    
    def get_geojson(self, layer: str, use_cache: bool = True) -> Dict:
        """Fetch GeoJSON layer with caching"""
        url = f'{self.base_url}/api/geojson/{layer}'
        headers = {'Accept': 'application/geo+json'}
        
        if use_cache and layer in self._etags:
            headers['If-None-Match'] = self._etags[layer]
        
        response = requests.get(url, headers=headers)
        
        if response.status_code == 304:
            print(f'{layer}: Using cached data')
            return None  # Use previously cached data
        
        response.raise_for_status()
        
        if 'ETag' in response.headers:
            self._etags[layer] = response.headers['ETag']
        
        return response.json()
    
    def get_server_status(self) -> Dict:
        """Get live server status"""
        response = requests.get(f'{self.base_url}/api/status')
        response.raise_for_status()
        return response.json()
    
    def trigger_export(self) -> bool:
        """Trigger manual map export"""
        response = requests.post(f'{self.base_url}/api/export')
        response.raise_for_status()
        return response.json().get('success', False)

# Usage
client = VintageAtlasClient('http://localhost:42421')

# Get map config
config = client.get_map_config()
print(f"World size: {config['mapSizeX']} x {config['mapSizeZ']}")

# Fetch GeoJSON layers
signs = client.get_geojson('signs')
print(f"Found {len(signs['features'])} signs")

# Monitor server in loop
while True:
    status = client.get_server_status()
    print(f"Players online: {status['playersOnline']}")
    time.sleep(10)
```

## 🌐 Web Dashboard Example

```html
<!DOCTYPE html>
<html>
<head>
  <title>VintageAtlas Dashboard</title>
  <script src="https://cdn.jsdelivr.net/npm/ol@latest/dist/ol.js"></script>
  <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/ol@latest/ol.css">
</head>
<body>
  <div id="map" style="width: 100%; height: 600px;"></div>
  <div id="status"></div>

  <script>
    const API_BASE = 'http://localhost:42421';

    // Initialize map with dynamic config
    async function initMap() {
      const config = await fetch(`${API_BASE}/api/map-config`)
        .then(res => res.json());

      const map = new ol.Map({
        target: 'map',
        layers: [
          new ol.layer.Tile({
            source: new ol.source.XYZ({
              url: `${API_BASE}/tiles/{z}/{x}_{y}.png`,
              tileGrid: new ol.tilegrid.TileGrid({
                extent: config.worldExtent,
                origin: config.worldOrigin,
                resolutions: config.tileResolutions,
                tileSize: [config.tileSize, config.tileSize]
              }),
              wrapX: false
            })
          })
        ],
        view: new ol.View({
          center: config.defaultCenter,
          zoom: config.defaultZoom,
          resolutions: config.viewResolutions,
          extent: config.worldExtent
        })
      });

      // Add GeoJSON layers
      const layers = ['signs', 'traders', 'translocators'];
      for (const layer of layers) {
        const data = await fetch(`${API_BASE}/api/geojson/${layer}`)
          .then(res => res.json());

        map.addLayer(new ol.layer.Vector({
          source: new ol.source.Vector({
            features: new ol.format.GeoJSON().readFeatures(data)
          })
        }));
      }

      // Update status every 5 seconds
      updateStatus();
      setInterval(updateStatus, 5000);
    }

    async function updateStatus() {
      const status = await fetch(`${API_BASE}/api/status`)
        .then(res => res.json());

      document.getElementById('status').innerHTML = `
        <h3>${status.serverName}</h3>
        <p>Players online: ${status.playersOnline}</p>
        <ul>
          ${status.players.map(p => `
            <li>${p.name} at [${p.x}, ${p.z}]</li>
          `).join('')}
        </ul>
      `;
    }

    initMap();
  </script>
</body>
</html>
```

## 📱 Mobile App Example (React Native)

```typescript
import React, { useEffect, useState } from 'react';
import { View, Text, FlatList } from 'react-native';

const VintageAtlasApp = () => {
  const [config, setConfig] = useState(null);
  const [players, setPlayers] = useState([]);

  const API_BASE = 'http://your-server.com:42421';

  useEffect(() => {
    // Load config once
    fetch(`${API_BASE}/api/map-config`)
      .then(res => res.json())
      .then(setConfig);

    // Poll players
    const interval = setInterval(() => {
      fetch(`${API_BASE}/api/status`)
        .then(res => res.json())
        .then(data => setPlayers(data.players));
    }, 5000);

    return () => clearInterval(interval);
  }, []);

  return (
    <View>
      <Text>Server: {config?.serverName}</Text>
      <Text>Players Online: {players.length}</Text>
      
      <FlatList
        data={players}
        renderItem={({ item }) => (
          <View>
            <Text>{item.name}</Text>
            <Text>Position: [{item.x}, {item.z}]</Text>
          </View>
        )}
        keyExtractor={item => item.name}
      />
    </View>
  );
};

export default VintageAtlasApp;
```

## 🎮 Discord Bot Example

```python
import discord
import requests
import asyncio

class VintageAtlasBot(discord.Client):
    def __init__(self):
        super().__init__()
        self.api_base = 'http://localhost:42421'
    
    async def on_ready(self):
        print(f'Bot ready: {self.user}')
        self.loop.create_task(self.update_status())
    
    async def on_message(self, message):
        if message.author == self.user:
            return
        
        if message.content == '!players':
            status = requests.get(f'{self.api_base}/api/status').json()
            players = '\n'.join([
                f"{p['name']} at [{p['x']}, {p['z']}]"
                for p in status['players']
            ])
            await message.channel.send(f"**Players Online:**\n{players}")
        
        elif message.content == '!map':
            config = requests.get(f'{self.api_base}/api/map-config').json()
            await message.channel.send(
                f"**Map Info:**\n"
                f"World: {config['worldName']}\n"
                f"Size: {config['mapSizeX']} x {config['mapSizeZ']}\n"
                f"Total Tiles: {config['tileStats']['totalTiles']}"
            )
    
    async def update_status(self):
        while True:
            try:
                status = requests.get(f'{self.api_base}/api/status').json()
                activity = discord.Game(
                    f"{status['playersOnline']} players online"
                )
                await self.change_presence(activity=activity)
            except:
                pass
            
            await asyncio.sleep(30)

bot = VintageAtlasBot()
bot.run('YOUR_BOT_TOKEN')
```

---

## 📚 More Resources

- **Main README**: `README.md`
- **Upgrade Guide**: `UPGRADE-GUIDE.md`
- **Implementation Summary**: `IMPLEMENTATION-SUMMARY.md`
- **API Reference**: This file

## 🐛 Troubleshooting

If you encounter issues:

1. Check server logs for errors
2. Verify API endpoints are accessible
3. Test with curl/Postman first
4. Enable CORS if accessing from external domain
5. Check firewall settings

---

**Happy Mapping!** 🗺️

