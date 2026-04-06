---
phase: 01-foundation
plan: 02
type: execute
wave: 2
depends_on:
  - 1-01
files_modified:
  - src/DigimonBot.Core/Services/IKimiRepositoryManager.cs
  - src/DigimonBot.Core/Services/KimiRepositoryManager.cs
  - src/DigimonBot.AI/Services/IKimiExecutionService.cs
  - src/DigimonBot.AI/Services/KimiExecutionService.cs
  - src/DigimonBot.Messaging/Commands/KimiCommand.cs
  - src/DigimonBot.Host/Program.cs
autonomous: true
requirements:
  - KIMI-CMD-001
  - KIMI-REPO-001
  - KIMI-EXEC-001
  - KIMI-INT-001
user_setup: []
must_haves:
  truths:
    - /kimi --help displays usage information
    - /kimi --new-repo creates a git repository with README
    - /kimi --list-repos shows all repositories
    - /kimi --switch-repo changes active repository
    - Kimi CLI execution works with timeout protection
  artifacts:
    - path: src/DigimonBot.Core/Services/KimiRepositoryManager.cs
      provides: Repository lifecycle management
      exports: [CreateRepositoryAsync, ListRepositoriesAsync, SwitchRepositoryAsync, GetActiveRepositoryAsync]
    - path: src/DigimonBot.AI/Services/KimiExecutionService.cs
      provides: CLI execution with timeout
      exports: [ExecuteAsync, ExecuteWithTimeoutAsync]
    - path: src/DigimonBot.Messaging/Commands/KimiCommand.cs
      provides: Command handler
      exports: [Name, Aliases, Description, ExecuteAsync]
    - path: src/DigimonBot.Host/Program.cs
      provides: Service registration
      contains: KimiCommand, KimiRepositoryManager, KimiExecutionService registration
  key_links:
    - from: KimiCommand
      to: KimiRepositoryManager
      via: Constructor injection
      pattern: Command -> Manager -> Repository
    - from: KimiExecutionService
      to: KimiRepositoryManager
      via: Gets active repo path
      pattern: Execute in active repo directory
    - from: KimiCommand
      to: KimiConfigService
      via: Access control check
      pattern: Check whitelist before execution
---

<objective>
Implement the core features of Phase 1 - repository management, CLI execution, and command handling. Build on top of the infrastructure from Plan 01.

Purpose: Provide user-facing functionality for creating repositories and executing kimi CLI commands.
Output: Working /kimi command with repository management and CLI execution capabilities.
</objective>

<execution_context>
@C:/Users/MA Huan/.config/opencode/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@.planning/1-CONTEXT.md
@.planning/phases/1-foundation-week-1/1-RESEARCH.md
@.planning/phases/1-foundation-week-1/1-01-SUMMARY.md

@src/DigimonBot.Messaging/Commands/ICommand.cs
@src/DigimonBot.Messaging/Commands/StatusCommand.cs
@src/DigimonBot.Host/Services/VisionService.cs
@src/DigimonBot.Data/Repositories/Sqlite/SqliteUserDataRepository.cs

<interfaces>
From ICommand.cs:
```csharp
public interface ICommand
{
    string Name { get; }
    string[] Aliases { get; }
    string Description { get; }
    Task<CommandResult> ExecuteAsync(CommandContext context);
}
```

From CommandContext (in ICommand.cs):
```csharp
public class CommandContext
{
    public string UserId { get; set; } = "";
    public string OriginalUserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Message { get; set; } = "";
    public string[] Args { get; set; } = Array.Empty<string>();
    public long GroupId { get; set; }
    public bool IsGroupMessage { get; set; }
    public bool ShouldAddPrefix { get; set; }
    public List<string> MentionedUserIds { get; set; } = new();
    public string? TargetUserId { get; set; }
    public string? TargetOriginalUserId { get; set; }
}
```

From VisionService.cs (Process execution pattern):
```csharp
var psi = new ProcessStartInfo
{
    FileName = "curl",
    Arguments = $"...",
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    CreateNoWindow = true
};
using var process = Process.Start(psi);
```
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Implement repository manager</name>
  <files>src/DigimonBot.Core/Services/IKimiRepositoryManager.cs, src/DigimonBot.Core/Services/KimiRepositoryManager.cs</files>
  <action>
