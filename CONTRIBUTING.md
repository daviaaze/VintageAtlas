# Contributing to VintageAtlas

Thank you for your interest in contributing to VintageAtlas! This document provides guidelines and instructions for contributing.

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 SDK
- Vintage Story 1.20.1+
- Git
- (Optional) Nix for reproducible development environment

### Development Environment Setup

#### Option 1: Using Nix (Recommended)

```bash
# Clone the repository
git clone https://github.com/daviaaze/VintageAtlas.git
cd VintageAtlas

# Enter Nix development shell (automatically sets up everything)
nix develop

# Build the mod
cd VintageAtlas
dotnet build
```

#### Option 2: Manual Setup

```bash
# Clone the repository
git clone https://github.com/daviaaze/VintageAtlas.git
cd VintageAtlas

# Download Vintage Story
wget https://cdn.vintagestory.at/gamefiles/stable/vs_archive_1.20.1.tar.gz
tar -xzf vs_archive_1.20.1.tar.gz

# Set environment variable
export VINTAGE_STORY=$(pwd)/vintagestory

# Build the mod
cd VintageAtlas
dotnet build
```

## 📝 Code Style

### General Guidelines

- **Formatting**: Use the default C# formatting (4 spaces for indentation)
- **Naming Conventions**:
  - PascalCase for classes, methods, properties
  - camelCase for local variables, parameters
  - _camelCase for private fields
- **Documentation**: Add XML comments for public APIs
- **Null Safety**: Use nullable reference types where appropriate

### Example

```csharp
namespace VintageAtlas.Core;

/// <summary>
/// Manages server configuration and validation
/// </summary>
public class ConfigValidator
{
    private readonly ILogger _logger;
    
    public ConfigValidator(ILogger logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Validates the provided configuration
    /// </summary>
    public List<string> Validate(ModConfig config)
    {
        // Implementation
    }
}
```

## 🏗️ Architecture

VintageAtlas follows a clean architecture pattern:

```
VintageAtlas/
├── Core/              # Configuration, interfaces, validators
├── Models/            # Data transfer objects
├── Export/            # Map generation logic
├── Tracking/          # Historical data tracking
├── Web/
│   ├── Server/        # HTTP server infrastructure
│   └── API/           # REST API controllers
└── Commands/          # In-game chat commands
```

### Design Principles

1. **Separation of Concerns**: Each component has a single responsibility
2. **Dependency Injection**: Use interfaces for testability
3. **SOLID Principles**: Follow SOLID design principles
4. **Fail Fast**: Validate early and provide helpful error messages

## 🔄 Workflow

### 1. Fork and Clone

```bash
# Fork the repository on GitHub
# Then clone your fork
git clone https://github.com/daviaaze/VintageAtlas.git
cd VintageAtlas
```

### 2. Create a Branch

```bash
# Create a feature branch
git checkout -b feature/my-awesome-feature

# Or a bugfix branch
git checkout -b bugfix/fix-issue-123
```

### 3. Make Changes

- Write code following the style guidelines
- Add tests if applicable
- Update documentation
- Ensure the build succeeds

```bash
cd VintageAtlas
dotnet build --configuration Release
```

### 4. Commit Changes

```bash
# Stage your changes
git add .

# Commit with a descriptive message
git commit -m "feat: add player teleportation tracking

- Track teleportation events
- Add API endpoint for teleport history
- Update admin dashboard with teleport stats"
```

### Commit Message Format

