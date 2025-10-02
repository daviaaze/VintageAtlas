#!/usr/bin/env bash
# Test script for VintageAtlas tile system

echo "🧪 Testing VintageAtlas Tile System"
echo "===================================="
echo ""

# Test 1: API endpoint
echo "1️⃣ Testing API endpoint..."
API_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:42422/api/map-config")
if [ "$API_STATUS" = "200" ]; then
    echo "   ✅ API responding (HTTP $API_STATUS)"
else
    echo "   ❌ API not responding (HTTP $API_STATUS)"
fi
echo ""

# Test 2: Positive coordinate tile
echo "2️⃣ Testing tile with positive coordinates..."
TILE1_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:42422/tiles/5/10_10.png")
TILE1_SIZE=$(curl -s -o /dev/null -w "%{size_download}" "http://localhost:42422/tiles/5/10_10.png")
echo "   URL: /tiles/5/10_10.png"
echo "   Status: HTTP $TILE1_STATUS"
echo "   Size: $TILE1_SIZE bytes"
if [ "$TILE1_STATUS" = "200" ] || [ "$TILE1_STATUS" = "404" ]; then
    echo "   ✅ Tile endpoint responding"
else
    echo "   ❌ Tile endpoint error"
fi
echo ""

# Test 3: Negative coordinate tile (the fix!)
echo "3️⃣ Testing tile with NEGATIVE coordinates (the fix!)..."
TILE2_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:42422/tiles/5/124_-126.png")
TILE2_SIZE=$(curl -s -o /dev/null -w "%{size_download}" "http://localhost:42422/tiles/5/124_-126.png")
echo "   URL: /tiles/5/124_-126.png"
echo "   Status: HTTP $TILE2_STATUS"
echo "   Size: $TILE2_SIZE bytes"
if [ "$TILE2_STATUS" = "200" ]; then
    echo "   ✅ Negative coordinates WORKING! Tile generated!"
elif [ "$TILE2_STATUS" = "404" ]; then
    echo "   ⚠️  Tile not found (chunk not generated yet)"
elif [ "$TILE2_STATUS" = "400" ]; then
    echo "   ❌ BAD REQUEST - Regex still doesn't match negative coords!"
else
    echo "   ❌ Unexpected status code"
fi
echo ""

# Test 4: Check server logs for tile generation
echo "4️⃣ Checking server logs for tile activity..."
LOG_COUNT=$(tail -100 test_server/VintagestoryData/Logs/server-debug.log 2>/dev/null | grep -c "Served tile" || echo "0")
if [ "$LOG_COUNT" -gt "0" ]; then
    echo "   ✅ Found $LOG_COUNT tile serve events in logs"
    echo "   Recent tile activity:"
    tail -100 test_server/VintagestoryData/Logs/server-debug.log 2>/dev/null | grep "Served tile" | tail -3 | sed 's/^/      /'
else
    echo "   ℹ️  No tile serve events found yet (check after browser request)"
fi
echo ""

# Test 5: Check if DynamicTileGenerator is initialized
echo "5️⃣ Checking if dynamic tile generation is enabled..."
LIVE_SERVER_LOG=$(tail -50 test_server/VintagestoryData/Logs/server-main.log 2>/dev/null | grep -i "dynamic tile" || echo "")
if [ -n "$LIVE_SERVER_LOG" ]; then
    echo "   ✅ Dynamic tile generation enabled:"
    echo "$LIVE_SERVER_LOG" | sed 's/^/      /'
else
    echo "   ⚠️  No dynamic tile generation message found"
fi
echo ""

echo "===================================="
echo "📊 Summary:"
echo "   - Open browser: http://localhost:42422/"
echo "   - Check browser console for tile requests"
echo "   - Look for HTTP 200 (success) or 404 (chunk not generated)"
echo "   - HTTP 400 means regex is still broken"

