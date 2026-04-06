---
phase: 02-execution-git
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - src/DigimonBot.AI/Services/KimiExecutionService.cs
  - src/DigimonBot.Core/Services/KimiRepositoryManager.cs
  - src/DigimonBot.Messaging/Commands/KimiCommand.cs
autonomous: true
requirements:
  - KIMI-GIT-002
  - KIMI-EXEC-001
  - KIMI-OUT-001
user_setup: []
must_haves:
  truths:
    - Kimi execution automatically commits changes to git
    - Commit messages are generated with timestamp and user info
    - Execution results include commit hash
    - Git errors are handled gracefully
  artifacts:
    - path: src/DigimonBot.Core/Services/GitCommitService.cs
      provides: Git commit automation
      exports: [CommitChangesAsync, GenerateCommitMessage]
    - path: src/DigimonBot.AI/Services/KimiExecutionService.cs
      provides: Updated execution with auto-commit
      exports: [ExecuteAsync, ExecuteAndCommitAsync]
  key_links:
    - from: KimiExecutionService
      to: GitCommitService
      via: Constructor injection
      pattern: After successful execution, auto-commit
    - from: GitCommitService
      to: KimiRepositoryManager
      via: Gets active repo path
      pattern: Commit in active repository
---

<objective>
Implement automatic git commit after kimi CLI execution. When users run kimi commands that modify files, the changes should be automatically committed to git with a descriptive message.

Purpose: Track all changes made by kimi CLI in version control, providing history and enabling collaboration through git.
Output: Working auto-commit system integrated into kimi execution flow.
</objective>

<execution_context>
@C:/Users/MA Huan/.config/opencode/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@.planning/ROADMAP.md (Phase 2 section)
@.planning/REQUIREMENTS.md (KIMI-GIT-002)

@src/DigimonBot.AI/Services/KimiExecutionService.cs
@src/DigimonBot.Core/Services/KimiRepositoryManager.cs
@.planning/milestones/v1.0-phases/1-02-SUMMARY.md

**Phase 1 Foundation Complete:**
- KimiExecutionService exists with basic execution
- KimiRepositoryManager handles repo lifecycle
- KimiCommand integrates execution
- All repositories are git-initialized

