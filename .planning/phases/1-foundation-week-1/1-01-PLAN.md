---
phase: 01-foundation
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - src/DigimonBot.Host/Configs/KimiConfig.cs
  - src/DigimonBot.Host/Services/KimiConfigService.cs
  - src/DigimonBot.Data/Database/KimiDatabaseInitializer.cs
  - src/DigimonBot.Data/Repositories/IKimiRepositoryRepository.cs
  - src/DigimonBot.Data/Repositories/Sqlite/KimiRepositoryRepository.cs
  - Data/kimi_config.json
autonomous: true
requirements:
  - KIMI-CFG-001
  - KIMI-CFG-002
  - KIMI-INT-002
user_setup: []
must_haves:
  truths:
    - Configuration file exists with default values
    - Configuration hot-reload works when file changes
    - Database schema created for repositories and sessions
    - Repository pattern implemented for database access
  artifacts:
    - path: Data/kimi_config.json
      provides: Default configuration template
      contains: AccessControl, Execution, Output, Git sections
    - path: src/DigimonBot.Host/Configs/KimiConfig.cs
      provides: Configuration POCO classes
      min_lines: 80
    - path: src/DigimonBot.Host/Services/KimiConfigService.cs
      provides: Hot-reload implementation
      exports: [KimiConfig, CurrentConfig, OnConfigChanged]
    - path: src/DigimonBot.Data/Database/KimiDatabaseInitializer.cs
      provides: Database initialization
      exports: [Initialize, CreateConnection]
    - path: src/DigimonBot.Data/Repositories/Sqlite/KimiRepositoryRepository.cs
      provides: Repository CRUD operations
      exports: [CreateAsync, GetByNameAsync, GetAllAsync, SetActiveAsync]
  key_links:
    - from: KimiConfigService
      to: Data/kimi_config.json
      via: FileSystemWatcher
      pattern: FileSystemWatcher with debouncing
    - from: KimiRepositoryRepository
      to: KimiDatabaseInitializer
      via: CreateConnection method
      pattern: using var connection = _db.CreateConnection()
---

<objective>
Create the infrastructure foundation for the Kimi Agent Coding System - configuration management with hot-reload and database layer for repository tracking.

Purpose: Establish the core infrastructure that all other components depend on. Configuration allows runtime access control changes, and database stores repository/session metadata.
Output: Working configuration system with hot-reload + SQLite database with repository tables.
</objective>

<execution_context>
@C:/Users/MA Huan/.config/opencode/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@.planning/1-CONTEXT.md
@.planning/phases/1-foundation-week-1/1-RESEARCH.md

@src/DigimonBot.Host/Configs/AppSettings.cs
@src/DigimonBot.Host/Program.cs
@src/DigimonBot.Data/Database/DatabaseInitializer.cs
@src/DigimonBot.Data/Repositories/IUserDataRepository.cs
@src/DigimonBot.Data/Repositories/Sqlite/SqliteUserDataRepository.cs

<interfaces>
From AppSettings.cs:
```csharp
public class AppSettings
{
    public QQBotConfig QQBot { get; set; } = new();
    public AIConfig AI { get; set; } = new();
    public DataConfig Data { get; set; } = new();
    public AdminConfig Admin { get; set; } = new();
}
```

From DatabaseInitializer.cs pattern:
```csharp
public class DatabaseInitializer
{
    public DatabaseInitializer(string connectionString) { }
    public void Initialize()
    public SqliteConnection CreateConnection()
}
```

From Repository pattern:
```csharp
public interface IUserDataRepository
{
    Task<UserEconomy?> GetAsync(string userId);
    Task<UserEconomy> GetOrCreateAsync(string userId);
}
```
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create configuration classes</name>
  <files>src/DigimonBot.Host/Configs/KimiConfig.cs, Data/kimi_config.json</files>
  <action>
Create KimiConfig.cs with nested classes matching the schema from CONTEXT.md:
- KimiConfig (root)
  - AccessControlConfig (Mode, Whitelist, NonWhitelistAccess)
  - ExecutionConfig (KimiCliPath, DefaultTimeoutSeconds, MaxTimeoutSeconds, BasePath)
  - OutputConfig (DefaultMode, MaxSummaryLength, MaxMessageLength, IncludeCloneUrl)
  - GitConfig (AutoCommit, DefaultBranch)

All properties must have sensible defaults inline (e.g., `= "open"`, `= 300`).

Then create Data/kimi_config.json with the same structure, populated with default values. Include Chinese comments explaining each section using _comment properties.

Follow the exact naming and structure from CONTEXT.md section 5 (Configuration System).
  </action>
  <verify>
    <automated>cat Data/kimi_config.json | grep -q "AccessControl" && echo "Config file created"</automated>
  </verify>
  <done>
    - KimiConfig.cs exists with all nested config classes
    - All properties have inline default values
    - Data/kimi_config.json created with valid JSON and Chinese comments
  </done>
</task>

<task type="auto">
  <name>Task 2: Implement configuration service with hot-reload</name>
  <files>src/DigimonBot.Host/Services/KimiConfigService.cs</files>
  <action>
