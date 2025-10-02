#!/usr/bin/env bash
# Helper script to restart test server with latest mod

set -e

echo "🔄 Restarting Test Server with Latest Mod"
echo "=========================================="
echo ""

# Find running server
SERVER_PID=$(ps aux | grep -i vintagestoryserver | grep -v grep | awk '{print $2}' | head -1)

if [ -n "$SERVER_PID" ]; then
    echo "⏹️  Stopping current server (PID: $SERVER_PID)..."
    kill $SERVER_PID
    sleep 2
    
    # Make sure it's stopped
    if ps -p $SERVER_PID > /dev/null 2>&1; then
        echo "   Force killing..."
        kill -9 $SERVER_PID 2>/dev/null || true
    fi
    echo "   ✅ Server stopped"
else
    echo "ℹ️  No running server found"
fi

echo ""
echo "🔨 Building latest version..."
cd "$(dirname "$0")"

# Build frontend and backend
nix develop --command bash -c "cd VintageAtlas/frontend && npm run build && cd ../.. && build-vintageatlas" 2>&1 | tail -10

# Find latest mod
LATEST_MOD=$(ls -t VintageAtlas/VintageAtlas-v*.zip 2>/dev/null | head -1)

if [ -n "$LATEST_MOD" ]; then
    echo ""
    echo "📦 Latest mod: $(basename $LATEST_MOD)"
    echo "   Copying to test server..."
    cp "$LATEST_MOD" test_server/VintagestoryData/Mods/
    echo "   ✅ Mod copied"
fi

echo ""
echo "🚀 Starting server (run in your terminal):"
echo ""
echo "   cd $(pwd)"
echo "   nix develop --command test-server"
echo ""
echo "⚠️  IMPORTANT: Must run from project root!"
echo ""
echo "=========================================="
echo "📝 After server starts:"
echo "   1. Open: http://localhost:42422/"
echo "   2. Check browser console for tile requests"
echo "   3. Run: ./test-tiles.sh to verify tile system"

