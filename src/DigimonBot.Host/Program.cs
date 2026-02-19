using DigimonBot.AI.Services;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;
using DigimonBot.Host.Configs;
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

                // 注册核心服务
                services.AddSingleton<IDigimonRepository>(provider =>
                    new JsonDigimonRepository(settings.Data.DigimonDatabasePath));
                
                services.AddSingleton<IDigimonManager, InMemoryDigimonManager>();
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

                // 注册命令
                services.AddSingleton<CommandRegistry>(provider =>
                {
                    var registry = new CommandRegistry();
                    var digimonManager = provider.GetRequiredService<IDigimonManager>();
                    var repository = provider.GetRequiredService<IDigimonRepository>();
                    var evolutionEngine = provider.GetRequiredService<IEvolutionEngine>();

                    registry.Register(new StatusCommand(digimonManager, repository, evolutionEngine));
                    registry.Register(new EvolutionPathCommand(digimonManager, repository, evolutionEngine));
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
                    // registry.Register(new SimpleJrrpCommand()); // 简单算法版本（可选）

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
