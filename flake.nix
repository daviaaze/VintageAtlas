{
  description = "VintageAtlas - A comprehensive mapping and server monitoring solution for Vintage Story";

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

        # Custom script to set up VintageAtlas development environment
        setupVintageAtlas = pkgs.writeScriptBin "setup-vintageatlas" ''
          #!${pkgs.bash}/bin/bash
          echo "╔════════════════════════════════════════════════════════════════╗"
          echo "║   VintageAtlas - Development Environment                      ║"
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
              all_found=true
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
          echo "  build-vintageatlas       - Build the mod (Release)"
          echo "  build-vintageatlas-debug - Build the mod (Debug)"
          echo "  install-vintageatlas     - Install to VintagestoryData/Mods"
          echo "  dotnet build             - Build manually"
          echo ""
          echo "📖 Documentation:"
          echo "  README.md                - Repository overview"
          echo "  VintageAtlas/README.md   - User guide"
          echo "  CONTRIBUTING.md          - Developer guide"
          echo "  PUSH-TO-GITHUB.md        - GitHub setup"
          echo ""
        '';

        # Build script for VintageAtlas (Release)
        buildVintageAtlas = pkgs.writeScriptBin "build-vintageatlas" ''
          #!${pkgs.bash}/bin/bash
          set -e

          echo "🔨 Building VintageAtlas (Release)..."

          if [ -z "$VINTAGE_STORY" ]; then
            echo "❌ Error: VINTAGE_STORY environment variable not set!"
            echo "Set it to your Vintage Story installation directory."
            exit 1
          fi

          # Build the main project
          dotnet build VintageAtlas/VintageAtlas.csproj --configuration Release

          if [ $? -eq 0 ]; then
            echo ""
            echo "✅ Build successful!"
            echo ""
            echo "📦 Output: VintageAtlas/bin/Release/Mods/vintageatlas/"
            echo ""
            echo "📋 Built files:"
            ls -lh VintageAtlas/bin/Release/Mods/vintageatlas/ | grep -E '\.(dll|json)$' || true
            echo ""
            
            # Create zip package
            echo "📦 Creating zip package..."
            VERSION=$(date +%Y%m%d-%H%M%S)
            ZIP_NAME="VintageAtlas-v$VERSION.zip"
            
            cd VintageAtlas/bin/Release/Mods/vintageatlas/
            ${pkgs.zip}/bin/zip -r "../../../../$ZIP_NAME" *
            cd ../../../../
            
            if [ -f "$ZIP_NAME" ]; then
              echo ""
              echo "✅ Package created successfully!"
              echo "📦 $ZIP_NAME ($(du -h "$ZIP_NAME" | cut -f1))"
              echo ""
            fi
            
            echo "📁 To install manually, copy to:"
            echo "  ~/.config/VintagestoryData/Mods/"
            echo ""
            echo "Or run: install-vintageatlas"
          else
            echo "❌ Build failed!"
            exit 1
          fi
        '';

        # Build script for VintageAtlas (Debug)
        buildVintageAtlasDebug = pkgs.writeScriptBin "build-vintageatlas-debug" ''
          #!${pkgs.bash}/bin/bash
          set -e

          echo "🔨 Building VintageAtlas (Debug)..."

          if [ -z "$VINTAGE_STORY" ]; then
            echo "❌ Error: VINTAGE_STORY environment variable not set!"
            exit 1
          fi

          dotnet build VintageAtlas/VintageAtlas.csproj --configuration Debug

          if [ $? -eq 0 ]; then
            echo "✅ Build successful (Debug)!"
            echo "📦 Output: VintageAtlas/bin/Debug/Mods/vintageatlas/"
          else
            echo "❌ Build failed!"
            exit 1
          fi
        '';

        # Package script
        packageVintageAtlas = pkgs.writeScriptBin "package-vintageatlas" ''
          #!${pkgs.bash}/bin/bash
          set -e

          echo "📦 Packaging VintageAtlas..."

          if [ ! -d "VintageAtlas/bin/Release/Mods/vintageatlas" ]; then
            echo "❌ Build output not found!"
            echo "Run 'build-vintageatlas' first."
            exit 1
          fi

          cd VintageAtlas/bin/Release/Mods
          tar -czf ../../../../VintageAtlas-$(date +%Y%m%d).tar.gz vintageatlas/
          cd ../../../../

          echo "✅ Package created!"
          ls -lh VintageAtlas-*.tar.gz | tail -1
        '';

        # Install script
        installVintageAtlas = pkgs.writeScriptBin "install-vintageatlas" ''
          #!${pkgs.bash}/bin/bash
          set -e

          MODS_DIR="$HOME/.config/VintagestoryData/Mods"
          SOURCE_DIR="VintageAtlas/bin/Release/Mods/vintageatlas"
          TARGET_DIR="$MODS_DIR/vintageatlas"

          echo "📦 Installing VintageAtlas..."

          if [ ! -d "$SOURCE_DIR" ]; then
            echo "❌ Build output not found!"
            echo "Run 'build-vintageatlas' first."
            exit 1
          fi

          # Create mods directory if it doesn't exist
          mkdir -p "$MODS_DIR"

          # Remove old installation (both old and new names)
          for old_dir in "$MODS_DIR/WebCartographer" "$MODS_DIR/webcartographer" "$TARGET_DIR"; do
            if [ -d "$old_dir" ]; then
              echo "🗑️  Removing old installation: $old_dir"
              rm -rf "$old_dir"
            fi
          done

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
          echo "  1. Server: /atlas export  (or /va export)"
          echo "  2. Browser: http://localhost:<port>/"
          echo "  3. Admin: http://localhost:<port>/adminDashboard.html"
          echo ""
          echo "💡 Tip: Port defaults to game_port + 1"
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
            zip

            # Code Quality Tools
            nixpkgs-fmt # For formatting nix files

            # Custom Scripts
            setupVintageAtlas
            buildVintageAtlas
            buildVintageAtlasDebug
            packageVintageAtlas
            installVintageAtlas
          ];

          # Environment variables that persist in the shell
          DOTNET_CLI_TELEMETRY_OPTOUT = "1";
          DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1";
        };

        # Optional: Add packages that can be built/run directly
        packages = {
          inherit 
            setupVintageAtlas 
            buildVintageAtlas 
            buildVintageAtlasDebug
            packageVintageAtlas
            installVintageAtlas;
          
          # Default package - show setup info when running nix run
          default = setupVintageAtlas;
        };
      }
    );
}
