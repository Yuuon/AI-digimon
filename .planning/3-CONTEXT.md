# Phase 3: General Command Executor - Implementation Context

## Phase Overview

**Phase:** 3 - General Command Executor  
**Goal:** Enable dynamic command execution from user-created binaries  
**Status:** Ready for planning  
**Last Updated:** 2026-04-06  

---

## Locked Decisions

### 1. Registration Authority & Flow

**Decision:** Registration is NOT handled by the .NET program

**Architecture:**
```
User → /kimi "create a command that..." → Kimi CLI (AI Agent)
                                           ↓
                                    [Writes code, builds binary]
                                           ↓
                                    [SKILL file guides DB manipulation]
                                           ↓
                                    SQLite Database (direct access)
                                           ↓
User → /newcmd → .NET Program → Lookup → Execute Binary
```

**Key Points:**
- .NET program is **execution-only** for custom commands
- Registration happens via AI agent (kimi CLI) manipulating database directly
- SKILL file provides guidance to AI agent on registration requirements
- Not all /kimi requests create commands - only when explicitly requested

**SKILL File Responsibility:**
- Guide AI to check for name duplication (internal + registered)
- Guide AI to write to CustomCommands table
- Guide AI to store binary in appropriate location
- Guide AI to validate command name format

---

### 2. Command Resolution Strategy

**Decision:** AI agent handles duplication prevention

**Rules:**
- AI agent MUST check against:
  1. All internal commands (ICommand implementations)
  2. All existing custom commands (CustomCommands table)
- AI agent rejects if any conflict found
- Command stored as **pure name** (no / or ! prefix)
- At runtime, bot prefix (/ or !) added by execution layer

**Execution Resolution Flow:**
```csharp
1. Parse command text (remove / or ! prefix)
2. Check internal CommandRegistry
3. If not found, query CustomCommands table
4. If found, check whitelist requirement
5. If whitelisted (or no whitelist required), execute binary
6. If not found anywhere → "Unknown command"
```

---

### 3. Binary Storage Location

**Decision:** Store in project repository (kimi workspace)

**Storage Pattern:**
- Binary path stored in database as relative path
- Base path: `{KimiConfig.Execution.BasePath}/{repoName}/`
- Binary location: `{repoName}/bin/{commandName}` or `{repoName}/{commandName}`
- AI agent decides exact location within repo

**Example:**
```
kimi-workspace/
├── my-hello-command/
│   ├── src/
│   ├── bin/
│   │   └── hello                    ← Binary
│   └── .git/
└── another-command/
    └── awesome-tool                 ← Binary
```

**Security:**
- .NET program validates binary path is within allowed directory
- Prevents execution of system binaries (/bin/sh, C:\Windows\System32\ etc.)

---

### 4. Database Schema

**Decision:** Simplified schema focused on execution

**Table: CustomCommands**
```sql
CREATE TABLE CustomCommands (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,              -- Pure name (no / or !)
    Aliases TEXT,                           -- JSON array ["alias1", "alias2"]
    BinaryPath TEXT NOT NULL,               -- Relative path from base
    OwnerUserId TEXT NOT NULL,              -- Who created it
    RequiresWhitelist INTEGER DEFAULT 0,    -- 0=false, 1=true
    Description TEXT,                       -- Help text
    CreatedAt TEXT NOT NULL,                -- ISO 8601
    LastUsedAt TEXT,                        -- ISO 8601
    UseCount INTEGER DEFAULT 0
);

CREATE INDEX idx_custom_commands_name ON CustomCommands(Name);
CREATE INDEX idx_custom_commands_aliases ON CustomCommands(Aliases); -- SQLite supports JSON indexing in recent versions
```

**Entity Model:**
```csharp
public class CustomCommand
{
    public int Id { get; set; }
    public string Name { get; set; } = "";              // e.g., "hello"
    public string[] Aliases { get; set; } = Array.Empty<string>();  // e.g., ["hi", "greet"]
    public string BinaryPath { get; set; } = "";        // e.g., "my-hello-command/bin/hello"
    public string OwnerUserId { get; set; } = "";
    public bool RequiresWhitelist { get; set; }
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int UseCount { get; set; }
}
```

**Notes:**
- `Aliases` stored as JSON array in SQLite (TEXT field)
- `Name` is unique and primary lookup key
- Duplication check must validate both Name AND all Aliases
- Whitelist requirement is binary (true/false)

---

## AI Agent SKILL File (For Reference)

**File:** `.claude/skills/register-custom-command/SKILL.md` (Separate from this project)

**Purpose:** Guide AI agent when user requests command creation

**Key Instructions for AI:**
1. Check for name conflicts against:
   - Internal commands: status, shop, inventory, kimi, help, etc.
   - Existing custom commands (query CustomCommands table)
