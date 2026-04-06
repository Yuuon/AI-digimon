# Phase 1: Foundation - Implementation Context

## Phase Overview

**Phase:** 1 - Foundation  
**Goal:** Basic infrastructure, command handler, and repository management  
**Status:** Ready for implementation  
**Last Updated:** 2026-04-06  

---

## Locked Decisions

### 1. Access Control Model

**Decision:** Three-tier access control with whitelist override

**Configuration Structure:**
```json
{
  "AccessControl": {
    "Mode": "open",
    "Whitelist": ["123456789", "987654321"],
    "NonWhitelistAccess": "read-only"
  }
}
```

**Modes:**
- `open` - Everyone has full access (default for group chat)
- `whitelist` - Only whitelisted users have access (level determined by NonWhitelistAccess)

**NonWhitelistAccess Options:**
- `read-only` - Non-whitelist users can view repos and read output, cannot execute kimi
- `restricted` - Non-whitelist users completely blocked from all kimi functions

**Whitelist Behavior:**
- Whitelisted users ALWAYS have full read/write access regardless of mode
- Check order: 1) Is user in whitelist? → Full access. 2) Mode is open? → Full access. 3) Apply NonWhitelistAccess restriction

**Implementation Note:** Use existing AdminConfig pattern from codebase (StatusCommand.cs line 26-34)

---

### 2. Database Architecture

**Decision:** Separate database file, no user isolation, shared workspace

**Database File:** `Data/kimi_data.db` (separate from `bot_data.db`)

**Rationale:** 
- Kimi function is completely separate from Digimon data
- Everyone works in same workspace for collaborative coding
- No user separation at this phase (may add in future)

**Schema:**
```sql
-- Repositories table
CREATE TABLE KimiRepositories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Path TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    LastUsedAt TEXT,
    SessionCount INTEGER DEFAULT 0
);

-- Sessions table (for tracking execution history)
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

**Implementation Pattern:** Follow existing SQLite + Dapper pattern (SqliteUserDataRepository.cs)

---

### 3. Kimi CLI Integration

**Decision:** Execute kimi CLI directly in repo directory, replicate all documented arguments

**Kimi CLI Path:** `kimi` (assumes in PATH, no hardcoded path)

**Working Directory:** Always execute in active repository directory

**Timeout:** Configurable (default: 300 seconds / 5 minutes)

**Arguments to Support (from kimi CLI documentation):**

**Basic Options:**
- `--version`, `-V` - Show version
- `--help`, `-h` - Show help
- `--verbose` - Detailed runtime info
- `--debug` - Log debug info

**Configuration:**
- `--agent NAME` - Use built-in agent (default, okabe)
- `--agent-file PATH` - Custom agent file
- `--config STRING` - TOML/JSON config string
- `--config-file PATH` - Config file path

**Model & Work Directory:**
- `--model NAME`, `-m` - Specify LLM model
- `--work-dir PATH`, `-w` - Working directory (auto-set to repo path)
- `--add-dir PATH` - Add directories to workspace

**Session Management:**
- `--continue`, `-C` - Continue previous session
- `--session [ID]`, `--resume [ID]`, `-S`, `-r` - Resume session

**Input:**
- `--prompt TEXT`, `-p` - Pass user prompt
- `--command TEXT`, `-c` - Alias for --prompt

**Loop Control:**
- `--max-steps-per-turn N` - Max steps per turn
- `--max-retries-per-step N` - Max retries per step
- `--max-ralph-iterations N` - Ralph loop iterations

**UI Modes:**
- `--print` - Non-interactive print mode
- `--quiet` - Shortcut for --print --output-format text --final-message-only
- `--input-format FORMAT` - text or stream-json
- `--output-format FORMAT` - text or stream-json
- `--final-message-only` - Only output final message

**MCP:**
- `--mcp-config-file PATH` - MCP config file
- `--mcp-config JSON` - MCP config JSON string

**Approval:**
- `--yolo`, `-y`, `--yes`, `--auto-approve` - Auto-approve all operations

**Plan & Thinking:**
- `--plan` - Start in plan mode
- `--thinking` - Enable thinking mode
- `--no-thinking` - Disable thinking mode

**Skills:**
- `--skills-dir PATH` - Additional skills directories

**Command Format:**
```bash
kimi [OPTIONS] [SUBCOMMAND]
```

**Implementation Pattern:** Use Process.Start with ProcessStartInfo (VisionService.cs line 167-177)

---

### 4. Repository Management

**Decision:** Simple project/session manager, shared workspace, explicit repo creation

**Commands:**
- `/kimi --new-repo [name]` - Create new repository
- `/kimi --switch-repo <name>` - Switch active repository
- `/kimi --list-repos` - List all repositories
- `/kimi --current-repo` - Show current repository

**Repository Structure:**
```
{BasePath}/{RepoName}/
  ├── .git/                    # Git repository
  ├── .kimi/                   # Kimi session state
  │   ├── context.jsonl
  │   ├── state.json
  │   └── wire.jsonl
  └── README.md                # Auto-generated with creation info
