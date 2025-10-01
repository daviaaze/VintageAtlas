# WebCartographer - Nix Development Guide

## 🚀 Quick Start with Nix Flakes

This project includes a Nix flake for a reproducible development environment.

### Prerequisites

- Nix with flakes enabled
- Vintage Story installed (Steam or standalone)

### Enable Nix Flakes

If you haven't enabled flakes yet:

```bash
# Add to ~/.config/nix/nix.conf
experimental-features = nix-command flakes
```

Or run with:
```bash
nix --extra-experimental-features "nix-command flakes" develop
```

---

## 🛠️ Using the Development Shell

### Enter the Development Environment

```bash
cd /path/to/WebCartographer
nix develop
```

This automatically sets up:
- ✅ .NET 7.0 SDK and runtime
- ✅ MSBuild
- ✅ OmniSharp (C# LSP for your editor)
- ✅ Vintage Story package reference
- ✅ All build tools and utilities
- ✅ Custom build scripts

### Show Available Commands

Once in the nix shell:

```bash
setup-webcartographer
```

Output:
```
╔════════════════════════════════════════════════════════════════╗
║   WebCartographer - Development Environment                   ║
╚════════════════════════════════════════════════════════════════╝

📂 VINTAGE_STORY: /nix/store/.../share/vintagestory
✅ Directory found
   ✓ VintagestoryAPI.dll
   ✓ VintagestoryLib.dll
✅ All required DLLs found

📋 Available commands:
  build-webcartographer       - Build the mod (Release)
  build-webcartographer-debug - Build the mod (Debug)
  test-webcartographer        - Run tests
  install-webcartographer     - Install to VintagestoryData/Mods
  dotnet build                - Build manually
  dotnet test                 - Run tests manually

📖 Documentation:
  QUICK-START.md              - Quick start guide
  UNIFIED-MOD-GUIDE.md        - Complete documentation
  INTEGRATION-COMPLETE.md     - Technical details
```

---

## 📋 Custom Build Scripts

### Build Release Version

```bash
build-webcartographer
```

Output:
```
🔨 Building WebCartographer (Release)...
✅ Build successful!
📦 Output: WebCartographer/bin/Release/Mods/mod/
📁 To install, copy to: ~/.config/VintagestoryData/Mods/
Or run: install-webcartographer
```

### Build Debug Version

```bash
build-webcartographer-debug
```

### Run Tests

```bash
test-webcartographer
```

### Install to Vintage Story

Automatically installs the built mod to your VintagestoryData folder:

```bash
install-webcartographer
```

Output:
```
📦 Installing WebCartographer...
📋 Copying files to ~/.config/VintagestoryData/Mods/WebCartographer...
✅ Installation complete!

🚀 Start Vintage Story to use the mod!

📖 Quick Start:
  1. Client: /exportcolors
  2. Server: /webc export
  3. Browser: http://localhost:42421/
```

---

## 🔧 Manual Build Commands

You can also use dotnet directly:

```bash
# Build release
dotnet build WebCartographer/WebCartographer.csproj --configuration Release

# Build debug
dotnet build WebCartographer/WebCartographer.csproj --configuration Debug

# Run tests
dotnet test WebCartographer.Tests/WebCartographer.Tests.csproj

# Clean
dotnet clean
```

---

## 🌍 Environment Variables

The flake automatically sets:

- `VINTAGE_STORY` - Points to Vintage Story installation in Nix store
- `DOTNET_CLI_TELEMETRY_OPTOUT=1` - Disable telemetry
- `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1` - Skip first-time setup

### Using Your Own Vintage Story Installation

If you want to use your Steam/local Vintage Story instead:

```bash
# Set before entering nix shell
export VINTAGE_STORY="/home/user/.local/share/Steam/steamapps/common/VintageStory"

# Or use direnv
echo 'export VINTAGE_STORY="/path/to/vintagestory"' > .envrc
direnv allow

# Then enter nix shell
nix develop
```

---

## 📦 Packages

You can run individual scripts without entering the shell:

```bash
# Show setup info
nix run

# Or explicitly
nix run .#setupWebCartographer

# Build without entering shell
nix run .#buildWebCartographer

# Install without entering shell
nix run .#installWebCartographer
```

---

## 🔄 Using with direnv

For automatic environment activation:

1. **Install direnv:**
   ```bash
   # NixOS
   nix-env -iA nixpkgs.direnv
   
   # Or add to configuration.nix
   environment.systemPackages = [ pkgs.direnv ];
   ```

2. **Hook direnv into your shell:**
   ```bash
   # For bash
   echo 'eval "$(direnv hook bash)"' >> ~/.bashrc
   
   # For zsh
   echo 'eval "$(direnv hook zsh)"' >> ~/.zshrc
   
   # For fish
   echo 'direnv hook fish | source' >> ~/.config/fish/config.fish
   ```

3. **Create `.envrc` in project root:**
   ```bash
   use flake
   ```

4. **Allow direnv:**
   ```bash
   direnv allow
   ```

Now the environment activates automatically when you `cd` into the directory!

---

## 🎯 Workflow Examples

### First Time Setup

```bash
# Enter development shell
nix develop

# Check environment
setup-webcartographer

# Build the mod
build-webcartographer

# Install to Vintage Story
install-webcartographer
```

### Daily Development

```bash
# With direnv, just cd to the project
cd ~/Projects/WebCartographer

# Make your changes...
# Then rebuild
build-webcartographer

# Test
test-webcartographer

# Install
install-webcartographer
```

### Quick Build and Install

```bash
# Single command (outside nix shell)
nix develop --command sh -c "build-webcartographer && install-webcartographer"
```

---

## 🏗️ What's in the Nix Environment?

### Development Tools
- **dotnet-sdk_7** - .NET 7.0 SDK (matches project requirement)
- **dotnet-runtime_7** - .NET 7.0 runtime
- **msbuild** - MSBuild for project building
- **omnisharp-roslyn** - C# language server for IDE integration

### Game Dependencies
- **vintagestory** - Vintage Story package from nixpkgs
- Provides DLLs: VintagestoryAPI.dll, VintagestoryLib.dll, etc.

### Utilities
- **git** - Version control
- **curl** / **wget** - Download utilities
- **unzip** - Archive extraction
- **nixpkgs-fmt** - Nix code formatter

### Custom Scripts
- **setup-webcartographer** - Environment info
- **build-webcartographer** - Build release
- **build-webcartographer-debug** - Build debug
- **test-webcartographer** - Run tests
- **install-webcartographer** - Install mod

---

## 🐛 Troubleshooting

### "VINTAGE_STORY not set" error

The flake automatically uses nixpkgs Vintage Story. If you need a different version:

```bash
export VINTAGE_STORY="/path/to/your/vintagestory"
nix develop
```

### "DLL not found" errors when building

Make sure VINTAGE_STORY points to a directory containing:
- VintagestoryAPI.dll
- VintagestoryLib.dll
- Mods/VSSurvivalMod.dll
- Lib/*.dll

### Build fails with "unable to find net7.0"

The flake uses .NET 7.0 SDK. If you see this error:
1. Exit the nix shell
2. Run `nix flake update` to update nixpkgs
3. Re-enter with `nix develop`

### OmniSharp doesn't work in editor

Make sure your editor is started from within the nix shell:

```bash
nix develop
code .  # or vim, emacs, etc.
```

Or use direnv for automatic environment activation.

---

## 🔄 Updating the Flake

### Update all inputs

```bash
nix flake update
```

### Update specific input

```bash
nix flake lock --update-input nixpkgs
```

### Check flake

```bash
nix flake check
```

### Show flake info

```bash
nix flake show
```

---

## 📖 Flake Structure

```nix
{
  description = "WebCartographer - Vintage Story Map Generator with Live Server";
  
  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixpkgs-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };
  
  outputs = {
    devShells.default = {
      # .NET 7.0 SDK
      # Vintage Story
      # Custom build scripts
    };
    
    packages = {
      # Individual scripts
    };
  };
}
```

---

## 🎓 Learning Resources

### Nix Flakes
- [Nix Flakes Wiki](https://nixos.wiki/wiki/Flakes)
- [Practical Nix Flakes](https://serokell.io/blog/practical-nix-flakes)

### Direnv
- [direnv documentation](https://direnv.net/)
- [Using direnv with Nix](https://nixos.wiki/wiki/Development_environment_with_nix-shell)

### .NET on Nix
- [.NET on NixOS](https://nixos.wiki/wiki/.NET)

---

## 🎉 Benefits of Using Nix

✅ **Reproducible** - Same environment on any machine  
✅ **Isolated** - Doesn't interfere with system packages  
✅ **Declarative** - Environment defined in code  
✅ **Cacheable** - Binary cache for fast setup  
✅ **Version-controlled** - Flake lock ensures consistency  
✅ **Cross-platform** - Works on Linux, macOS, WSL  

---

## 📝 Example Sessions

### Session 1: First Build

```bash
$ nix develop
🎉 Welcome to WebCartographer development environment

$ setup-webcartographer
╔════════════════════════════════════════════════════════════════╗
║   WebCartographer - Development Environment                   ║
╚════════════════════════════════════════════════════════════════╝
✅ All required DLLs found

$ build-webcartographer
🔨 Building WebCartographer (Release)...
✅ Build successful!

$ install-webcartographer
📦 Installing WebCartographer...
✅ Installation complete!
```

### Session 2: Quick Rebuild

```bash
$ nix develop -c build-webcartographer
🔨 Building WebCartographer (Release)...
✅ Build successful!
```

### Session 3: Test and Install

```bash
$ nix develop

$ test-webcartographer
🧪 Running WebCartographer tests...
✅ All tests passed!

$ install-webcartographer
✅ Installation complete!
```

---

## 🔐 Security Note

The flake uses `allowUnfree = true` to support any potentially unfree dependencies. If you prefer to only use free software, set this to `false` in `flake.nix`.

---

## 💡 Tips

1. **Use direnv** for automatic environment activation
2. **Run scripts without shell:** `nix run .#buildWebCartographer`
3. **Update regularly:** `nix flake update` monthly
4. **Commit flake.lock** to version control for reproducibility
5. **Use nix-direnv** for better caching with direnv

---

**Happy modding with Nix! 🎮✨**

