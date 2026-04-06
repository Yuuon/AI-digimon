# Testing Patterns

**Analysis Date:** 2026-04-06

## Test Framework

**Primary Framework:** xUnit 2.6.2
**Supporting Libraries:**
- `Moq` 4.20.70 - Mocking framework
- `Microsoft.NET.Test.Sdk` 17.8.0 - Test runner
- `coverlet.collector` 6.0.0 - Code coverage

**Test Runner:** xunit.runner.visualstudio 2.5.4

## Test Project Organization

### Project Structure
```
tests/
├── DigimonBot.Core.Tests/        # Unit tests for Core project
│   ├── Commands/
│   ├── Models/
│   └── Services/
├── DigimonBot.AI.Tests/          # Unit tests for AI project
│   └── Services/
└── IntegrationTest/              # Integration tests (Console app)
    └── Program.cs
```

### Test Project Configuration
```xml
<PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
</PropertyGroup>
```

### Project References
- Test projects reference source projects being tested
- Moq for mocking dependencies
- Real `LoggerFactory` used instead of mocks (due to extension methods)

## Test Naming Conventions

### Test Class Naming
- Pattern: `{ClassName}Tests`
- Examples:
  - `EvolutionEngineTests` for `EvolutionEngine`
  - `EmotionValuesTests` for `EmotionValues`
  - `JrrpCommandTests` for `JrrpCommand`

### Test Method Naming
- Pattern: `{MethodUnderTest}_{Scenario}_{ExpectedResult}`
- Examples:
  ```csharp
  CheckAndEvolveAsync_TokenNotEnough_NoEvolution
  CheckAndEvolveAsync_AllConditionsMet_Evolves
  ApplyEmotionAnalysisAsync_DeltaExceedsMax_IsClamped
  GetEmotionDescription_AllZero_ReturnsDefault
  ```

## Test Structure

### Standard Test Pattern
```csharp
public class EvolutionEngineTests
{
    private readonly EvolutionEngine _engine;
    private readonly Dictionary<string, DigimonDefinition> _digimonDb;

    public EvolutionEngineTests()
    {
        _engine = new EvolutionEngine();
        _digimonDb = CreateTestDigimonDatabase();
    }

    private Dictionary<string, DigimonDefinition> CreateTestDigimonDatabase()
    {
        // Test data setup
    }

    [Fact]
    public async Task CheckAndEvolveAsync_AllConditionsMet_Evolves()
    {
        // Arrange
        var userDigimon = new UserDigimon
        {
            CurrentDigimonId = "baby",
            Emotions = new EmotionValues { Courage = 20 },
            TotalTokensConsumed = 2000
        };

        // Act
        var result = await _engine.CheckAndEvolveAsync(userDigimon, _digimonDb);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("baby", result.OldDigimonId);
        Assert.Equal("child_courage", result.NewDigimonId);
    }
}
```

### Test Attributes

**[Fact]** - Simple test with no parameters
```csharp
[Fact]
public void Name_IsJrrp()
{
    Assert.Equal("jrrp", _command.Name);
}
```

**[Theory]** - Parameterized tests with [InlineData]
```csharp
[Theory]
[InlineData(DigimonStage.Ultimate, "究极体")]
[InlineData(DigimonStage.SuperUltimate, "超究极体")]
public void BuildEvolutionAnnouncement_FinalForms_HasSpecialText(DigimonStage stage, string expectedText)
{
    // Test with multiple inputs
}

[Theory]
[InlineData(10, 10, 10, 10, 100)]    // 完全满足
[InlineData(5, 10, 10, 10, 87.5)]    // 勇气50%, 其他100%
[InlineData(0, 0, 0, 0, 0)]          // 完全不满足
public void CalculateMatchScore_ReturnsExpectedScore(int c, int f, int l, int k, double expected)
{
    // Multiple test cases
}
```

## Mocking Patterns

### Moq Setup
```csharp
public class AIClientFactoryTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly ILoggerFactory _loggerFactory;
    private readonly AIClientFactory _factory;

    public AIClientFactoryTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        
        // Real LoggerFactory used (not mocked) due to extension methods
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        _factory = new AIClientFactory(_httpClientFactoryMock.Object, _loggerFactory);
    }

    private void SetupHttpClient()
    {
        _httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());
    }
}
```

### Mock Behavior
- Use `Mock<T>` for interface dependencies
- Use `It.IsAny<string>()` for flexible parameter matching
- Real implementations preferred for simple dependencies

## Assertion Patterns

### Standard Assertions
```csharp
// Equality
Assert.Equal(expected, actual);
Assert.NotEqual(unexpected, actual);

// Null checks
Assert.Null(result);
Assert.NotNull(result);

// Boolean
Assert.True(condition);
Assert.False(condition);

// Type checking
Assert.IsType<DeepSeekClient>(client);
Assert.IsType<GLMClient>(client);

// String containment
Assert.Contains("expected substring", actualString);

// Collection
Assert.Single(collection);
Assert.Empty(collection);
```

### Precision for Floating Point
```csharp
Assert.Equal(expectedScore, score * 100, precision: 1);
```

## Test Data

