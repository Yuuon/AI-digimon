---
name: custom-command-registration
description: 注册新的自定义命令到自定义命令数据库
---

# SKILL: Register Custom Command

## Objective
Guide users through creating and registering custom commands for the QQ bot.

## When to Use
When the user explicitly asks to:
- "Create a command that..."
- "Make a bot command for..."
- "Add a new command..."
- Build a tool/binary they want accessible via chat

## When NOT to Use
- General coding tasks without command registration
- One-time scripts
- Code that shouldn't be accessible to all chat users

## Registration Workflow

### Step 1: Validate Request
- Ensure user wants a reusable chat command
- Understand what the command should do
- Confirm it's appropriate for group chat use

### Step 2: Check for Duplicates
**CRITICAL:** Before registering, check ALL existing commands:

1. **Internal Commands** (CANNOT override):
   - status, shop, inventory, help, kimi, kimichat, jrrp, checkin, attack, reset,
     evolution-path, evolution-list, evolution-select, whatisthis, customcmds, customs, cmds,
     tavern-toggle, tavern-characters, tavern-load, tavern-chat, check-monitor,
     reload-tavern, special-focus, reload-personality, reload-dialogue, set-emotion
   - Check src/DigimonBot.Messaging/Commands/ folder for the complete list

2. **Existing Custom Commands** (query database):
   ```sql
   SELECT Name, Aliases FROM CustomCommands;
   ```

3. **Validate Name Format:**
   - Alphanumeric + hyphens only: `^[a-zA-Z0-9-]+$`
   - No spaces, no special characters
   - Length: 2-32 characters
   - Examples: `hello-world`, `weather-check`, `roll-dice`

4. **Check Aliases** (if provided):
   - Same format rules as name
   - Must not conflict with any existing name or alias

### Step 3: Build the Binary
- Create code in user's repository
- Compile/build to produce executable
- Test that it works
- Place binary in appropriate location (e.g., `repo-name/bin/command-name`)

### Step 4: Register in Database
**Database:** `Data/kimi_data.db` (SQLite)

**SQL Insert:**
```sql
INSERT INTO CustomCommands (
    Name, 
    Aliases, 
    BinaryPath, 
    OwnerUserId, 
    RequiresWhitelist, 
    Description, 
    CreatedAt, 
    UseCount
) VALUES (
    'command-name',                          -- Name
    '["alias1", "alias2"]',                  -- Aliases as JSON array (or '[]' if none)
    'repo-name/bin/command-name',            -- Relative path to binary
    '123456789',                             -- User's QQ number
    0,                                       -- 0=false, 1=true for whitelist
    'Description of what this command does', -- Help text
    '2026-04-06T14:30:00',                   -- ISO 8601 timestamp
    0                                        -- Initial use count
);
```

### Step 5: Confirm Registration
- Verify entry in database
- Show user how to use: `/<command-name>`
- Explain whitelist requirement if enabled
- Provide alias information

## Validation Rules

### Name Validation
```
VALID:   hello, my-command, test123, roll-dice
INVALID: hello world, my_command, test!, 123 (too short), /hello (has prefix)
```

### Duplicate Prevention
**Query to check existing:**
```sql
-- Check name
SELECT COUNT(*) FROM CustomCommands WHERE Name = 'proposed-name';

-- Check aliases (JSON search)
SELECT * FROM CustomCommands WHERE Aliases LIKE '%"proposed-alias"%';
```

If any count > 0, REJECT and ask user for different name.

### Path Validation
- Path must be relative to kimi workspace
- Use forward slashes: `repo-name/bin/command`
- No absolute paths
- No parent directory references (`..`)

## Security Guidelines

### Binary Safety
- Build from source code user can review
- Don't download pre-built binaries from internet
- Test execution before registration

### Permission Levels
**RequiresWhitelist = 0 (Open):**
- Anyone can use
- Good for: games, utilities, fun commands

**RequiresWhitelist = 1 (Restricted):**
- Only whitelisted users can use
- Good for: administrative tools, resource-intensive commands
- Explain to user if unsure

## Examples

### Example 1: Simple Hello Command

**User Request:** "Create a command that says hello back"

**Steps:**
1. Check duplicates: "hello" not taken
2. Create `hello-world/src/hello.c`:
   ```c
   #include <stdio.h>
   int main() {
       printf("Hello from bot!\n");
       return 0;
   }
   ```
3. Build: `gcc -o bin/hello src/hello.c`
4. Register:
   ```sql
   INSERT INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, 
       RequiresWhitelist, Description, CreatedAt, UseCount)
   VALUES ('hello', '["hi"]', 'hello-world/bin/hello', '123456789', 0, 
       'Say hello back', '2026-04-06T14:30:00', 0);
   ```
5. Confirm: "Command registered! Use /hello or /hi"

### Example 2: Weather Check (with API)

**User Request:** "Create a command to check weather"

**Analysis:** Requires API key - explain security concerns

**Steps:**
1. Discuss: "This needs an API key. Should be restricted?"
2. If user confirms, set RequiresWhitelist = 1
3. Create command with config file for API key
4. Register with whitelist requirement

## Common Issues

### "Name already taken"
- Query existing: `SELECT Name FROM CustomCommands;`
- Suggest alternatives
- Offer to use alias instead

### "Binary not found"
- Verify path is correct
- Check file permissions (executable)
- Ensure path is relative

### "Registration failed"
- Check database is not locked
- Verify SQL syntax
- Ensure all required fields provided

## Database Access

**SQLite CLI:**
```bash
sqlite3 Data/kimi_data.db
```

