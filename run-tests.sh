#!/usr/bin/env bash
# Script to run VintageAtlas tests in Nix environment

set -e

echo "=== Running VintageAtlas Tests ==="
echo

# Check if in nix shell
if [ -z "$VINTAGE_STORY" ]; then
    echo "⚠️  VINTAGE_STORY environment variable not set"
    echo "Running 'nix develop' to enter development shell..."
    echo
    exec nix develop --command "$0" "$@"
fi

cd "$(dirname "$0")/VintageAtlas.Tests"

# Parse command line arguments
FILTER=""
VERBOSE=false
COVERAGE=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --filter)
            FILTER="$2"
            shift 2
            ;;
        --verbose|-v)
            VERBOSE=true
            shift
            ;;
        --coverage|-c)
            COVERAGE=true
            shift
            ;;
        --help|-h)
            echo "Usage: $0 [options]"
            echo
            echo "Options:"
            echo "  --filter <pattern>   Run tests matching pattern"
            echo "  --verbose, -v        Show detailed output"
            echo "  --coverage, -c       Collect code coverage"
            echo "  --help, -h           Show this help message"
            echo
            echo "Examples:"
            echo "  $0                                    # Run all tests"
            echo "  $0 --filter ConfigValidatorTests      # Run specific test class"
            echo "  $0 --verbose                          # Run with detailed output"
            echo "  $0 --coverage                         # Run with coverage report"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Build test command
TEST_CMD="dotnet test"

if [ -n "$FILTER" ]; then
    TEST_CMD="$TEST_CMD --filter \"FullyQualifiedName~$FILTER\""
    echo "🔍 Running tests matching: $FILTER"
fi

if [ "$VERBOSE" = true ]; then
    TEST_CMD="$TEST_CMD --logger \"console;verbosity=detailed\""
    echo "📝 Verbose output enabled"
fi

if [ "$COVERAGE" = true ]; then
    TEST_CMD="$TEST_CMD --collect:\"XPlat Code Coverage\""
    echo "📊 Code coverage collection enabled"
fi

echo
echo "Running: $TEST_CMD"
echo

# Run tests
eval $TEST_CMD

# Generate coverage report if requested
if [ "$COVERAGE" = true ]; then
    echo
    echo "=== Generating Coverage Report ==="
    
    COVERAGE_FILE=$(find . -name "coverage.cobertura.xml" | head -n 1)
    
    if [ -n "$COVERAGE_FILE" ]; then
        echo "📈 Coverage report: $COVERAGE_FILE"
        
        # Try to generate HTML report if reportgenerator is available
        if command -v reportgenerator &> /dev/null; then
            REPORT_DIR="coverage-report"
            reportgenerator -reports:"$COVERAGE_FILE" -targetdir:"$REPORT_DIR"
            echo "✅ HTML report generated: $REPORT_DIR/index.html"
            echo
            echo "To view: xdg-open $REPORT_DIR/index.html"
        else
            echo "💡 Install reportgenerator for HTML reports:"
            echo "   dotnet tool install -g dotnet-reportgenerator-globaltool"
        fi
    fi
fi

echo
echo "✅ Tests complete!"

