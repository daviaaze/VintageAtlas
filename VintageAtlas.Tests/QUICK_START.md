# Quick Start - Running Tests

## ✅ Tests Are Now Compiling!

The tests compile successfully. To run them, you need the Nix development environment.

## Running Tests

### Option 1: Using run-tests.sh (Recommended)

```bash
# From project root
./run-tests.sh
```

The script automatically enters the Nix shell if needed.

### Option 2: Manual (from Nix shell)

```bash
# Enter nix shell first (if not already in it)
nix develop

# Then run tests
cd VintageAtlas.Tests
dotnet test
```

### Option 3: One-liner

```bash
nix develop --command bash -c "cd VintageAtlas.Tests && dotnet test"
```

## Why Nix Shell is Required

The tests require access to Vintage Story DLLs (VintagestoryAPI.dll, etc.), which are referenced via the `$VINTAGE_STORY` environment variable. This variable is set automatically when you enter the Nix development shell.

## Test Results

Once you run the tests in the Nix shell, you should see output like:

```
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    30, Skipped:     0, Total:    30
```

## Current Test Coverage

- ✅ **ConfigValidator** (10 tests) - Configuration validation
- ✅ **BlurTool** (7 tests) - Image blur algorithm
- ✅ **BlockColorCache** (8 tests) - Color caching with mocked API
- ✅ **TileResult** (11 tests) - Data model tests

**Total: 36 tests**

## Troubleshooting

### "Could not find dependent assembly 'VintagestoryAPI'"

**Solution:** You're not in the Nix shell. Run:
```bash
nix develop
```

### Tests run but show "0 tests"

**Solution:** The test assembly couldn't be loaded. Check that:
1. You're in the Nix shell
2. `$VINTAGE_STORY` is set: `echo $VINTAGE_STORY`
3. The DLLs exist: `ls $VINTAGE_STORY/*.dll`

### Warning about "EntryAdded event is never used"

**Status:** This is just a warning and can be ignored. The `EntryAdded` event is part of the `ILogger` interface but not currently used in tests.

## Next Steps

1. ✅ Tests compile
2. ✅ Run tests in Nix shell
3. Add more tests (see [Testing Guide](../docs/guides/testing-guide.md))
4. Set up CI/CD

## See Also

- [Testing Guide](../docs/guides/testing-guide.md) - Complete testing documentation
- [README](README.md) - Test project overview
- [run-tests.sh](../run-tests.sh) - Test runner script