**Common Queries:**
```sql
-- List all custom commands
SELECT Name, Aliases FROM CustomCommands;

-- Check specific command
SELECT * FROM CustomCommands WHERE Name = 'command-name';

-- Delete command (if needed)
DELETE FROM CustomCommands WHERE Name = 'command-name';
```

## Notes

- Registration is permanent until manually deleted
- Owner can always use their own commands
- Usage stats tracked automatically by bot
- Description shown in `/customcmds` list

---

# SKILL: SQLite3 CLI Usage

## Overview

On the production machine `sqlite3` is installed and is the primary tool for direct database
operations. The bot uses two SQLite databases:

| Database | Path | Contents |
|---|---|---|
| Main data | `Data/digimon_data.db` | Digimon, users, evolution |
| Kimi data | `Data/kimi_data.db` | Custom commands, sessions, repositories |

## Opening a Database

```bash
# Open interactively
sqlite3 Data/kimi_data.db

# Run a single statement and exit (preferred for scripting)
sqlite3 Data/kimi_data.db "SELECT * FROM CustomCommands;"

# Run a SQL file
sqlite3 Data/kimi_data.db < changes.sql
```

## Useful Dot-Commands (Interactive Mode)

```
.tables               -- list all tables
.schema               -- show CREATE statements for all tables
.schema CustomCommands -- show CREATE for one table
.headers on           -- show column names in output
.mode column          -- align output in columns
.mode csv             -- output as CSV
.quit                 -- exit
```

Set these at the start of any interactive session for readable output:
```
sqlite3 Data/kimi_data.db
.headers on
.mode column
```

## CRUD Operations

### CREATE — Insert a row

```sql
INSERT INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('my-cmd', '["mc"]', 'my-repo/bin/my-cmd', '123456789', 0, 'Does something useful', datetime('now'), 0);
```

Use `datetime('now')` for the current UTC timestamp instead of a hardcoded string.

### READ — Query rows

```sql
-- All rows, all columns
SELECT * FROM CustomCommands;

-- Specific columns
SELECT Name, Aliases, Description FROM CustomCommands;

-- Filter by column value
SELECT * FROM CustomCommands WHERE Name = 'my-cmd';

-- Search inside JSON alias array
SELECT * FROM CustomCommands WHERE Aliases LIKE '%"mc"%';

-- Count rows
SELECT COUNT(*) FROM CustomCommands;

-- Sort results
SELECT Name, UseCount FROM CustomCommands ORDER BY UseCount DESC;
```

### UPDATE — Modify a row

```sql
-- Update a single field
UPDATE CustomCommands SET Description = 'Updated description' WHERE Name = 'my-cmd';

-- Update multiple fields at once
UPDATE CustomCommands SET BinaryPath = 'new-repo/bin/my-cmd', RequiresWhitelist = 1 WHERE Name = 'my-cmd';

-- Increment a counter
UPDATE CustomCommands SET UseCount = UseCount + 1 WHERE Name = 'my-cmd';
```

Always include a `WHERE` clause — omitting it updates **every row**.

### DELETE — Remove a row

```sql
-- Delete one command by name
DELETE FROM CustomCommands WHERE Name = 'my-cmd';

-- Verify before deleting (dry-run with SELECT)
SELECT * FROM CustomCommands WHERE Name = 'my-cmd';
-- If the above looks correct, then run:
DELETE FROM CustomCommands WHERE Name = 'my-cmd';
```

## Batch Execution (Non-Interactive)

Run multiple statements from a single shell command using a here-doc:

```bash
sqlite3 Data/kimi_data.db <<'EOF'
INSERT INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('cmd-a', '[]', 'repo/bin/cmd-a', '111', 0, 'Command A', datetime('now'), 0);

INSERT INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('cmd-b', '["b"]', 'repo/bin/cmd-b', '111', 0, 'Command B', datetime('now'), 0);
EOF
```

## Transactions

Wrap multiple writes in a transaction so they all succeed or all fail:

```sql
BEGIN TRANSACTION;
UPDATE CustomCommands SET Description = 'New desc' WHERE Name = 'cmd-a';
DELETE FROM CustomCommands WHERE Name = 'old-cmd';
COMMIT;
-- To undo: ROLLBACK; instead of COMMIT;
```

## Inspecting the Schema

```bash
# Show schema of a specific table
sqlite3 Data/kimi_data.db ".schema CustomCommands"

# List all tables
sqlite3 Data/kimi_data.db ".tables"

# Show pragma info (column names and types)
sqlite3 Data/kimi_data.db "PRAGMA table_info(CustomCommands);"
```

`PRAGMA table_info` output columns: `cid | name | type | notnull | dflt_value | pk`

## Safety Tips

1. **Always SELECT before DELETE/UPDATE** — confirm the right rows are targeted.
2. **Use WHERE clauses** — a bare `UPDATE` or `DELETE` without `WHERE` modifies every row.
3. **Use transactions for multi-step changes** — makes it easy to roll back on error.
4. **Do not write to the database while the bot is actively using it** — SQLite uses file-level locking; concurrent writes from the bot process and the CLI can cause `SQLITE_BUSY` errors. Prefer making changes when the bot is idle or stopped.
5. **Backup before bulk changes:**
   ```bash
   cp Data/kimi_data.db Data/kimi_data.db.bak
   ```

## Common Error Messages

| Error | Cause | Fix |
|---|---|---|
| `database is locked` | Bot process holds a write lock | Wait or stop the bot, then retry |
| `no such table` | Wrong DB file or table not yet created | Check `.tables`; confirm the correct `.db` path |
| `UNIQUE constraint failed` | Duplicate primary key / unique column | Query existing rows first, use a different value |
| `NOT NULL constraint failed` | Missing required column value | Provide a value for all non-nullable columns |
