# Kimi CLI 服务模式 .NET 封装指南

本文档提供将 Kimi Code CLI 以服务模式集成到 .NET 应用程序的完整方案。

## 架构概述

```
┌─────────────────┐      HTTP API      ┌──────────────────┐
│   .NET 应用      │ ◄────────────────► │  Kimi Web 服务   │
│ (KimiServiceClient)│                   │  (kimi web)      │
└─────────────────┘                    └──────────────────┘
                                              │
                                              ▼
                                        ┌─────────────┐
                                        │  Kimi API   │
                                        └─────────────┘
```

## 前置要求

1. 已安装 Kimi CLI：`pip install kimi-cli`
2. 已完成登录配置：`kimi login`
3. .NET 8.0 或更高版本

## 启动 Kimi Web 服务

### 手动启动（开发/测试）

```bash
# 基础启动
kimi web --port 5494 --no-open

# 指定工作目录（所有文件操作基于此目录）
kimi web --port 5494 --no-open --work-dir /path/to/project

# 后台启动（Linux/macOS）
nohup kimi web --port 5494 --no-open > /tmp/kimi-web.log 2>&1 &

# 后台启动（Windows）
start /B kimi web --port 5494 --no-open
```

### 自动启动（生产环境）

.NET 应用中自动管理服务生命周期（见下方完整代码）。

---

## 完整 C# 封装代码

### 1. 数据模型 (KimiModels.cs)

```csharp
using System.Text.Json.Serialization;

namespace KimiIntegration.Models;

/// <summary>
/// 聊天请求
/// </summary>
public class ChatRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("yolo")]
    public bool Yolo { get; set; } = true;

    [JsonPropertyName("work_dir")]
    public string? WorkDir { get; set; }
}

/// <summary>
/// 聊天响应
/// </summary>
public class ChatResponse
{
    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("tool_calls")]
    public List<ToolCallInfo>? ToolCalls { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }
}

/// <summary>
/// 工具调用信息
/// </summary>
public class ToolCallInfo
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public Dictionary<string, object>? Params { get; set; }
}

/// <summary>
/// 会话信息
/// </summary>
public class SessionInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("work_dir")]
    public string WorkDir { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("last_activity")]
    public DateTime LastActivity { get; set; }

    [JsonPropertyName("message_count")]
    public int MessageCount { get; set; }
}

/// <summary>
/// 流式响应块
/// </summary>
public class StreamChunk
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // "delta", "tool_call", "complete", "error"

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("tool_call")]
    public ToolCallInfo? ToolCall { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}

/// <summary>
/// Kimi 服务配置
/// </summary>
public class KimiServiceOptions
{
    /// <summary>
    /// 服务基础 URL，默认 http://127.0.0.1:5494
    /// </summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:5494";

    /// <summary>
    /// 请求超时时间，默认 5 分钟
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 是否自动管理服务进程
    /// </summary>
    public bool AutoManageProcess { get; set; } = false;

    /// <summary>
    /// Kimi CLI 可执行文件路径
    /// </summary>
    public string? KimiExecutablePath { get; set; }

    /// <summary>
    /// 默认工作目录
    /// </summary>
    public string? DefaultWorkDir { get; set; }

    /// <summary>
    /// 服务启动端口
    /// </summary>
    public int Port { get; set; } = 5494;
}
```

### 2. 核心客户端 (KimiServiceClient.cs)

