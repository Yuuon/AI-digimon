---
phase: 03-general-command-executor
plan: 02
type: execute
wave: 1
depends_on:
  - 3-01
files_modified:
  - src/DigimonBot.Core/Services/ICustomCommandExecutor.cs
  - src/DigimonBot.Core/Services/CustomCommandExecutor.cs
  - src/DigimonBot.Messaging/Handlers/CommandRouter.cs
  - src/DigimonBot.Host/Program.cs
autonomous: true
requirements:
  - KIMI-CMD-001
  - KIMI-SEC-001
  - KIMI-EXEC-001
user_setup: []
must_haves:
  truths:
    - Custom commands execute when invoked
    - Whitelist checks work before execution
    - Path validation prevents directory escape
    - Execution updates usage statistics
    - Unknown commands return clear error
  artifacts:
    - path: src/DigimonBot.Core/Services/ICustomCommandExecutor.cs
      provides: Executor interface
      exports: [ExecuteAsync]
    - path: src/DigimonBot.Core/Services/CustomCommandExecutor.cs
      provides: Binary execution service
      exports: [ExecuteAsync, ValidatePath]
    - path: src/DigimonBot.Messaging/Handlers/CommandRouter.cs (modified)
      provides: Command resolution
      contains: Internal → Custom → Unknown fallback
  key_links:
    - from: CommandRouter
      to: CustomCommandExecutor
      via: Constructor injection
      pattern: Fallback after internal commands
    - from: CustomCommandExecutor
      to: CustomCommandRepository
      via: Lookup by name/alias
      pattern: GetByNameAsync → GetByAliasAsync
    - from: CustomCommandExecutor
      to: Process execution
      via: ProcessStartInfo
      pattern: Same as VisionService/KimiExecutionService
---

<objective>
Implement the custom command execution service and integrate it into the command routing pipeline. Enable users to execute registered custom binaries with proper security checks.

Purpose: Execute user-created binaries as chat commands with whitelist validation and security sandboxing.
Output: Working execution layer integrated with existing command system.
</objective>

<execution_context>
@C:/Users/MA Huan/.config/opencode/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@.planning/3-CONTEXT.md
@src/DigimonBot.AI/Services/KimiExecutionService.cs
@src/DigimonBot.Messaging/Commands/KimiCommand.cs
@src/DigimonBot.Host/Program.cs

**Execution Flow from CONTEXT.md:**
```csharp
1. Parse command (remove / or ! prefix)
2. Check internal CommandRegistry
3. If not found, query CustomCommands table
4. If found, check whitelist requirement
5. If whitelisted (or no whitelist required), execute binary
6. If not found anywhere → "Unknown command"
```

**Security Requirements:**
- Path validation: binary must be within base directory
- Reject paths containing `..` or absolute paths
- Whitelist check if RequiredWhitelist = true
- Timeout protection (30s default for custom commands)
- No shell execution (direct process only)

**Process Execution Pattern:**
```csharp
var psi = new ProcessStartInfo
{
    FileName = fullPath,
    Arguments = string.Join(" ", args),
    WorkingDirectory = Path.GetDirectoryName(fullPath),
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false
};
```
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create executor interface and implementation</name>
  <files>src/DigimonBot.Core/Services/ICustomCommandExecutor.cs, src/DigimonBot.Core/Services/CustomCommandExecutor.cs</files>
  <action>
Create the custom command executor:

**Interface (ICustomCommandExecutor):**
```csharp
public interface ICustomCommandExecutor
{
    /// <summary>
    /// 执行自定义命令
    /// </summary>
    Task<ExecutionResult> ExecuteAsync(
        CustomCommand command, 
        string[] args, 
        string userId,
        int timeoutSeconds = 30);
    
    /// <summary>
    /// 验证二进制路径安全
    /// </summary>
    bool ValidatePath(string binaryPath);
}
```

**Implementation (CustomCommandExecutor):**

1. **Constructor dependencies:**
   - KimiConfig (for BasePath)
   - ILogger<CustomCommandExecutor>

2. **ExecuteAsync**:
   ```csharp
   public async Task<ExecutionResult> ExecuteAsync(
       CustomCommand command, string[] args, string userId, int timeoutSeconds = 30)
   {
       // Build full path
       var fullPath = Path.Combine(_config.Execution.BasePath, command.BinaryPath);
       
       // Security: Validate path
       if (!ValidatePath(fullPath))
       {
           return new ExecutionResult 
           { 
               Success = false, 
               Error = "非法的二进制路径" 
           };
       }
       
       // Check file exists
       if (!File.Exists(fullPath))
       {
           return new ExecutionResult 
           { 
               Success = false, 
               Error = $"找不到可执行文件: {command.BinaryPath}" 
           };
       }
       
       // Execute with timeout
       using var process = new Process
       {
           StartInfo = new ProcessStartInfo
           {
               FileName = fullPath,
               Arguments = string.Join(" ", args),
               WorkingDirectory = Path.GetDirectoryName(fullPath),
               RedirectStandardOutput = true,
               RedirectStandardError = true,
               UseShellExecute = false,
               CreateNoWindow = true
           }
       };
       
       // ... execution logic same as KimiExecutionService
       // Return ExecutionResult with output/error/exit code
   }
   ```

