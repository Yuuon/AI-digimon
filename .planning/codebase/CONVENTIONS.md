# Coding Conventions

**Analysis Date:** 2026-04-06

## Project Configuration

**SDK:** .NET 8.0 (`net8.0`)
**Project Format:** SDK-style `.csproj` files
**Configuration in Project Files:**
- `ImplicitUsings` = `enable` - Implicit global usings enabled
- `Nullable` = `enable` - Nullable reference types enabled
- `IsPackable` = `false` for test projects

## Naming Conventions

### Types
- **Classes:** PascalCase
  - Example: `EvolutionEngine`, `DigimonDefinition`, `CommandResult`
  - Implementation classes match interface name without `I`: `IEvolutionEngine` → `EvolutionEngine`

- **Interfaces:** PascalCase with `I` prefix
  - Example: `ICommand`, `IEvolutionEngine`, `IDigimonRepository`

- **Enums:** PascalCase
  - Example: `DigimonStage`, `EmotionType`, `AIProvider`
  - Enum members: PascalCase
    - Example: `Baby1`, `SuperUltimate`, `Courage`, `Friendship`

- **Records/Structs:** Not detected (using classes)

### Members
- **Properties:** PascalCase
  - Example: `public string Name { get; set; }`, `public int TotalTokensConsumed { get; set; }`
  - Auto-implemented properties preferred
  - Default values initialized inline: `= "";`, `= new();`

- **Methods:** PascalCase, Async suffix for async methods
  - Example: `CheckAndEvolveAsync`, `GetAvailableEvolutions`, `ExecuteAsync`
  - Private helper methods: PascalCase (no underscore prefix)
    - Example: `ExecuteEvolutionAsync`, `ExtractJson`, `FixTruncatedJson`

- **Fields:** 
  - Private fields: `_camelCase` with underscore prefix
    - Example: `private readonly HttpClient _httpClient;`, `private readonly ILogger<GLMClient> _logger;`
  - Constants: PascalCase with `private const` or `public const`
    - Example: `private const double APPROACHING_THRESHOLD = 0.8;`

- **Parameters:** camelCase
  - Example: `userDigimon`, `digimonDb`, `targetId`, `cancellationToken`

### Files and Namespaces
- **Files:** Match class name exactly
  - Example: `EvolutionEngine.cs` contains `EvolutionEngine` class
  - One primary class per file

- **Namespaces:** Match folder structure
  - Example: `DigimonBot.Core.Services`, `DigimonBot.Messaging.Commands`
  - Pattern: `{ProjectName}.{Folder}.{Subfolder}`

## Code Style

### Bracing Style
- **Allman style** (brace on new line)
```csharp
public class EvolutionEngine : IEvolutionEngine
{
    public Task<EvolutionResult?> CheckAndEvolveAsync(
        UserDigimon userDigimon, 
        IReadOnlyDictionary<string, DigimonDefinition> digimonDb)
    {
        // Implementation
    }
}
```

### Indentation
- 4 spaces per indentation level
- No tabs

### Line Length
- No explicit limit observed
- Complex LINQ chains broken into multiple lines

### Expression-Bodied Members
- Used for simple single-expression methods and properties
```csharp
public string Name => "jrrp";
public int CalculateComplexity() => Requirements.CalculateComplexity();
```

## Documentation

### XML Documentation Comments
- **Required for all public APIs**
- Use `<summary>` tags for all public types and members
- Use `<param>` and `<returns>` for methods with parameters
- Language: **Chinese (Simplified)**

```csharp
/// <summary>
/// 进化引擎接口
/// </summary>
public interface IEvolutionEngine
{
    /// <summary>
    /// 检查并执行进化（自动选择最高优先级）
    /// </summary>
    /// <param name="userDigimon">用户数码宝贝实例</param>
    /// <param name="digimonDb">数码宝贝数据库</param>
    /// <returns>进化结果，如果没有进化则返回null</returns>
    Task<EvolutionResult?> CheckAndEvolveAsync(
        UserDigimon userDigimon, 
        IReadOnlyDictionary<string, DigimonDefinition> digimonDb);
}
```

### Inline Comments
- Use for complex logic explanation
- Language: **Chinese**
- Format: `// 中文说明`

```csharp
// 2. 预处理历史对话：取最后10条
var recentHistory = history.TakeLast(10).ToList();

// 关键修复：找到第一个 User 消息作为起点
// GLM API 要求 messages 必须以 user 角色开始，不能以 assistant 开始
```

