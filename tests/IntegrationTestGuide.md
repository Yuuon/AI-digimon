# 集成测试指南

## 1. 单元测试

### 运行所有测试

```bash
# 运行所有测试
dotnet test

# 运行特定项目测试
dotnet test tests/DigimonBot.Core.Tests
dotnet test tests/DigimonBot.AI.Tests

# 详细输出
dotnet test --verbosity normal

# 带覆盖率报告（需要安装coverlet）
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### 测试项目说明

| 测试项目 | 覆盖内容 |
|---------|---------|
| `DigimonBot.Core.Tests` | 核心领域模型、进化引擎、情感追踪 |
| `DigimonBot.AI.Tests` | AI客户端工厂、人格引擎 |

## 2. 手动集成测试

### 2.1 测试AI连接

创建一个简单的测试程序来验证AI配置：

```bash
# 创建一个临时测试程序
cd tests
mkdir AITest && cd AITest
dotnet new console
dotnet add reference ../../src/DigimonBot.AI

# 创建测试代码文件 Program.cs，内容见下方
```

**AITest/Program.cs:**

```csharp
using DigimonBot.AI.Services;
using Microsoft.Extensions.Logging;

// 配置
var config = new AIClientConfig
{
    Provider = AIProvider.DeepSeek, // 或 AIProvider.GLM
    ApiKey = "your-api-key",
    Model = "deepseek-chat", // 或 "glm-4-flash"
    TimeoutSeconds = 30
};

// 创建客户端
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var httpClient = new HttpClient();
var factory = new AIClientFactory(
    new TestHttpClientFactory(httpClient), 
    loggerFactory
);

var client = factory.CreateClient(config);

// 测试对话
Console.WriteLine("测试简单对话...");
try
{
    var response = await client.ChatAsync(
        new List<ChatMessage>(),
        "你是一个测试助手，请简短回复。"
    );
    Console.WriteLine($"回复: {response.Content}");
    Console.WriteLine($"Token使用: {response.TotalTokens}");
}
catch (Exception ex)
{
    Console.WriteLine($"错误: {ex.Message}");
}

// 测试情感分析
Console.WriteLine("\n测试情感分析...");
try
{
    var emotion = await client.AnalyzeEmotionAsync(
        "我想和你一起战斗！",
        "好呀，我们一起加油！"
    );
    Console.WriteLine($"勇气: {emotion.CourageDelta}");
    Console.WriteLine($"友情: {emotion.FriendshipDelta}");
    Console.WriteLine($"爱心: {emotion.LoveDelta}");
    Console.WriteLine($"知识: {emotion.KnowledgeDelta}");
}
catch (Exception ex)
{
    Console.WriteLine($"错误: {ex.Message}");
}

// 辅助类
public class TestHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;
    public TestHttpClientFactory(HttpClient client) => _client = client;
    public HttpClient CreateClient(string name) => _client;
}
```

### 2.2 测试进化系统

创建测试脚本来验证进化逻辑：

```bash
cd tests
mkdir EvolutionTest && cd EvolutionTest
dotnet new console
dotnet add reference ../../src/DigimonBot.Core
```

**EvolutionTest/Program.cs:**

```csharp
using DigimonBot.Core.Models;
using DigimonBot.Core.Services;

var engine = new EvolutionEngine();

// 创建测试数据
var digimonDb = new Dictionary<string, DigimonDefinition>
{
    ["botamon"] = new DigimonDefinition
    {
        Id = "botamon",
        Name = "浮球兽",
        Stage = DigimonStage.Baby1,
        NextEvolutions = new List<EvolutionOption>
        {
            new EvolutionOption
            {
                TargetId = "koromon",
                Requirements = new EmotionValues { Friendship = 5, Love = 5 },
                MinTokens = 2000,
                Priority = 1,
                Description = "在温暖的陪伴下成长"
            }
        }
    },
    ["koromon"] = new DigimonDefinition
    {
        Id = "koromon",
        Name = "滚球兽",
        Stage = DigimonStage.Baby2
    }
};

