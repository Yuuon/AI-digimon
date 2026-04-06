---
phase: 03-general-command-executor
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - src/DigimonBot.Data/Database/DatabaseInitializer.cs
  - src/DigimonBot.Data/Repositories/ICustomCommandRepository.cs
  - src/DigimonBot.Data/Repositories/Sqlite/CustomCommandRepository.cs
  - src/DigimonBot.Core/Models/CustomCommand.cs
autonomous: true
requirements:
  - KIMI-INT-002
user_setup: []
must_haves:
  truths:
    - CustomCommands table exists in database
    - Repository provides CRUD operations
    - Duplicate detection works for names and aliases
    - Query by name and alias functions work
  artifacts:
    - path: src/DigimonBot.Core/Models/CustomCommand.cs
      provides: Entity model
      exports: [CustomCommand]
    - path: src/DigimonBot.Data/Repositories/ICustomCommandRepository.cs
      provides: Repository interface
      exports: [CreateAsync, GetByNameAsync, GetByAliasAsync, ListAsync, UpdateUsageAsync]
    - path: src/DigimonBot.Data/Repositories/Sqlite/CustomCommandRepository.cs
      provides: SQLite implementation
      min_lines: 150
  key_links:
    - from: CustomCommandRepository
      to: DatabaseInitializer
      via: CreateConnection
      pattern: using var connection = _db.CreateConnection()
    - from: GetByAliasAsync
      to: CustomCommands table
      via: JSON extraction query
      pattern: json_each for alias matching
---

<objective>
Create the database infrastructure for custom commands - table schema, entity model, and repository layer for CRUD operations.

Purpose: Store custom command metadata (name, aliases, binary path, whitelist requirement) that AI agents write and .NET program reads.
Output: Working database layer with duplicate detection and alias resolution.
</objective>

<execution_context>
@C:/Users/MA Huan/.config/opencode/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@.planning/3-CONTEXT.md
@src/DigimonBot.Data/Database/DatabaseInitializer.cs
@src/DigimonBot.Data/Repositories/Sqlite/KimiRepositoryRepository.cs

**Database Schema from CONTEXT.md:**
```sql
CREATE TABLE CustomCommands (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Aliases TEXT,                           -- JSON array
    BinaryPath TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    RequiresWhitelist INTEGER DEFAULT 0,
    Description TEXT,
    CreatedAt TEXT NOT NULL,
    LastUsedAt TEXT,
    UseCount INTEGER DEFAULT 0
);

CREATE INDEX idx_custom_commands_name ON CustomCommands(Name);
CREATE INDEX idx_custom_commands_aliases ON CustomCommands(Aliases);
```

**Entity Model:**
```csharp
public class CustomCommand
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public string BinaryPath { get; set; } = "";
    public string OwnerUserId { get; set; } = "";
    public bool RequiresWhitelist { get; set; }
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int UseCount { get; set; }
}
```

**Key Requirements:**
- Aliases stored as JSON array in SQLite
- Duplicate detection: check both Name AND all Aliases
- Query by alias requires JSON extraction
- Usage tracking: LastUsedAt, UseCount
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create CustomCommand entity model</name>
  <files>src/DigimonBot.Core/Models/CustomCommand.cs</files>
  <action>
Create the CustomCommand entity model class:

```csharp
namespace DigimonBot.Core.Models;

public class CustomCommand
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public string BinaryPath { get; set; } = "";
    public string OwnerUserId { get; set; } = "";
    public bool RequiresWhitelist { get; set; }
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int UseCount { get; set; }
}
```

Add XML documentation comments in Chinese:
```csharp
/// <summary>
/// 自定义命令实体
/// </summary>
```
  </action>
  <verify>
    <automated>grep -q "class CustomCommand" src/DigimonBot.Core/Models/CustomCommand.cs && echo "Entity created"</automated>
  </verify>
  <done>
    - CustomCommand class with all properties
    - Aliases as string[] array
    - XML documentation in Chinese
  </done>
</task>

<task type="auto">
  <name>Task 2: Create repository interface</name>
  <files>src/DigimonBot.Data/Repositories/ICustomCommandRepository.cs</files>
  <action>
Create the repository interface:

```csharp
namespace DigimonBot.Data.Repositories;

public interface ICustomCommandRepository
{
    /// <summary>
    /// 创建自定义命令
    /// </summary>
    Task<CustomCommand> CreateAsync(CustomCommand command);
    
    /// <summary>
    /// 根据名称获取命令
    /// </summary>
    Task<CustomCommand?> GetByNameAsync(string name);
    
    /// <summary>
    /// 根据别名获取命令
    /// </summary>
    Task<CustomCommand?> GetByAliasAsync(string alias);
    
    /// <summary>
    /// 检查名称或别名是否已存在
    /// </summary>
    Task<bool> ExistsAsync(string name, string[]? aliases = null);
    
    /// <summary>
    /// 列出所有命令
    /// </summary>
    Task<IEnumerable<CustomCommand>> ListAsync();
    
    /// <summary>
    /// 更新使用统计
    /// </summary>
    Task UpdateUsageAsync(int id);
    
    /// <summary>
    /// 删除命令
    /// </summary>
    Task DeleteAsync(int id);
}
```