```

**Base Path:** Configurable (default: `./kimi-workspace`)

**Naming:**
- If name provided: use that name
- If no name: `kimi-{timestamp}` format (e.g., `kimi-20240406-143022`)

**Active Repository:**
- Only ONE active repository globally (shared by all users)
- Stored in database (KimiRepositories.IsActive)
- When switching, update IsActive flag
- If no active repo and user runs kimi command, auto-create with timestamp name

**Git Initialization:**
- Auto-initialize git repo on creation
- Create README.md with creation timestamp
- No initial commit (kimi will handle commits)

**Directory Navigation:**
- When user executes `/kimi --switch-repo <name>`, switch to that repo directory
- All subsequent kimi commands run in that directory until switched again

---

### 5. Configuration System

**Decision:** Separate config file with full hot-reload support

**Config File:** `Data/kimi_config.json`

**Schema:**
```json
{
  "AccessControl": {
    "Mode": "open",
    "Whitelist": [],
    "NonWhitelistAccess": "read-only"
  },
  "Execution": {
    "KimiCliPath": "kimi",
    "DefaultTimeoutSeconds": 300,
    "MaxTimeoutSeconds": 600,
    "BasePath": "./kimi-workspace"
  },
  "Output": {
    "DefaultMode": "summary",
    "MaxSummaryLength": 1000,
    "MaxMessageLength": 2000,
    "IncludeCloneUrl": true
  },
  "Git": {
    "AutoCommit": true,
    "DefaultBranch": "main"
  }
}
```

**Hot Reload:**
- File watcher on `Data/kimi_config.json`
- Reload entire config on change
- No restart required
- Log config reload events
- Currently focused on AccessControl section

**Default Values:**
- All settings have sensible defaults
- Config file can be partial (missing keys use defaults)

**Validation:**
- Validate on load
- Log warnings for invalid values
- Use defaults for invalid entries (don't crash)

---

### 6. Command Interface

**Decision:** Single `/kimi` command with subcommands and options

**Command Format:**
```
/kimi [OPTIONS] [MESSAGE]
/kimi --new-repo [name]
/kimi --switch-repo <name>
/kimi --list-repos
/kimi --current-repo
/kimi --help
```

**Examples:**
```
/kimi Create a hello world program in Python
/kimi --new-repo my-project
/kimi --switch-repo my-project
/kimi --list-repos
/kimi --yolo Refactor the main function
/kimi --model kimi-k2 "Explain this code"
```

**Help Message:**
Show available options and examples when `/kimi --help` is used

---

## Code Context

### Files to Create

1. **Configuration**
   - `src/DigimonBot.Host/Configs/KimiConfig.cs` - Configuration class
   - `Data/kimi_config.json` - Config file template

2. **Database**
   - `src/DigimonBot.Data/Database/KimiDatabaseInitializer.cs` - Database setup
   - `src/DigimonBot.Data/Repositories/IKimiRepositoryRepository.cs` - Interface
   - `src/DigimonBot.Data/Repositories/Sqlite/KimiRepositoryRepository.cs` - Implementation

3. **Services**
   - `src/DigimonBot.Core/Services/IKimiRepositoryManager.cs` - Interface
   - `src/DigimonBot.Core/Services/KimiRepositoryManager.cs` - Implementation
   - `src/DigimonBot.AI/Services/IKimiExecutionService.cs` - Interface
   - `src/DigimonBot.AI/Services/KimiExecutionService.cs` - Implementation
   - `src/DigimonBot.Host/Services/KimiConfigService.cs` - Config loader with hot-reload

4. **Command**
   - `src/DigimonBot.Messaging/Commands/KimiCommand.cs` - Command handler

### Files to Modify

1. **Program.cs** - Register new services and command
2. **AppSettings.cs** - Add KimiConfig property (optional, if integrating)

### Patterns to Follow

1. **Configuration Loading** (AppSettings.cs, Program.cs lines 36-37)
2. **Database Initialization** (DatabaseInitializer.cs)
3. **Repository Pattern** (IUserDataRepository.cs, SqliteUserDataRepository.cs)
4. **Command Pattern** (StatusCommand.cs)
5. **Process Execution** (VisionService.cs lines 167-177)

### External Dependencies

- **kimi CLI** - Must be installed and in PATH
- **Git** - Must be installed for repository management

---

## Implementation Sequence

1. **Configuration System**
   - Create KimiConfig.cs
   - Create KimiConfigService with hot-reload
   - Create default kimi_config.json

2. **Database Layer**
   - Create KimiDatabaseInitializer
   - Create repository interfaces and implementations
   - Add migration to existing DatabaseInitializer

3. **Repository Management**
   - Implement KimiRepositoryManager
   - Create repo creation logic
   - Create repo switching logic

4. **Command Handler**
   - Implement KimiCommand
   - Parse arguments
   - Integrate with repository manager

5. **Execution Service**
   - Implement KimiExecutionService
   - Process execution with timeout
   - Output capture

6. **Registration**
   - Register all services in Program.cs
   - Add KimiCommand to CommandRegistry

---

## Success Criteria

- [ ] Configuration system with hot-reload working
- [ ] Database schema created and migrations applied
- [ ] `/kimi --help` displays usage
- [ ] `/kimi --new-repo test` creates a repository
- [ ] `/kimi --list-repos` lists repositories
- [ ] `/kimi --switch-repo test` switches active repo
- [ ] Basic kimi CLI execution works (e.g., `/kimi --version`)

---

## Deferred to Later Phases

- AI summarization (Phase 3)
- Git server exposure (Phase 2)
- Public clone URLs (Phase 2)
- Auto-commit after execution (Phase 2)
- Security hardening (Phase 3)
- Rate limiting (Phase 3)
- Audit logging (Phase 3)
- Multi-user isolation (Future enhancement)

---

## Notes

- Kimi CLI handles its own authentication (login/logout) - bot does NOT manage API keys
- Everyone shares the same workspace - collaborative coding environment
- Configuration is primarily access-related for now
- Follow existing codebase patterns strictly
- Use Chinese comments as per convention (CONVENTIONS.md)

---

*Context created by discuss-phase workflow*  
*Downstream agents: Use this context to implement Phase 1 without asking the user*