// 测试1: 条件不足，不应该进化
Console.WriteLine("测试1: 条件不足...");
var digimon1 = new UserDigimon
{
    CurrentDigimonId = "botamon",
    Emotions = new EmotionValues { Friendship = 3, Love = 3 },
    TotalTokensConsumed = 1000
};
var result1 = await engine.CheckAndEvolveAsync(digimon1, digimonDb);
Console.WriteLine(result1 == null ? "✓ 未进化 (预期)" : "✗ 错误进化");

// 测试2: 条件满足，应该进化
Console.WriteLine("\n测试2: 条件满足...");
var digimon2 = new UserDigimon
{
    CurrentDigimonId = "botamon",
    Emotions = new EmotionValues { Friendship = 10, Love = 10 },
    TotalTokensConsumed = 3000
};
var result2 = await engine.CheckAndEvolveAsync(digimon2, digimonDb);
Console.WriteLine(result2?.Success == true ? $"✓ 进化成功: {result2.NewDigimonName}" : "✗ 未进化");

// 测试3: 查看进化进度
Console.WriteLine("\n测试3: 查看进度...");
var progress = engine.GetProgress(digimon1, digimonDb["botamon"]);
Console.WriteLine($"Token进度: {progress.TokenProgressPercent:F1}%");
Console.WriteLine($"情感进度: {progress.EmotionProgressPercent:F1}%");
```

## 3. 端到端测试

### 3.1 测试Bot服务（无需QQ）

修改 `BotService.cs` 添加测试模式：

```csharp
// 在BotService.cs中添加测试方法
public async Task TestModeAsync()
{
    _logger.LogInformation("Running in TEST MODE");
    
    // 模拟消息处理
    var testContext = new Messaging.Handlers.MessageContext
    {
        UserId = "test_user",
        UserName = "测试用户",
        Content = "你好，数码宝贝！",
        IsGroupMessage = false
    };

    var result = await _messageHandler.HandleMessageAsync(testContext);
    
    _logger.LogInformation("Test result: {Response}", result.Response);
}
```

### 3.2 控制台测试程序

创建控制台程序模拟交互：

```bash
cd tests
mkdir ConsoleTest && cd ConsoleTest
dotnet new console
dotnet add reference ../../src/DigimonBot.Core
dotnet add reference ../../src/DigimonBot.AI
dotnet add reference ../../src/DigimonBot.Data
```

**ConsoleTest/Program.cs:**

```csharp
using DigimonBot.AI.Services;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;

Console.WriteLine("=== 数码宝贝Bot 控制台测试 ===\n");

// 1. 初始化服务
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var repository = new JsonDigimonRepository("../../Data/digimon_database.json");
var digimonManager = new InMemoryDigimonManager(repository);
var evolutionEngine = new EvolutionEngine();
var emotionTracker = new EmotionTracker();

// 2. 配置AI（从环境变量或输入读取）
Console.Write("选择AI提供商 (deepseek/glm): ");
var provider = Console.ReadLine() ?? "deepseek";

Console.Write("输入API Key: ");
var apiKey = Console.ReadLine() ?? "";

// 创建AI客户端
var httpClient = new HttpClient();
var aiConfig = new AIClientConfig
{
    Provider = provider == "glm" ? AIProvider.GLM : AIProvider.DeepSeek,
    ApiKey = apiKey,
    TimeoutSeconds = 30
};
var factory = new AIClientFactory(
    new SimpleHttpClientFactory(httpClient),
    loggerFactory
);
var aiClient = factory.CreateClient(aiConfig);

// 3. 创建用户数码宝贝
Console.Write("\n输入你的用户ID: ");
var userId = Console.ReadLine() ?? "test";
var userDigimon = await digimonManager.GetOrCreateAsync(userId);
var currentDef = repository.GetById(userDigimon.CurrentDigimonId)!;

