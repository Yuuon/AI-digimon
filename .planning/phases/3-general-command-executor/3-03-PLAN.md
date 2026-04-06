---
phase: 03-general-command-executor
plan: 03
type: execute
wave: 1
depends_on: []
files_modified:
  - .claude/skills/register-custom-command/SKILL.md
autonomous: true
requirements:
  - SKILL-REG-001
user_setup: []
must_haves:
  truths:
    - SKILL file exists and is valid
    - SKILL provides clear registration workflow
    - SKILL includes duplicate detection guidance
    - SKILL includes database manipulation examples
  artifacts:
    - path: .claude/skills/register-custom-command/SKILL.md
      provides: AI agent guidance
      exports: [Registration workflow, Validation rules, SQL examples]
      min_lines: 150
  key_links:
    - from: AI Agent (kimi CLI)
      to: CustomCommands database
      via: Direct SQLite manipulation
      pattern: Read SKILL → Validate → Write to DB
---

<objective>
Create a SKILL file that guides AI agents (kimi CLI) on how to register custom commands in the database. This SKILL file lives outside the .NET program and is used by the AI when users request command creation.

Purpose: Provide clear instructions to AI agents for registering custom commands without going through the .NET program API.
Output: Complete SKILL.md file for the register-custom-command skill.
</objective>

<execution_context>
@C:/Users/MA Huan/.config/opencode/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@.planning/3-CONTEXT.md

**Architecture from CONTEXT.md:**
```
User → /kimi "create a command that..." → Kimi CLI (AI Agent)
                                           ↓
                                    [Reads SKILL file]
                                           ↓
                                    [Writes code, builds binary]
                                           ↓
                                    [Direct DB manipulation]
                                           ↓
                                    SQLite Database
```

**Key Points:**
- AI agent writes directly to SQLite database
- No .NET API involved in registration
- SKILL file provides the "how-to" guide
- Registration happens during /kimi session

**Database Schema:**
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
```
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create SKILL file directory structure</name>
  <files>.claude/skills/register-custom-command/SKILL.md</files>
  <action>
Create the SKILL file for AI agent guidance:

**Directory:** `.claude/skills/register-custom-command/`

**SKILL.md content structure:**

```markdown
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
   - status, shop, inventory, help, kimi, etc.
   - Check src/DigimonBot.Messaging/Commands/ folder

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
       printf("Hello from bot!\\n");
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
```

This SKILL file should be comprehensive enough that any AI agent can follow it to register commands correctly.
  </action>
  <verify>
    <automated>grep -q "SKILL: Register Custom Command" .claude/skills/register-custom-command/SKILL.md && grep -q "INSERT INTO CustomCommands" .claude/skills/register-custom-command/SKILL.md && echo "SKILL file created"</automated>
  </verify>
  <done>
    - SKILL.md exists with complete guidance
    - Database schema documented
    - SQL examples provided
    - Validation rules clear
    - Common issues addressed
  </done>
</task>

<task type="auto">
  <name>Task 2: Create example registration script</name>
  <files>.claude/skills/register-custom-command/examples/hello-world.sh</files>
  <action>
Create an example shell script showing the registration process:

**File:** `.claude/skills/register-custom-command/examples/register-example.sh`

```bash
#!/bin/bash
# Example: Registering a "hello" command

# 1. Check for duplicates
echo "Checking for existing 'hello' command..."
sqlite3 Data/kimi_data.db "SELECT COUNT(*) FROM CustomCommands WHERE Name = 'hello';"
# If result is 0, proceed

# 2. Build binary
cd kimi-workspace
mkdir -p hello-world/bin
cat > hello-world/src/hello.c << 'EOF'
#include <stdio.h>
int main(int argc, char *argv[]) {
    if (argc > 1) {
        printf("Hello, %s!\n", argv[1]);
    } else {
        printf("Hello, World!\n");
    }
    return 0;
}
EOF
gcc -o hello-world/bin/hello hello-world/src/hello.c

# 3. Register in database
echo "Registering command..."
sqlite3 Data/kimi_data.db << EOF
INSERT INTO CustomCommands (
    Name, Aliases, BinaryPath, OwnerUserId, 
    RequiresWhitelist, Description, CreatedAt, UseCount
) VALUES (
    'hello',
    '["hi", "hey"]',
    'hello-world/bin/hello',
    '123456789',
    0,
    'Say hello to the world or a specific person',
    '$(date -Iseconds)',
    0
);
EOF

