# Kimi ACP 工具调用权限修复文档

## 问题描述

在使用 Kimi ACP (Agent Client Protocol) 执行需要工具调用的任务时（如文件操作、Shell 命令），程序会在工具调用处卡住。

### 现象
- 简单对话（如"你好"）可以正常回复
- 复杂任务（如"查看当前目录文件"）会在需要执行工具时卡住
- AI 返回思考过程，但执行工具前停止响应

## 问题原因

### 根本原因
Kimi ACP 在执行工具调用前会发送 `session/request_permission` 请求要求客户端批准。之前的实现错误地将响应作为**通知**发送，而不是**JSON-RPC 响应**。

### 错误 vs 正确格式对比

#### ❌ 错误格式（通知）
```json
{
  "jsonrpc": "2.0",
  "method": "session/permission_response",
  "params": {
    "sessionId": "xxx",
    "toolCallId": "xxx",
    "selectedOption": "approve_for_session"
  }
}
```

#### ✅ 正确格式（JSON-RPC 响应）
```json
{
  "jsonrpc": "2.0",
  "id": 0,
  "result": {
    "outcome": {
      "outcome": "selected",
      "optionId": "approve_for_session"
    }
  }
}
```

### 关键区别

| 特性 | 请求 (Request) | 通知 (Notification) | 响应 (Response) |
|------|---------------|-------------------|----------------|
| `id` 字段 | ✅ 有 | ❌ 无 | ✅ 有（与请求相同） |
| `method` 字段 | ✅ 有 | ✅ 有 | ❌ 无 |
| `result`/`error` | ❌ 无 | ❌ 无 | ✅ 有 |
| 需要回复 | ✅ 是 | ❌ 否 | ❌ 否 |

## 解决方案

### ACP 协议流程

#### 正常对话流程
```
客户端 -> 服务端: session/prompt (id: 3)
服务端 -> 客户端: session/update (agent_thought_chunk)
服务端 -> 客户端: session/update (agent_message_chunk)
服务端 -> 客户端: response (id: 3, result: {stopReason: "end_turn"})
```

#### 工具调用流程（修复前）
```
客户端 -> 服务端: session/prompt (id: 3)
服务端 -> 客户端: session/update (agent_thought_chunk)
服务端 -> 客户端: session/update (tool_call)
服务端 -> 客户端: session/request_permission (id: 0)  <-- 请求
客户端 -> 服务端: session/permission_response (通知)   <-- ❌ 错误！
[服务端等待响应，流程卡住]
```

#### 工具调用流程（修复后）
```
客户端 -> 服务端: session/prompt (id: 3)
服务端 -> 客户端: session/update (agent_thought_chunk)
服务端 -> 客户端: session/update (tool_call)
服务端 -> 客户端: session/request_permission (id: 0)  <-- 请求
客户端 -> 服务端: response (id: 0, result: {...})     <-- ✅ 正确！
服务端 -> 客户端: session/update (tool_result)
服务端 -> 客户端: session/update (agent_message_chunk)
服务端 -> 客户端: response (id: 3, result: {...})
```

## 代码修改

### 文件: `KimiAcpClient.cs`

#### 1. 提取请求 ID
```csharp
else if (method == "session/request_permission" && root.TryGetProperty("params", out var permParams))
{
    // 从 root 获取 id，从 params 获取其他数据
    var requestId = root.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
    HandlePermissionRequest(requestId, permParams.Clone());
}
```

#### 2. 修改方法签名
```csharp
// 修改前
private void HandlePermissionRequest(JsonElement paramsElement)

// 修改后
private void HandlePermissionRequest(int requestId, JsonElement paramsElement)
```

#### 3. 修正响应格式
```csharp
// 发送权限响应（JSON-RPC 响应格式）
_ = Task.Run(async () =>
{
    try
    {
        // 注意：这里需要发送 JSON-RPC 响应（带 id），不是通知
        var response = new Dictionary<string, object>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,  // 必须使用请求中的 id
            ["result"] = new Dictionary<string, object>
            {
                ["outcome"] = new Dictionary<string, string>
                {
                    ["outcome"] = "selected",
                    ["optionId"] = selectedOptionId
                }
            }
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);
        await _stdin!.WriteLineAsync(json);
        await _stdin.FlushAsync();
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "[KimiAcp] 发送权限响应失败");
    }
});
```

### 完整修改后的 HandlePermissionRequest 方法

```csharp
/// <summary>
/// 处理工具调用权限请求 - 自动批准（YOLO 模式）
/// </summary>
private void HandlePermissionRequest(int requestId, JsonElement paramsElement)
{
    try
    {
        if (!AutoApproveTools)
        {
            _logger?.LogWarning("[KimiAcp] 自动批准已禁用，无法执行工具调用");
            return;
        }
        
        var sessionId = paramsElement.GetProperty("sessionId").GetString() ?? "";
        var toolCall = paramsElement.GetProperty("toolCall");
        var toolCallId = toolCall.GetProperty("toolCallId").GetString() ?? "";
        var options = paramsElement.GetProperty("options");
        
        // 查找 "approve_for_session" 或 "approve" 选项
        string? selectedOptionId = null;
        foreach (var option in options.EnumerateArray())
        {
            var kind = option.GetProperty("kind").GetString();
            if (kind == "allow_always" || kind == "allow_once")
            {
                selectedOptionId = option.GetProperty("optionId").GetString();
                break;
            }
        }

        if (selectedOptionId == null)
        {
            _logger?.LogWarning("[KimiAcp] 未找到批准选项，无法处理权限请求");
            return;
        }

        _logger?.LogInformation("[KimiAcp] 自动批准工具调用: {ToolCallId}, 选项: {Option}", 
            toolCallId, selectedOptionId);

        // 发送权限响应（JSON-RPC 响应格式）
        _ = Task.Run(async () =>
        {
            try
            {
                var response = new Dictionary<string, object>
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = requestId,
                    ["result"] = new Dictionary<string, object>
                    {
                        ["outcome"] = new Dictionary<string, string>
                        {
                            ["outcome"] = "selected",
                            ["optionId"] = selectedOptionId
                        }
                    }
                };

                var json = JsonSerializer.Serialize(response, JsonOptions);
                _logger?.LogDebug("[KimiAcp] -> 发送权限响应: {Json}", json);

                await _stdin!.WriteLineAsync(json);
                await _stdin.FlushAsync();
                
                _logger?.LogInformation("[KimiAcp] 权限响应已发送: id={Id}", requestId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[KimiAcp] 发送权限响应失败");
            }
        });
    }
    catch (Exception ex)
    {
        _logger?.LogWarning(ex, "[KimiAcp] 处理权限请求失败");
    }
}
```