3. **ValidatePath**:
   ```csharp
   public bool ValidatePath(string fullPath)
   {
       // Must be within base path
       var baseFullPath = Path.GetFullPath(_config.Execution.BasePath);
       var targetFullPath = Path.GetFullPath(fullPath);
       
       // Check for directory traversal
       if (!targetFullPath.StartsWith(baseFullPath, StringComparison.OrdinalIgnoreCase))
       {
           _logger.LogWarning("路径尝试逃逸基目录: {Path}", fullPath);
           return false;
       }
       
       // Check for .. in path
       if (fullPath.Contains(".."))
       {
           return false;
       }
       
       return true;
   }
   ```

4. **ExecutionResult** (reuse existing or create new):
   - Success (bool)
   - Output (string)
   - Error (string)
   - ExitCode (int)
   - DurationMs (int)

Use ArgumentList for security if possible, otherwise validate arguments.
  </action>
  <verify>
    <automated>grep -q "ICustomCommandExecutor" src/DigimonBot.Core/Services/ICustomCommandExecutor.cs && grep -q "ValidatePath" src/DigimonBot.Core/Services/CustomCommandExecutor.cs && echo "Executor created"</automated>
  </verify>
  <done>
    - Executor service implements ExecuteAsync
    - Path validation prevents directory escape
    - Timeout protection (30s)
    - Security checks in place
  </done>
</task>

<task type="auto">
  <name>Task 2: Integrate into command routing</name>
  <files>src/DigimonBot.Messaging/Handlers/CommandRouter.cs</files>
  <action>
Find and modify the command routing/lookup logic. This is typically in:
- CommandRouter.cs
- Or DigimonMessageHandler.cs where commands are resolved

**Integration Pattern:**

1. **Locate command resolution code** - Look for where ICommand instances are looked up by name

2. **Add fallback to custom commands:**
   ```csharp
   // Existing: Check internal command
   if (_commandRegistry.TryGetCommand(commandName, out var internalCmd))
   {
       return await internalCmd.ExecuteAsync(context);
   }
   
   // NEW: Check custom commands
   var customCmd = await _customCommandRepo.GetByNameAsync(commandName);
   if (customCmd == null)
   {
       customCmd = await _customCommandRepo.GetByAliasAsync(commandName);
   }
   
   if (customCmd != null)
   {
       // Check whitelist
       if (customCmd.RequiresWhitelist && !_whitelist.Contains(context.UserId))
       {
           return new CommandResult 
           { 
               Success = false, 
               Message = "❌ 此命令需要白名单权限" 
           };
       }
       
       // Execute
       var result = await _customCommandExecutor.ExecuteAsync(
           customCmd, context.Args, context.UserId);
       
       // Update usage stats
       await _customCommandRepo.UpdateUsageAsync(customCmd.Id);
       
       // Format result
       return new CommandResult
       {
           Success = result.Success,
           Message = FormatExecutionResult(result, customCmd)
       };
   }
   
   // Unknown command
   return new CommandResult 
   { 
       Success = false, 
       Message = "❌ 未知命令" 
   };
   ```

3. **Helper method for formatting:**
   ```csharp
   private string FormatExecutionResult(ExecutionResult result, CustomCommand cmd)
   {
       var sb = new StringBuilder();
       sb.AppendLine($"🚀 **{cmd.Name}** 执行结果");
       sb.AppendLine();
       
       if (!string.IsNullOrEmpty(result.Output))
       {
           sb.AppendLine(result.Output);
       }
       
       if (!result.Success && !string.IsNullOrEmpty(result.Error))
       {
           sb.AppendLine($"❌ 错误: {result.Error}");
       }
       
       sb.AppendLine($"⏱️ 耗时: {result.DurationMs}ms");
       
       return sb.ToString();
   }
   ```

4. **Dependencies to inject:**
   - ICustomCommandRepository
   - ICustomCommandExecutor
   - KimiConfig (for whitelist)

