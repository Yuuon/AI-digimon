using DigimonBot.Core.Models.Kimi;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// Kimi代码助手命令 - 执行Kimi CLI命令进行AI辅助编程
/// </summary>
public class KimiCommand : ICommand
{
    private readonly IKimiRepositoryManager _repoManager;
    private readonly IKimiExecutionService _executionService;
    private readonly IKimiRepositoryRepository _repoRepository;
    private readonly IKimiAgentMonitor _agentMonitor;
    private readonly ILogger<KimiCommand> _logger;

    // 配置参数（由DI注入，从KimiConfigService获取）
    private readonly Func<KimiCommandConfig> _getConfig;

    public KimiCommand(
        IKimiRepositoryManager repoManager,
        IKimiExecutionService executionService,
        IKimiRepositoryRepository repoRepository,
        Func<KimiCommandConfig> getConfig,
        IKimiAgentMonitor agentMonitor,
        ILogger<KimiCommand> logger)
    {
        _repoManager = repoManager;
        _executionService = executionService;
        _repoRepository = repoRepository;
        _getConfig = getConfig;
        _agentMonitor = agentMonitor;
        _logger = logger;
    }

    public string Name => "kimi";
    public string[] Aliases => new[] { "kimichat", "kimi助手" };
    public string Description => "Kimi代码助手 - AI辅助编程";

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        try
        {
            var config = _getConfig();

            // 访问控制检查
            if (!CheckAccess(context.OriginalUserId, config))
            {
                return new CommandResult
                {
                    Success = false,
                    Message = "⛔ 你没有权限使用此命令。请联系管理员获取访问权限。"
                };
            }

            var args = context.Args ?? Array.Empty<string>();
            if (args.Length == 0)
            {
                return GetHelpResult();
            }

            var firstArg = args[0].ToLowerInvariant();

            // --status 和 --cancel 无需等待，直接处理（绕过忙碌检查）
            return firstArg switch
            {
                "--help" or "-h" => GetHelpResult(),
                "--status" => HandleStatus(),
                "--cancel" or "--interrupt" => HandleCancel(),
                "--new-repo" => await HandleNewRepo(args, context),
                "--list-repos" => await HandleListRepos(),
                "--switch-repo" => await HandleSwitchRepo(args),
                "--current-repo" => await HandleCurrentRepo(),
                _ => await HandleKimiExecution(args, context, config)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[KimiCommand] 命令执行异常");
            return new CommandResult
            {
                Success = false,
                Message = $"❌ 命令执行失败: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 检查用户访问权限
    /// </summary>
    private static bool CheckAccess(string userId, KimiCommandConfig config)
    {
        // 白名单用户始终允许
        if (config.Whitelist.Contains(userId))
            return true;

        // 开放模式允许所有人
        if (string.Equals(config.AccessMode, "open", StringComparison.OrdinalIgnoreCase))
            return true;

        // 白名单模式下，检查非白名单用户的权限
        if (string.Equals(config.AccessMode, "whitelist", StringComparison.OrdinalIgnoreCase))
        {
            // restricted = 完全禁止
            if (string.Equals(config.NonWhitelistAccess, "restricted", StringComparison.OrdinalIgnoreCase))
                return false;

            // read-only = 允许读取操作（默认放行，具体限制在执行时判断）
            if (string.Equals(config.NonWhitelistAccess, "read-only", StringComparison.OrdinalIgnoreCase))
                return true;

            // 未知的NonWhitelistAccess值，安全默认拒绝
            return false;
        }

        // 未知的访问模式，安全默认拒绝
        return false;
    }

    /// <summary>
    /// 检查是否为只读用户（非白名单 + read-only模式）
    /// </summary>
    private static bool IsReadOnly(string userId, KimiCommandConfig config)
    {
        if (config.Whitelist.Contains(userId))
            return false;

        if (string.Equals(config.AccessMode, "whitelist", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(config.NonWhitelistAccess, "read-only", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 显示当前Agent状态
    /// </summary>
    private CommandResult HandleStatus()
    {
        if (!_agentMonitor.IsBusy)
        {
            return new CommandResult
            {
                Success = true,
                Message = "✅ **Kimi Agent 状态**: 空闲，可接受新任务。"
            };
        }

        var task = _agentMonitor.CurrentTask;
        if (task == null)
        {
            return new CommandResult
            {
                Success = true,
                Message = "⏳ **Kimi Agent 状态**: 忙碌中。"
            };
        }

        var elapsed = task.Elapsed;
        var elapsedStr = elapsed.TotalMinutes >= 1
            ? $"{(int)elapsed.TotalMinutes}分{elapsed.Seconds}秒"
            : $"{elapsed.Seconds}秒";

        return new CommandResult
        {
            Success = true,
            Message = $"""
                ⏳ **Kimi Agent 状态**: 忙碌中

                发起用户: {task.UserId}
                任务内容: {task.Command}
                已运行: {elapsedStr}

                使用 `/kimi --cancel` 可中断当前任务。
                """
        };
    }

    /// <summary>
    /// 取消当前正在运行的任务
    /// </summary>
    private CommandResult HandleCancel()
    {
        if (!_agentMonitor.IsBusy)
        {
            return new CommandResult
            {
                Success = false,
                Message = "ℹ️ 当前没有正在运行的Kimi任务。"
            };
        }

        var cancelled = _agentMonitor.TryCancel();
        return cancelled
            ? new CommandResult { Success = true, Message = "🛑 已发送中断信号，任务正在停止..." }
            : new CommandResult { Success = false, Message = "⚠️ 无法中断任务（任务可能已结束）。" };
    }

    /// <summary>
    /// 处理创建新仓库
    /// </summary>
    private async Task<CommandResult> HandleNewRepo(string[] args, CommandContext context)
    {
        string? repoName = args.Length > 1 ? args[1] : null;

        try
        {
            var repo = await _repoManager.CreateRepositoryAsync(repoName, context.OriginalUserId);
            return new CommandResult
            {
                Success = true,
                Message = $"""
                    ✅ 仓库创建成功！
                    📦 名称: {repo.Name}
                    📁 路径: {repo.Path}
                    🔄 已设为当前活动仓库
                    """
            };
        }
        catch (InvalidOperationException ex)
        {
            return new CommandResult
            {
                Success = false,
                Message = $"❌ 创建仓库失败: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 处理列出所有仓库
    /// </summary>
    private async Task<CommandResult> HandleListRepos()
    {
        var repos = (await _repoManager.ListRepositoriesAsync()).ToList();

        if (repos.Count == 0)
        {
            return new CommandResult
            {
                Success = true,
                Message = "📦 暂无仓库。使用 `/kimi --new-repo [名称]` 创建一个。"
            };
        }

        var lines = new List<string> { $"📦 **仓库列表** (共 {repos.Count} 个):\n" };

        foreach (var repo in repos)
        {
            var active = repo.IsActive ? " ✅ [活动]" : "";
            var lastUsed = repo.LastUsedAt.HasValue
                ? repo.LastUsedAt.Value.ToString("yyyy-MM-dd HH:mm")
                : "从未使用";
            lines.Add($"  • {repo.Name}{active} - 会话: {repo.SessionCount}, 最后使用: {lastUsed}");
        }

        return new CommandResult
        {
            Success = true,
            Message = string.Join("\n", lines)
        };
    }

    /// <summary>
    /// 处理切换仓库
    /// </summary>
    private async Task<CommandResult> HandleSwitchRepo(string[] args)
    {
        if (args.Length < 2)
        {
            return new CommandResult
            {
                Success = false,
                Message = "❌ 请指定仓库名称: `/kimi --switch-repo <名称>`"
            };
        }

        var name = args[1];
        var success = await _repoManager.SwitchRepositoryAsync(name);

        return success
            ? new CommandResult { Success = true, Message = $"✅ 已切换到仓库: {name}" }
            : new CommandResult { Success = false, Message = $"❌ 仓库 '{name}' 不存在。使用 `/kimi --list-repos` 查看可用仓库。" };
    }

    /// <summary>
    /// 处理查看当前仓库
    /// </summary>
    private async Task<CommandResult> HandleCurrentRepo()
    {
        var repo = await _repoManager.GetActiveRepositoryAsync();

        if (repo == null)
        {
            return new CommandResult
            {
                Success = true,
                Message = "📦 当前没有活动仓库。使用 `/kimi --new-repo [名称]` 创建一个。"
            };
        }

        return new CommandResult
        {
            Success = true,
            Message = $"""
                📦 **当前活动仓库**
                名称: {repo.Name}
                路径: {repo.Path}
                创建时间: {repo.CreatedAt:yyyy-MM-dd HH:mm}
                会话数: {repo.SessionCount}
                """
        };
    }

    /// <summary>
    /// 处理Kimi CLI执行（带忙碌保护）
    /// </summary>
    private async Task<CommandResult> HandleKimiExecution(string[] args, CommandContext context, KimiCommandConfig config)
    {
        // 只读用户不能执行Kimi命令
        if (IsReadOnly(context.OriginalUserId, config))
        {
            return new CommandResult
            {
                Success = false,
                Message = "⛔ 你的权限为只读，无法执行Kimi命令。只能使用仓库管理命令（--list-repos, --current-repo）。"
            };
        }

        // 检查Agent是否空闲，若忙碌则拒绝
        if (!_agentMonitor.TryBeginTask(context.OriginalUserId, string.Join(" ", args), out var cancellationToken))
        {
            var task = _agentMonitor.CurrentTask;
            var busyMsg = task != null
                ? $"⏳ **Kimi Agent 正忙** - 任务执行中（已运行 {FormatElapsed(task.Elapsed)}）。\n使用 `/kimi --status` 查看详情，`/kimi --cancel` 中断任务。"
                : "⏳ **Kimi Agent 正忙** - 请稍候再试。";

            return new CommandResult { Success = false, Message = busyMsg };
        }

        try
        {
            // 确保有可用仓库
            var repo = await _repoManager.EnsureRepositoryExistsAsync(context.OriginalUserId);

            // 构建Kimi CLI参数
            var kimiArgs = string.Join(" ", args);

            // 更新仓库使用信息
            await _repoRepository.UpdateLastUsedAsync(repo.Name);
            await _repoRepository.IncrementSessionCountAsync(repo.Name);

            // 执行Kimi CLI（传入取消令牌）
            var result = await _executionService.ExecuteAsync(repo.Path, kimiArgs, config.DefaultTimeoutSeconds, cancellationToken);

            // 格式化输出
            return FormatExecutionResult(result, repo.Name);
        }
        finally
        {
            // 无论成功、失败还是取消，都必须释放状态
            _agentMonitor.EndTask();
        }
    }

    /// <summary>
    /// 格式化执行结果
    /// </summary>
    private static CommandResult FormatExecutionResult(ExecutionResult result, string repoName)
    {
        if (result.Success)
        {
            var output = result.Output;
            // 截断过长输出
            if (output.Length > 2000)
            {
                output = output[..1900] + $"\n\n... (输出已截断，共 {result.Output.Length} 字符)";
            }

            return new CommandResult
            {
                Success = true,
                Message = $"""
                    ✅ **Kimi执行完成** (仓库: {repoName}, 耗时: {result.DurationMs}ms)

                    {output}
                    """
            };
        }
        else
        {
            var error = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
            if (error.Length > 1000)
            {
                error = error[..900] + $"\n\n... (错误信息已截断)";
            }

            return new CommandResult
            {
                Success = false,
                Message = $"""
                    ❌ **Kimi执行失败** (仓库: {repoName}, 耗时: {result.DurationMs}ms, 退出码: {result.ExitCode})

                    {error}
                    """
            };
        }
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalMinutes >= 1
            ? $"{(int)elapsed.TotalMinutes}分{elapsed.Seconds}秒"
            : $"{elapsed.Seconds}秒";
    }

    /// <summary>
    /// 获取帮助信息
    /// </summary>
    private static CommandResult GetHelpResult()
    {
        return new CommandResult
        {
            Success = true,
            Message = """
                🤖 **Kimi 代码助手**

                用法: /kimi [选项] [消息]

                Agent管理:
                  --status              查看当前Agent执行状态
                  --cancel              中断当前正在运行的任务
                  --interrupt           同上（--cancel的别名）

                仓库管理:
                  --new-repo [名称]     创建新仓库
                  --list-repos          列出所有仓库
                  --switch-repo <名称>  切换到指定仓库
                  --current-repo        显示当前仓库

                Kimi CLI 选项:
                  --prompt, -p <文本>   发送消息给Kimi
                  --model, -m <模型>    指定模型
                  --yolo, -y           自动确认所有操作
                  --plan               计划模式
                  --thinking           启用思考模式

                示例:
                  /kimi --new-repo my-project
                  /kimi --switch-repo my-project
                  /kimi 用Python写个Hello World
                  /kimi --status
                  /kimi --cancel
                """
        };
    }
}

/// <summary>
/// Kimi命令配置（从KimiConfigService提取，避免跨项目依赖）
/// </summary>
public class KimiCommandConfig
{
    /// <summary>访问模式: open / whitelist</summary>
    public string AccessMode { get; set; } = "open";

    /// <summary>白名单用户ID列表</summary>
    public List<string> Whitelist { get; set; } = new();

    /// <summary>非白名单用户访问权限: read-only / restricted</summary>
    public string NonWhitelistAccess { get; set; } = "read-only";

    /// <summary>默认超时时间（秒）</summary>
    public int DefaultTimeoutSeconds { get; set; } = 300;
}