```csharp
using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using KimiIntegration.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KimiIntegration;

/// <summary>
/// Kimi Web 服务客户端
/// </summary>
public class KimiServiceClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly KimiServiceOptions _options;
    private readonly ILogger<KimiServiceClient>? _logger;
    private Process? _kimiProcess;
    private bool _isDisposed;
    private readonly SemaphoreSlim _processLock = new(1, 1);

    public KimiServiceClient(
        IOptions<KimiServiceOptions> options,
        ILogger<KimiServiceClient>? logger = null)
    {
        _options = options.Value;
        _logger = logger;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_options.BaseUrl),
            Timeout = _options.Timeout
        };
    }

    public KimiServiceClient(KimiServiceOptions options, ILogger<KimiServiceClient>? logger = null)
    {
        _options = options;
        _logger = logger;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_options.BaseUrl),
            Timeout = _options.Timeout
        };
    }

    #region 服务生命周期管理

    /// <summary>
    /// 确保服务正在运行
    /// </summary>
    public async Task EnsureServiceRunningAsync(CancellationToken ct = default)
    {
        if (!_options.AutoManageProcess) return;

        await _processLock.WaitAsync(ct);
        try
        {
            if (_kimiProcess?.HasExited == false) return;

            _logger?.LogInformation("Starting Kimi web service...");

            var executable = _options.KimiExecutablePath ?? FindKimiExecutable();
            var arguments = $"web --port {_options.Port} --no-open";

            if (!string.IsNullOrEmpty(_options.DefaultWorkDir))
            {
                arguments += $" --work-dir \"{_options.DefaultWorkDir}\"";
            }

            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _kimiProcess = new Process { StartInfo = psi };
            _kimiProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger?.LogDebug("[Kimi] {Output}", e.Data);
            };
            _kimiProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger?.LogWarning("[Kimi] {Error}", e.Data);
            };

            _kimiProcess.Start();
            _kimiProcess.BeginOutputReadLine();
            _kimiProcess.BeginErrorReadLine();

            // 等待服务就绪
            await WaitForServiceReadyAsync(ct);
            _logger?.LogInformation("Kimi web service started on port {Port}", _options.Port);
        }
        finally
        {
            _processLock.Release();
        }
    }

    /// <summary>
    /// 停止托管的服务
    /// </summary>
    public async Task StopServiceAsync(CancellationToken ct = default)
    {
        if (!_options.AutoManageProcess || _kimiProcess == null) return;

        await _processLock.WaitAsync(ct);
        try
        {
            if (_kimiProcess?.HasExited == false)
            {
                _logger?.LogInformation("Stopping Kimi web service...");
                _kimiProcess.Kill(entireProcessTree: true);
                await _kimiProcess.WaitForExitAsync(ct);
                _logger?.LogInformation("Kimi web service stopped");
            }
        }
        finally
        {
            _processLock.Release();
        }
    }

    private async Task WaitForServiceReadyAsync(CancellationToken ct)
    {
        var maxRetries = 30;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var response = await _httpClient.GetAsync("/health", ct);
                if (response.IsSuccessStatusCode) return;
            }
            catch { /* ignore */ }

            await Task.Delay(500, ct);
        }
        throw new TimeoutException("Kimi web service failed to start within 15 seconds");
    }

    private static string FindKimiExecutable()
    {
        // 尝试常见路径
        var candidates = new[]
        {
            "kimi",  // PATH 中
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "kimi"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "Python", "Scripts", "kimi.exe"),
            "/usr/local/bin/kimi",
            "/usr/bin/kimi"
        };

        foreach (var candidate in candidates)
        {
            if (candidate == "kimi" || File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("Could not find kimi executable. Please install kimi-cli or specify path.");
    }

    #endregion

    #region 聊天 API

    /// <summary>
    /// 发送单条消息并获取回复
    /// </summary>
    public async Task<ChatResponse> ChatAsync(
        string message,
        string? sessionId = null,
        string? workDir = null,
        bool yolo = true,
        CancellationToken ct = default)
    {
        await EnsureServiceRunningAsync(ct);

        var request = new ChatRequest
        {
            Message = message,
            SessionId = sessionId,
            WorkDir = workDir ?? _options.DefaultWorkDir,
            Yolo = yolo
        };

        var response = await _httpClient.PostAsJsonAsync("/api/chat", request, ct);
        await EnsureSuccessAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);
        return result ?? throw new InvalidOperationException("Empty response from Kimi service");
    }

    /// <summary>
    /// 流式聊天，返回 IAsyncEnumerable
    /// </summary>
    public async IAsyncEnumerable<StreamChunk> ChatStreamAsync(
        string message,
        string? sessionId = null,
        string? workDir = null,
        bool yolo = true,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureServiceRunningAsync(ct);

        var request = new ChatRequest
        {
            Message = message,
            SessionId = sessionId,
            WorkDir = workDir ?? _options.DefaultWorkDir,
            Yolo = yolo
        };

        using var response = await _httpClient.PostAsJsonAsync("/api/chat/stream", request, ct);
        await EnsureSuccessAsync(response, ct);

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;

            var chunk = JsonSerializer.Deserialize<StreamChunk>(line);
            if (chunk != null) yield return chunk;
        }
    }

    /// <summary>
    /// 同步聊天（阻塞直到完成）
    /// </summary>
    public async Task<string> ChatSimpleAsync(
        string message,
        string? sessionId = null,
        string? workDir = null,
        bool yolo = true,
        CancellationToken ct = default)
    {
        var response = await ChatAsync(message, sessionId, workDir, yolo, ct);
        return response.Response;
    }

    #endregion

    #region 会话管理

    /// <summary>
    /// 获取所有活跃会话
    /// </summary>
    public async Task<List<SessionInfo>> GetSessionsAsync(CancellationToken ct = default)
    {
        await EnsureServiceRunningAsync(ct);

        var response = await _httpClient.GetAsync("/api/sessions", ct);
        await EnsureSuccessAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<List<SessionInfo>>(cancellationToken: ct);
        return result ?? new List<SessionInfo>();
    }

    /// <summary>
    /// 获取会话详情
    /// </summary>
    public async Task<SessionInfo?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await EnsureServiceRunningAsync(ct);

        var response = await _httpClient.GetAsync($"/api/sessions/{sessionId}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<SessionInfo>(cancellationToken: ct);
    }

    /// <summary>
    /// 删除会话
    /// </summary>
    public async Task<bool> DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await EnsureServiceRunningAsync(ct);

        var response = await _httpClient.DeleteAsync($"/api/sessions/{sessionId}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return false;

        await EnsureSuccessAsync(response, ct);
        return true;
    }

    /// <summary>
    /// 创建新会话
    /// </summary>
    public async Task<SessionInfo> CreateSessionAsync(string? workDir = null, CancellationToken ct = default)
    {
        await EnsureServiceRunningAsync(ct);

        var request = new { work_dir = workDir ?? _options.DefaultWorkDir };
        var response = await _httpClient.PostAsJsonAsync("/api/sessions", request, ct);
        await EnsureSuccessAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<SessionInfo>(cancellationToken: ct);
        return result ?? throw new InvalidOperationException("Empty response");
    }

    #endregion

    #region 文件操作（通过 Kimi 代理）

    /// <summary>
    /// 读取文件内容（通过 Kimi 服务）
    /// </summary>
    public async Task<string> ReadFileAsync(string path, string? sessionId = null, CancellationToken ct = default)
    {
        var prompt = $"读取文件 {path} 的内容并原样返回";
        var response = await ChatAsync(prompt, sessionId, yolo: true, ct: ct);
        return response.Response;
    }

    /// <summary>
    /// 分析代码文件
    /// </summary>
    public async Task<string> AnalyzeCodeAsync(string path, string? sessionId = null, CancellationToken ct = default)
    {
        var prompt = $"请分析文件 {path}，说明其功能、关键逻辑和潜在问题";
        var response = await ChatAsync(prompt, sessionId, yolo: true, ct: ct);
        return response.Response;
    }

    #endregion

    #region 工具方法

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var content = await response.Content.ReadAsStringAsync(ct);
        throw new KimiServiceException(
            $"Kimi service error: {(int)response.StatusCode} {response.ReasonPhrase}",
            response.StatusCode,
            content);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _httpClient.Dispose();
        _processLock.Dispose();

        if (_kimiProcess?.HasExited == false)
        {
            try
            {
                _kimiProcess.Kill(entireProcessTree: true);
                _kimiProcess.WaitForExit(TimeSpan.FromSeconds(5));
            }
            catch { /* ignore */ }
        }

        _kimiProcess?.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Kimi 服务异常
/// </summary>
public class KimiServiceException : Exception
{
    public System.Net.HttpStatusCode StatusCode { get; }
    public string? ResponseContent { get; }

    public KimiServiceException(string message, System.Net.HttpStatusCode statusCode, string? responseContent = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
    }
}
```