If CommandRouter doesn't exist, modify the appropriate location in DigimonMessageHandler or wherever command lookup happens.
  </action>
  <verify>
    <automated>grep -q "ICustomCommandRepository" src/DigimonBot.Messaging/Handlers/CommandRouter.cs && grep -q "ICustomCommandExecutor" src/DigimonBot.Messaging/Handlers/CommandRouter.cs && echo "Integration complete"</automated>
  </verify>
  <done>
    - Command resolution includes custom commands
    - Whitelist checks before execution
    - Usage stats updated after execution
    - Chinese user messages
  </done>
</task>

<task type="auto">
  <name>Task 3: Register services in DI</name>
  <files>src/DigimonBot.Host/Program.cs</files>
  <action>
Register new services in Program.cs:

1. **Add repository:**
   ```csharp
   services.AddSingleton<ICustomCommandRepository, CustomCommandRepository>();
   ```

2. **Add executor:**
   ```csharp
   services.AddSingleton<ICustomCommandExecutor, CustomCommandExecutor>();
   ```

3. **Update command router/handler registration:**
   If CommandRouter is a service, inject dependencies:
   ```csharp
   services.AddSingleton<CommandRouter>(provider =>
   {
       return new CommandRouter(
           provider.GetRequiredService<CommandRegistry>(),
           provider.GetRequiredService<ICustomCommandRepository>(),
           provider.GetRequiredService<ICustomCommandExecutor>(),
           settings.Admin,  // For whitelist
           provider.GetRequiredService<ILogger<CommandRouter>>());
   });
   ```
   
   Or if it's DigimonMessageHandler that needs updating, ensure it receives the dependencies.

4. **Database initialization:**
   Add call to initialize CustomCommands table:
   ```csharp
   var dbInitializer = provider.GetRequiredService<DatabaseInitializer>();
   dbInitializer.InitializeCustomCommands();
   ```

Follow existing registration patterns for similar services.
  </action>
  <verify>
    <automated>grep -q "ICustomCommandRepository" src/DigimonBot.Host/Program.cs && grep -q "ICustomCommandExecutor" src/DigimonBot.Host/Program.cs && echo "Services registered"</automated>
  </verify>
  <done>
    - Repository registered in DI
    - Executor registered in DI
    - Command handler receives dependencies
    - Database initialization called
  </done>
</task>

<task type="auto">
  <name>Task 4: Add list commands functionality</name>
  <files>src/DigimonBot.Messaging/Commands/ListCustomCommands.cs</files>
  <action>
Create a command to list available custom commands:

```csharp
public class ListCustomCommands : ICommand
{
    private readonly ICustomCommandRepository _repository;
    
    public string Name => "customcmds";
    public string[] Aliases => new[] { "customs", "cmds" };
    public string Description => "列出所有自定义命令";
    
    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        var commands = await _repository.ListAsync();
        
        var sb = new StringBuilder();
        sb.AppendLine("📋 **自定义命令列表**");
        sb.AppendLine();
        
        foreach (var cmd in commands)
        {
            var aliasText = cmd.Aliases?.Length > 0 
                ? $" (别名: {string.Join(", ", cmd.Aliases)})" 
                : "";
            var whitelistBadge = cmd.RequiresWhitelist ? " 🔒" : "";
            
            sb.AppendLine($"• **/{cmd.Name}**{aliasText}{whitelistBadge}");
            if (!string.IsNullOrEmpty(cmd.Description))
            {
                sb.AppendLine($"  {cmd.Description}");
            }
        }
        
        if (!commands.Any())
        {
            sb.AppendLine("暂无自定义命令");
            sb.AppendLine("使用 /kimi 创建你的第一个命令！");
        }
        
        return new CommandResult 
        { 
            Success = true, 
            Message = sb.ToString() 
        };
    }
}
```

Register this command in CommandRegistry (internal command, not custom).
  </action>
  <verify>
    <automated>grep -q "class ListCustomCommands" src/DigimonBot.Messaging/Commands/ListCustomCommands.cs && echo "List command created"</automated>
  </verify>
  <done>
    - List command shows all custom commands
    - Displays aliases and whitelist status
    - Chinese user interface
  </done>
</task>

</tasks>

<verification>
After completing all tasks:
1. Build: dotnet build
2. Custom command lookup works
3. Whitelist validation works
4. Path validation prevents escapes
5. List command displays custom commands
</verification>

<success_criteria>
- Custom commands execute when invoked
- Whitelist checks work correctly
- Path validation prevents directory escape
- Execution updates usage statistics
- List command shows available commands
- Unknown commands return clear error
- All code compiles
</success_criteria>

<output>
After completion, create `.planning/phases/3-general-command-executor/3-02-SUMMARY.md`
</output>
