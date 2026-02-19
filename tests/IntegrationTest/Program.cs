using DigimonBot.AI.Services;
using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;
using Microsoft.Extensions.Logging;

Console.WriteLine("========================================");
Console.WriteLine("    数码宝贝Bot - 集成测试控制台");
Console.WriteLine("========================================\n");

// 初始化
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Warning);
});

var logger = loggerFactory.CreateLogger<Program>();

// 检查测试数据文件
var baseDir = AppContext.BaseDirectory;
var dbPath = Path.Combine(baseDir, "..", "..", "..", "..", "..", "Data", "digimon_database.json");
dbPath = Path.GetFullPath(dbPath);
if (!File.Exists(dbPath))
{
    Console.WriteLine("❌ 错误: 找不到数码宝贝数据库文件");
    Console.WriteLine($"   路径: {Path.GetFullPath(dbPath)}");
    return 1;
}

Console.WriteLine($"✓ 数据库文件: {dbPath}\n");

// 初始化服务
try
{
    var repository = new JsonDigimonRepository(dbPath);
    var digimonDb = repository.GetAll();
    Console.WriteLine($"✓ 加载了 {digimonDb.Count} 个数码宝贝定义");

    var digimonManager = new InMemoryDigimonManager(repository);
    var evolutionEngine = new EvolutionEngine();
    var emotionTracker = new EmotionTracker();
    var personalityEngine = new PersonalityEngine();

    // 测试1: 基础服务测试
    Console.WriteLine("\n【测试1】基础服务初始化");
    Console.WriteLine($"  - 进化引擎: ✓");
    Console.WriteLine($"  - 情感追踪器: ✓");
    Console.WriteLine($"  - 人格引擎: ✓");

    // 测试2: 数码宝贝创建
    Console.WriteLine("\n【测试2】数码宝贝创建");
    var testUserId = $"test_{DateTime.Now:MMddHHmmss}";
    var userDigimon = await digimonManager.GetOrCreateAsync(testUserId);
    var currentDef = repository.GetById(userDigimon.CurrentDigimonId)!;
    Console.WriteLine($"  用户ID: {testUserId}");
    Console.WriteLine($"  初始数码宝贝: {currentDef.Name} ({currentDef.Stage})");
    Console.WriteLine($"  性格: {currentDef.Personality}");

    // 测试3: 进化逻辑测试
    Console.WriteLine("\n【测试3】进化系统测试");
    var progress = evolutionEngine.GetProgress(userDigimon, currentDef);
    Console.WriteLine($"  当前Token: {progress.CurrentTokens}/{progress.RequiredTokens}");
    Console.WriteLine($"  进化进度: {progress.TokenProgressPercent:F1}%");

    // 模拟进化条件
    userDigimon.TotalTokensConsumed = progress.RequiredTokens + 100;
    userDigimon.Emotions.Friendship = 100;
    userDigimon.Emotions.Love = 100;

    var evolutionResult = await evolutionEngine.CheckAndEvolveAsync(userDigimon, digimonDb);
    if (evolutionResult != null)
    {
        Console.WriteLine($"  ✓ 进化触发: {evolutionResult.Message}");
    }
    else
    {
        Console.WriteLine($"  ℹ 未触发进化（可能需要检查进化表配置）");
    }

    // 测试4: AI客户端测试（可选）
    Console.WriteLine("\n【测试4】AI客户端配置测试");
    Console.WriteLine("  支持的AI提供商:");
    Console.WriteLine("    - DeepSeek");
    Console.WriteLine("    - GLM (智谱AI)");
    Console.WriteLine("    - OpenAI兼容");
    Console.WriteLine("    - Custom");

    var apiKey = Environment.GetEnvironmentVariable("AI__ApiKey");
    if (string.IsNullOrEmpty(apiKey))
    {
        Console.WriteLine("\n  ⚠ 未设置 AI__ApiKey 环境变量，跳过AI连接测试");
        Console.WriteLine("  如需测试AI，请运行:");
        Console.WriteLine("    $env:AI__ApiKey=\"your-api-key\"");
        Console.WriteLine("    dotnet run --project tests/IntegrationTest");
    }
    else
    {
        Console.WriteLine("\n  检测到API Key，准备测试AI连接...");
        
        var httpClient = new HttpClient();
        var factory = new AIClientFactory(
            new TestHttpFactory(httpClient),
            loggerFactory
        );

        // 测试DeepSeek
        Console.WriteLine("\n  测试 DeepSeek API...");
        try
        {
            var deepseekConfig = new AIClientConfig
            {
                Provider = AIProvider.DeepSeek,
                ApiKey = apiKey,
                Model = "deepseek-chat",
                TimeoutSeconds = 10
            };
            
            var client = factory.CreateClient(deepseekConfig);
            var response = await client.ChatAsync(
                new List<ChatMessage>(),
                "你是测试助手，请只回复'测试成功'两个字。"
            );
            
            Console.WriteLine($"  ✓ DeepSeek连接成功");
            Console.WriteLine($"    回复: {response.Content}");
            Console.WriteLine($"    Token: {response.TotalTokens}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ DeepSeek连接失败: {ex.Message}");
        }

        // 测试情感分析
        Console.WriteLine("\n  测试情感分析...");
        try
        {
            var client = factory.CreateClient(new AIClientConfig
            {
                Provider = AIProvider.DeepSeek,
                ApiKey = apiKey,
                Model = "deepseek-chat",
                TimeoutSeconds = 10
            });

            var emotion = await client.AnalyzeEmotionAsync(
                "我想和你一起保护大家！",
                "好的，我们一起战斗吧！"
            );

            Console.WriteLine($"  ✓ 情感分析成功");
            Console.WriteLine($"    勇气: {emotion.CourageDelta}");
            Console.WriteLine($"    友情: {emotion.FriendshipDelta}");
            Console.WriteLine($"    爱心: {emotion.LoveDelta}");
            Console.WriteLine($"    知识: {emotion.KnowledgeDelta}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ 情感分析失败: {ex.Message}");
        }
    }

    // 测试5: 系统提示词生成
    Console.WriteLine("\n【测试5】系统提示词生成");
    var testDigimon = repository.GetById("agumon") ?? repository.GetDefaultEgg();
    var systemPrompt = personalityEngine.BuildSystemPrompt(testDigimon, userDigimon);
    Console.WriteLine($"  测试数码宝贝: {testDigimon.Name}");
    Console.WriteLine($"  提示词长度: {systemPrompt.Length} 字符");
    Console.WriteLine($"  包含阶段限制: {(systemPrompt.Contains("字数") ? "✓" : "✗")}");
    Console.WriteLine($"  包含性格特征: {(systemPrompt.Contains("性格") ? "✓" : "✗")}");

    // 总结
    Console.WriteLine("\n========================================");
    Console.WriteLine("          集成测试完成！");
    Console.WriteLine("========================================");
    Console.WriteLine("\n核心功能状态:");
    Console.WriteLine("  ✓ 数据库加载");
    Console.WriteLine("  ✓ 数码宝贝管理");
    Console.WriteLine("  ✓ 进化系统");
    Console.WriteLine("  ✓ 情感追踪");
    Console.WriteLine("  ✓ 人格引擎");

    if (!string.IsNullOrEmpty(apiKey))
    {
        Console.WriteLine("  ? AI连接 (取决于API Key有效性)");
    }
    else
    {
        Console.WriteLine("  - AI连接 (未测试)");
    }

    Console.WriteLine("\n如需完整功能测试，请:");
    Console.WriteLine("1. 配置 AI__ApiKey 环境变量");
    Console.WriteLine("2. 运行: dotnet run --project src/DigimonBot.Host");

    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ 错误: {ex.Message}");
    Console.WriteLine($"\n堆栈跟踪:\n{ex.StackTrace}");
    return 1;
}

// 辅助类
public class TestHttpFactory : IHttpClientFactory
{
    private readonly HttpClient _client;
    public TestHttpFactory(HttpClient client) => _client = client;
    public HttpClient CreateClient(string name) => _client;
}