Create repository manager service that coordinates repository lifecycle:

Interface (IKimiRepositoryManager):
- CreateRepositoryAsync(string? name, string userId) -> Task<KimiRepository>
- ListRepositoriesAsync() -> Task<IEnumerable<KimiRepository>>
- SwitchRepositoryAsync(string name) -> Task<bool>
- GetActiveRepositoryAsync() -> Task<KimiRepository?>
- EnsureRepositoryExistsAsync() -> Task<KimiRepository> (auto-create if none)

Implementation (KimiRepositoryManager):
- Inject IKimiRepositoryRepository and KimiConfig
- CreateRepositoryAsync:
  - Generate name if not provided: "kimi-{timestamp}"
  - Create directory at {BasePath}/{name}
  - Run git init --initial-branch=main
  - Configure git user.email and user.name
  - Create README.md with creation info
  - Save to database via repository
  - Set as active repository
- Use SemaphoreSlim for thread safety during git operations
- Git CLI execution via Process (follow VisionService pattern)

From RESEARCH.md:
- Execute git commands in repo directory
- Use Process with RedirectStandardOutput/Error
- Timeout protection for git operations
  </action>
  <verify>
    <automated>grep -q "IKimiRepositoryManager" src/DigimonBot.Core/Services/IKimiRepositoryManager.cs && grep -q "git init" src/DigimonBot.Core/Services/KimiRepositoryManager.cs && echo "Repository manager implemented"</automated>
  </verify>
  <done>
    - Repository manager creates repos with git init
    - README.md auto-generated on creation
    - Active repository tracking works
    - Thread-safe with SemaphoreSlim
  </done>
</task>

<task type="auto">
  <name>Task 2: Implement execution service</name>
  <files>src/DigimonBot.AI/Services/IKimiExecutionService.cs, src/DigimonBot.AI/Services/KimiExecutionService.cs</files>
  <action>
Create execution service for running kimi CLI commands:

Interface (IKimiExecutionService):
- ExecuteAsync(string repoPath, string arguments, int timeoutSeconds) -> Task<ExecutionResult>

ExecutionResult model:
```csharp
public class ExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";
    public int ExitCode { get; set; }
    public int DurationMs { get; set; }
}
```

Implementation from RESEARCH.md:
```csharp
using var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "kimi",
        Arguments = arguments,
        WorkingDirectory = repoPath,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    }
};

process.Start();
var outputTask = process.StandardOutput.ReadToEndAsync();
var errorTask = process.StandardError.ReadToEndAsync();

var completed = await Task.Run(() => process.WaitForExit(timeoutMs));

if (!completed)
{
    process.Kill();
    throw new TimeoutException();
}
```

Important:
- Read stdout/stderr asynchronously to avoid deadlock
- Kill process on timeout
- Return ExecutionResult with all details
- Log execution details
  </action>
  <verify>
    <automated>grep -q "Process.Start" src/DigimonBot.AI/Services/KimiExecutionService.cs && grep -q "WaitForExit" src/DigimonBot.AI/Services/KimiExecutionService.cs && echo "Execution service implemented"</automated>
  </verify>
  <done>
    - Process execution with timeout protection
    - Asynchronous output reading
    - Process killed on timeout
    - ExecutionResult contains all details
  </done>
</task>

<task type="auto">
  <name>Task 3: Implement KimiCommand handler</name>
  <files>src/DigimonBot.Messaging/Commands/KimiCommand.cs</files>
  <action>
Create KimiCommand implementing ICommand:

Properties:
- Name = "kimi"
- Aliases = new[] { "kimichat", "kimi助手" }
- Description = "Execute kimi CLI commands for coding assistance"

ExecuteAsync implementation:
1. Check access control using KimiConfig:
   - If user in whitelist -> allow
   - If mode is "open" -> allow
   - If non-whitelist and mode is "whitelist":
     - If NonWhitelistAccess == "restricted" -> deny
     - If NonWhitelistAccess == "read-only" -> check if command is read-only

