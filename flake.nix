{
  description = "WebCartographer - Vintage Story Map Generator with Live Server";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixpkgs-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs =
    {
      self,
      nixpkgs,
      flake-utils,
    }:
    flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = import nixpkgs {
          inherit system;
          config = {
            allowUnfree = true; # In case any tools need unfree packages
          };
        };

        vintageStory = pkgs.vintagestory;

        # Custom script to set up WebCartographer development environment
        setupWebCartographer = pkgs.writeScriptBin "setup-webcartographer" ''
          #!${pkgs.bash}/bin/bash
          echo "╔════════════════════════════════════════════════════════════════╗"
          echo "║   WebCartographer - Development Environment                   ║"
          echo "╚════════════════════════════════════════════════════════════════╝"
          echo ""

          # Check if VINTAGE_STORY is set
          if [ -z "$VINTAGE_STORY" ]; then
            echo "⚠️  VINTAGE_STORY environment variable not set!"
            echo ""
            echo "Please set it to your Vintage Story installation path:"
            echo "  export VINTAGE_STORY=\"/home/user/.local/share/Steam/steamapps/common/VintageStory\""
            echo ""
            echo "Or add it to your .envrc file:"
            echo "  echo 'export VINTAGE_STORY=\"/path/to/vintagestory\"' >> .envrc"
            echo "  direnv allow"
            echo ""
          else
            echo "📂 VINTAGE_STORY: $VINTAGE_STORY"
            if [ ! -d "$VINTAGE_STORY" ]; then
              echo "❌ Directory does not exist!"
            else
              echo "✅ Directory found"
              
              # Check for required DLLs
              local all_found=true
              for dll in VintagestoryAPI.dll VintagestoryLib.dll; do
                if [ -f "$VINTAGE_STORY/$dll" ]; then
                  echo "   ✓ $dll"
                else
                  echo "   ✗ $dll (missing)"
                  all_found=false
                fi
              done
              
              if [ "$all_found" = true ]; then
                echo "✅ All required DLLs found"
              else
                echo "⚠️  Some DLLs are missing"
              fi
            fi
          fi

          echo ""
          echo "📋 Available commands:"
          echo "  build-webcartographer       - Build the mod (Release)"
          echo "  build-webcartographer-debug - Build the mod (Debug)"
          echo "  test-webcartographer        - Run tests"
          echo "  install-webcartographer     - Install to VintagestoryData/Mods"
          echo "  dotnet build                - Build manually"
          echo "  dotnet test                 - Run tests manually"
          echo ""
          echo "📖 Documentation:"
          echo "  QUICK-START.md              - Quick start guide"
          echo "  UNIFIED-MOD-GUIDE.md        - Complete documentation"
          echo "  INTEGRATION-COMPLETE.md     - Technical details"
          echo ""
        '';

        # Build script for WebCartographer (Release)
        buildWebCartographer = pkgs.writeScriptBin "build-webcartographer" ''
          #!${pkgs.bash}/bin/bash
          set -e

          echo "🔨 Building WebCartographer (Release)..."

          if [ -z "$VINTAGE_STORY" ]; then
            echo "❌ Error: VINTAGE_STORY environment variable not set!"
            echo "Set it to your Vintage Story installation directory."
            exit 1
          fi

          # Build the main project
          dotnet build WebCartographer/WebCartographer.csproj --configuration Release

          if [ $? -eq 0 ]; then
            echo ""
            echo "✅ Build successful!"
            echo ""
            echo "📦 Output: WebCartographer/bin/Release/Mods/mod/"
            echo ""
            echo "📋 Built files:"
            ls -lh WebCartographer/bin/Release/Mods/mod/ | grep -E '\.(dll|json)$' || true
            echo ""
            echo "📁 To install, copy to:"
            echo "  ~/.config/VintagestoryData/Mods/"
            echo ""
            echo "Or run: install-webcartographer"
          else
            echo "❌ Build failed!"
            exit 1
          fi
        '';

        # Build script for WebCartographer (Debug)
        buildWebCartographerDebug = pkgs.writeScriptBin "build-webcartographer-debug" ''
          #!${pkgs.bash}/bin/bash
          set -e

          echo "🔨 Building WebCartographer (Debug)..."

          if [ -z "$VINTAGE_STORY" ]; then
            echo "❌ Error: VINTAGE_STORY environment variable not set!"
            exit 1
          fi

          dotnet build WebCartographer/WebCartographer.csproj --configuration Debug

          if [ $? -eq 0 ]; then
            echo "✅ Build successful (Debug)!"
            echo "📦 Output: WebCartographer/bin/Debug/Mods/mod/"
          else
            echo "❌ Build failed!"
            exit 1
          fi
        '';

        # Test script
        testWebCartographer = pkgs.writeScriptBin "test-webcartographer" ''
          #!${pkgs.bash}/bin/bash
          set -e

          echo "🧪 Running WebCartographer tests..."

          if [ -z "$VINTAGE_STORY" ]; then
            echo "❌ Error: VINTAGE_STORY environment variable not set!"
            exit 1
          fi

          dotnet test WebCartographer.Tests/WebCartographer.Tests.csproj

          if [ $? -eq 0 ]; then
            echo "✅ All tests passed!"
          else
            echo "❌ Tests failed!"
            exit 1
          fi
        '';

        # Install script
        installWebCartographer = pkgs.writeScriptBin "install-webcartographer" ''
          #!${pkgs.bash}/bin/bash
          set -e

          MODS_DIR="$HOME/.config/VintagestoryData/Mods"
          SOURCE_DIR="WebCartographer/bin/Release/Mods/mod"
          TARGET_DIR="$MODS_DIR/WebCartographer"

          echo "📦 Installing WebCartographer..."

          if [ ! -d "$SOURCE_DIR" ]; then
            echo "❌ Build output not found!"
            echo "Run 'build-webcartographer' first."
            exit 1
          fi

          # Create mods directory if it doesn't exist
          mkdir -p "$MODS_DIR"

          # Remove old installation
          if [ -d "$TARGET_DIR" ]; then
            echo "🗑️  Removing old installation..."
            rm -rf "$TARGET_DIR"
          fi

          # Copy new build
          echo "📋 Copying files to $TARGET_DIR..."
          cp -r "$SOURCE_DIR" "$TARGET_DIR"

          echo ""
          echo "✅ Installation complete!"
          echo ""
          echo "📁 Installed to: $TARGET_DIR"
          echo ""
          echo "🎮 Files installed:"
          ls -lh "$TARGET_DIR" | grep -E '\.(dll|json)$' || true
          echo ""
          echo "🚀 Start Vintage Story to use the mod!"
          echo ""
          echo "📖 Quick Start:"
          echo "  1. Client: /exportcolors"
          echo "  2. Server: /webc export"
          echo "  3. Browser: http://localhost:42421/"
          echo ""
        '';

      in
      {
        devShells.default = pkgs.mkShell {
          env = {
            VINTAGE_STORY = "${vintageStory}/share/vintagestory";
          };
          buildInputs = with pkgs; [
            # .NET Development - .NET 8.0 for WebCartographer (matches Vintage Story 1.21.1)
            dotnet-sdk_8
            dotnet-runtime_8
            msbuild
            vintageStory
            icu
            
            # Development Tools
            omnisharp-roslyn # C# language server for LSP

            # General Development Utilities
            git
            curl
            wget
            unzip

            # Code Quality Tools
            nixpkgs-fmt # For formatting nix files

            # Custom Scripts
            setupWebCartographer
            buildWebCartographer
            buildWebCartographerDebug
            testWebCartographer
            installWebCartographer
          ];

          # Environment variables that persist in the shell
          DOTNET_CLI_TELEMETRY_OPTOUT = "1";
          DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1";
        };

        # Optional: Add packages that can be built/run directly
        packages = {
          inherit 
            setupWebCartographer 
            buildWebCartographer 
            buildWebCartographerDebug
            testWebCartographer
            installWebCartographer;
        };

        # Default package - show setup info when running nix run
        defaultPackage = setupWebCartographer;
      }
    );
}
