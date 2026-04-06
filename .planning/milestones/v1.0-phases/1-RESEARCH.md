# Phase 1 Research: Foundation Implementation

**Phase:** 1 - Foundation  
**Research Date:** 2026-04-06  
**Researcher:** Claude Code  
**Mode:** Ecosystem + Implementation  

---

## Executive Summary

This research document provides implementation guidance for Phase 1 (Foundation) of the Kimi Agent Coding System. The phase focuses on configuration management with hot-reload, database setup, repository management, and basic command handling.

**Key Findings:**
- FileSystemWatcher is the standard approach for configuration hot-reload in .NET
- Process execution with timeout requires careful async handling
- Git repository initialization is straightforward via CLI
- All patterns exist in the current codebase and should be followed closely

---

## Standard Stack

### Configuration Hot-Reload

**Technology:** `System.IO.FileSystemWatcher`  
**Confidence:** High - Built into .NET, battle-tested  

**Why this stack:**
- Native .NET support, no external dependencies
- Event-driven architecture fits well with existing bot patterns
- Works reliably across Windows, Linux, and macOS
- Used extensively in production .NET applications

**Basic Implementation Pattern:**
```csharp
using var watcher = new FileSystemWatcher("Data")
{
    Filter = "kimi_config.json",
    NotifyFilter = NotifyFilters.LastWrite
};

watcher.Changed += (sender, e) => 
{
    // Handle config reload
    ReloadConfiguration();
};

watcher.EnableRaisingEvents = true;
```

**Important Considerations:**
- FileSystemWatcher can raise multiple events for a single change (buffer overflow)
- Use debouncing (e.g., 100ms delay) to handle rapid successive events
- Always handle Error events to detect buffer overflows

### Process Execution

**Technology:** `System.Diagnostics.Process`  
**Confidence:** High - Standard .NET approach  

**Execution Pattern with Timeout:**
```csharp
using var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "kimi",
        Arguments = $"--prompt \"{message}\"",
        WorkingDirectory = repoPath,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    }
};

process.Start();

// Async wait with timeout
var completed = await Task.Run(() => 
    process.WaitForExit(timeoutMilliseconds));

if (!completed)
{
    process.Kill();
    throw new TimeoutException("Kimi CLI execution timed out");
}

var output = await process.StandardOutput.ReadToEndAsync();
var error = await process.StandardError.ReadToEndAsync();
```

### Git Repository Management

**Technology:** Git CLI via Process  
**Confidence:** High - Most reliable approach  

**Why CLI over libraries:**
- LibGit2Sharp adds heavy dependency (~10MB)
- CLI commands are well-documented and stable
- Matches existing codebase pattern (VisionService uses curl)
- Easier to debug and maintain

**Repository Creation Commands:**
```bash
git init --initial-branch=main
git config user.email "kimi@digimonbot.local"
git config user.name "Kimi Agent"
```

---

## Architecture Patterns

### Configuration Service Pattern

Based on existing `AppSettings` pattern in codebase:

1. **Configuration Class** - POCO with defaults
2. **Configuration Service** - Loads, validates, watches for changes
3. **DI Registration** - Singleton, injected into commands
4. **Hot Reload** - FileSystemWatcher updates singleton instance

**File Structure:**
```
src/DigimonBot.Host/Configs/
  ├── KimiConfig.cs              # Configuration POCO
  └── KimiConfigService.cs       # Loader + watcher

src/DigimonBot.Data/Database/
  └── KimiDatabaseInitializer.cs # Separate DB initialization
```

### Repository Management Pattern

**KimiRepositoryManager** service:
- Create repositories (git init + README)
- Track active repository (single global state)
- Switch between repositories
- List all repositories

**Database Schema:**
```sql
-- Simple tracking, no user isolation
CREATE TABLE KimiRepositories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Path TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    LastUsedAt TEXT,
    SessionCount INTEGER DEFAULT 0
);
```

### Command Pattern

Follow existing `ICommand` interface exactly:

```csharp
public class KimiCommand : ICommand
{
    private readonly KimiConfig _config;
    private readonly IKimiRepositoryManager _repoManager;
    private readonly IKimiExecutionService _executionService;
    private readonly ILogger<KimiCommand> _logger;

    public string Name => "kimi";
    public string[] Aliases => new[] { "kimichat" };
    public string Description => "Execute kimi CLI commands";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        // Check access control
        if (!HasAccess(context))
        {
            return new CommandResult 
            { 
                Success = false, 
                Message = "❌ Access denied" 
            };
        }

        // Parse arguments and execute
        // ...
    }
}
```

