# VintageAtlas Quick Start Guide

**5-minute guide to get you started with VintageAtlas development**

---

## Prerequisites

- NixOS (or Nix package manager)
- Text editor / IDE (VS Code recommended)

---

## Development Setup

### 1. Clone and Enter Dev Shell

```bash
cd /path/to/VintageAtlas
nix develop  # Activates development environment
```

This provides: .NET SDK, Node.js, and all build tools.

### 2. Quick Build & Test

```bash
# Build and start test server in one command
quick-test

# Or manually:
dotnet build --configuration Release
cd test_server && ./start.sh
```

### 3. Access the Map

Open browser: `http://localhost:42422`

---

## Common Commands

### Backend (C# Mod)

```bash
# Quick build and test
quick-test

# Build release version (includes frontend)
build-vintageatlas

# Build without frontend
dotnet build --configuration Release
```

### Frontend (Vue.js - Future)

```bash
cd VintageAtlas/frontend
npm install
npm run dev      # Development server
npm run build    # Production build
```

### In-Game Commands

```bash
/atlas export              # Full map export
/atlas export incremental  # Update changed tiles only
/atlas status              # Show mod status
/atlas config              # Show configuration
```

---

## Project Structure

```
VintageAtlas/
├── Core/              # Configuration, interfaces
├── Export/            # Map tile generation
├── Tracking/          # World change tracking
├── Web/               # HTTP server & API
├── Commands/          # Chat commands
├── frontend/          # Vue.js frontend (future)
├── html/              # Static web assets
└── docs/              # Documentation
```

---

## Key Files

| File | Purpose |
|------|---------|
| `VintageAtlasModSystem.cs` | Main mod entry point |
| `modinfo.json` | Mod metadata |
| `Directory.Build.props` | Build configuration |
| `flake.nix` | Nix development environment |

---

## Development Workflow

### Making Backend Changes

1. Edit C# files in `VintageAtlas/`
2. Run `quick-test` to rebuild and restart server
3. Test in-game or via web browser
4. Check logs: `test_server/Logs/server-main.log`

### Making Frontend Changes (Current - Static)

1. Edit files in `VintageAtlas/html/`
2. Refresh browser (no rebuild needed)

### Making Frontend Changes (Future - Vue.js)

1. Edit files in `VintageAtlas/frontend/src/`
2. Hot reload happens automatically
3. Run `npm run build` when ready to test with mod

---

## Testing

### Manual Testing

```bash
# Start test server
quick-test

# In another terminal
curl http://localhost:42422/api/status
curl http://localhost:42422/api/map-config
```

### Unit Testing (Future)

```bash
cd VintageAtlas.Tests
dotnet test
```

---

## Important Constraints

⚠️ **Critical:** Read [Vintage Story Modding Constraints](guides/vintagestory-modding-constraints.md)

**Key rules:**

- ✅ **Main thread only** for chunk/world access
- ✅ Use `EnqueueMainThreadTask()` for game state
- ❌ **Never block** the main thread
- ❌ **Never cache** chunk references (prevents unloading)

---

## Documentation Guide

### For Development

- [Architecture Overview](architecture/architecture-overview.md) - System design
- [API Integration](architecture/api-integration.md) - VS API patterns
- [Modding Constraints](guides/vintagestory-modding-constraints.md) - **Read this first!**

### For Frontend Work

- [Frontend Plan](planning/FRONTEND-PLAN.md) - Modernization roadmap

### For Active Work

- [Dynamic Tile Consolidation](implementation/dynamic-tile-consolidation.md) - Current implementation

---

## Troubleshooting

### Build fails

```bash
# Check VINTAGE_STORY environment variable
echo $VINTAGE_STORY

# Should point to VS installation, e.g.:
# /home/user/.local/share/Steam/steamapps/common/VintageStory
```

### Server won't start

```bash
# Check if port is in use
lsof -i :42420  # Game server
lsof -i :42422  # Web server
```

### Map tiles not generating

```bash
# Check output directory permissions
ls -la test_server/ModData/VintageAtlas/

# Check logs
tail -f test_server/Logs/server-main.log
```

### Need a missing tool

```bash
# Use nix run for one-off commands
nix run nixpkgs#jq
nix run nixpkgs#curl
```

---

## Getting Help

1. **Check logs:** `test_server/Logs/server-main.log`
2. **Review docs:** Start with [docs/README.md](README.md)
3. **Search issues:** Check GitHub issues for similar problems
4. **Ask for help:** Open a new issue with logs and steps to reproduce

---

## Next Steps

### For Backend Development

1. Read [Modding Constraints](guides/vintagestory-modding-constraints.md) ⚠️ **Required**
2. Review [Architecture Overview](architecture/architecture-overview.md)
3. Check [API Integration Guide](architecture/api-integration.md)
4. Look at [Testing Guide](guides/testing-guide.md)

### For Frontend Development

1. Review [Frontend Modernization Plan](planning/FRONTEND-PLAN.md)
2. Check current implementation in `html/`
3. Plan Vue.js migration

### For Contributing

1. Read `CONTRIBUTING.md` (if exists)
2. Check open issues and PRs
3. Follow code style in existing files

---

**Ready to contribute?** Read the specific documentation for your area of interest!

**Questions?** Start with [docs/README.md](README.md) for the full documentation index.