## Language Features

### C# 10+ Features Used
- **File-scoped namespaces**
  ```csharp
  namespace DigimonBot.Core.Models;
  ```

- **Global usings** (ImplicitUsings enabled)

- **Nullable reference types** with null-forgiving operator
  ```csharp
  public string Content { get; set; } = "";
  ```

- **Pattern matching** and switch expressions
  ```csharp
  public int GetValue(EmotionType type) => type switch
  {
      EmotionType.Courage => Courage,
      EmotionType.Friendship => Friendship,
      _ => 0
  };
  ```

- **Range syntax** for string slicing
  ```csharp
  var dbPath = connectionString["Data Source=".Length..];
  ```

- **Collection initializers** with `new()`
  ```csharp
  public List<EvolutionOption> NextEvolutions { get; set; } = new();
  ```

- **Raw string literals** for multi-line strings
  ```csharp
  var message = $"""
      🌟 **{args.CurrentDigimonName}** 可以进化了！
      
      检测到 **{args.AvailableEvolutions.Count}** 个可进化分支：
      """;
  ```

- **Required members:** Not used
- **Records:** Not used (classes preferred)

## Error Handling

### Exception Patterns
- Use try-catch for external API calls
- Log exceptions before re-throwing or returning defaults
- Specific exception types not heavily used

```csharp
try
{
    var result = await _httpClient.PostAsync(url, content);
    response.EnsureSuccessStatusCode();
    // ...
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error calling GLM API");
    throw;
}
```

### Null Handling
- Initialize collection properties to empty collections, not null
- Use null-conditional operator sparingly
- Explicit null checks for reference parameters

## Dependency Injection

### Constructor Injection Pattern
```csharp
public class GLMClient : IAIClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GLMClient> _logger;
    private readonly string _apiKey;

    public GLMClient(HttpClient httpClient, ILogger<GLMClient> logger, string apiKey, ...)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = apiKey;
    }
}
```

### Service Lifetime
- Not explicitly configured in code (assumed configured in Host)
- Factory pattern used for creating clients: `AIClientFactory`

## Async Patterns

### Naming
- All async methods end with `Async` suffix
- Examples: `CheckAndEvolveAsync`, `ExecuteAsync`, `ChatAsync`

### Return Types
- `Task<T>` for async methods with return values
- `Task` for async void-equivalent
- `Task<T?>` for nullable returns

### Cancellation
- CancellationToken parameter present in background service methods
- Not consistently passed through all async methods

## Logging

### Framework
- **Microsoft.Extensions.Logging.Abstractions**
- ILogger<T> injected into services

### Log Levels
- `LogInformation` for operational events
- `LogDebug` for detailed diagnostics
- `LogWarning` for recoverable issues
- `LogError` for exceptions and failures

### Message Format
- Use structured logging with named placeholders
- Prefix for feature areas: `[特别关注]`, `[BotService]`, `[ExtractImageInfo]`

```csharp
_logger.LogInformation("[特别关注] 检测到关注用户发言: Group={GroupId}, User={User}", 
    groupId, userName);
_logger.LogDebug("提取后的消息内容: '{Content}'", content);
```

## Data Access

### Repository Pattern
- Interfaces defined in Core: `IDigimonRepository`, `IUserDataRepository`
- Implementations in Data project: `JsonDigimonRepository`, `SqliteUserDataRepository`
- Pattern: `I{Entity}Repository` with `Sqlite{Entity}Repository` or `Json{Entity}Repository`

### Database
- SQLite with Dapper micro-ORM
- Raw SQL for table creation in `DatabaseInitializer`

## Configuration

### Settings Classes
- POCO classes with properties
- `IOptions<T>` pattern for injection
- Located in `Configs` folder

```csharp
public class AppSettings
{
    public QQBotConfig QQBot { get; set; } = new();
    public AIConfig AI { get; set; } = new();
}
```

## String Handling

### String Literals
- Prefer interpolated strings `$"..."` for dynamic content
- Raw string literals for multi-line content
- Chinese characters used directly (UTF-8)

### String Comparison
- Use `StringComparison.OrdinalIgnoreCase` for case-insensitive comparison
- Example: `.Equals(targetId, StringComparison.OrdinalIgnoreCase)`

---

*Convention analysis: 2026-04-06*