Create KimiConfigService that:
1. Loads configuration from Data/kimi_config.json on startup
2. Uses FileSystemWatcher to monitor the config file for changes
3. Implements debouncing (100ms) to handle multiple rapid change events
4. Reloads configuration when file changes, with error handling
5. Exposes CurrentConfig property for read-only access

Key implementation details from RESEARCH.md:
- Use FileSystemWatcher with Filter = "kimi_config.json"
- Set NotifyFilter = NotifyFilters.LastWrite
- Implement debouncing using DateTime comparison
- On error, log and keep using previous config (don't crash)
- Implement IDisposable to clean up watcher

Pattern to follow: Research.md "Configuration Service with Hot-Reload" code example.
  </action>
  <verify>
    <automated>grep -q "FileSystemWatcher" src/DigimonBot.Host/Services/KimiConfigService.cs && echo "FileSystemWatcher implemented"</automated>
  </verify>
  <done>
    - KimiConfigService implements IDisposable
    - FileSystemWatcher configured with debouncing
    - Configuration reloads on file change without restart
    - CurrentConfig property exposes read-only config
  </done>
</task>

<task type="auto">
  <name>Task 3: Create database initializer for kimi tables</name>
  <files>src/DigimonBot.Data/Database/KimiDatabaseInitializer.cs</files>
  <action>
Create KimiDatabaseInitializer following the existing DatabaseInitializer pattern:
1. Accept connection string in constructor
2. Implement Initialize() method that creates tables if not exist
3. Implement CreateConnection() that returns SqliteConnection

Schema from CONTEXT.md:
```sql
CREATE TABLE KimiRepositories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Path TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    LastUsedAt TEXT,
    SessionCount INTEGER DEFAULT 0
);

CREATE TABLE KimiSessions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RepoId INTEGER NOT NULL,
    UserId TEXT NOT NULL,
    Command TEXT NOT NULL,
    DurationMs INTEGER,
    Success INTEGER,
    ExecutedAt TEXT NOT NULL,
    FOREIGN KEY (RepoId) REFERENCES KimiRepositories(Id)
);

CREATE INDEX idx_kimi_sessions_repoid ON KimiSessions(RepoId);
CREATE INDEX idx_kimi_sessions_userid ON KimiSessions(UserId);
CREATE INDEX idx_kimi_sessions_executedat ON KimiSessions(ExecutedAt);
```

Use ExecuteNonQuery for table creation. Use DateTime.UtcNow.ToString("O") for timestamps.
  </action>
  <verify>
    <automated>grep -q "KimiRepositories" src/DigimonBot.Data/Database/KimiDatabaseInitializer.cs && echo "Database schema defined"</automated>
  </verify>
  <done>
    - KimiDatabaseInitializer class created
    - Initialize() creates all tables and indexes
    - CreateConnection() returns SqliteConnection
    - Uses separate connection string from main bot database
  </done>
</task>

<task type="auto">
  <name>Task 4: Implement repository layer</name>
  <files>src/DigimonBot.Data/Repositories/IKimiRepositoryRepository.cs, src/DigimonBot.Data/Repositories/Sqlite/KimiRepositoryRepository.cs</files>
  <action>
Create repository interface and implementation following existing patterns:

Interface (IKimiRepositoryRepository):
- CreateAsync(string name, string path) -> Task<KimiRepository>
- GetByNameAsync(string name) -> Task<KimiRepository?>
- GetAllAsync() -> Task<IEnumerable<KimiRepository>>
- SetActiveAsync(string name) -> Task (clears other active flags, sets this one)
- GetActiveAsync() -> Task<KimiRepository?>
- UpdateLastUsedAsync(string name) -> Task
- IncrementSessionCountAsync(string name) -> Task

Implementation (KimiRepositoryRepository):
- Inject KimiDatabaseInitializer
- Use Dapper for SQL execution
- Follow pattern from SqliteUserDataRepository.cs
- Use parameterized queries
- Handle null results gracefully

KimiRepository model:
```csharp
public class KimiRepository
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int SessionCount { get; set; }
}
```
  </action>
  <verify>
    <automated>grep -q "IKimiRepositoryRepository" src/DigimonBot.Data/Repositories/IKimiRepositoryRepository.cs && grep -q "Dapper" src/DigimonBot.Data/Repositories/Sqlite/KimiRepositoryRepository.cs && echo "Repository layer complete"</automated>
  </verify>
  <done>
    - Repository interface defines all required methods
    - Implementation uses Dapper with parameterized queries
    - SetActiveAsync correctly manages single active repo
    - All async methods use proper Task return types
  </done>
</task>

</tasks>

<verification>
After completing all tasks:
1. Verify Data/kimi_config.json is valid JSON
2. Verify KimiConfigService compiles with FileSystemWatcher
3. Verify KimiDatabaseInitializer has all SQL statements
4. Verify repository uses Dapper (check using statement)
</verification>

<success_criteria>
- Configuration system complete with hot-reload
- Database layer ready for repository operations
- All code follows existing codebase patterns
- No compilation errors
</success_criteria>

<output>
After completion, create `.planning/phases/1-foundation-week-1/1-01-SUMMARY.md`
</output>