### 3. 高级封装 - Agent 会话管理 (KimiAgentSession.cs)

```csharp
using KimiIntegration.Models;
using Microsoft.Extensions.Logging;

namespace KimiIntegration;

/// <summary>
/// 高级 Agent 会话封装，维护上下文和状态
/// </summary>
public class KimiAgentSession : IDisposable
{
    private readonly KimiServiceClient _client;
    private readonly ILogger<KimiAgentSession>? _logger;
    private readonly string _workDir;
    private string? _sessionId;
    private readonly List<ChatMessage> _history = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public string? SessionId => _sessionId;
    public IReadOnlyList<ChatMessage> History => _history.AsReadOnly();
    public string WorkDir => _workDir;

    public KimiAgentSession(
        KimiServiceClient client,
        string workDir,
        ILogger<KimiAgentSession>? logger = null)
    {
        _client = client;
        _workDir = workDir;
        _logger = logger;
    }

    /// <summary>
    /// 初始化会话
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var session = await _client.CreateSessionAsync(_workDir, ct);
        _sessionId = session.Id;
        _logger?.LogInformation("Agent session initialized: {SessionId}", _sessionId);
    }

    /// <summary>
    /// 恢复已有会话
    /// </summary>
    public async Task ResumeAsync(string sessionId, CancellationToken ct = default)
    {
        var session = await _client.GetSessionAsync(sessionId, ct);
        if (session == null)
            throw new InvalidOperationException($"Session {sessionId} not found");

        _sessionId = sessionId;
        _logger?.LogInformation("Agent session resumed: {SessionId}", _sessionId);
    }

    /// <summary>
    /// 发送消息并获取回复
    /// </summary>
    public async Task<string> SendAsync(string message, bool yolo = true, CancellationToken ct = default)
    {
        if (_sessionId == null)
            throw new InvalidOperationException("Session not initialized. Call InitializeAsync first.");

        await _lock.WaitAsync(ct);
        try
        {
            _history.Add(new ChatMessage { Role = "user", Content = message });

            var response = await _client.ChatAsync(message, _sessionId, _workDir, yolo, ct);

            _history.Add(new ChatMessage { Role = "assistant", Content = response.Response });

            return response.Response;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 执行代码分析任务
    /// </summary>
    public async Task<CodeAnalysisResult> AnalyzeProjectAsync(string? targetPath = null, CancellationToken ct = default)
    {
        var path = targetPath ?? _workDir;
        var prompt = $"""
            请分析项目路径: {path}
            1. 项目结构和主要文件
            2. 技术栈和依赖
            3. 核心功能模块
            4. 潜在问题和改进建议
            请以结构化方式返回分析结果。
            """;

        var response = await SendAsync(prompt, yolo: true, ct: ct);
        return new CodeAnalysisResult
        {
            RawResponse = response,
            ProjectPath = path,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 执行代码生成任务
    /// </summary>
    public async Task<CodeGenerationResult> GenerateCodeAsync(
        string requirement,
        string? outputPath = null,
        CancellationToken ct = default)
    {
        var prompt = $"""
            请根据以下需求生成代码:
            {requirement}
            """;

        if (outputPath != null)
        {
            prompt += $"\n\n请将生成的代码保存到: {outputPath}";
        }

        var response = await SendAsync(prompt, yolo: true, ct: ct);
        return new CodeGenerationResult
        {
            RawResponse = response,
            Requirement = requirement,
            OutputPath = outputPath,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 执行代码重构任务
    /// </summary>
    public async Task<CodeRefactorResult> RefactorCodeAsync(
        string filePath,
        string refactorGoal,
        CancellationToken ct = default)
    {
        var prompt = $"""
            请重构文件: {filePath}
            重构目标: {refactorGoal}
            请说明修改内容并执行重构。
            """;

        var response = await SendAsync(prompt, yolo: true, ct: ct);
        return new CodeRefactorResult
        {
            RawResponse = response,
            FilePath = filePath,
            RefactorGoal = refactorGoal,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 清空历史记录（保留会话）
    /// </summary>
    public void ClearHistory()
    {
        _history.Clear();
    }

    public void Dispose()
    {
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class CodeAnalysisResult
{
    public string RawResponse { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class CodeGenerationResult
{
    public string RawResponse { get; set; } = string.Empty;
    public string Requirement { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public DateTime Timestamp { get; set; }
}

public class CodeRefactorResult
{
    public string RawResponse { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string RefactorGoal { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
```

