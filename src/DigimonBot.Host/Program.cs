using DigimonBot.AI.Services;
using DigimonBot.Core.Events;
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
                
                // 注册性格配置服务
                services.AddSingleton<IPersonalityConfigService>(provider => 
                {
                    var logger = provider.GetRequiredService<ILogger<PersonalityConfigService>>();
                    return new PersonalityConfigService(logger, "Data/digimon_personalities.json");
                });
                
                // 注册对话配置服务
                services.AddSingleton<IDialogueConfigService>(provider => 
                {
                    var logger = provider.GetRequiredService<ILogger<DialogueConfigService>>();
                    return new DialogueConfigService(logger, "Data/digimon_dialogue_config.json");
                });
                
                // 注册酒馆配置服务（需要先于其他酒馆服务注册）
                services.AddSingleton<ITavernConfigService>(provider => 
                {
                    var logger = provider.GetRequiredService<ILogger<TavernConfigService>>();
                    return new TavernConfigService(logger, "Data/tavern_config.json");
                });
                
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
                    var personalityConfig = provider.GetRequiredService<IPersonalityConfigService>();
                    var dialogueConfig = provider.GetRequiredService<IDialogueConfigService>();
                    var logger = provider.GetRequiredService<ILogger<BattleService>>();
                    return new BattleService(
                        aiClient, 
                        personalityConfig,
                        dialogueConfig,
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
                        provider.GetRequiredService<IPersonalityConfigService>(),
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
                    
                    // 添加重载酒馆配置指令
                    registry.Register(new ReloadTavernConfigCommand(
                        provider.GetRequiredService<ITavernConfigService>(),
                        adminConfig,
                        provider.GetRequiredService<ILogger<ReloadTavernConfigCommand>>()));
                    
                    // 添加特别关注管理指令
                    registry.Register(new SpecialFocusCommand(
                        provider.GetRequiredService<ITavernConfigService>(),
                        adminConfig,
                        provider.GetRequiredService<ILogger<SpecialFocusCommand>>()));
                    
                    // 添加重载性格配置指令
                    registry.Register(new ReloadPersonalityConfigCommand(
                        provider.GetRequiredService<IPersonalityConfigService>(),
                        adminConfig,
                        provider.GetRequiredService<ILogger<ReloadPersonalityConfigCommand>>()));
                    
                    // 添加重载对话配置指令
                    registry.Register(new ReloadDialogueConfigCommand(
                        provider.GetRequiredService<IDialogueConfigService>(),
                        adminConfig,
                        provider.GetRequiredService<ILogger<ReloadDialogueConfigCommand>>()));
                    
                    // 添加进化相关指令
                    registry.Register(new EvolutionListCommand(
                        digimonManager,
                        repository,
                        evolutionEngine,
                        provider.GetRequiredService<ILogger<EvolutionListCommand>>()));
                    
                    registry.Register(new EvolutionSelectCommand(
                        digimonManager,
                        repository,
                        evolutionEngine,
                        provider.GetRequiredService<Core.Events.IEventPublisher>(),
                        provider.GetRequiredService<IPersonalityEngine>(),
                        provider.GetRequiredService<ILogger<EvolutionSelectCommand>>()));

                    // 注册 Kimi 代码助手命令
                    var kimiConfigSvc = provider.GetRequiredService<KimiConfigService>();
                    registry.Register(new KimiCommand(
                        provider.GetRequiredService<IKimiRepositoryManager>(),
                        provider.GetRequiredService<IKimiExecutionService>(),
                        provider.GetRequiredService<IKimiRepositoryRepository>(),
                        provider.GetRequiredService<IKimiServiceClient>(),
                        () =>
                        {
                            var currentCfg = kimiConfigSvc.CurrentConfig;
                            return new KimiCommandConfig
                            {
                                AccessMode = currentCfg.AccessControl.Mode,
                                Whitelist = new List<string>(currentCfg.AccessControl.Whitelist),
                                NonWhitelistAccess = currentCfg.AccessControl.NonWhitelistAccess,
                                DefaultTimeoutSeconds = currentCfg.Execution.DefaultTimeoutSeconds,
                                AutoCommit = currentCfg.Git.AutoCommit
                            };
                        },
                        provider.GetRequiredService<IKimiAgentMonitor>(),
                        provider.GetRequiredService<IGitCommitService>(),
                        provider.GetService<IGitHttpServer>(),
                        provider.GetRequiredService<ILogger<KimiCommand>>()));

                    // 注册自定义命令列表命令
                    registry.Register(new ListCustomCommandsCommand(
                        provider.GetRequiredService<ICustomCommandRepository>()));

                    return registry;
                });

                // 注册 Kimi Agent 服务
                services.AddSingleton<KimiConfigService>();
                services.AddSingleton<IKimiAgentMonitor, KimiAgentMonitor>();

                var kimiDbInitializer = new KimiDatabaseInitializer("Data Source=Data/kimi_data.db");
                kimiDbInitializer.Initialize();
                services.AddSingleton(kimiDbInitializer);

                services.AddSingleton<IKimiRepositoryRepository, KimiRepositoryRepository>();

                // 注册自定义命令服务
                services.AddSingleton<ICustomCommandRepository, DigimonBot.Data.Repositories.Sqlite.CustomCommandRepository>();
                services.AddSingleton<ICustomCommandExecutor>(provider =>
                {
                    var kimiConfig = provider.GetRequiredService<KimiConfigService>();
                    return new DigimonBot.Data.Services.CustomCommandExecutor(
                        kimiConfig.CurrentConfig.Execution.BasePath,
                        provider.GetRequiredService<ILogger<DigimonBot.Data.Services.CustomCommandExecutor>>(),
                        kimiConfig.CurrentConfig.Execution.DefaultTimeoutSeconds);
                });

                services.AddSingleton<IKimiRepositoryManager>(provider =>
                {
                    var kimiConfig = provider.GetRequiredService<KimiConfigService>();
                    return new KimiRepositoryManager(
                        provider.GetRequiredService<IKimiRepositoryRepository>(),
                        provider.GetRequiredService<ILogger<KimiRepositoryManager>>(),
                        kimiConfig.CurrentConfig.Execution.BasePath,
                        kimiConfig.CurrentConfig.Git.DefaultBranch,
                        kimiConfig.CurrentConfig.Execution.GitCommandTimeoutMs);
                });

                // 注册 Kimi ACP 服务客户端（JSON-RPC over stdio 方式）
                services.AddSingleton<IKimiServiceClient>(provider =>
                {
                    var kimiConfig = provider.GetRequiredService<KimiConfigService>();
                    var options = new DigimonBot.Core.Models.Kimi.KimiServiceOptions
                    {
                        KimiExecutablePath = kimiConfig.CurrentConfig.Execution.KimiCliPath,
                        DefaultWorkDir = kimiConfig.CurrentConfig.Execution.BasePath,
                        TimeoutSeconds = kimiConfig.CurrentConfig.Execution.DefaultTimeoutSeconds,
                        ProcessKillTimeoutMs = kimiConfig.CurrentConfig.Execution.ProcessKillTimeoutMs
                    };
                    return new DigimonBot.AI.Services.KimiServiceClient(
                        options,
                        provider.GetRequiredService<ILogger<DigimonBot.AI.Services.KimiServiceClient>>());
                });

                services.AddSingleton<IKimiExecutionService>(provider =>
                {
                    return new KimiExecutionService(
                        provider.GetRequiredService<IKimiServiceClient>(),
                        provider.GetRequiredService<ILogger<KimiExecutionService>>());
                });

                // 注册 Git 提交服务
                services.AddSingleton<IGitCommitService>(provider =>
                    new GitCommitService(provider.GetRequiredService<ILogger<GitCommitService>>()));

                // 注册 Git HTTP 服务器（条件启用）
                services.AddSingleton<IGitHttpServer>(provider =>
                {
                    var kimiConfig = provider.GetRequiredService<KimiConfigService>();
                    return new GitHttpServer(
                        provider.GetRequiredService<ILogger<GitHttpServer>>(),
                        kimiConfig.CurrentConfig.Execution.BasePath,
                        kimiConfig.CurrentConfig.Git.HttpPort,
                        kimiConfig.CurrentConfig.Git.PublicGitUrl,
                        kimiConfig.CurrentConfig.Git.EnableHttpServer);
                });

                // Git HTTP 服务器注册为托管服务（EnableHttpServer 在运行时检查）
                services.AddHostedService(provider =>
                    (GitHttpServer)provider.GetRequiredService<IGitHttpServer>());

                // 注册消息处理器（传递自定义命令依赖）
                services.AddSingleton<IMessageHandler>(provider =>
                {
                    var kimiConfig = provider.GetService<KimiConfigService>();
                    var whitelist = kimiConfig != null
                        ? new List<string>(kimiConfig.CurrentConfig.AccessControl.Whitelist)
                        : new List<string>();

                    return new DigimonMessageHandler(
                        provider.GetRequiredService<CommandRegistry>(),
                        provider.GetRequiredService<IDigimonManager>(),
                        provider.GetRequiredService<IDigimonRepository>(),
                        provider.GetRequiredService<IAIClient>(),
                        provider.GetRequiredService<IPersonalityEngine>(),
                        provider.GetRequiredService<IEvolutionEngine>(),
                        provider.GetRequiredService<IEmotionTracker>(),
                        provider.GetRequiredService<IEventPublisher>(),
                        provider.GetRequiredService<ILogger<DigimonMessageHandler>>(),
                        provider.GetRequiredService<IGroupModeConfig>(),
                        provider.GetRequiredService<ITavernService>(),
                        provider.GetRequiredService<IGroupChatMonitorService>(),
                        provider.GetService<ICustomCommandRepository>(),
                        provider.GetService<ICustomCommandExecutor>(),
                        whitelist);
                });

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
