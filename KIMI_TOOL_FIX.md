# Kimi ACP 工具调用修复

## 问题描述

执行具体任务时（如"调查这个卡住的问题"），Kimi 会在需要执行工具调用时卡住。具体表现为：
- AI 返回一些思考内容
- 说"首先让我查看一下..."
- 然后完全卡住，没有后续响应

## 问题原因

当 Kimi ACP 需要执行工具（如 Shell 命令、文件操作等）时，会发送 `session/request_permission` 请求要求客户端批准。之前的代码没有处理这个请求，导致工具调用无法执行，流程卡住。

## 修复内容

### 1. 自动批准工具调用

修改 `KimiAcpClient.cs`：
- 添加 `HandlePermissionRequest` 方法
- 处理 `session/request_permission` 通知
- 自动发送 `session/permission_response` 响应

```csharp
// 当收到权限请求时，自动批准
private void HandlePermissionRequest(JsonElement paramsElement)
{
    // 解析 toolCallId 和批准选项
    // 发送 permission_response 通知
}
```

### 2. YOLO 模式配置

添加 `AutoApproveTools` 属性：

```csharp
public bool AutoApproveTools { get; set; } = true;
```

可以在创建客户端时配置：

```csharp
// 自动批准所有工具调用（默认）
var client = new KimiAcpClient(autoApproveTools: true);

// 或者手动批准（需要额外实现 UI）
var client = new KimiAcpClient(autoApproveTools: false);
```

## ACP 协议流程

### 正常对话流程
```
客户端 -> 服务端: session/prompt (发送消息)
服务端 -> 客户端: session/update (agent_thought_chunk, 思考过程)
服务端 -> 客户端: session/update (agent_message_chunk, 回复内容)
服务端 -> 客户端: response (stopReason: end_turn, 完成)
```

### 工具调用流程（修复前）
```
客户端 -> 服务端: session/prompt (发送消息)
服务端 -> 客户端: session/update (agent_thought_chunk, 思考过程)
服务端 -> 客户端: session/update (tool_call, 工具调用请求)
服务端 -> 客户端: session/request_permission (请求批准)  <-- 卡住！
[客户端无响应，流程中断]
```

### 工具调用流程（修复后）
```
客户端 -> 服务端: session/prompt (发送消息)
服务端 -> 客户端: session/update (agent_thought_chunk, 思考过程)
服务端 -> 客户端: session/update (tool_call, 工具调用请求)
服务端 -> 客户端: session/request_permission (请求批准)
客户端 -> 服务端: session/permission_response (自动批准)  <-- 修复！
服务端 -> 客户端: session/update (tool_result, 工具执行结果)
服务端 -> 客户端: session/update (agent_message_chunk, 回复内容)
服务端 -> 客户端: response (stopReason: end_turn, 完成)
```

## 使用示例

### 代码示例

```csharp
var session = new KimiAcpSession("/workdir", logger, 
    kimiCliPath: "/home/ubuntu/.local/bin/kimi",
    autoApproveTools: true);  // 自动批准工具调用

await session.ConnectAsync();
await session.CreateSessionAsync();

// 现在可以正常执行需要工具调用的任务了
var result = await session.ChatAsync("请分析当前目录的代码结构");
// AI 会自动执行 ls、cat 等命令来获取信息
```

### 配置文件

```json
{
  "Kimi": {
    "Execution": {
      "Mode": "acp",
      "KimiCliPath": "/home/ubuntu/.local/bin/kimi",
      "AutoApproveTools": true
    }
  }
}
```

## 安全提示

自动批准工具调用（YOLO 模式）意味着 AI 可以自动执行：
- Shell 命令（ls, cat, grep 等）
- 文件操作（读取、写入、删除）
- 其他系统操作

**在生产环境中：**
1. 确保 AI 运行在受限环境中
2. 使用专用的工作目录（非系统目录）
3. 考虑定期备份重要数据
4. 可以通过 `AutoApproveTools = false` 禁用自动批准（需要额外实现审批 UI）

## 测试验证

运行集成测试验证修复：

```bash
cd /home/ubuntu/Kimi/AI-digimon
dotnet run --project tests/IntegrationTest -- --acp
```

测试会验证：
- 简单对话（不需要工具）
- 文件列表查询（需要 Shell 工具）
- 代码分析（需要文件读取工具）
