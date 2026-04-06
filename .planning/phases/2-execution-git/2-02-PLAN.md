---
phase: 02-execution-git
plan: 02
type: execute
wave: 2
depends_on:
  - 2-01
files_modified:
  - src/DigimonBot.Host/Services/GitHttpServer.cs
  - src/DigimonBot.Host/Program.cs
  - Data/kimi_config.json
autonomous: true
requirements:
  - KIMI-GIT-001
  - KIMI-GIT-003
user_setup: []
must_haves:
  truths:
    - Git HTTP server exposes repositories for cloning
    - Clone URLs are generated for each repository
    - URLs follow pattern: http://bot-server/git/<repo-name>.git
    - Server is read-only (no push)
  artifacts:
    - path: src/DigimonBot.Host/Services/GitHttpServer.cs
      provides: HTTP git server
      exports: [StartAsync, StopAsync, GetCloneUrl]
    - path: src/DigimonBot.Core/Services/IGitHttpServer.cs
      provides: Server interface
      exports: [StartAsync, StopAsync, GetCloneUrl, IsRunning]
  key_links:
    - from: KimiCommand
      to: GitHttpServer
      via: GetCloneUrl()
      pattern: Display clone URL in execution response
    - from: GitHttpServer
      to: KimiRepositoryManager
      via: Reads repo paths
      pattern: Serve repositories from base path
---

<objective>
Set up a Git HTTP server to expose repositories for public cloning. Generate clone URLs that users can use to download their coding session repositories.

Purpose: Allow users to clone their work via standard git commands, enabling collaboration and backup.
Output: Running HTTP server serving git repositories with generated clone URLs.
</objective>

<execution_context>
@C:/Users/MA Huan/.config/opencode/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@.planning/ROADMAP.md (Phase 2 section)
@.planning/REQUIREMENTS.md (KIMI-GIT-001, KIMI-GIT-003)
@.planning/milestones/v1.0-phases/1-02-SUMMARY.md

**Configuration from REQUIREMENTS.md:**
```json
{
  "Git": {
    "PublicGitUrl": "http://your-server/git",
    "AutoCommit": true,
    "DefaultBranch": "main"
  }
}
```

**Clone URL Format:**
- Pattern: `http://bot-server/git/<repo-name>.git`
- Example: `http://192.168.1.100:8080/git/my-project.git`

**Options for Git HTTP Server:**
1. **Embedded Kestrel server** - Serve git repos via HTTP (simple, self-contained)
2. **nginx + git-http-backend** - Traditional approach (requires external setup)
3. **Node.js git-http-server** - Requires Node.js runtime

**Recommended:** Embedded Kestrel server using ASP.NET Core's built-in static file serving with custom middleware for git smart HTTP protocol, OR simply serve the git directories and let users clone via `git clone --depth 1 http://server/repo-path`

**Simplest Approach:**
Since we just need read-only access, serve the repository directories as static files with the correct git HTTP headers. Git clients can then use the "dumb" HTTP protocol.

**Git Dumb HTTP Protocol:**
- Serve `.git` directory contents over HTTP
- Git client reads objects directly
- No server-side git logic needed
- Works for cloning (read-only)

**Directory Structure Served:**
```
http://server/git/my-repo.git/
  HEAD
  objects/
  refs/
  config
  description
  info/
```
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create GitHttpServer service</name>
  <files>src/DigimonBot.Core/Services/IGitHttpServer.cs, src/DigimonBot.Host/Services/GitHttpServer.cs</files>
  <action>
Create GitHttpServer service using ASP.NET Core's built-in server:

Interface (IGitHttpServer):
- Task StartAsync(CancellationToken cancellationToken)
- Task StopAsync(CancellationToken cancellationToken)
- string GetCloneUrl(string repoName)
- bool IsRunning { get; }

