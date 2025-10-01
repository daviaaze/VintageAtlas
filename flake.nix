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
          echo "📋 Build commands:"
          echo "  build-vintageatlas       - Build the mod (Release)"
          echo "  build-vintageatlas-debug - Build the mod (Debug)"
          echo "  install-vintageatlas     - Install to VintagestoryData/Mods"
          echo ""
          echo "🧪 Test commands:"
          echo "  quick-test               - Build and start test server"
          echo "  test-server              - Start server with test data"
          echo "  test-client              - Launch client to connect"
          echo "  test-complete            - Full test environment + browser"
          echo ""
          echo "💡 Testing workflow:"
          echo "  Terminal 1: quick-test      (or test-server)"
          echo "  Terminal 2: test-client"
          echo "  In game:    /atlas export"
          echo "  Browser:    http://localhost:42422/"
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

          # Build the Vue.js frontend first
          if [ -d "VintageAtlas/frontend" ]; then
            echo ""
            echo "🎨 Building Vue.js frontend..."
            cd VintageAtlas/frontend
            
            # Check if node_modules exists
            if [ ! -d "node_modules" ]; then
              echo "📦 Installing frontend dependencies..."
              ${pkgs.nodejs}/bin/npm install
            fi
            
            echo "📦 Building frontend..."
            ${pkgs.nodejs}/bin/npm run build
            
            if [ $? -eq 0 ]; then
              echo "✅ Frontend build successful!"
            else
              echo "❌ Frontend build failed!"
              exit 1
            fi
            
            cd ../..
            echo ""
          fi

          # Build the main project
          echo "🔨 Building C# mod..."
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

        # Test server script - launches a Vintage Story server with test data
        testServer = pkgs.writeScriptBin "test-server" ''
          #!${pkgs.bash}/bin/bash
          set -e

          # Use absolute paths to avoid issues
          TEST_DATA="$(realpath "$(pwd)/test_server_2")"
          MOD_PATH="$(realpath "$(pwd)/VintageAtlas/bin/Release/Mods")"
          PORT="''${VS_TEST_PORT:-42421}"  # Default test port

          echo "🧪 Starting VintageAtlas Test Server..."
          echo ""

          # Check if test_server directory exists
          if [ ! -d "$TEST_DATA" ]; then
            echo "⚠️  Warning: test_server directory not found at: $TEST_DATA"
            echo "Creating test server data directory..."
            mkdir -p "$TEST_DATA"
          fi

          # Check if mod is built
          if [ ! -d "$MOD_PATH/vintageatlas" ]; then
            echo "❌ VintageAtlas mod not found!"
            echo "Please run 'build-vintageatlas' first."
            exit 1
          fi

          echo "📂 Test Data: $TEST_DATA"
          echo "🔌 Server Port: $PORT"
          echo "📦 Mod Path: $MOD_PATH"
          echo ""
          echo "🌐 Web Interface will be available at:"
          echo "   http://localhost:$((PORT + 1))/"
          echo ""
          echo "📋 Server Commands:"
          echo "   /atlas export  - Generate map"
          echo "   /atlas status  - Check mod status"
          echo "   /help atlas    - Show all commands"
          echo ""
          echo "Press Ctrl+C to stop the server"
          echo "─────────────────────────────────────────────────────"
          echo ""

          # Ensure Saves directory has proper permissions
          chmod -R u+rw "$TEST_DATA/Saves" 2>/dev/null || true
          
          # Launch server with custom data path and mod path
          echo "Starting server with:"
          echo "  dataPath: $TEST_DATA"
          echo "  modPath: $MOD_PATH"
          echo ""
          
          # Change to test_data directory so relative paths work
          cd "$TEST_DATA"
          
          ${vintageStory}/bin/vintagestory-server \
            --dataPath="$TEST_DATA" \
            --addModPath="$MOD_PATH" \
            --port=$PORT \
            --maxclients=4
        '';

        # Test client script - launches a client to connect to test server
        testClient = pkgs.writeScriptBin "test-client" ''
          #!${pkgs.bash}/bin/bash

          PORT="''${VS_TEST_PORT:-42421}"
          HOST="''${VS_TEST_HOST:-localhost}"

          echo "🎮 Launching Vintage Story Client (Test Mode)..."
          echo ""
          echo "🔗 Connecting to: $HOST:$PORT"
          echo ""
          echo "💡 Manual connection:"
          echo "   1. Click 'Multiplayer'"
          echo "   2. Enter server: $HOST:$PORT"
          echo "   3. Join and test VintageAtlas features"
          echo ""
          echo "🌐 After joining, open browser:"
          echo "   http://localhost:$((PORT + 1))/"
          echo ""

          # Launch client
          ${vintageStory}/bin/vintagestory
        '';

        # Complete test environment - server + browser
        testComplete = pkgs.writeScriptBin "test-complete" ''
          #!${pkgs.bash}/bin/bash

          PORT="''${VS_TEST_PORT:-42421}"
          WEB_PORT=$((PORT + 1))

          echo "╔════════════════════════════════════════════════════════════════╗"
          echo "║   VintageAtlas - Complete Test Environment                    ║"
          echo "╚════════════════════════════════════════════════════════════════╝"
          echo ""
          echo "🚀 This will:"
          echo "   1. Start a test server on port $PORT"
          echo "   2. Open the web interface at http://localhost:$WEB_PORT"
          echo ""
          echo "📋 Test Steps:"
          echo "   1. Wait for server to start"
          echo "   2. Connect with 'test-client' in another terminal"
          echo "   3. Run '/atlas export' in game"
          echo "   4. Check web interface for map"
          echo ""
          echo "Press Enter to continue, or Ctrl+C to cancel..."
          read

          # Start server in background
          echo "🖥️  Starting test server..."
          test-server &
          SERVER_PID=$!

          # Wait a bit for server to start
          sleep 5

          # Open browser
          echo "🌐 Opening web interface..."
          ${pkgs.xdg-utils}/bin/xdg-open "http://localhost:$WEB_PORT" 2>/dev/null || \
            echo "   → http://localhost:$WEB_PORT"

          echo ""
          echo "✅ Test environment running!"
          echo ""
          echo "📋 Next steps:"
          echo "   • In another terminal: test-client"
          echo "   • In game: /atlas export"
          echo "   • Check browser for map updates"
          echo ""
          echo "Press Ctrl+C to stop everything"

          # Wait for Ctrl+C
          trap 'echo ""; echo "[STOP] Stopping test server..."; kill $SERVER_PID 2>/dev/null; exit 0' INT
          wait $SERVER_PID
        '';

        # Quick test - build, install to test, and start
        quickTest = pkgs.writeScriptBin "quick-test" ''
          #!${pkgs.bash}/bin/bash
          set -e

          echo "⚡ Quick Test - Building and Starting..."
          echo ""

          # Build
          echo "1️⃣  Building VintageAtlas..."
          build-vintageatlas || exit 1

          echo ""
          echo "2️⃣  Starting test server..."
          echo ""
          echo "💡 In another terminal, run: test-client"
          echo ""
          sleep 2

          # Start server
          test-server
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

            sqlite
            
            # Development Tools
            omnisharp-roslyn # C# language server for LSP

            # Node.js for frontend development
            nodejs
            nodePackages.npm

            # General Development Utilities
            git
            curl
            wget
            unzip
            zip
            xdg-utils # For opening browser in test-complete

            # Code Quality Tools
            nixpkgs-fmt # For formatting nix files

            # Custom Scripts
            setupVintageAtlas
            buildVintageAtlas
            buildVintageAtlasDebug
            packageVintageAtlas
            installVintageAtlas
            
            # Test Scripts
            testServer
            testClient
            testComplete
            quickTest
          ];

          # Environment variables that persist in the shell
          DOTNET_CLI_TELEMETRY_OPTOUT = "1";
          DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1";
          
          # Make ICU library available to C# language server
          LD_LIBRARY_PATH = "${pkgs.icu}/lib:${pkgs.dotnet-sdk_8}/lib";
          
          # Prefer using the Nix-provided .NET SDK
          DOTNET_ROOT = "${pkgs.dotnet-sdk_8}";
        };

        # Optional: Add packages that can be built/run directly
        packages = {
          inherit 
            setupVintageAtlas 
            buildVintageAtlas
            buildVintageAtlasDebug
            packageVintageAtlas
            installVintageAtlas
            testServer
            testClient
            testComplete
            quickTest;
          
          # Default package - show setup info when running nix run
          default = setupVintageAtlas;
        };
      }
    );
}
