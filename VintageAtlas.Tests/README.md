# VintageAtlas Tests

This project contains automated tests for VintageAtlas.

## Structure

- `Unit/` - Fast, isolated unit tests
- `Integration/` - Tests with mocked VS API dependencies
- `Mocks/` - Mock implementations of Vintage Story interfaces
- `Fixtures/` - Test data and helpers

## Running Tests

### All tests

```bash
nix develop
cd VintageAtlas.Tests
dotnet test
```

### Specific test class

```bash
dotnet test --filter "FullyQualifiedName~ConfigValidatorTests"
```

### With coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Verbose output

```bash
dotnet test --logger "console;verbosity=detailed"
```

## Writing Tests

### Test Structure (AAA Pattern)

```csharp
[Fact]
public void MethodName_Scenario_ExpectedResult()
{
    // Arrange - Set up test data
    var input = GetTestInput();

    // Act - Execute method under test
    var result = MethodUnderTest(input);

    // Assert - Verify results
    result.Should().Be(expectedValue);
}
```

### Using Assertions

```csharp
// FluentAssertions examples
result.Should().BeTrue();
result.Should().Be(42);
result.Should().NotBeNull();
result.Should().BeOfType<MyClass>();
collection.Should().HaveCount(3);
collection.Should().Contain(item);
action.Should().Throw<ArgumentException>();
```

### Using Mocks

```csharp
var mockLogger = new MockLogger();
// ... run code that logs
mockLogger.HasErrors().Should().BeFalse();
mockLogger.Notifications.Should().Contain(msg => msg.Contains("Success"));
```

## Coverage Goals

- Core logic: 80%+
- Export logic: 70%+
- Web/API: 60%+

## See Also

- [Full Testing Guide](../docs/guides/testing-guide.md)
- [Contributing Guidelines](../CONTRIBUTING.md)