echo "Registration complete! Use /hello, /hi, or /hey"
```

Also create a README for the skill:

**File:** `.claude/skills/register-custom-command/README.md`

```markdown
# Register Custom Command Skill

This skill guides AI agents through registering custom commands for the QQ bot.

## Usage

When a user asks to create a new command during a /kimi session:

1. Read SKILL.md for complete guidance
2. Follow the registration workflow
3. Execute SQL directly against SQLite database
4. Confirm success to user

## Files

- `SKILL.md` - Complete guidance for AI agents
- `examples/` - Example registrations
- `README.md` - This file

## Important Notes

- Registration happens OUTSIDE the .NET program
- AI writes directly to SQLite database
- .NET program only READS from database (execution)
- Duplicate checking is AI's responsibility
```
  </action>
  <verify>
    <automated>ls -la .claude/skills/register-custom-command/ && cat .claude/skills/register-custom-command/README.md</automated>
  </verify>
  <done>
    - Example script created
    - README documented
    - Directory structure complete
  </done>
</task>

<task type="auto">
  <name>Task 3: Add skill activation guidance</name>
  <files>.claude/skills/register-custom-command/activation.md</files>
  <action>
Create guidance on how this skill is activated:

**File:** `.claude/skills/register-custom-command/activation.md`

```markdown
# Skill Activation

## How AI Knows to Use This Skill

The AI agent (kimi CLI) should reference this skill when:

### Explicit Triggers (User says)
- "Create a command that..."
- "Make a bot command for..."
- "Add a new command called..."
- "I want a /something command"
- "Build me a tool I can use via chat"

### Implicit Triggers (Context)
- User has been coding with /kimi
- User asks to "make this reusable"
- User wants to "share this with the group"

### Workflow Integration

1. User: `/kimi Create a dice rolling command`
2. AI: [Thinks] "This is a command creation request"
3. AI: [Reads] `.claude/skills/register-custom-command/SKILL.md`
4. AI: [Follows] Registration workflow in SKILL.md
5. AI: [Executes] SQL to register command
6. AI: [Reports] "Command registered! Use /roll-dice"

## Integration with Kimi CLI

To make this skill available to kimi CLI:

### Option 1: Environment Variable
```bash
export CLAUDE_SKILLS_PATH="/path/to/.claude/skills"
```

### Option 2: Configuration File
Add to `~/.config/kimi/skills.json`:
```json
{
  "skills": [
    {
      "name": "register-custom-command",
      "path": "/path/to/.claude/skills/register-custom-command",
      "trigger": ["create.*command", "add.*command"]
    }
  ]
}
```

### Option 3: Direct Reference
In the conversation, AI can reference:
> "Let me check the skill file for registering commands..."
> [Reads .claude/skills/register-custom-command/SKILL.md]

## Testing the Skill

### Manual Test
1. Start a /kimi session
2. Say: "Create a hello command"
3. AI should:
   - Check SKILL.md
   - Validate name
   - Build binary
   - Register in DB
   - Confirm success

### Verification
```bash
# Check database
sqlite3 Data/kimi_data.db "SELECT * FROM CustomCommands WHERE Name = 'hello';"

# Test execution
/hello
```

## Troubleshooting

### AI Doesn't Use the Skill
- Ensure skill file is readable
- Check trigger patterns match user intent
- Verify path is correct

### Registration Fails
- Check database permissions
- Verify SQL syntax
- Ensure no duplicates

### Command Not Found After Registration
- .NET program may need restart to pick up new commands
- Or check if command resolution is working
- Verify database entry exists
```

Also update the main SKILL.md to include a note about how it's discovered.
  </action>
  <verify>
    <automated>grep -q "Skill Activation" .claude/skills/register-custom-command/activation.md && echo "Activation guidance created"</automated>
  </verify>
  <done>
    - Activation documentation complete
    - Integration options documented
    - Testing guide provided
  </done>
</task>

</tasks>

<verification>
After completing all tasks:
1. SKILL.md exists with complete workflow
2. Examples directory has working examples
3. README explains the skill
4. Activation guide shows how AI discovers the skill
5. All paths and SQL are correct
</verification>

<success_criteria>
- SKILL.md provides clear registration workflow
- Database schema documented
- SQL examples are correct
- Duplicate detection guidance included
- Security guidelines present
- Examples work when followed
- AI can understand and execute the guidance
</success_criteria>

<output>
After completion, create `.planning/phases/3-general-command-executor/3-03-SUMMARY.md`
</output>