Follow [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` New feature
- `fix:` Bug fix
- `docs:` Documentation changes
- `style:` Code style changes (formatting, etc.)
- `refactor:` Code refactoring
- `perf:` Performance improvements
- `test:` Adding or updating tests
- `chore:` Maintenance tasks

### 5. Push and Create PR

```bash
# Push to your fork
git push origin feature/my-awesome-feature

# Go to GitHub and create a Pull Request
```

## 🧪 Testing

### Building

```bash
cd VintageAtlas
dotnet build --configuration Release
```

### Manual Testing

1. Copy built mod to Vintage Story server:

   ```bash
   cp -r bin/Release/Mods/vintageatlas /path/to/VS/Mods/
   ```

2. Start the server and test your changes

3. Check logs for errors:

   ```bash
   tail -f /path/to/VS/Logs/server-main.txt
   ```

### Automated Testing

(Future: We plan to add unit tests)

## 📋 Pull Request Guidelines

### Before Submitting

- ✅ Code builds without errors or warnings
- ✅ Changes are tested manually
- ✅ Documentation is updated
- ✅ Commit messages follow conventions
- ✅ Branch is up to date with `main`

### PR Description

Include:

1. **What**: Brief description of changes
2. **Why**: Motivation and context
3. **How**: Technical details if complex
4. **Testing**: How you tested the changes
5. **Screenshots**: If UI changes

### Example PR Description

```markdown
## Add Player Teleportation Tracking

### What
Adds tracking for player teleportation events (translocators, teleport commands).

### Why
Requested in #42. Useful for server admins to monitor player movement patterns.

### How
- Added `TeleportEvent` model
- Extended `HistoricalTracker` with teleport recording
- Created `/api/teleports` endpoint
- Updated admin dashboard with teleport visualization

### Testing
- Manually tested on local server with 3 players
- Verified API returns correct data
- Checked database schema updates correctly

### Screenshots
![Teleport Dashboard](screenshots/teleport-dashboard.png)
```

## 🐛 Reporting Bugs

### Before Reporting

1. Check [existing issues](https://github.com/daviaaze/VintageAtlas/issues)
2. Update to the latest version
3. Check the [documentation](VintageAtlas/README.md)

### Bug Report Template

```markdown
**Describe the bug**
A clear description of what the bug is.

**To Reproduce**
1. Go to '...'
2. Click on '...'
3. See error

**Expected behavior**
What you expected to happen.

**Screenshots**
If applicable, add screenshots.

**Environment:**
 - OS: [e.g. Ubuntu 22.04]
 - Vintage Story Version: [e.g. 1.20.1]
 - VintageAtlas Version: [e.g. 1.0.0]

**Server Logs**
```

Paste relevant log lines here

```

**Additional context**
Any other context about the problem.
```

## 💡 Feature Requests

### Before Requesting

1. Check [existing issues](https://github.com/daviaaze/VintageAtlas/issues)
2. Consider if it fits the mod's scope
3. Think about implementation complexity

### Feature Request Template

```markdown
**Is your feature request related to a problem?**
A clear description of the problem. Ex. I'm always frustrated when [...]

**Describe the solution you'd like**
A clear description of what you want to happen.

**Describe alternatives you've considered**
Any alternative solutions or features you've considered.

**Additional context**
Any other context or screenshots about the feature request.
```

## 📚 Documentation

### Types of Documentation

1. **Code Comments**: For complex logic
2. **XML Documentation**: For public APIs
3. **README**: User-facing documentation
4. **Architecture Docs**: For major design decisions

### Where to Document

- **API Changes**: Update `VintageAtlas/README.md`
- **Configuration**: Update config examples
- **Architecture**: Update `REFACTORING-COMPLETE.md`
- **Changelog**: Update `VintageAtlas/CHANGELOG.md`

## 🎯 Areas Needing Help

### High Priority

- [ ] Unit tests for core components
- [ ] Integration tests for API endpoints
- [ ] Performance profiling and optimization
- [ ] Multi-language support

### Medium Priority

- [ ] Additional map rendering modes
- [ ] WebSocket support for real-time updates
- [ ] Player permission system
- [ ] Backup integration

### Low Priority

- [ ] Additional chart types in dashboard
- [ ] Export formats (PDF, CSV)
- [ ] Mobile app companion

## 📞 Getting Help

- **Questions**: [GitHub Discussions](https://github.com/daviaaze/VintageAtlas/discussions)
- **Issues**: [GitHub Issues](https://github.com/daviaaze/VintageAtlas/issues)
- **Discord**: Vintage Story Official Discord

## 📄 License

By contributing, you agree that your contributions will be licensed under the same license as the project (see [LICENSE](LICENSE)).

## 🙏 Thank You

Your contributions make VintageAtlas better for everyone. Thank you for taking the time to contribute!

---

*Happy coding! 🚀*