2. Parse arguments:
   - --new-repo [name] -> CreateRepositoryAsync
   - --list-repos -> ListRepositoriesAsync
   - --switch-repo <name> -> SwitchRepositoryAsync
   - --current-repo -> GetActiveRepositoryAsync
   - --help -> Show help message
   - Anything else -> Execute kimi command

3. For kimi execution:
   - Get active repository (auto-create if none)
   - Build arguments string (filter allowed args per whitelist)
   - Call KimiExecutionService.ExecuteAsync
   - Format and return result

Help message (Chinese):
```
🤖 **Kimi 代码助手**

用法: /kimi [选项] [消息]

仓库管理:
  --new-repo [名称]     创建新仓库
  --list-repos          列出所有仓库
  --switch-repo <名称>  切换到指定仓库
  --current-repo        显示当前仓库

Kimi CLI 选项:
  --prompt, -p <文本>   发送消息给Kimi
  --model, -m <模型>    指定模型
  --yolo, -y           自动确认所有操作
  --plan               计划模式
  --thinking           启用思考模式

示例:
  /kimi --new-repo my-project
  /kimi --switch-repo my-project
  /kimi 用Python写个Hello World
```
  </action>
  <verify>
    <automated>grep -q "class KimiCommand" src/DigimonBot.Messaging/Commands/KimiCommand.cs && grep -q "ICommand" src/DigimonBot.Messaging/Commands/KimiCommand.cs && echo "Command handler implemented"</automated>
  </verify>
  <done>
    - KimiCommand implements ICommand
    - Access control checks implemented
    - All subcommands (--new-repo, --list-repos, etc.) work
    - Help message in Chinese
    - Integrates with RepositoryManager and ExecutionService
  </done>
</task>

<task type="auto">
  <name>Task 4: Register services in Program.cs</name>
  <files>src/DigimonBot.Host/Program.cs</files>
  <action>
Modify Program.cs to register all Kimi services:

Add to ConfigureServices section:
1. KimiConfigService (Singleton)
   ```csharp
   services.AddSingleton<KimiConfigService>();
   ```

2. KimiDatabaseInitializer (Singleton)
   ```csharp
   var kimiDbInitializer = new KimiDatabaseInitializer(
       settings.Data.KimiSqliteConnectionString ?? "Data Source=Data/kimi_data.db");
   kimiDbInitializer.Initialize();
   services.AddSingleton(kimiDbInitializer);
   ```

3. Repository (Scoped or Singleton)
   ```csharp
   services.AddSingleton<IKimiRepositoryRepository, KimiRepositoryRepository>();
   ```

4. Repository Manager (Singleton)
   ```csharp
   services.AddSingleton<IKimiRepositoryManager, KimiRepositoryManager>();
   ```

5. Execution Service (Singleton)
   ```csharp
   services.AddSingleton<IKimiExecutionService, KimiExecutionService>();
   ```

Add to CommandRegistry registration:
```csharp
registry.Register(new KimiCommand(
    provider.GetRequiredService<IKimiRepositoryManager>(),
    provider.GetRequiredService<IKimiExecutionService>(),
    provider.GetRequiredService<KimiConfigService>(),
    provider.GetRequiredService<ILogger<KimiCommand>>()));
```

Follow existing registration patterns in Program.cs lines 177-323.
  </action>
  <verify>
    <automated>grep -q "KimiCommand" src/DigimonBot.Host/Program.cs && grep -q "KimiConfigService" src/DigimonBot.Host/Program.cs && grep -q "KimiDatabaseInitializer" src/DigimonBot.Host/Program.cs && echo "Services registered"</automated>
  </verify>
  <done>
    - All Kimi services registered in DI container
    - KimiCommand registered in CommandRegistry
    - Database initializer called on startup
    - Follows existing registration patterns
  </done>
</task>

</tasks>

<verification>
After completing all tasks:
1. Build project: dotnet build
2. Verify no compilation errors
3. Check that all services are registered
4. Verify command appears in /help output
</verification>

<success_criteria>
- /kimi --help displays correctly
- /kimi --new-repo creates repository with git init
- /kimi --list-repos shows repositories
- /kimi --switch-repo changes active repo
- Services properly registered in DI
- All code compiles without errors
</success_criteria>

<output>
After completion, create `.planning/phases/1-foundation-week-1/1-02-SUMMARY.md`
</output>
