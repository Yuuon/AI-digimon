# Kimi Agent Coding System - Requirements

## 1. Functional Requirements

### 1.1 Command Interface

**Requirement:** Implement `/kimi` command handler
- **ID:** KIMI-CMD-001
- **Priority:** High
- **Description:** Bot must recognize and parse `/kimi` commands with arguments
- **Acceptance Criteria:**
  - Command format: `/kimi <action> [--arg1 value1] [--arg2 value2] ...`
  - Support all standard kimi CLI arguments (e.g., `--write`, `--read`, `--ask`)
  - Custom arguments for repo management: `--new-repo`, `--switch-repo`, `--repo-name`
  - Output control: `--summary` (default), `--full`
  - Help command: `/kimi --help` shows usage instructions

### 1.2 Repository Management

**Requirement:** Git repository lifecycle management
- **ID:** KIMI-REPO-001
- **Priority:** High
- **Description:** Create, switch, and manage git repositories for coding sessions
- **Acceptance Criteria:**
  - `--new-repo [name]` creates a new repository with optional name
  - If no name provided, generate unique name: `kimi-work-{timestamp}-{random}`
  - `--switch-repo <name>` changes active repository context
  - `--list-repos` shows all available repositories
  - Repository storage location: configurable base path + repo name
  - Each repo initialized with README.md containing creation info
  - Auto-commit after each kimi execution with timestamp message

**Requirement:** Repository state tracking
- **ID:** KIMI-REPO-002
- **Priority:** Medium
- **Description:** Track which repository is active per user/group
- **Acceptance Criteria:**
  - Store active repo per user in SQLite database
  - If no active repo and user runs kimi command, prompt to create one
  - Support per-group shared repos and per-user private repos (configurable)
  - Repository metadata: creation time, owner, last used, commit count

### 1.3 CLI Execution

**Requirement:** Execute kimi CLI commands
- **ID:** KIMI-EXEC-001
- **Priority:** High
- **Description:** Execute kimi CLI with provided arguments and capture output
- **Acceptance Criteria:**
  - Execute kimi CLI in working directory of active repository
  - Capture stdout and stderr completely
  - Timeout protection: kill process after configurable timeout (default: 5 minutes)
  - Stream output to log files for debugging
  - Handle kimi CLI exit codes properly
  - Support for read-only operations vs write operations

**Requirement:** Argument validation and security
- **ID:** KIMI-EXEC-002
- **Priority:** High
- **Description:** Validate and sanitize all arguments before execution
- **Acceptance Criteria:**
  - Prevent command injection attacks
  - Validate file paths don't escape repo directory
  - Block dangerous arguments (e.g., shell escape sequences)
  - Whitelist approach for allowed arguments
  - Log all commands for audit trail

### 1.4 Output Processing

**Requirement:** AI-powered output summarization
- **ID:** KIMI-OUT-001
- **Priority:** High
- **Description:** Summarize kimi CLI output using AI before sending to chat
- **Acceptance Criteria:**
  - Default behavior: AI summarizes execution results
  - Summary includes: what was done, key changes, any errors, next steps
  - Configurable via `kimi_config.json`: `OutputMode` ("summary" | "full")
  - Summary max length: 1000 characters (respecting QQ message limits)
  - If output is short (< 500 chars), send full output instead
  - Error handling: always show error details even in summary mode

**Requirement:** Full output option
- **ID:** KIMI-OUT-002
- **Priority:** Medium
- **Description:** Allow users to request full unmodified output
- **Acceptance Criteria:**
  - `--full` flag sends complete output (may be truncated if >2000 chars)
  - Full output split into multiple messages if needed
  - Indicate truncation: "... (output truncated, see git repo for full details)"

### 1.5 Git Integration

**Requirement:** Public git server exposure
- **ID:** KIMI-GIT-001
- **Priority:** High
- **Description:** Make repositories publicly cloneable
- **Acceptance Criteria:**
  - Expose git repos via HTTP/HTTPS server (e.g., using `git-http-backend` or simple file server)
  - Generate clone URLs: `git clone http://bot-server/git/<repo-name>.git`
  - Include clone URL in every execution response
  - Read-only access for cloning (no push)
  - Optional: web interface to browse repos