---

## Don't Hand-Roll

### Configuration Validation

**Don't:** Write custom JSON schema validation  
**Do:** Use `System.Text.Json` with try-catch, apply defaults for missing fields

**Rationale:**
- Simple configs don't need complex validation
- Code-based defaults are more maintainable than schema files
- Follow existing `AppSettings` pattern

### Process Timeout

**Don't:** Use `CancellationToken` with `Process.WaitForExitAsync`  
**Do:** Use `WaitForExit(int milliseconds)` in a `Task.Run` with timeout check

**Rationale:**
- Process doesn't respect CancellationToken naturally
- WaitForExit with timeout is the standard pattern
- Kill() is the only reliable way to terminate

### Git Operations

**Don't:** Use LibGit2Sharp library  
**Do:** Use git CLI via Process

**Rationale:**
- Avoids 10MB+ dependency
- CLI is more transparent and debuggable
- Matches existing codebase pattern (VisionService)

### Repository Isolation

**Don't:** Implement per-user repository isolation in Phase 1  
**Do:** Single shared workspace for all users

**Rationale:**
- Phase 1 scope is "simple open code function"
- Per-user isolation adds complexity (Phase 3+ feature)
- Collaborative workspace fits group chat use case

---

## Common Pitfalls

### 1. FileSystemWatcher Double Events

**Problem:** FileSystemWatcher often fires multiple Changed events for a single file modification.

**Solution:** Implement debouncing:
```csharp
private DateTime _lastReload = DateTime.MinValue;
private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(100);

private void OnConfigChanged(object sender, FileSystemEventArgs e)
{
    var now = DateTime.Now;
    if (now - _lastReload < _debounceInterval)
        return;
    
    _lastReload = now;
    ReloadConfiguration();
}
```

### 2. Process Deadlock with Output Streams

**Problem:** Synchronous ReadToEnd() can deadlock if output buffer fills.

**Solution:** Always use async reads:
```csharp
// Before starting process
process.Start();

// Read async to avoid deadlock
var outputTask = process.StandardOutput.ReadToEndAsync();
var errorTask = process.StandardError.ReadToEndAsync();

await Task.WhenAll(outputTask, errorTask);
```

### 3. Access Control Race Conditions

**Problem:** Config hot-reload during command execution.

**Solution:** Use immutable config snapshot:
```csharp
public async Task<CommandResult> ExecuteAsync(CommandContext context)
{
    // Capture config at execution time
    var configSnapshot = _configService.CurrentConfig;
    
    // Use snapshot for entire execution
    if (!configSnapshot.HasAccess(context.UserId))
    {
        // ...
    }
}
```

### 4. Git Repository Corruption

**Problem:** Concurrent git operations on same repo.

**Solution:** Simple locking mechanism:
```csharp
private readonly SemaphoreSlim _repoLock = new(1, 1);

public async Task ExecuteInRepoAsync(string repoPath, Func<Task> action)
{
    await _repoLock.WaitAsync();
    try
    {
        await action();
    }
    finally
    {
        _repoLock.Release();
    }
}
```

### 5. Working Directory Context

**Problem:** Process working directory doesn't change correctly.

**Solution:** Always set WorkingDirectory explicitly:
```csharp
var psi = new ProcessStartInfo
{
    WorkingDirectory = repoPath,  // Always set this
    // ...
};
```

---

## Code Examples

### Configuration Service with Hot-Reload

```csharp
public class KimiConfigService : IDisposable
{
    private readonly ILogger<KimiConfigService> _logger;
    private readonly FileSystemWatcher _watcher;
    private KimiConfig _currentConfig;
    private readonly string _configPath;
    private DateTime _lastReload = DateTime.MinValue;

    public KimiConfig CurrentConfig => _currentConfig;

    public KimiConfigService(ILogger<KimiConfigService> logger)
    {
        _logger = logger;
        _configPath = Path.Combine("Data", "kimi_config.json");
        
        LoadConfiguration();
        SetupWatcher();
    }

    private void SetupWatcher()
    {
        _watcher = new FileSystemWatcher("Data", "kimi_config.json")
        {
            NotifyFilter = NotifyFilters.LastWrite
        };
        
        _watcher.Changed += OnConfigChanged;
        _watcher.EnableRaisingEvents = true;
    }

    private void OnConfigChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: ignore events within 100ms
        var now = DateTime.Now;
        if (now - _lastReload < TimeSpan.FromMilliseconds(100))
            return;
        
        _lastReload = now;
        
        _logger.LogInformation("[KimiConfig] Configuration file changed, reloading...");
        LoadConfiguration();
    }

    private void LoadConfiguration()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                _currentConfig = new KimiConfig(); // Use defaults
                return;
            }

            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<KimiConfig>(json);
            _currentConfig = config ?? new KimiConfig();
            
            _logger.LogInformation("[KimiConfig] Configuration loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[KimiConfig] Failed to load configuration, using defaults");
            _currentConfig = new KimiConfig();
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
```

