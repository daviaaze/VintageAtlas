# VintageAtlas Testing Guide

Complete guide for testing VintageAtlas using the integrated test environment.

## 🎯 Quick Start

The fastest way to test VintageAtlas:

```bash
# Terminal 1: Build and start test server
quick-test

# Terminal 2: Launch game client
test-client
```

That's it! The mod is now running with test data.

## 📋 Available Test Commands

### `quick-test` - One-Command Testing
Builds the mod and starts a test server in one go.

```bash
quick-test
```

**What it does:**
1. Builds VintageAtlas (Release mode)
2. Starts test server on port 42421
3. Web interface at http://localhost:42422

**Best for:** Quick iteration during development

---

### `test-server` - Start Test Server
Launches a Vintage Story server with test data.

```bash
test-server
```

**Configuration:**
- **Port:** 42421 (customizable with `VS_TEST_PORT`)
- **Data:** `./test_server/` directory
- **Mods:** Loads from `VintageAtlas/bin/Release/Mods`
- **Web UI:** http://localhost:42422

**Server commands:**
```
/atlas export     - Generate map tiles
/atlas status     - Check mod status
/help atlas       - Show all commands
```

**Custom port:**
```bash
VS_TEST_PORT=45000 test-server
# Web UI will be at http://localhost:45001
```

---

### `test-client` - Launch Game Client
Starts Vintage Story client ready to connect to test server.

```bash
test-client
```

**What it does:**
1. Launches Vintage Story
2. Shows connection info (localhost:42421)
3. Instructions for joining server

**Manual steps:**
1. Click "Multiplayer"
2. Enter server: `localhost:42421`
3. Create/join world
4. Test mod features

**Custom connection:**
```bash
VS_TEST_HOST=192.168.1.100 VS_TEST_PORT=45000 test-client
```

---

### `test-complete` - Full Test Environment
Complete testing environment with server + browser.

```bash
test-complete
```

**What it does:**
1. Starts test server in background
2. Opens web interface in browser
3. Shows next steps

**Includes:**
- ✅ Test server running
- ✅ Browser opened to map interface
- ✅ Instructions for next steps

Press Ctrl+C to stop everything.

---

## 🔄 Complete Testing Workflow

### 1. Initial Setup

```bash
# Enter development environment
nix develop

# Check setup
setup-vintageatlas
```

### 2. Build the Mod

```bash
# Build release version
build-vintageatlas

# Or build debug version
build-vintageatlas-debug
```

### 3. Start Test Environment

**Option A: Quick Test (Recommended)**
```bash
# Terminal 1
quick-test
```

**Option B: Manual Start**
```bash
# Terminal 1
test-server

# Terminal 2 (after server starts)
test-client
```

**Option C: Complete Environment**
```bash
test-complete
# Opens browser automatically
# Run test-client in another terminal
```

### 4. In-Game Testing

Once connected to the server:

```
/atlas export
```

This will:
- Generate map tiles
- Extract GeoJSON data (signs, traders, translocators)
- Create HTML files for web interface

### 5. View Web Interface

Open browser to:
```
http://localhost:42422/
```

**Features to test:**
- ✅ Map tiles loading
- ✅ Layer toggles (terrain, traders, signs, etc.)
- ✅ Live player positions
- ✅ Interactive features (click on traders, signs)
- ✅ Search functionality
- ✅ Historical data (if enabled)

### 6. Test API Endpoints

```bash
# Map configuration
curl http://localhost:42422/api/map-config

# Server status
curl http://localhost:42422/api/status

# GeoJSON layers
curl http://localhost:42422/api/geojson/signs
curl http://localhost:42422/api/geojson/traders
curl http://localhost:42422/api/geojson/translocators

# Tile with caching
curl -I http://localhost:42422/tiles/9/1000_1000.png
```

## 🧪 Testing Scenarios

### Scenario 1: Basic Map Export

**Steps:**
1. Start test server: `quick-test`
2. Connect client: `test-client`
3. In game: `/atlas export`
4. Check browser: Map should appear

**Expected Result:**
- ✅ Tiles generated in `test_server/vintageatlas/html/data/world/`
- ✅ Web interface shows map
- ✅ Can zoom and pan

---

### Scenario 2: Dynamic Chunk Updates