**Requirement:** Automatic commit and push
- **ID:** KIMI-GIT-002
- **Priority:** Medium
- **Description:** Commit changes after each execution
- **Acceptance Criteria:**
  - Auto-commit all changes with message: "kimi execution at {timestamp} by {user}"
  - Push to origin (if configured)
  - Handle git errors gracefully (don't fail entire request)
  - Include commit hash in response

## 2. Configuration Requirements

### 2.1 Configuration File

**Requirement:** External configuration file
- **ID:** KIMI-CFG-001
- **Priority:** High
- **Description:** Store all settings in `Data/kimi_config.json`
- **Acceptance Criteria:**
  - JSON format with schema validation
  - Hot-reload support (changes apply without bot restart)
  - Separate sections: `Security`, `Execution`, `Output`, `Git`, `Repositories`
  - Default values for all settings
  - Validation on load with clear error messages

**Configuration Schema:**
```json
{
  "Security": {
    "AccessMode": "open",
    "Whitelist": [],
    "RequireAuthForWrite": false,
    "MaxReposPerUser": 10
  },
  "Execution": {
    "KimiCliPath": "/usr/local/bin/kimi",
    "WorkingDirectoryBase": "./kimi-workspace",
    "DefaultTimeoutSeconds": 300,
    "MaxTimeoutSeconds": 600,
    "AllowedArguments": ["--write", "--read", "--ask", "--file", "--dir"]
  },
  "Output": {
    "DefaultMode": "summary",
    "MaxSummaryLength": 1000,
    "MaxMessageLength": 2000,
    "IncludeCloneUrl": true,
    "IncludeCommitHash": true
  },
  "Git": {
    "PublicGitUrl": "http://your-server/git",
    "AutoCommit": true,
    "AutoPush": false,
    "DefaultBranch": "main"
  },
  "Repositories": {
    "DefaultRepoMode": "per-user",
    "CleanupOldRepos": true,
    "MaxRepoAgeDays": 30,
    "MaxRepoSizeMB": 100
  }
}
```

### 2.2 Access Control

**Requirement:** Configurable access control
- **ID:** KIMI-SEC-001
- **Priority:** High
- **Description:** Control who can execute kimi commands
- **Acceptance Criteria:**
  - Mode: `open` (anyone), `whitelist` (only listed QQ numbers), `admin-only` (only bot admins)
  - Hot-reloadable: changes effective immediately
  - Per-mode permissions:
    - `open`: all operations allowed
    - `whitelist`: same as open but restricted to list
    - `admin-only`: only admins, write operations may need extra auth
  - Special `--read` flag allows read-only access even in restricted modes

### 2.3 Hot Reload

**Requirement:** Runtime configuration reload
- **ID:** KIMI-CFG-002
- **Priority:** Medium
- **Description:** Reload configuration without restarting bot
- **Acceptance Criteria:**
  - File watcher detects changes to `kimi_config.json`
  - Validate new config before applying
  - Rollback on validation failure
  - Admin command: `/kimi-reload` forces reload
  - Log all configuration changes

## 3. Non-Functional Requirements

### 3.1 Performance

**Requirement:** Response time
- **ID:** KIMI-PERF-001
- **Priority:** High
- **Description:** Acceptable response times for user experience
- **Acceptance Criteria:**
  - Acknowledge command receipt within 2 seconds
  - Execute kimi CLI and return summary within 30 seconds (for typical requests)
  - Timeout handling for long-running operations
  - Progress indicator for operations >10 seconds
  - Queue multiple requests (don't block bot)

**Requirement:** Resource limits
- **ID:** KIMI-PERF-002
- **Priority:** Medium
- **Description:** Prevent resource exhaustion
- **Acceptance Criteria:**
  - Max concurrent executions: configurable (default: 3)
  - Queue overflow handling: reject with message
  - Memory limits: track and enforce per-repo size
  - CPU time limits: kill processes exceeding threshold

### 3.2 Reliability

**Requirement:** Error handling
- **ID:** KIMI-REL-001
- **Priority:** High
- **Description:** Graceful handling of all error conditions
- **Acceptance Criteria:**
  - kimi CLI not found: clear error message
  - Git errors: informative message without stack trace
  - Timeout: cancel execution, partial results if available
  - Network errors: retry logic for git operations
  - All errors logged with context

**Requirement:** Data persistence
- **ID:** KIMI-REL-002
- **Priority:** Medium
- **Description:** Ensure data survives bot restarts
- **Acceptance Criteria:**
  - All repo metadata in SQLite
  - Git repos on disk (not in memory)
  - Configuration in file (not embedded)
  - Graceful shutdown: finish current operations

### 3.3 Security

**Requirement:** Sandboxing
- **ID:** KIMI-SEC-002
- **Priority:** High
- **Description:** Isolate kimi execution environment
- **Acceptance Criteria:**
  - kimi CLI runs in restricted directory (can't access outside)
  - No shell injection vulnerabilities
  - File path validation: no `../` traversal
  - Network access limited (if possible via kimi config)
  - Regular security audit of command parsing

**Requirement:** Audit logging
- **ID:** KIMI-SEC-003
- **Priority:** Medium
- **Description:** Log all kimi executions for accountability
- **Acceptance Criteria:**
  - Log: user QQ, command, arguments, timestamp, duration, success/failure
  - Separate log file: `logs/kimi-execution.log`
  - Retention: configurable (default: 90 days)
  - Admin command: `/kimi-logs` shows recent executions

### 3.4 Usability

**Requirement:** Help system
- **ID:** KIMI-UX-001
- **Priority:** Medium
- **Description:** Comprehensive help for users
- **Acceptance Criteria:**
  - `/kimi --help` shows available commands and arguments
  - `/kimi-examples` shows common usage examples
  - Error messages suggest correct usage
  - Documentation link in every response

**Requirement:** Progress feedback
- **ID:** KIMI-UX-002
- **Priority:** Medium
- **Description:** Keep users informed during long operations
- **Acceptance Criteria:**
  - Immediate acknowledgment: "Executing kimi command..."
  - Progress updates for multi-step operations
  - Timeout warnings: "Still working... ({elapsed}s)"
  - Cancellation support: user can cancel with `/kimi-cancel`

## 4. Integration Requirements

### 4.1 Existing Bot Integration

**Requirement:** Command registration
- **ID:** KIMI-INT-001
- **Priority:** High
- **Description:** Register kimi commands in existing command system
- **Acceptance Criteria:**
  - Use existing `ICommand` interface pattern
  - Register in `CommandFactory` or equivalent
  - Follow existing naming conventions
  - Support both group and private chat

**Requirement:** Database integration
- **ID:** KIMI-INT-002
- **Priority:** High
- **Description:** Store kimi-specific data in existing SQLite database
- **Acceptance Criteria:**
  - New table: `kimi_repos` (id, user_qq, group_id, repo_name, repo_path, active, created_at, last_used)
  - New table: `kimi_executions` (id, user_qq, group_id, repo_name, command, duration_ms, success, executed_at)
  - Migration script for database schema update
  - Use existing Dapper-based repository pattern

**Requirement:** AI client integration
- **ID:** KIMI-INT-003
- **Priority:** High
- **Description:** Use existing AI client for output summarization
- **Acceptance Criteria:**
  - Reuse `IAIClient` or `DeepSeekClient` for summaries
  - Use existing prompt templates approach
  - Configurable summarization prompt in `Data/kimi_prompts.json`
  - Fallback if AI service unavailable: send raw output (truncated)

### 4.2 Git Server Integration

**Requirement:** HTTP git server
- **ID:** KIMI-GIT-003
- **Priority:** Medium
- **Description:** Expose repositories via HTTP for cloning
- **Acceptance Criteria:**
  - Options: 
    - Embed simple HTTP server in bot
    - Use external nginx/apache with directory mapping
    - Use `git instaweb` or similar
  - Serve repos read-only
  - Directory listing disabled or styled
  - Support for `git clone` and `git fetch`

## 5. Testing Requirements

### 5.1 Unit Tests

- Command parsing and validation
- Repository name generation
- Path sanitization
- Configuration validation
- Access control logic

### 5.2 Integration Tests

- Full command execution flow
- Git repository operations
- Database persistence
- AI summarization
- Configuration hot-reload

### 5.3 Security Tests

- Command injection attempts
- Path traversal attacks
- Argument fuzzing
- Access control bypass attempts

## 6. Documentation Requirements

### 6.1 User Documentation

- `/kimi` command usage guide
- Repository management tutorial
- Common use cases and examples
- Troubleshooting guide

### 6.2 Admin Documentation

- Configuration reference
- Security best practices
- Monitoring and logging guide
- Backup and recovery procedures

### 6.3 Developer Documentation

- Architecture overview
- Code structure and patterns
- Extension points
- Testing guide