### Inline Test Data
- Use `[InlineData]` for simple value combinations
- Up to 16 parameters supported

### Object Initializers
```csharp
var userDigimon = new UserDigimon
{
    CurrentDigimonId = "baby",
    Emotions = new EmotionValues { Courage = 20 },
    TotalTokensConsumed = 2000
};
```

### Test Database Setup
```csharp
private Dictionary<string, DigimonDefinition> CreateTestDigimonDatabase()
{
    return new Dictionary<string, DigimonDefinition>
    {
        ["baby"] = new DigimonDefinition
        {
            Id = "baby",
            Name = "幼年期",
            Stage = DigimonStage.Baby2,
            NextEvolutions = new List<EvolutionOption>
            {
                new EvolutionOption
                {
                    TargetId = "child_courage",
                    Requirements = new EmotionValues { Courage = 10 },
                    MinTokens = 1000,
                    Priority = 1
                }
            }
        },
        // ... more test data
    };
}
```

## Async Testing

### Async Test Methods
```csharp
[Fact]
public async Task CheckAndEvolveAsync_AllConditionsMet_Evolves()
{
    // Arrange
    var userDigimon = CreateTestUserDigimon();

    // Act
    var result = await _engine.CheckAndEvolveAsync(userDigimon, _digimonDb);

    // Assert
    Assert.NotNull(result);
}
```

### Testing Async Methods
- Always use `async Task` for test methods calling async code
- Never use `.Result` or `.Wait()` in tests

## Integration Testing

### Integration Test Project
- Located at: `tests/IntegrationTest/IntegrationTest.csproj`
- OutputType: `Exe` (Console application)
- Not a traditional test project - runs as interactive console app

### Pattern
```csharp
// Manual integration test
Console.WriteLine("【测试1】基础服务初始化");
var evolutionEngine = new EvolutionEngine();
var emotionTracker = new EmotionTracker();
Console.WriteLine($"  - 进化引擎: ✓");

// Environment-based conditional testing
var apiKey = Environment.GetEnvironmentVariable("AI__ApiKey");
if (!string.IsNullOrEmpty(apiKey))
{
    // Run live API tests
}
else
{
    Console.WriteLine("  ⚠ 未设置 AI__ApiKey 环境变量，跳过AI连接测试");
}
```

### Running Integration Tests
```bash
# Set environment variable first
$env:AI__ApiKey="your-api-key"
dotnet run --project tests/IntegrationTest
```

## Coverage

### Tools
- **Coverlet** for coverage collection
- Visual Studio Test Explorer integration

### Running Tests with Coverage
```bash
# Run all tests
dotnet test

# Run with coverage (requires coverlet or dotnet-coverage)
dotnet test --collect:"XPlat Code Coverage"

# Run specific project
dotnet test tests/DigimonBot.Core.Tests
dotnet test tests/DigimonBot.AI.Tests
```

## Test Categories

### Unit Tests
- Fast, isolated tests
- Mock external dependencies
- Located in `{Project}.Tests` projects

### Integration Tests
- Test component interactions
- May use real dependencies (SQLite, HTTP clients)
- Located in `IntegrationTest` project

### No E2E Tests Detected
- No browser automation tests
- No full bot conversation tests

## Common Test Patterns

### Constructor Injection Testing
```csharp
public class EmotionTrackerTests
{
    private readonly EmotionTracker _tracker;

    public EmotionTrackerTests()
    {
        _tracker = new EmotionTracker(
            LoggerFactory.Create(builder => { }).CreateLogger<EmotionTracker>()
        );
    }
}
```

### Exception Testing
```csharp
[Fact]
public void CreateClient_CustomProvider_WithoutBaseUrl_ThrowsException()
{
    SetupHttpClient();
    
    var config = new AIClientConfig
    {
        Provider = AIProvider.Custom,
        ApiKey = "test-key"
        // BaseUrl is null
    };

    Assert.Throws<ArgumentException>(() => _factory.CreateClient(config));
}
```

### State Verification
```csharp
[Fact]
public void Clone_CreatesIndependentCopy()
{
    var original = new EmotionValues { Courage = 10 };
    var clone = original.Clone();
    
    // Modify original
    original.Courage = 100;

    Assert.Equal(10, clone.Courage);    // Clone unchanged
    Assert.Equal(100, original.Courage); // Original changed
}
```

## Best Practices Observed

1. **One assertion per concept** - Multiple asserts OK for related properties
2. **Descriptive test names** - Clear scenario and expectation in name
3. **No test ordering dependencies** - Tests are independent
4. **Proper async/await** - No blocking calls in async tests
5. **Real logger factory** - Not mocked, uses actual implementation
6. **Test data builders** - Private helper methods for complex setup
7. **Theory for data variations** - Reduces duplication for similar tests

## Missing Patterns

1. **No test fixtures** - No shared setup across multiple test classes
2. **No BDD-style tests** - No Given/When/Then comments
3. **No snapshot testing** - All assertions are explicit
4. **No property-based testing** - Only explicit test cases
5. **No CI/CD integration** - No GitHub Actions workflows detected

---

*Testing analysis: 2026-04-06*