**Git Commit Requirements:**
- Auto-commit after successful kimi execution
- Commit message format: "kimi execution at {timestamp} by {user}"
- Include commit hash in execution response
- Handle git errors gracefully (don't fail entire request)
- Use git CLI via Process (follow existing patterns)

**Commit Message Format:**
```
kimi execution at 2026-04-06 14:30:22 by user_123456789

Command: kimi --yolo create hello.py
Duration: 15.3s
Exit code: 0
```
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create GitCommitService</name>
  <files>src/DigimonBot.Core/Services/IGitCommitService.cs, src/DigimonBot.Core/Services/GitCommitService.cs</files>
  <action>
Create GitCommitService for automated git operations:

Interface (IGitCommitService):
- Task<string?> CommitChangesAsync(string repoPath, string userId, string command, int durationMs, int exitCode)
- string GenerateCommitMessage(string userId, string command, int durationMs, int exitCode)

Implementation (GitCommitService):
- Execute git commands via Process (follow VisionService pattern)
- Commands to run:
  1. git add -A (stage all changes)
  2. git status --porcelain (check if there are changes)
  3. git commit -m "message" (commit if changes exist)
  4. git rev-parse HEAD (get commit hash)
- Return commit hash or null if no changes
- Handle errors gracefully - log but don't throw
- If no changes to commit, return null (not an error)

Commit message format:
```
kimi execution at {timestamp} by {userId}

Command: {command}
Duration: {durationMs}ms
Exit code: {exitCode}
```

Use ArgumentList for security (from Phase 1 review fixes).
  </action>
  <verify>
    <automated>grep -q "IGitCommitService" src/DigimonBot.Core/Services/IGitCommitService.cs && grep -q "CommitChangesAsync" src/DigimonBot.Core/Services/GitCommitService.cs && echo "GitCommitService created"</automated>
  </verify>
  <done>
    - GitCommitService implements commit workflow
    - Commit messages include metadata
    - Returns commit hash on success
    - Gracefully handles no-changes case
  </done>
</task>

<task type="auto">
  <name>Task 2: Integrate auto-commit into execution flow</name>
  <files>src/DigimonBot.AI/Services/KimiExecutionService.cs</files>
  <action>
Update KimiExecutionService to auto-commit after execution:

1. Inject IGitCommitService into KimiExecutionService
2. Modify ExecuteAsync to:
   - Run kimi CLI as before
   - If execution successful (ExitCode == 0):
     - Call gitCommitService.CommitChangesAsync()
     - Add commit hash to ExecutionResult
   - Return ExecutionResult with commit info

ExecutionResult model update:
```csharp
public class ExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";
    public int ExitCode { get; set; }
    public int DurationMs { get; set; }
    public string? CommitHash { get; set; }  // NEW
    public bool Committed { get; set; }      // NEW
}
```

Update ExecuteAsync signature to accept userId for commit tracking:
```csharp
Task<ExecutionResult> ExecuteAsync(
    string repoPath, 
    string userId,      // NEW
    string arguments, 
    int timeoutSeconds = 300)
```

Important: Commit should happen even if output reading fails, as long as kimi process completed.
  </action>
  <verify>
    <automated>grep -q "CommitHash" src/DigimonBot.AI/Services/KimiExecutionService.cs && grep -q "IGitCommitService" src/DigimonBot.AI/Services/KimiExecutionService.cs && echo "Auto-commit integrated"</automated>
  </verify>
  <done>
    - KimiExecutionService calls GitCommitService after execution
    - ExecutionResult includes commit hash
    - UserId passed through for commit tracking
    - Services registered in DI
  </done>
</task>

<task type="auto">
  <name>Task 3: Update KimiCommand to display commit info</name>
  <files>src/DigimonBot.Messaging/Commands/KimiCommand.cs</files>
  <action>
Update KimiCommand response formatting to include commit information:

1. Pass userId to execution service
2. Update response message format:

```
🤖 **Kimi 执行结果**

[Output from kimi CLI]

✅ 已自动提交到 Git
提交: abcd1234
仓库: my-project
```

Or if no changes:
```
🤖 **Kimi 执行结果**

[Output from kimi CLI]

ℹ️ 无文件变更
```

3. If commit failed (but execution succeeded):
```
⚠️ 执行成功但 Git 提交失败
[Error message]
```

4. Include clone URL hint (for Phase 2 Plan 02):
```
💡 克隆仓库: git clone http://your-server/git/my-project
```

Update all ExecuteAsync calls to pass context.UserId.
  </action>
  <verify>
    <automated>grep -q "已自动提交" src/DigimonBot.Messaging/Commands/KimiCommand.cs && grep -q "CommitHash" src/DigimonBot.Messaging/Commands/KimiCommand.cs && echo "Response formatting updated"</automated>
  </verify>
  <done>
    - Responses show commit hash when available
    - Chinese messages for user feedback
    - Handles commit failures gracefully
  </done>
</task>

<task type="auto">
  <name>Task 4: Register services in DI</name>
  <files>src/DigimonBot.Host/Program.cs</files>
  <action>
Register GitCommitService in Program.cs:

1. Add to ConfigureServices:
```csharp
services.AddSingleton<IGitCommitService, GitCommitService>();
```

2. Update KimiExecutionService registration to inject IGitCommitService

3. Verify KimiCommand receives updated KimiExecutionService with userId parameter support

Registration order (already done for most):
- KimiConfigService
- KimiDatabaseInitializer
- IKimiRepositoryRepository
- IKimiRepositoryManager
- IGitCommitService (NEW)
- IKimiExecutionService (updated)
- KimiCommand (updated)
  </action>
  <verify>
    <automated>grep -q "IGitCommitService" src/DigimonBot.Host/Program.cs && echo "Services registered"</automated>
  </verify>
  <done>
    - GitCommitService registered in DI
    - KimiExecutionService updated registration
    - All dependencies resolved
  </done>
</task>

</tasks>

<verification>
After completing all tasks:
1. Build project: dotnet build
2. Test: Run kimi command that creates a file
3. Verify: Check git log shows commit
4. Verify: Commit message includes user ID and command
</verification>

<success_criteria>
- Auto-commit works after kimi execution
- Commit messages include timestamp, user, command
- Commit hash returned in ExecutionResult
- Git errors don't break execution flow
- User sees commit confirmation in chat
</success_criteria>

<output>
After completion, create `.planning/phases/2-execution-git/2-01-SUMMARY.md`
</output>