**Steps:**
1. Server running with mod
2. Place/break blocks in-game
3. Wait 30 seconds
4. Refresh map in browser

**Expected Result:**
- ✅ Modified chunks regenerate automatically
- ✅ Changes appear on map without full export
- ✅ Server logs show: "Regenerating X modified chunk tiles"

---

### Scenario 3: GeoJSON Features

**Test Signs:**
```
1. Place a sign in-game
2. Add text: "<AM:BASE>\nMy Base"
3. Wait or run /atlas export
4. Check browser - sign should appear
5. Click sign - popup shows info
```

**Test Traders:**
```
1. Find a trader (or spawn with creative)
2. Run /atlas export
3. Check map - trader icon appears
4. Click trader - shows wares
```

**Test Translocators:**
```
1. Build a teleporter network
2. Run /atlas export
3. Check map - lines connecting portals
4. Click lines - shows connection info
```

---

### Scenario 4: Live Data

**Test Player Tracking:**
```
1. Connect to server
2. Open web interface
3. Enable "Players" layer in sidebar
4. Move around in-game
5. Watch position update on map (every 5 seconds)
```

**Test Server Status:**
```
1. Check header - shows player count
2. Sidebar shows online players
3. API endpoint: curl http://localhost:42422/api/status
```

---

### Scenario 5: API Caching

**Test ETag Support:**
```bash
# First request
curl -I http://localhost:42422/api/geojson/signs
# Note the ETag header

# Second request with ETag
curl -H 'If-None-Match: "123456"' \
     -I http://localhost:42422/api/geojson/signs
# Should return 304 Not Modified
```

---

### Scenario 6: Configuration API

**Test Dynamic Config:**
```bash
# Get map configuration
curl http://localhost:42422/api/map-config | jq

# Should return:
# - worldExtent
# - defaultCenter
# - zoom levels
# - tile resolutions
# - server metadata
```

**Frontend Integration:**
```
1. Open browser console
2. Check initialization log:
   "[VintageAtlas] Map configuration loaded from server"
3. Verify map uses API config values
```

---

## 🐛 Troubleshooting

### Server won't start

**Check:**
```bash
# Is mod built?
ls -la VintageAtlas/bin/Release/Mods/vintageatlas/

# Check test_server permissions
ls -ld test_server/

# Try with verbose output
test-server 2>&1 | tee server.log
```

**Common issues:**
- Mod not built → Run `build-vintageatlas`
- Port already in use → Change port: `VS_TEST_PORT=45000 test-server`
- Missing dependencies → Check `setup-vintageatlas` output

---

### Client can't connect

**Check:**
```bash
# Server running?
ps aux | grep VintagestoryServer

# Port accessible?
nc -zv localhost 42421

# Firewall?
sudo ufw status
```

**Fix:**
```bash
# Allow port
sudo ufw allow 42421
```

---

### Map not showing

**Check:**
```bash
# Tiles generated?
ls test_server/vintageatlas/html/data/world/9/

# Web server running?
curl http://localhost:42422/api/status

# Browser console errors?
# Open browser dev tools (F12)
```

**Fix:**
```
1. In game: /atlas export
2. Wait for completion
3. Check server logs
4. Refresh browser (Ctrl+Shift+R)
```

---

### API not responding

**Check:**
```bash
# Server running?
curl http://localhost:42422/api/health

# Check logs
tail -f test_server/VintagestoryData/Logs/server-main.log

# Mod loaded?
# In server console: /moddb list
```

---

## 📊 Performance Testing

### Test Incremental Updates

```bash
# 1. Start server with monitoring
test-server | tee server.log

# 2. In game, place 100 blocks
# 3. Check logs after 30 seconds:
grep "Regenerating" server.log

# Expected: Only affected chunks regenerated
# Not: Full map export
```

### Test API Caching

```bash
# Measure without caching
time curl http://localhost:42422/api/geojson/signs > /dev/null

# Measure with caching (second request)
time curl http://localhost:42422/api/geojson/signs > /dev/null

# Should be significantly faster
```

### Test Tile Serving

```bash
# First request (cache miss)
time curl http://localhost:42422/tiles/9/1000_1000.png > /dev/null

# Second request (cache hit)
time curl -H 'If-None-Match: "..."' \
     http://localhost:42422/tiles/9/1000_1000.png > /dev/null

# Should return 304 immediately
```

---

