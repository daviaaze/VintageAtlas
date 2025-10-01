# VintageAtlas

**A comprehensive mapping and server monitoring solution for Vintage Story**

[![Build Status](https://github.com/daviaaze/VintageAtlas/workflows/Build%20VintageAtlas/badge.svg)](https://github.com/daviaaze/VintageAtlas/actions)
[![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)](https://github.com/daviaaze/VintageAtlas/releases)
[![Vintage Story](https://img.shields.io/badge/Vintage%20Story-1.20.1+-green.svg)](https://www.vintagestory.at)

> ⚠️ **Note**: This is a refactored and production-ready version of the original WebCartographer by Th3Dilli

## 🚀 Quick Start

### For Players & Server Admins

1. Download the latest release from [Releases](https://github.com/daviaaze/VintageAtlas/releases)
2. Extract to your server's `Mods/` directory
3. Start the server and access the web UI at `http://localhost:<port>/`

**Full documentation**: See [VintageAtlas/README.md](VintageAtlas/README.md)

## 🛠️ For Developers

### Building from Source

**With Nix (Recommended):**

```bash
nix develop
cd VintageAtlas
dotnet build --configuration Release
```

**Without Nix:**

```bash
# Set VINTAGE_STORY environment variable
export VINTAGE_STORY=/path/to/vintagestory

cd VintageAtlas
dotnet build --configuration Release
```

### CI/CD

This project uses GitHub Actions for automated building and releases:

- **Build**: Runs on every push and PR to `main`, `master`, or `develop`
- **Release**: Triggered by version tags (e.g., `v1.0.0`)

#### Creating a Release

```bash
# Tag the version
git tag v1.0.1
git push origin v1.0.1

# GitHub Actions will automatically:
# 1. Build the mod
# 2. Package it as .tar.gz
# 3. Create a GitHub release
# 4. Attach the package to the release
```

## 📁 Repository Structure

```
.
├── VintageAtlas/              # Main mod source code
│   ├── Core/                  # Configuration & interfaces
│   ├── Models/                # Data models
│   ├── Export/                # Map generation
│   ├── Tracking/              # Historical data
│   ├── Web/                   # Web server & API
│   ├── Commands/              # Chat commands
│   └── html/                  # Web UI
│
├── .github/
│   └── workflows/
│       ├── build.yml          # CI build workflow
│       └── release.yml        # Release automation
│
├── archive/                   # Archived original code
├── assets/                    # Logos and images
├── docs/                      # Documentation
├── flake.nix                  # Nix development environment
├── .gitignore
└── README.md                  # This file
```

## 🌟 Features

- **Dynamic Map Generation**: Multiple rendering modes with zoom levels
- **Live Web Server**: Real-time player and entity tracking
- **Historical Tracking**: Heatmaps, paths, and server statistics
- **RESTful API**: Full API for integrations
- **Production Ready**: Request throttling, caching, CORS support

For detailed features, see [VintageAtlas/README.md](VintageAtlas/README.md)

## 📖 Documentation

- **User Guide**: [VintageAtlas/README.md](VintageAtlas/README.md)
- **Changelog**: [VintageAtlas/CHANGELOG.md](VintageAtlas/CHANGELOG.md)
- **Refactoring Notes**: [REFACTORING-COMPLETE.md](REFACTORING-COMPLETE.md)
- **Quick Start**: [QUICK-START.md](QUICK-START.md)
- **Deployment**: [DEPLOYMENT-GUIDE.md](DEPLOYMENT-GUIDE.md)

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Setup

```bash
# Clone the repository
git clone https://github.com/daviaaze/VintageAtlas.git
cd VintageAtlas

# Enter Nix development environment (recommended)
nix develop

# Or set up manually
export VINTAGE_STORY=/path/to/vintagestory

# Build and test
cd VintageAtlas
dotnet build
```

## 📊 Project Status

- ✅ **Production Ready**: v1.0.0 released
- ✅ **CI/CD**: Automated builds and releases
- ✅ **Documentation**: Complete and up-to-date
- ✅ **Testing**: Successfully tested on multiple servers

## 🙏 Credits

- **Original WebCartographer**: [Th3Dilli](https://gitlab.com/th3dilli_vintagestory/WebCartographer)
- **Webmap Base**: [vs-webmap by Drakker](https://bitbucket.org/vs-webmap/aurafury-webmap/)
- **Refactored Architecture**: daviaaze

## 📄 License

See [LICENSE](LICENSE) file for details.

## 🔗 Links

- **GitHub**: <https://github.com/daviaaze/VintageAtlas>
- **Vintage Story Mods**: <https://mods.vintagestory.at>
- **Issues**: <https://github.com/daviaaze/VintageAtlas/issues>
- **Discussions**: <https://github.com/daviaaze/VintageAtlas/discussions>

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/daviaaze/VintageAtlas/issues)
- **Discussions**: [GitHub Discussions](https://github.com/daviaaze/VintageAtlas/discussions)
- **Discord**: Vintage Story Official Discord

---

**Made with ❤️ for the Vintage Story community**