## 测试验证

### 手动测试脚本

```bash
cd /tmp && python3 << 'PYEOF'
import asyncio
import json
import sys

async def test():
    proc = await asyncio.create_subprocess_exec(
        "kimi", "acp",
        stdin=asyncio.subprocess.PIPE,
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.PIPE
    )
    
    session_id = None
    
    async def read():
        while True:
            try:
                line = await proc.stdout.readline()
                if not line:
                    break
                line = line.decode().strip()
                if not line:
                    continue
                print(f"[RECV] {line[:150]}...", file=sys.stderr)
                
                msg = json.loads(line)
                if msg.get("id") == 2 and "result" in msg:
                    nonlocal session_id
                    session_id = msg["result"].get("sessionId")
                    print(f"[INFO] Session: {session_id}", file=sys.stderr)
                
                # 处理权限请求
                if msg.get("method") == "session/request_permission":
                    request_id = msg.get("id")
                    print(f"[INFO] Permission request id: {request_id}", file=sys.stderr)
                    
                    # 发送正确的响应格式
                    response = {
                        "jsonrpc": "2.0",
                        "id": request_id,
                        "result": {
                            "outcome": {
                                "outcome": "selected",
                                "optionId": "approve_for_session"
                            }
                        }
                    }
                    resp_json = json.dumps(response)
                    print(f"[SEND] {resp_json}", file=sys.stderr)
                    proc.stdin.write(resp_json.encode() + b"\n")
                    await proc.stdin.drain()
                    
            except:
                pass
    
    asyncio.create_task(read())
    
    # Initialize
    init = {"jsonrpc": "2.0", "id": 1, "method": "initialize", 
            "params": {"protocolVersion": 1, "capabilities": {}, 
                       "clientInfo": {"name": "test", "version": "1.0"}}}
    proc.stdin.write(json.dumps(init).encode() + b"\n")
    await proc.stdin.drain()
    await asyncio.sleep(2)
    
    # Create session
    new_session = {"jsonrpc": "2.0", "id": 2, "method": "session/new",
                   "params": {"cwd": "/tmp", "mcpServers": []}}
    proc.stdin.write(json.dumps(new_session).encode() + b"\n")
    await proc.stdin.drain()
    await asyncio.sleep(3)
    
    # Send prompt that requires tool
    if session_id:
        prompt = {"jsonrpc": "2.0", "id": 3, "method": "session/prompt",
                  "params": {"sessionId": session_id,
                             "prompt": [{"type": "text", "text": "列出当前目录"}]}}
        proc.stdin.write(json.dumps(prompt).encode() + b"\n")
        await proc.stdin.drain()
    
    await asyncio.sleep(20)
    proc.kill()

asyncio.run(test())
PYEOF
```

### 预期输出

成功时应该看到：
```
[INFO] Permission request id: 0
[SEND] {"jsonrpc": "2.0", "id": 0, "result": {"outcome": {"outcome": "selected", "optionId": "approve_for_session"}}}
[RECV] {"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"...","update":{...}}}...
[RECV] {"jsonrpc":"2.0","id":3,"result":{"stopReason":"end_turn"}}
```

## ACP 协议规范

### 权限请求消息

```json
{
  "jsonrpc": "2.0",
  "id": 0,
  "method": "session/request_permission",
  "params": {
    "sessionId": "uuid",
    "toolCall": {
      "toolCallId": "uuid/tool_id",
      "content": [...],
      "title": "Shell: ls"
    },
    "options": [
      {"kind": "allow_once", "name": "Approve once", "optionId": "approve"},
      {"kind": "allow_always", "name": "Approve for this session", "optionId": "approve_for_session"},
      {"kind": "reject_once", "name": "Reject", "optionId": "reject"}
    ]
  }
}
```

### 权限响应消息

```json
{
  "jsonrpc": "2.0",
  "id": 0,
  "result": {
    "outcome": {
      "outcome": "selected",
      "optionId": "approve_for_session"
    }
  }
}
```

## 参考文档

- [ACP Protocol Specification](https://agentclientprotocol.com/)
- [JSON-RPC 2.0 Specification](https://www.jsonrpc.org/specification)
- Kimi CLI ACP 实现: `/home/ubuntu/.local/share/uv/tools/kimi-cli/lib/python*/site-packages/acp/`

## 相关文件

- `src/DigimonBot.AI/Services/KimiAcpClient.cs` - ACP 客户端实现
- `src/DigimonBot.AI/Services/KimiAcpSession.cs` - 会话管理
- `src/DigimonBot.AI/Services/KimiAcpExecutionService.cs` - 执行服务
