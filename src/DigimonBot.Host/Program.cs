using DigimonBot.AI.Services;
using DigimonBot.Core.Services;
using DigimonBot.Data.Database;
using DigimonBot.Data.Repositories;
using DigimonBot.Data.Repositories.Sqlite;
using DigimonBot.Data.Services;
using DigimonBot.Host.Configs;
using DigimonBot.Host.Services;
using DigimonBot.Messaging.Commands;
using DigimonBot.Messaging.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Host;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static Microsoft.Extensions.Hosting.IHostBuilder CreateHostBuilder(string[] args) =>
        Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostContext, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((hostContext, services) =>
            {
                // 配置绑定
                services.Configure<AppSettings>(hostContext.Configuration);
                var settings = hostContext.Configuration.Get<AppSettings>() ?? new AppSettings();

                // 确保数据目录存在
                var dataDir = Path.GetDirectoryName(settings.Data.DigimonDatabasePath);
                if (!string.IsNullOrEmpty(dataDir) && !Directory.Exists(dataDir))
                {
                    Directory.CreateDirectory(dataDir);
                }

                // 注册核心配置数据仓库
                services.AddSingleton<IDigimonRepository>(provider =>
                    new JsonDigimonRepository(settings.Data.DigimonDatabasePath));
                
                services.AddSingleton<IItemRepository>(provider =>
                    new JsonItemRepository(settings.Data.ItemsDatabasePath));

                // 注册数据库初始化器
                services.AddSingleton<DatabaseInitializer>(provider =>
                    new DatabaseInitializer(settings.Data.SqliteConnectionString));

                // 初始化数据库（创建表结构）
                var dbInitializer = new DatabaseInitializer(settings.Data.SqliteConnectionString);
                dbInitializer.Initialize();
                services.AddSingleton(dbInitializer);

                // 注册 SQLite 数据仓库
                services.AddSingleton<IDigimonStateRepository, SqliteDigimonStateRepository>();
                services.AddSingleton<IUserDataRepository, SqliteUserDataRepository>();
                services.AddSingleton<IInventoryRepository, SqliteInventoryRepository>();
                services.AddSingleton<ICheckInRepository, SqliteCheckInRepository>();

                // 注册数码宝贝管理器（使用持久化实现）
                services.AddSingleton<IDigimonManager>(provider =>
                {
                    var stateRepo = provider.GetRequiredService<IDigimonStateRepository>();
                    var digimonRepo = provider.GetRequiredService<IDigimonRepository>();
                    return new PersistentDigimonManager(
                        stateRepo, 
                        digimonRepo, 
                        settings.Data.GoldTokenDivisor);
                });

                // 注册核心服务
                services.AddSingleton<IEvolutionEngine, EvolutionEngine>();
                services.AddSingleton<IEmotionTracker, EmotionTracker>();
                services.AddSingleton<Core.Events.IEventPublisher, EventPublisher>();
                services.AddSingleton<Core.Services.IGroupModeConfig, GroupModeConfig>();

                // 注册HTTP客户端
                services.AddHttpClient();
                
                // 注册AI客户端工厂
                services.AddSingleton<AIClientFactory>();
                
                // 根据配置注册对应的AI客户端
                services.AddSingleton<IAIClient>(provider =>
                {
                    var factory = provider.GetRequiredService<AIClientFactory>();
                    var clientConfig = settings.AI.ToClientConfig();
                    
                    // 验证配置
                    if (string.IsNullOrWhiteSpace(clientConfig.ApiKey))
                    {
                        throw new InvalidOperationException(
                            "AI API Key is not configured. Please set ApiKey in appsettings.json " +
                            "or use environment variable: AI__ApiKey");
                    }
                    
                    return factory.CreateClient(clientConfig);
                });

                services.AddSingleton<IPersonalityEngine, PersonalityEngine>();

                // 注册识图服务
                services.AddSingleton<IVisionService, VisionService>();
                
                // 注册图床上传服务
                services.AddSingleton<IImageUploadService, ImageUploadService>();
                
                // 注册消息历史服务
                services.AddSingleton<IMessageHistoryService, MessageHistoryService>();
                
                // 注册图片URL解析服务（由BotService实现）
                // 注意：BotService是IHostedService，在Host启动后才可用
                // 使用延迟解析避免循环依赖
                services.AddSingleton<Core.Services.IImageUrlResolver>(provider => 
                {
                    // 从已注册的服务中获取BotService实例
                    var botService = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
                        .OfType<BotService>()
                        .FirstOrDefault();
                    if (botService == null)
                    {
                        throw new InvalidOperationException("BotService not found. Make sure it's registered as a hosted service.");
                    }
                    return botService;
                });
                
                // 注册酒馆相关服务
                services.AddSingleton<ITavernCharacterParser, TavernCharacterParser>();
                services.AddSingleton<ITavernService, TavernService>();
                services.AddSingleton<IGroupChatMonitorService, GroupChatMonitorService>();

                // 注册战斗服务
                services.AddSingleton<IBattleService>(provider =>
                {
                    var aiClient = provider.GetRequiredService<IAIClient>();
                    var logger = provider.GetRequiredService<ILogger<BattleService>>();
                    return new BattleService(
                        aiClient, 
                        logger, 
                        settings.Data.BattleProtectionSeconds);
                });

                // 注册命令
                services.AddSingleton<CommandRegistry>(provider =>
                {
                    var registry = new CommandRegistry();
                    var digimonManager = provider.GetRequiredService<IDigimonManager>();
                    var repository = provider.GetRequiredService<IDigimonRepository>();
                    var evolutionEngine = provider.GetRequiredService<IEvolutionEngine>();
                    var userDataRepository = provider.GetRequiredService<IUserDataRepository>();
                    var inventoryRepository = provider.GetRequiredService<IInventoryRepository>();
                    var itemRepository = provider.GetRequiredService<IItemRepository>();

                    registry.Register(new StatusCommand(
                        digimonManager, 
                        repository, 
                        evolutionEngine,
                        settings.Admin,
                        provider.GetRequiredService<ILogger<StatusCommand>>()));
                    registry.Register(new EvolutionPathCommand(
                        digimonManager, 
                        repository, 
                        evolutionEngine,
                        settings.Admin,
                        provider.GetRequiredService<ILogger<EvolutionPathCommand>>()));
                    registry.Register(new ResetCommand(digimonManager));
                    registry.Register(new HelpCommand(registry));
                    
                    // 添加今日人品指令
                    registry.Register(new JrrpCommand());
                    
                    // 添加情感值管理指令（带白名单检查）
                    registry.Register(new SetEmotionCommand(
                        digimonManager,
                        provider.GetRequiredService<IEmotionTracker>(),
                        settings.Admin,
                        provider.GetRequiredService<ILogger<SetEmotionCommand>>()
                    ));
                    
                    // 添加商店和背包指令
                    registry.Register(new ShopCommand(
                        itemRepository,
                        userDataRepository,
                        inventoryRepository,
                        provider.GetRequiredService<ILogger<ShopCommand>>()));
                    
                    registry.Register(new InventoryCommand(
                        inventoryRepository,
                        itemRepository,
                        digimonManager,
                        provider.GetRequiredService<ILogger<InventoryCommand>>()));
                    
                    registry.Register(new UseItemCommand(
                        inventoryRepository,
                        itemRepository,
                        digimonManager,
                        provider.GetRequiredService<ILogger<UseItemCommand>>()));
                    
                    // 添加攻击指令
                    registry.Register(new AttackCommand(
                        digimonManager,
                        provider.GetRequiredService<IDigimonStateRepository>(),
                        repository,
                        provider.GetRequiredService<IBattleService>(),
                        provider.GetRequiredService<ILogger<AttackCommand>>()));
                    
                    // 添加签到指令
                    registry.Register(new CheckInCommand(
                        provider.GetRequiredService<ICheckInRepository>(),
                        inventoryRepository,
                        itemRepository,
                        digimonManager,
                        repository,
                        provider.GetRequiredService<IAIClient>(),
                        provider.GetRequiredService<IPersonalityEngine>(),
                        provider.GetRequiredService<ILogger<CheckInCommand>>()));
                    
                    // 添加识图指令（使用IServiceProvider延迟解析IImageUrlResolver）
                    registry.Register(new WhatIsThisCommand(
                        provider.GetRequiredService<IVisionService>(),
                        provider.GetRequiredService<IMessageHistoryService>(),
                        provider, // IServiceProvider用于延迟解析
                        provider.GetRequiredService<ILogger<WhatIsThisCommand>>()));
                    
                    // 添加酒馆指令
                    var tavernService = provider.GetRequiredService<ITavernService>();
                    var adminConfig = settings.Admin;
                    
                    registry.Register(new TavernToggleCommand(
                        tavernService, adminConfig,
                        provider.GetRequiredService<ILogger<TavernToggleCommand>>()));
                    
                    registry.Register(new ListCharactersCommand(tavernService));
                    
                    registry.Register(new LoadCharacterCommand(
                        tavernService,
                        provider.GetRequiredService<ILogger<LoadCharacterCommand>>()));
                    
                    registry.Register(new TavernChatCommand(
                        tavernService,
                        provider.GetRequiredService<ILogger<TavernChatCommand>>()));
                    
                    // 添加监测状态调试指令
                    registry.Register(new CheckMonitorCommand(
                        provider.GetRequiredService<IGroupChatMonitorService>(),
                        tavernService,
                        provider.GetRequiredService<ILogger<CheckMonitorCommand>>()));

                    return registry;
                });

                // 注册消息处理器
                services.AddSingleton<IMessageHandler, DigimonMessageHandler>();

                // 注册Bot服务
                services.AddHostedService<BotService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            });
}