### Process Execution with Timeout

```csharp
public class KimiExecutionService : IKimiExecutionService
{
    private readonly ILogger<KimiExecutionService> _logger;

    public async Task<ExecutionResult> ExecuteAsync(
        string repoPath, 
        string arguments, 
        int timeoutSeconds = 300)
    {
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

        _logger.LogInformation("[KimiExecution] Starting: kimi {Args} in {Path}", 
            arguments, repoPath);

        var stopwatch = Stopwatch.StartNew();
        process.Start();

        // Read output asynchronously to avoid deadlock
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        // Wait with timeout
        var timeoutMs = timeoutSeconds * 1000;
        var completed = await Task.Run(() => process.WaitForExit(timeoutMs));

        stopwatch.Stop();

        if (!completed)
        {
            _logger.LogWarning("[KimiExecution] Process timed out after {Timeout}s", timeoutSeconds);
            process.Kill();
            
            return new ExecutionResult
            {
                Success = false,
                Error = $"Execution timed out after {timeoutSeconds} seconds",
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }

        var output = await outputTask;
        var error = await errorTask;

        _logger.LogInformation("[KimiExecution] Completed in {Duration}ms", 
            stopwatch.ElapsedMilliseconds);

        return new ExecutionResult
        {
            Success = process.ExitCode == 0,
            Output = output,
            Error = error,
            ExitCode = process.ExitCode,
            DurationMs = (int)stopwatch.ElapsedMilliseconds
        };
    }
}
```

### Git Repository Initialization

```csharp
public class KimiRepositoryManager : IKimiRepositoryManager
{
    private readonly string _basePath;
    private readonly ILogger<KimiRepositoryManager> _logger;

    public async Task<RepositoryInfo> CreateRepositoryAsync(string? name = null)
    {
        var repoName = name ?? $"kimi-{DateTime.Now:yyyyMMdd-HHmmss}";
        var repoPath = Path.Combine(_basePath, repoName);

        if (Directory.Exists(repoPath))
        {
            throw new InvalidOperationException($"Repository '{repoName}' already exists");
        }

        Directory.CreateDirectory(repoPath);

        // Initialize git repository
        await ExecuteGitCommandAsync(repoPath, "init --initial-branch=main");
        
        // Configure git
        await ExecuteGitCommandAsync(repoPath, "config user.email \"kimi@digimonbot.local\"");
        await ExecuteGitCommandAsync(repoPath, "config user.name \"Kimi Agent\"");

        // Create README
        var readmeContent = $"""# {repoName}

Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
Repository for Kimi agent collaboration.
""";
        await File.WriteAllTextAsync(Path.Combine(repoPath, "README.md"), readmeContent);

        _logger.LogInformation("[KimiRepo] Created repository: {Name} at {Path}", 
            repoName, repoPath);

        return new RepositoryInfo
        {
            Name = repoName,
            Path = repoPath,
            CreatedAt = DateTime.Now
        };
    }

    private async Task ExecuteGitCommandAsync(string workingDir, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Git command failed: {error}");
        }
    }
}
```

---

## Access Control Implementation

Based on requirements from CONTEXT.md:

```csharp
public class KimiAccessControl
{
    private readonly KimiConfig _config;

    public KimiAccessControl(KimiConfig config)
    {
        _config = config;
    }

    public bool CanExecute(string userId)
    {
        // Whitelist always grants full access
        if (_config.AccessControl.Whitelist.Contains(userId))
            return true;

        // Open mode = everyone has full access
        if (_config.AccessControl.Mode == "open")
            return true;

        // Whitelist mode: non-whitelist users restricted
        if (_config.AccessControl.Mode == "whitelist")
        {
            return _config.AccessControl.NonWhitelistAccess == "read-only" 
                ? false  // read-only can't execute
                : false; // restricted can't do anything
        }

        return false;
    }

    public bool CanRead(string userId)
    {
        // Whitelist always grants access
        if (_config.AccessControl.Whitelist.Contains(userId))
            return true;

        // Open mode = everyone
        if (_config.AccessControl.Mode == "open")
            return true;

        // Whitelist mode
        if (_config.AccessControl.Mode == "whitelist")
        {
            return _config.AccessControl.NonWhitelistAccess != "restricted";
        }

        return false;
    }
}
```

