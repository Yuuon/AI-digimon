# /jrrp 指令开发范本

## 概述

`/jrrp`（今日人品）是一个展示如何添加新指令的完整范本。它实现了：

1. **指令定义** - 实现 `ICommand` 接口
2. **业务逻辑** - Hash算法计算人品值
3. **分段评语** - 0-100分的评语系统
4. **单元测试** - 完整的测试覆盖

## 文件结构

```
src/DigimonBot.Messaging/Commands/
├── JrrpCommand.cs          # 指令实现

tests/DigimonBot.Core.Tests/Commands/
├── JrrpCommandTests.cs     # 单元测试
```

## 核心实现

### 1. 指令类定义

```csharp
public class JrrpCommand : ICommand
{
    public string Name => "jrrp";                    // 指令名称
    public string[] Aliases => new[] { "今日人品" };  // 别名
    public string Description => "查看今日人品值";     // 描述

    public Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        // 实现逻辑...
    }
}
```

### 2. 算法实现

**核心思路：**
```
QQ号 + 日期 → MD5 Hash → 取模101 → 0-100人品值
```

**代码：**
```csharp
private int CalculateLuckValue(string input)
{
    using var md5 = MD5.Create();
    var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
    var hashNumber = BitConverter.ToUInt32(hashBytes, 0);
    return (int)(hashNumber % 101);  // 0-100
}
```

**输入示例：**
- 用户ID: `123456789`
- 日期: `20260218`
- 拼接: `"123456789:20260218"`

### 3. 评语系统

```csharp
private string GetComment(int luckValue)
{
    return luckValue switch
    {
        100 => "⭐⭐⭐⭐⭐ 完美无瑕！天选之子！",
        >= 90 => "⭐⭐⭐⭐⭐ 鸿运当头！诸事顺遂！",
        >= 80 => "⭐⭐⭐⭐☆ 福星高照！好运连连！",
        // ... 更多分段
        0 => "💀 大凶！建议重新投胎（不是）",
        _ => "❓ 神秘力量干扰"
    };
}
```

### 4. 注册指令

在 `Program.cs` 中注册：

```csharp
// 注册命令
services.AddSingleton<CommandRegistry>(provider =>
{
    var registry = new CommandRegistry();
    
    // ... 其他指令
    
    // 添加今日人品指令
    registry.Register(new JrrpCommand());
    
    return registry;
});
```

## 使用方式

### 用户输入
```
/jrrp
今日人品
人品
/运势
```

### Bot 回复示例
```
🎲 **小明** 的今日人品

📅 日期：2026年02月18日
🎰 人品值：87/100

💭 ⭐⭐⭐⭐☆ 福星高照！好运连连！

✨ 运气不错，把握机会！
```

## 单元测试范本

### 测试类结构

```csharp
public class JrrpCommandTests
{
    private readonly JrrpCommand _command;

    public JrrpCommandTests()
    {
        _command = new JrrpCommand();  // Arrange
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidLuckValue()  // Act & Assert
    {
        var context = new CommandContext
        {
            UserId = "123456789",
            UserName = "测试用户",
            Message = "/jrrp",
            Args = Array.Empty<string>(),
            GroupId = 0,
            IsGroupMessage = false
        };

        var result = await _command.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.NotNull(result.Message);
        Assert.Contains("人品值", result.Message);
    }
}
```

### 关键测试点

| 测试 | 说明 |
|------|------|
| `Name_IsJrrp` | 验证指令名称 |
| `Aliases_ContainsChineseNames` | 验证别名 |
| `ExecuteAsync_ReturnsValidLuckValue` | 验证返回值有效 |
| `ExecuteAsync_SameUserSameDay_ReturnsSameValue` | 验证同一天结果一致 |
| `ExecuteAsync_DifferentUsers_ReturnsResults` | 验证不同用户都有结果 |

## 扩展：简单算法版本

如果不想使用 MD5，可以使用简单算法：

```csharp
private int SimpleHash(string userId, string date)
{
    var combined = userId + date;
    var hash = 0;
    
    foreach (var c in combined)
    {
        hash = ((hash << 5) - hash) + c;
        hash = hash & 0x7FFFFFFF;  // 确保正数
    }
    
    return hash % 101;  // 0-100
}
```

## 如何基于此开发新指令

### 步骤1：创建指令类

```bash
# 复制模板
cp JrrpCommand.cs NewCommand.cs
```

### 步骤2：修改关键部分

```csharp
public class NewCommand : ICommand
{
    public string Name => "新指令名";
    public string[] Aliases => new[] { "别名1", "别名2" };
    public string Description => "指令描述";

    public Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        // 1. 解析参数
        var arg = context.Args.FirstOrDefault();
        
        // 2. 执行业务逻辑
        var result = DoSomething(context.UserId, arg);
        
        // 3. 返回结果
        return Task.FromResult(new CommandResult
        {
            Success = true,
            Message = $"结果：{result}"
        });
    }
}
```

### 步骤3：注册指令

在 `Program.cs` 中添加：
```csharp
registry.Register(new NewCommand());
```

### 步骤4：编写测试

```bash
# 创建测试文件
touch tests/DigimonBot.Core.Tests/Commands/NewCommandTests.cs
```

```csharp
public class NewCommandTests
{
    [Fact]
    public async Task ExecuteAsync_TestScenario()
    {
        var command = new NewCommand();
        var context = new CommandContext { /* ... */ };
        
        var result = await command.ExecuteAsync(context);
        
        Assert.True(result.Success);
    }
}
```

### 步骤5：运行测试

```bash
dotnet test --filter "FullyQualifiedName~NewCommandTests"
```

## 完整指令列表示例

| 指令 | 别名 | 功能 | 依赖 |
|------|------|------|------|
| `/status` | 状态, s | 查看数码宝贝状态 | IDigimonManager |
| `/path` | 进化路线, p | 查看进化路线 | IEvolutionEngine |
| `/reset` | 重置, r | 重置数码宝贝 | IDigimonManager |
| `/jrrp` | 今日人品, 运势 | 查看今日人品 | 无 |
| `/help` | 帮助, ? | 显示帮助 | CommandRegistry |

## 注意事项

1. **无状态设计** - 指令应该是无状态的，所有数据从 `CommandContext` 获取
2. **异步方法** - `ExecuteAsync` 必须是异步的
3. **错误处理** - 使用 `try-catch` 捕获异常，返回 `Success = false`
4. **依赖注入** - 如果需要服务，通过构造函数注入（参考 StatusCommand）

## 参考文件

- 完整实现：[src/DigimonBot.Messaging/Commands/JrrpCommand.cs](../src/DigimonBot.Messaging/Commands/JrrpCommand.cs)
- 单元测试：[tests/DigimonBot.Core.Tests/Commands/JrrpCommandTests.cs](../tests/DigimonBot.Core.Tests/Commands/JrrpCommandTests.cs)
- 注册代码：[src/DigimonBot.Host/Program.cs](../src/DigimonBot.Host/Program.cs)