Note: ExistsAsync checks both name and aliases for duplicates.
  </action>
  <verify>
    <automated>grep -q "ICustomCommandRepository" src/DigimonBot.Data/Repositories/ICustomCommandRepository.cs && echo "Interface created"</automated>
  </verify>
  <done>
    - All CRUD operations defined
    - Duplicate detection method
    - Chinese XML documentation
  </done>
</task>

<task type="auto">
  <name>Task 3: Implement SQLite repository</name>
  <files>src/DigimonBot.Data/Repositories/Sqlite/CustomCommandRepository.cs</files>
  <action>
Implement the SQLite repository with Dapper:

Key implementation details:

1. **Constructor**: Inject DatabaseInitializer (same pattern as KimiRepositoryRepository)

2. **CreateAsync**:
   - Serialize Aliases array to JSON: `JsonSerializer.Serialize(command.Aliases)`
   - Insert with parameterized query
   - Return created entity with ID

3. **GetByNameAsync**:
   - Simple SELECT by Name
   - Deserialize Aliases JSON to string[]

4. **GetByAliasAsync** (Tricky - requires JSON extraction):
   ```sql
   SELECT * FROM CustomCommands 
   WHERE json_array_contains(Aliases, @Alias)
   ```
   Or use LIKE query as fallback for older SQLite:
   ```sql
   SELECT * FROM CustomCommands 
   WHERE Aliases LIKE '%' || @Alias || '%'
   ```

5. **ExistsAsync**:
   - Check Name: `SELECT COUNT(*) FROM CustomCommands WHERE Name = @Name`
   - Check each alias with GetByAliasAsync
   - Return true if any match found

6. **UpdateUsageAsync**:
   ```sql
   UPDATE CustomCommands 
   SET UseCount = UseCount + 1, LastUsedAt = @Now 
   WHERE Id = @Id
   ```

7. **JSON Serialization**:
   - Use System.Text.Json for Aliases field
   - Handle null/empty arrays gracefully

Use existing patterns from SqliteUserDataRepository and KimiRepositoryRepository.
  </action>
  <verify>
    <automated>grep -q "CustomCommandRepository" src/DigimonBot.Data/Repositories/Sqlite/CustomCommandRepository.cs && grep -q "GetByAliasAsync" src/DigimonBot.Data/Repositories/Sqlite/CustomCommandRepository.cs && echo "Repository implemented"</automated>
  </verify>
  <done>
    - All interface methods implemented
    - JSON serialization for Aliases
    - Alias lookup works
    - Duplicate detection functional
  </done>
</task>

<task type="auto">
  <name>Task 4: Add database migration</name>
  <files>src/DigimonBot.Data/Database/DatabaseInitializer.cs</files>
  <action>
Add CustomCommands table creation to DatabaseInitializer:

1. Add new method or extend Initialize():
```csharp
public void InitializeCustomCommands()
{
    using var connection = CreateConnection();
    
    const string sql = @"
        CREATE TABLE IF NOT EXISTS CustomCommands (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL UNIQUE,
            Aliases TEXT,
            BinaryPath TEXT NOT NULL,
            OwnerUserId TEXT NOT NULL,
            RequiresWhitelist INTEGER DEFAULT 0,
            Description TEXT,
            CreatedAt TEXT NOT NULL,
            LastUsedAt TEXT,
            UseCount INTEGER DEFAULT 0
        );
        
        CREATE INDEX IF NOT EXISTS idx_custom_commands_name 
        ON CustomCommands(Name);
        
        CREATE INDEX IF NOT EXISTS idx_custom_commands_aliases 
        ON CustomCommands(Aliases);
    ";
    
    connection.Execute(sql);
}
```

2. Call this method in Program.cs during startup (similar to existing Initialize() call)

3. Ensure this runs after the main DatabaseInitializer.Initialize()
  </action>
  <verify>
    <automated>grep -q "CustomCommands" src/DigimonBot.Data/Database/DatabaseInitializer.cs && echo "Migration added"</automated>
  </verify>
  <done>
    - CustomCommands table schema added
    - Indexes created
    - Integration point in Program.cs ready
  </done>
</task>

</tasks>

<verification>
After completing all tasks:
1. Build: dotnet build
2. Verify CustomCommand model compiles
3. Verify repository uses Dapper
4. Check JSON serialization for Aliases
</verification>

<success_criteria>
- CustomCommand entity exists
- Repository implements all CRUD operations
- Duplicate detection works for names and aliases
- Database migration creates table with indexes
- All code compiles
</success_criteria>

<output>
After completion, create `.planning/phases/3-general-command-executor/3-01-SUMMARY.md`
</output>