---

## Dependencies

### No New NuGet Packages Required

All functionality uses built-in .NET 8 libraries:
- `System.IO.FileSystem.Watcher` - Configuration hot-reload
- `System.Diagnostics.Process` - CLI execution
- `System.Text.Json` - Configuration serialization (already used)
- `Microsoft.Data.Sqlite` - Database (already used)
- `Dapper` - Database access (already used)

### External Tools Required

- **kimi CLI** - Must be installed on server
- **Git** - Must be installed on server

### Validation Checklist

Before deployment:
- [ ] `kimi --version` works in terminal
- [ ] `git --version` works in terminal
- [ ] Write permissions to `./kimi-workspace` directory

---

## Security Considerations

### Argument Injection Prevention

**Problem:** User input in kimi arguments could be dangerous  
**Solution:** Validate arguments against whitelist:

```csharp
private readonly HashSet<string> _allowedArgs = new(StringComparer.OrdinalIgnoreCase)
{
    "--prompt", "-p",
    "--command", "-c",
    "--model", "-m",
    "--yolo", "-y",
    "--plan",
    "--thinking",
    "--verbose",
    // ... etc
};

public bool IsArgumentAllowed(string arg)
{
    return _allowedArgs.Contains(arg.Split('=')[0]);
}
```

### Path Traversal Prevention

**Problem:** `--add-dir` could access sensitive directories  
**Solution:** Validate all paths are within workspace:

```csharp
public bool IsPathSafe(string path, string workspaceRoot)
{
    var fullPath = Path.GetFullPath(path);
    var workspaceFullPath = Path.GetFullPath(workspaceRoot);
    
    return fullPath.StartsWith(workspaceFullPath, StringComparison.OrdinalIgnoreCase);
}
```

---

## Performance Considerations

### FileSystemWatcher Buffer Size

Default buffer is 8KB. For high-frequency changes, increase:
```csharp
_watcher.InternalBufferSize = 8192 * 2; // 16KB
```

### Process Execution Limits

- Max concurrent executions: 3 (configurable)
- Queue additional requests
- Timeout: 300s default, 600s max

### Database Connection Pooling

SQLite has limited concurrency. Use connection pooling:
```csharp
// In connection string:
// Data Source=Data/kimi_data.db;Pooling=true;Max Pool Size=10
```

---

## Testing Strategy

### Unit Tests

- Configuration validation
- Access control logic
- Argument parsing
- Path sanitization

### Integration Tests

- Repository creation
- Process execution with timeout
- Config hot-reload
- Database operations

### Manual Testing

- Execute `/kimi --help`
- Create repository with `/kimi --new-repo test`
- Switch repos with `/kimi --switch-repo test`
- Run kimi command in repo
- Modify config file, verify hot-reload

---

## References

1. [FileSystemWatcher Class](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher)
2. [Process Class](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process)
3. [git-init Documentation](https://git-scm.com/docs/git-init)
4. [Kimi CLI Documentation](https://moonshotai.github.io/kimi-cli/en/reference/kimi-command.html)
5. [DigimonBot Codebase Analysis](../codebase/ARCHITECTURE.md)

---

## Research Confidence

| Area | Confidence | Notes |
|------|------------|-------|
| FileSystemWatcher | High | Standard .NET pattern, well-documented |
| Process Execution | High | Existing codebase pattern (VisionService) |
| Git CLI | High | Standard approach, no edge cases expected |
| Database Schema | High | Simple schema, follows existing patterns |
| Access Control | High | Clear requirements from CONTEXT.md |
| Kimi CLI Args | High | From official documentation |

---

## RESEARCH COMPLETE

**Status:** Ready for planning  
**Next Step:** Run `/gsd-plan-phase 1` to create implementation tasks  

**Key Deliverables for Planning:**
1. Configuration service with hot-reload (FileSystemWatcher)
2. Database layer (SQLite + Dapper, separate file)
3. Repository manager (git CLI, single shared workspace)
4. Execution service (Process with timeout)
5. Command handler (ICommand pattern)
6. Access control (three-tier model)

All implementation patterns are validated against existing codebase and official documentation.
