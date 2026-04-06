# Kimi ACP .NET 客户端指南

本文档提供通过 **ACP (Agent Client Protocol)** 将 Kimi Code CLI 集成到 .NET 应用程序的完整方案。

## 为什么选择 ACP？

| 特性 | `kimi acp` | `kimi web` + CLI | CLI 单次调用 |
|------|------------|------------------|--------------|
| AI 聊天 API | ✅ `session/prompt` | ❌ 不支持 | ⚠️ 进程调用 |
| 会话保持 | ✅ 多会话管理 | ✅ HTTP API | ❌ 无状态 |
| 流式输出 | ✅ `session/update` | ❌ 阻塞 | ❌ 阻塞 |
| 取消操作 | ✅ `session/cancel` | ❌ 不支持 | ❌ 不支持 |
| 协议 | JSON-RPC over stdio | HTTP + 进程 | 进程 |

**ACP 是功能最完整、设计最优雅的方案。**

## 前置要求

1. 已安装 Kimi CLI：`pip install kimi-cli` (>= 1.30)
2. 已完成登录配置：`kimi login`
3. .NET 8.0 或更高版本

## 架构

```
┌─────────────────┐      JSON-RPC      ┌──────────────────┐
│   .NET 应用      │ ◄────────────────► │  Kimi ACP 服务   │
│ (KimiAcpClient) │     (stdin/stdout) │  (kimi acp)      │
└─────────────────┘                    └──────────────────┘
                                               │
                                               ▼
                                        ┌─────────────┐
                                        │  Kimi API   │
                                        └─────────────┘
```

## 快速开始

### 1. 基础用法

```csharp
using DigimonBot.AI.Services;

// 创建会话
var session = new KimiAcpSession("/path/to/workdir", logger);

// 连接 ACP 服务
await session.ConnectAsync();

// 创建新会话
await session.CreateSessionAsync();

// 发送消息并获取回复
var response = await session.ChatAsync("用 C# 写一个 Hello World");
Console.WriteLine(response);

// 断开连接
session.Disconnect();
```

### 2. 流式输出

```csharp
// 实时接收 AI 回复的每个字符
await session.ChatStreamingAsync(
    "分析这个项目的代码结构",
    onChunk: chunk => Console.Write(chunk),  // 实时输出
    ct: cancellationToken
);
```

### 3. 多会话管理

```csharp
// 列出所有会话
var sessions = await session.ListSessionsAsync();
foreach (var s in sessions)
{
    Console.WriteLine($"{s.SessionId}: {s.Title}");
}

// 恢复已有会话
await session.ResumeSessionAsync("session-uuid-here");

// 继续对话（上下文保持）
var response = await session.ChatAsync("基于刚才的分析给出改进建议");
```

## 完整 API 参考

### KimiAcpClient (底层客户端)

```csharp
var client = new KimiAcpClient(logger);

// 连接
await client.ConnectAsync();

// 初始化
var init = await client.InitializeAsync();

// 创建会话
var session = await client.CreateSessionAsync("/workdir");

// 发送消息（流式）
client.OnSessionUpdate += (s, e) =>
{
    if (e.UpdateType == "agent_message_chunk")
        Console.Write(e.Content);
};

var response = await client.SendPromptAsync(session.SessionId, "Hello");

// 取消操作
await client.CancelAsync(session.SessionId);
```

### KimiAcpSession (高级封装)

```csharp
var session = new KimiAcpSession("/workdir", logger);

// 事件
session.OnThoughtReceived += (s, thought) => 
    Console.WriteLine($"[思考] {thought}");
    
session.OnMessageReceived += (s, message) => 
    Console.WriteLine($"[回复] {message}");

// 方法
await session.ConnectAsync();
await session.CreateSessionAsync();
await session.LoadSessionAsync("session-id");
await session.ResumeSessionAsync("session-id");

// 聊天
var reply = await session.ChatAsync("消息");
await session.ChatStreamingAsync("消息", chunk => Console.Write(chunk));
await session.CancelAsync();

// 工具
var thought = session.GetThoughtProcess();  // 获取思考过程
```

## ACP 协议说明

### 方法列表

| 方法 | 说明 |
|------|------|
| `initialize` | 初始化连接 |
| `session/new` | 创建新会话 |
| `session/list` | 列出会话 |
| `session/load` | 加载会话 |
| `session/resume` | 恢复会话 |
| `session/prompt` | 发送消息（AI 聊天） |
| `session/cancel` | 取消操作 |

### 通知类型

通过 `session/update` 通知接收流式输出：

| UpdateType | 说明 |
|------------|------|
| `agent_thought_chunk` | AI 思考过程（流式） |
| `agent_message_chunk` | AI 回复内容（流式） |
| `available_commands_update` | 可用命令更新 |

## 高级示例

### 带取消令牌的聊天

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

try
{
    var response = await session.ChatAsync("复杂任务...", cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("用户取消了操作");
}
```

### 多会话并发（每个会话独立连接）

```csharp
var session1 = new KimiAcpSession("/project/A");
var session2 = new KimiAcpSession("/project/B");

await Task.WhenAll(
    session1.ConnectAsync(),
    session2.ConnectAsync()
);

await Task.WhenAll(
    session1.CreateSessionAsync(),
    session2.CreateSessionAsync()
);

// 同时对话
await Task.WhenAll(
    session1.ChatAsync("分析项目 A"),
    session2.ChatAsync("分析项目 B")
);
```

### 保存和恢复对话历史

```csharp
// 保存会话 ID
var sessionId = session.SessionId;
await File.WriteAllTextAsync("session.txt", sessionId);

// 之后恢复
var savedId = await File.ReadAllTextAsync("session.txt");
var newSession = new KimiAcpSession("/workdir");
await newSession.ConnectAsync();
await newSession.ResumeSessionAsync(savedId);

// 继续对话（保持上下文）
var response = await newSession.ChatAsync("继续刚才的话题");
```

## 错误处理

```csharp
try
{
    await session.ConnectAsync();
}
catch (FileNotFoundException)
{
    Console.WriteLine("kimi 命令未找到，请先安装 kimi-cli");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"连接失败: {ex.Message}");
}

try
{
    var response = await session.ChatAsync("...");
}
catch (KimiAcpException ex) when (ex.ErrorCode == -32000)
{
    Console.WriteLine("需要登录，请运行: kimi login");
}
catch (TimeoutException)
{
    Console.WriteLine("AI 响应超时");
}
```

## 项目文件 (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
  </ItemGroup>

</Project>
```

## 故障排除

### ACP 服务无法启动

```bash
# 检查 kimi 是否安装
which kimi
kimi --version

# 检查是否已登录
kimi login

# 手动测试 ACP
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1,"capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}' | kimi acp
```

### 会话未找到

- 确保使用正确的 `sessionId`
- 检查工作目录是否正确
- 会话可能被其他进程删除

### 超时

- 默认超时时间为 5 分钟
- 复杂任务可能需要更长时间
- 使用 `CancellationToken` 控制超时

## 相关文档

- [Kimi CLI 官方文档](https://moonshotai.github.io/kimi-cli/)
- [ACP 协议说明](https://agentclientprotocol.com/)
- [Kimi CLI GitHub](https://github.com/MoonshotAI/kimi-cli)