### 4. DI 扩展 (ServiceCollectionExtensions.cs)

```csharp
using KimiIntegration;
using KimiIntegration.Models;
using Microsoft.Extensions.DependencyInjection;

namespace KimiIntegration;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Kimi 服务客户端到 DI 容器
    /// </summary>
    public static IServiceCollection AddKimiService(
        this IServiceCollection services,
        Action<KimiServiceOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        services.AddSingleton<KimiServiceClient>();
        return services;
    }

    /// <summary>
    /// 添加 Kimi Agent 会话工厂
    /// </summary>
    public static IServiceCollection AddKimiAgentSession(this IServiceCollection services)
    {
        services.AddScoped<KimiAgentSession>(sp =>
        {
            var client = sp.GetRequiredService<KimiServiceClient>();
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KimiServiceOptions>>();
            var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<KimiAgentSession>>();

            var workDir = options.Value.DefaultWorkDir
                ?? throw new InvalidOperationException("DefaultWorkDir must be set");

            return new KimiAgentSession(client, workDir, logger);
        });

        return services;
    }
}
```

---

## 使用示例

### 基础用法

```csharp
using KimiIntegration;
using KimiIntegration.Models;

// 1. 配置并创建客户端
var options = new KimiServiceOptions
{
    BaseUrl = "http://127.0.0.1:5494",
    DefaultWorkDir = "/path/to/your/project",
    AutoManageProcess = true,  // 自动启动/停止服务
    Port = 5494
};

using var client = new KimiServiceClient(options);

// 2. 简单问答（无上下文）
var response = await client.ChatSimpleAsync("解释什么是依赖注入");
Console.WriteLine(response);

// 3. 带上下文的对话
var chat = await client.ChatAsync("分析这个项目的架构");
Console.WriteLine($"Session ID: {chat.SessionId}");

// 继续同一对话
var followUp = await client.ChatAsync("有哪些改进建议？", sessionId: chat.SessionId);
Console.WriteLine(followUp.Response);

// 4. 流式输出
await foreach (var chunk in client.ChatStreamAsync("写一个快速排序算法"))
{
    Console.Write(chunk.Content);
}
```