## 🔍 Debug Mode

### Enable Debug Logging

```bash
# Build debug version
build-vintageatlas-debug

# Or set in config
echo '{"DebugLogging": true}' > test_server/VintagestoryData/ModConfig/VintageAtlasConfig.json

# Start server
test-server
```

### Monitor Live Logs

```bash
# In another terminal
tail -f test_server/VintagestoryData/Logs/server-main.log | grep VintageAtlas
```

### Check Mod Status

In-game commands:
```
/atlas status       - Show mod status
/atlas config       - Show configuration
/moddb list         - List all mods
```

---

## 📖 Test Data

The `test_server/` directory contains:

```
test_server/
├── VintagestoryData/
│   ├── ModConfig/          # Mod configurations
│   ├── Saves/              # World save data
│   ├── Logs/               # Server logs
│   └── serverconfig.json   # Server config
└── vintageatlas/
    └── html/               # Generated web files
        ├── data/
        │   ├── world/      # Map tiles
        │   └── geojson/    # Vector data
        └── index.html      # Web interface
```

**Reset test data:**
```bash
# Backup current
mv test_server test_server.backup

# Create fresh
mkdir -p test_server

# Server will initialize on first run
```

---

## 🚀 Advanced Testing

### Test with Multiple Clients

```bash
# Terminal 1
test-server

# Terminal 2
VS_TEST_HOST=localhost test-client

# Terminal 3
VS_TEST_HOST=localhost test-client

# Verify:
# - Both clients connect
# - Both appear on map
# - Player count shows 2/4
```

### Test Remote Connection

```bash
# Server (on machine A)
test-server

# Client (on machine B)
VS_TEST_HOST=192.168.1.100 test-client

# Web browser
# Open: http://192.168.1.100:42422/
```

### Test Production Build

```bash
# Build release
build-vintageatlas

# Package
package-vintageatlas

# Test package
unzip VintageAtlas-*.zip -d /tmp/test
ls -la /tmp/test
```

---

## ✅ Test Checklist

Use this checklist for comprehensive testing:

### Core Functionality
- [ ] Mod loads without errors
- [ ] `/atlas export` works
- [ ] Map tiles generated
- [ ] Web interface accessible
- [ ] Layers toggle correctly

### Dynamic Features
- [ ] Chunk change tracking works
- [ ] Modified chunks regenerate (30s)
- [ ] No full re-export needed

### API Endpoints
- [ ] `/api/map-config` returns data
- [ ] `/api/status` shows server info
- [ ] `/api/geojson/*` return features
- [ ] `/tiles/{z}/{x}_{y}.png` serve tiles
- [ ] ETag caching works

### GeoJSON Features
- [ ] Signs display on map
- [ ] Traders show with icons
- [ ] Translocators show connections
- [ ] Click popups work

### Live Data
- [ ] Player positions update
- [ ] Server status shows in header
- [ ] Online players list works
- [ ] Real-time updates (5s interval)

### Performance
- [ ] Incremental updates < 30s
- [ ] API responses < 100ms
- [ ] Tile serving with cache < 10ms
- [ ] No memory leaks

### Browser Compatibility
- [ ] Works in Chrome/Chromium
- [ ] Works in Firefox
- [ ] Mobile responsive
- [ ] Dark mode works

---

## 📚 Additional Resources

- **API Documentation:** `API-EXAMPLES.md`
- **Implementation Details:** `IMPLEMENTATION-SUMMARY.md`
- **Upgrade Guide:** `UPGRADE-GUIDE.md`
- **User Guide:** `VintageAtlas/README.md`

## 🐛 Reporting Issues

When reporting issues, include:

1. **Output of:**
   ```bash
   setup-vintageatlas
   ```

2. **Server logs:**
   ```bash
   test_server/VintagestoryData/Logs/server-main.log
   ```

3. **Browser console errors** (F12 → Console)

4. **Steps to reproduce**

5. **Expected vs actual behavior**

---

**Happy Testing!** 🎮🗺️

For questions or issues, check the documentation or open an issue on GitHub.

---

**References:**
- [Vintage Story Server Startup Parameters](https://wiki.vintagestory.at/Server_startup_parameters)
- [Vintage Story Client Startup Parameters](https://wiki.vintagestory.at/Client_startup_parameters)
- [VintageAtlas Documentation](README.md)