Console.WriteLine($"\n你的数码宝贝: {currentDef.Name} ({currentDef.Stage.ToDisplayName()})");
Console.WriteLine("开始对话吧！(输入 'quit' 退出, 'status' 查看状态)\n");

// 4. 对话循环
var personalityEngine = new PersonalityEngine();
var chatHistory = new List<ChatMessage>();

while (true)
{
    Console.Write("你: ");
    var input = Console.ReadLine();
    
    if (input?.ToLower() == "quit") break;
    
    if (input?.ToLower() == "status")
    {
        ShowStatus(userDigimon, currentDef, evolutionEngine);
        continue;
    }

    // 构建提示词
    var systemPrompt = personalityEngine.BuildSystemPrompt(currentDef, userDigimon);
    
    try
    {
        // 调用AI
        var response = await aiClient.ChatAsync(chatHistory, systemPrompt);
        
        Console.WriteLine($"{currentDef.Name}: {response.Content}\n");
        
        // 情感分析
        var emotion = await aiClient.AnalyzeEmotionAsync(input!, response.Content);
        await emotionTracker.ApplyEmotionAnalysisAsync(userDigimon, emotion, "对话");
        
        // 记录对话
        await digimonManager.RecordConversationAsync(
            userId, input!, response.Content, response.TotalTokens, emotion
        );
        
        // 检查进化
        var evolutionResult = await evolutionEngine.CheckAndEvolveAsync(
            userDigimon, 
            repository.GetAll()
        );
        
        if (evolutionResult?.Success == true)
        {
            Console.WriteLine($"\n✨ {evolutionResult.Message}\n");
            await digimonManager.UpdateDigimonAsync(userId, evolutionResult.NewDigimonId);
            userDigimon = await digimonManager.GetAsync(userId) ?? userDigimon;
            currentDef = repository.GetById(userDigimon.CurrentDigimonId)!;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"错误: {ex.Message}");
    }
}

void ShowStatus(UserDigimon digimon, DigimonDefinition def, IEvolutionEngine engine)
{
    Console.WriteLine("\n=== 状态 ===");
    Console.WriteLine($"数码宝贝: {def.Name}");
    Console.WriteLine($"阶段: {def.Stage.ToDisplayName()}");
    Console.WriteLine($"性格: {def.Personality.ToDisplayName()}");
    Console.WriteLine($"勇气: {digimon.Emotions.Courage}");
    Console.WriteLine($"友情: {digimon.Emotions.Friendship}");
    Console.WriteLine($"爱心: {digimon.Emotions.Love}");
    Console.WriteLine($"知识: {digimon.Emotions.Knowledge}");
    Console.WriteLine($"累计Token: {digimon.TotalTokensConsumed}");
    
    var progress = engine.GetProgress(digimon, def);
    Console.WriteLine($"进化进度: {progress.TokenProgressPercent:F1}%\n");
}

public class SimpleHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;
    public SimpleHttpClientFactory(HttpClient client) => _client = client;
    public HttpClient CreateClient(string name) => _client;
}
```

## 4. 部署前检查清单

- [ ] 所有单元测试通过
- [ ] AI API连接测试成功
- [ ] 进化逻辑测试通过
- [ ] 情感分析功能正常
- [ ] 配置文件格式正确
- [ ] 数码宝贝数据库JSON有效
- [ ] 日志输出正常

## 5. 常见问题调试

### AI返回空响应
- 检查API Key是否有效
- 检查模型名称是否正确
- 查看日志中的错误信息

### 进化不触发
- 确认情感值和Token阈值
- 检查进化表配置
- 使用`GetProgress`查看当前进度

### 内存占用过高
- 限制聊天历史长度（已实现）
- 定期清理不活跃用户数据

## 6. 性能测试

```bash
# 压力测试（模拟多个用户）
dotnet run --project tests/LoadTest --users 100 --duration 60
```

建议在大规模部署前进行负载测试，验证：
- 内存使用情况
- API响应时间
- 并发处理能力
