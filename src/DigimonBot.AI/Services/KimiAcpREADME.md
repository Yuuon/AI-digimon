# Kimi ACP 客户端

通过 ACP (Agent Client Protocol) 与 Kimi Code CLI 通信的 .NET 客户端。

## 文件说明

| 文件 | 说明 |
|------|------|
| `KimiAcpClient.cs` | 底层 ACP 客户端，处理 JSON-RPC 协议 |
| `KimiAcpSession.cs` | 高级会话封装，提供易用的 API |
| `KimiServiceClient.cs` | 旧版 HTTP + CLI 混合客户端（已废弃） |

## 快速开始

```csharp
using DigimonBot.AI.Services;

// 创建会话
var session = new KimiAcpSession("/workdir", logger);

// 连接
await session.ConnectAsync();

// 创建新会话
await session.CreateSessionAsync();

// 聊天
var response = await session.ChatAsync("你好，Kimi！");
```

## 测试

```bash
# 运行 ACP 集成测试
cd /home/ubuntu/Kimi/AI-digimon
dotnet run --project tests/IntegrationTest -- --acp
```

## ACP vs HTTP/Web

| 特性 | ACP (推荐) | HTTP (kimi web) |
|------|-----------|-----------------|
| AI 聊天 | ✅ `session/prompt` | ❌ 不支持 |
| 流式输出 | ✅ 实时推送 | ❌ 不支持 |
| 取消操作 | ✅ `session/cancel` | ❌ 不支持 |
| 多会话 | ✅ 单连接多会话 | ⚠️ 需多个 HTTP 调用 |
| 协议 | JSON-RPC over stdio | HTTP REST |