### Agent 会话模式

```csharp
// 适合复杂的多轮任务
using var client = new KimiServiceClient(options);

await using var session = new KimiAgentSession(client, "/path/to/project");
await session.InitializeAsync();

// 多轮对话保持上下文
var analysis = await session.AnalyzeProjectAsync();
Console.WriteLine(analysis.RawResponse);

var generated = await session.GenerateCodeAsync(
    "创建一个用户认证服务，包含登录和注册功能",
    "src/Services/AuthService.cs");
Console.WriteLine(generated.RawResponse);

// 查看会话历史
foreach (var msg in session.History)
{
    Console.WriteLine($"[{msg.Role}]: {msg.Content[..Math.Min(100, msg.Content.Length)]}...");
}
```

### ASP.NET Core 集成

```csharp
// Program.cs
using KimiIntegration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKimiService(options =>
{
    options.BaseUrl = builder.Configuration["Kimi:BaseUrl"]!;
    options.DefaultWorkDir = builder.Configuration["Kimi:WorkDir"]!;
    options.AutoManageProcess = true;
    options.Port = 5494;
});

builder.Services.AddKimiAgentSession();

var app = builder.Build();

// 使用示例
app.MapPost("/api/analyze", async (
    string projectPath,
    KimiAgentSession session) =>
{
    await session.InitializeAsync();
    var result = await session.AnalyzeProjectAsync(projectPath);
    return Results.Ok(new { result.RawResponse, session.SessionId });
});

app.MapPost("/api/chat/{sessionId}", async (
    string sessionId,
    string message,
    KimiAgentSession session) =>
{
    await session.ResumeAsync(sessionId);
    var response = await session.SendAsync(message);
    return Results.Ok(new { Response = response });
});

app.Run();
```