2. Validate name format:
   - Alphanumeric + hyphens only
   - No spaces, no special characters
   - Length: 2-32 characters
3. Build binary and place in repository
4. Register in database with full metadata
5. Provide user with usage instructions

---

## Code Context

### Files to Create

1. **Database Layer:**
   - `src/DigimonBot.Data/Repositories/ICustomCommandRepository.cs`
   - `src/DigimonBot.Data/Repositories/Sqlite/CustomCommandRepository.cs`
   - Migration: Add CustomCommands table

2. **Execution Service:**
   - `src/DigimonBot.Core/Services/ICustomCommandExecutor.cs`
   - `src/DigimonBot.Core/Services/CustomCommandExecutor.cs`

3. **Command Integration:**
   - Modify `src/DigimonBot.Messaging/Handlers/MessageRouter.cs` or similar
   - Add custom command lookup to command resolution pipeline

4. **Configuration:**
   - Add to `KimiConfig.Repositories`: `CustomCommandsDbPath`

### Files to Modify

1. **Database Initialization:**
   - `src/DigimonBot.Data/Database/DatabaseInitializer.cs` - Add CustomCommands table creation

2. **Service Registration:**
   - `src/DigimonBot.Host/Program.cs` - Register repository and executor

3. **Command Resolution:**
   - Existing command router/handler - Add fallback to custom commands

### Patterns to Follow

1. **Repository Pattern:** Same as `KimiRepositoryRepository`
2. **Process Execution:** Same as `KimiExecutionService` (VisionService pattern)
3. **Access Control:** Same as `KimiCommand` whitelist checking

---

## Execution Flow

### User Input
```
User types: /hello-world
```

### Resolution Steps
```csharp
// 1. Parse command
var cmdText = "/hello-world";
var cmdName = cmdText.TrimStart('/', '!'); // "hello-world"

// 2. Check internal commands
if (_commandRegistry.TryGetCommand(cmdName, out var internalCmd))
{
    return await internalCmd.ExecuteAsync(context);
}

// 3. Check custom commands
var customCmd = await _customCommandRepo.GetByNameAsync(cmdName);
if (customCmd == null)
{
    // Check aliases
    customCmd = await _customCommandRepo.GetByAliasAsync(cmdName);
}

if (customCmd != null)
{
    // 4. Check whitelist
    if (customCmd.RequiresWhitelist && !IsWhitelisted(context.UserId))
    {
        return new CommandResult 
        { 
            Success = false, 
            Message = "❌ 此命令需要白名单权限" 
        };
    }
    
    // 5. Execute binary
    return await _customCommandExecutor.ExecuteAsync(customCmd, context);
}

// 6. Unknown command
return new CommandResult 
{ 
    Success = false, 
    Message = "❌ 未知命令" 
};
```

### Binary Execution
```csharp
var fullPath = Path.Combine(_basePath, customCmd.BinaryPath);

// Security: Validate path is within base directory
if (!IsPathSafe(fullPath, _basePath))
{
    throw new SecurityException("Invalid binary path");
}

// Execute with arguments
var result = await _processExecutor.ExecuteAsync(
    fileName: fullPath,
    arguments: context.Args,
    workingDirectory: Path.GetDirectoryName(fullPath),
    timeoutSeconds: 30
);

// Update usage stats
await _customCommandRepo.UpdateUsageAsync(customCmd.Id);
```

---

## Security Considerations

1. **Path Traversal Prevention:**
   - Validate binary path doesn't escape base directory
   - Reject paths containing `..` or absolute paths

2. **Permission Checks:**
   - Whitelist validation before execution
   - Owner can always execute their own commands

3. **Resource Limits:**
   - Timeout for custom commands (shorter than kimi - 30s default)
   - Max output size to prevent chat flooding

4. **No Shell Execution:**
   - Direct binary execution only
   - No shell interpretation of arguments

---

## Deferred to Future

- Command versioning/updating
- Command sharing between users
- Command marketplace/discovery
- Auto-completion for custom commands
- Command usage analytics dashboard

---

## Success Criteria

- [ ] Custom commands execute when invoked
- [ ] Whitelist checks work correctly
- [ ] Name/alias duplication prevented
- [ ] Binary path validation prevents escapes
- [ ] Usage stats tracked
- [ ] Unknown commands return clear error
- [ ] Integrates seamlessly with existing command system

---

## Integration Points

### With Existing System:
- Uses same database (SQLite + Dapper)
- Uses same process execution pattern
- Uses same whitelist mechanism
- Uses same logging infrastructure

### With AI Agent:
- SKILL file guides registration (external)
- AI writes directly to database
- .NET reads from database
- No direct communication channel needed

---

*Context created for Phase 3: General Command Executor*
*Downstream agents: Implement execution layer only - registration is AI-agent managed*