Implementation (GitHttpServer):
- Use WebHostBuilder with Kestrel
- Bind to configurable port (default: 8080)
- Serve repositories from base path (./kimi-workspace)
- Map URL pattern: /git/{repoName}.git/*
- Serve .git directory contents as static files
- Add correct MIME types for git objects
- Enable directory browsing for /git/ endpoint (show repo list)

Key implementation details:
```csharp
public class GitHttpServer : IGitHttpServer, IHostedService
{
    private IWebHost? _webHost;
    private readonly string _basePath;
    private readonly int _port;
    private readonly string _publicUrl;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _webHost = new WebHostBuilder()
            .UseKestrel(options => options.ListenAnyIP(_port))
            .Configure(app =>
            {
                // Serve git repositories
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(_basePath),
                    RequestPath = "/git",
                    ServeUnknownFileTypes = true,
                    DefaultContentType = "application/octet-stream"
                });

                // Add git-specific MIME types
                var provider = new FileExtensionContentTypeProvider();
                provider.Mappings[".pack"] = "application/x-git-packed-objects";
                provider.Mappings[".idx"] = "application/x-git-packed-objects-toc";
                
                // Directory browsing for repo list
                app.UseDirectoryBrowser(new DirectoryBrowserOptions
                {
                    FileProvider = new PhysicalFileProvider(_basePath),
                    RequestPath = "/git"
                });
            })
            .Build();

        await _webHost.StartAsync(cancellationToken);
    }
}
```

Generate clone URLs:
```csharp
public string GetCloneUrl(string repoName)
{
    return $"{_publicUrl}/git/{repoName}.git";
}
```

Configuration from KimiConfig.Git.PublicGitUrl
  </action>
  <verify>
    <automated>grep -q "IGitHttpServer" src/DigimonBot.Core/Services/IGitHttpServer.cs && grep -q "WebHostBuilder" src/DigimonBot.Host/Services/GitHttpServer.cs && echo "GitHttpServer created"</automated>
  </verify>
  <done>
    - GitHttpServer implements IHostedService
    - Serves repositories via HTTP
    - Generates clone URLs
    - Configurable port and base path
  </done>
</task>

<task type="auto">
  <name>Task 2: Update configuration</name>
  <files>Data/kimi_config.json</files>
  <action>
Update kimi_config.json with Git HTTP server settings:

Add to existing Git section:
```json
{
  "Git": {
    "AutoCommit": true,
    "DefaultBranch": "main",
    "EnableHttpServer": true,
    "HttpPort": 8080,
    "PublicGitUrl": "http://localhost:8080"
  }
}
```

Update KimiConfig.cs GitConfig class:
```csharp
public class GitConfig
{
    public bool AutoCommit { get; set; } = true;
    public string DefaultBranch { get; set; } = "main";
    public bool EnableHttpServer { get; set; } = true;
    public int HttpPort { get; set; } = 8080;
    public string PublicGitUrl { get; set; } = "http://localhost:8080";
}
```

Add Chinese comments explaining each setting:
```json
{
  "_comment": "Git HTTP服务器配置"
}
```
  </action>
  <verify>
    <automated>grep -q "EnableHttpServer" Data/kimi_config.json && grep -q "HttpPort" Data/kimi_config.json && echo "Config updated"</automated>
  </verify>
  <done>
    - Git server settings added to config
    - Default values set
    - Chinese comments included
  </done>
</task>

<task type="auto">
  <name>Task 3: Register Git HTTP server in DI</name>
  <files>src/DigimonBot.Host/Program.cs</files>
  <action>
Register GitHttpServer as a hosted service in Program.cs:

1. Add to ConfigureServices:
```csharp
if (settings.Kimi?.Git?.EnableHttpServer ?? true)
{
    services.AddSingleton<IGitHttpServer, GitHttpServer>();
    services.AddHostedService(provider => provider.GetRequiredService<IGitHttpServer>());
}
```

2. Update KimiCommand registration to inject IGitHttpServer:
```csharp
registry.Register(new KimiCommand(
    provider.GetRequiredService<IKimiRepositoryManager>(),
    provider.GetRequiredService<IKimiExecutionService>(),
    provider.GetRequiredService<KimiConfigService>(),
    provider.GetService<IGitHttpServer>(),  // Optional - may be null if disabled
    provider.GetRequiredService<ILogger<KimiCommand>>()));
```

3. Ensure KimiCommand handles null IGitHttpServer gracefully (don't show clone URLs if disabled)
  </action>
  <verify>
    <automated>grep -q "IGitHttpServer" src/DigimonBot.Host/Program.cs && grep -q "AddHostedService" src/DigimonBot.Host/Program.cs && echo "Server registered"</automated>
  </verify>
  <done>
    - GitHttpServer registered as hosted service
    - Conditionally registered based on config
    - KimiCommand receives optional dependency
  </done>
</task>

<task type="auto">
  <name>Task 4: Update KimiCommand to show clone URLs</name>
  <files>src/DigimonBot.Messaging/Commands/KimiCommand.cs</files>
  <action>
Update KimiCommand to display clone URLs in responses:

1. Inject optional IGitHttpServer:
```csharp
private readonly IGitHttpServer? _gitServer;

public KimiCommand(
    IKimiRepositoryManager repoManager,
    IKimiExecutionService executionService,
    KimiConfigService configService,
    IGitHttpServer? gitServer,  // Nullable
    ILogger<KimiCommand> logger)
{
    _gitServer = gitServer;
    // ...
}
```

2. Update response format to include clone URL:
```
🤖 **Kimi 执行结果**

[Output from kimi CLI]

✅ 已自动提交到 Git
提交: abcd1234

💡 克隆仓库:
```bash
git clone http://your-server/git/my-project.git
```
```

3. Helper method to format clone URL:
```csharp
private string FormatCloneUrl(string repoName)
{
    if (_gitServer?.IsRunning != true)
        return "";
    
    var url = _gitServer.GetCloneUrl(repoName);
    return $"💡 克隆仓库:\n```bash\ngit clone {url}\n```";
}
```

4. Show URL only if:
   - GitHttpServer is enabled and running
   - Repo exists and has commits
   - User has appropriate access level
  </action>
  <verify>
    <automated>grep -q "克隆仓库" src/DigimonBot.Messaging/Commands/KimiCommand.cs && grep -q "GetCloneUrl" src/DigimonBot.Messaging/Commands/KimiCommand.cs && echo "Clone URLs displayed"</automated>
  </verify>
  <done>
    - Clone URLs displayed in responses
    - Properly formatted with markdown
    - Only shown when server is running
  </done>
</task>

</tasks>

<verification>
After completing all tasks:
1. Build project: dotnet build
2. Start application
3. Create a repo: /kimi --new-repo test-repo
4. Clone it: git clone http://localhost:8080/git/test-repo.git
5. Verify files are accessible
</verification>

<success_criteria>
- Git HTTP server starts on configured port
- Repositories are accessible via HTTP
- Clone URLs generated correctly
- Users can clone repos with standard git commands
- Server is read-only (no push support needed for Phase 2)
</success_criteria>

<output>
After completion, create `.planning/phases/2-execution-git/2-02-SUMMARY.md`
</output>