### 配置 appsettings.json

```json
{
  "Kimi": {
    "BaseUrl": "http://127.0.0.1:5494",
    "WorkDir": "/home/ubuntu/projects",
    "Timeout": "00:05:00"
  }
}
```

---

## 项目文件 (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="8.0.0" />
  </ItemGroup>

</Project>
```

---

## 最佳实践

### 1. 会话管理策略

```csharp
// 方案 A: 每次任务新建会话（隔离性好）
var session = await client.CreateSessionAsync(workDir);
// ... 执行任务 ...
await client.DeleteSessionAsync(session.Id);

// 方案 B: 长会话复用（上下文连贯）
var response1 = await client.ChatAsync("分析代码", sessionId: "fixed-session");
var response2 = await client.ChatAsync("基于分析结果重构", sessionId: "fixed-session");
```

### 2. 错误处理和重试

```csharp
public async Task<string> RobustChatAsync(string message, int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await _client.ChatSimpleAsync(message);
        }
        catch (KimiServiceException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            if (i == maxRetries - 1) throw;
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i))); // 指数退避
        }
    }
    throw new InvalidOperationException("Unexpected");
}
```

### 3. 并发控制

```csharp
// Kimi CLI 同一时间只能处理一个请求
// 使用 SemaphoreSlim 控制并发
private readonly SemaphoreSlim _kimiLock = new(1, 1);

public async Task ProcessBatchAsync(List<string> tasks)
{
    foreach (var task in tasks)
    {
        await _kimiLock.WaitAsync();
        try
        {
            await _client.ChatSimpleAsync(task);
        }
        finally
        {
            _kimiLock.Release();
        }
    }
}
```

### 4. 日志记录

```csharp
using Microsoft.Extensions.Logging;

// 启用详细日志
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

var client = new KimiServiceClient(options, loggerFactory.CreateLogger<KimiServiceClient>());
```

---

## 故障排除

### 问题：连接被拒绝

```bash
# 检查服务是否运行
curl http://127.0.0.1:5494/health

# 手动启动服务测试
kimi web --port 5494 --no-open --verbose
```

### 问题：超时

```csharp
// 增加超时时间
var options = new KimiServiceOptions
{
    Timeout = TimeSpan.FromMinutes(10)  // 复杂任务需要更长时间
};
```

### 问题：会话丢失

```csharp
// 定期保存会话 ID
var sessionId = chat.SessionId;
await File.WriteAllTextAsync("session.id", sessionId);

// 恢复会话
var savedId = await File.ReadAllTextAsync("session.id");
var response = await client.ChatAsync("继续", sessionId: savedId);
```

---

## API 端点参考

| 端点 | 方法 | 说明 |
|------|------|------|
| `/health` | GET | 健康检查 |
| `/api/chat` | POST | 单次聊天 |
| `/api/chat/stream` | POST | 流式聊天 |
| `/api/sessions` | GET | 列出所有会话 |
| `/api/sessions` | POST | 创建新会话 |
| `/api/sessions/{id}` | GET | 获取会话详情 |
| `/api/sessions/{id}` | DELETE | 删除会话 |

---

## 安全注意事项

1. **YOLO 模式**: 生产环境谨慎使用 `--yolo`，会自动执行文件修改和命令
2. **工作目录**: 限制 Kimi 可访问的文件范围
3. **API 密钥**: 通过环境变量或配置文件管理，不要硬编码
4. **网络暴露**: `kimi web` 默认只监听 localhost，不要直接暴露到公网

---

## 相关文档

- [Kimi CLI 官方文档](https://moonshotai.github.io/kimi-cli/)
- [Kimi CLI GitHub](https://github.com/MoonshotAI/kimi-cli)
