using DigimonBot.Core.Models.Kimi;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Messaging.Commands;

/// <summary>
/// Kimi代码助手命令 - 通过 Kimi ACP 协议进行AI辅助编程
/// 流程：用户发送聊天消息 → 通过ACP协议调用Kimi服务 → 自动提交Git → 返回摘要消息+克隆链接
/// </summary>
public class KimiCommand : ICommand
{
    private readonly IKimiRepositoryManager _repoManager;
    private readonly IKimiExecutionService _executionService;
    private readonly IKimiRepositoryRepository _repoRepository;
    private readonly IKimiServiceClient _serviceClient;
    private readonly IKimiAgentMonitor _agentMonitor;
    private readonly IGitCommitService _gitCommitService;
    private readonly IGitHttpServer? _gitHttpServer;
    private readonly ILogger<KimiCommand> _logger;

    // 配置参数（由DI注入，从KimiConfigService获取）
    private readonly Func<KimiCommandConfig> _getConfig;

    /// <summary>
    /// 当前活跃会话ID（由 kimi service 管理，.NET 端仅跟踪当前选择的会话）
    /// </summary>
    private string? _currentSessionId;

    public KimiCommand(
        IKimiRepositoryManager repoManager,
        IKimiExecutionService executionService,
        IKimiRepositoryRepository repoRepository,
        IKimiServiceClient serviceClient,
        Func<KimiCommandConfig> getConfig,
        IKimiAgentMonitor agentMonitor,
        IGitCommitService gitCommitService,
        IGitHttpServer? gitHttpServer,
        ILogger<KimiCommand> logger)
    {
        _repoManager = repoManager;
        _executionService = executionService;
        _repoRepository = repoRepository;
        _serviceClient = serviceClient;
        _getConfig = getConfig;
        _agentMonitor = agentMonitor;
        _gitCommitService = gitCommitService;
        _gitHttpServer = gitHttpServer;
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
                "--list-sessions" => await HandleListSessions(),
                "--switch-session" => await HandleSwitchSession(args),
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

            // 切换仓库时重置会话状态 - 会话由 kimi service 管理
            _currentSessionId = null;
            _logger.LogInformation("[KimiCommand] 创建新仓库 '{Name}'，已重置会话状态", repo.Name);

            return new CommandResult
            {
                Success = true,
                Message = $"""
                    ✅ 仓库创建成功！
                    📦 名称: {repo.Name}
                    📁 路径: {repo.Path}
                    🔄 已设为当前活动仓库
                    💬 会话已重置
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

        if (success)
        {
            // 切换仓库时重置会话状态 - 会话由 kimi service 管理
            _currentSessionId = null;
            _logger.LogInformation("[KimiCommand] 切换到仓库 '{Name}'，已重置会话状态", name);
        }

        return success
            ? new CommandResult { Success = true, Message = $"✅ 已切换到仓库: {name}\n💬 会话已重置，新的对话将创建新会话" }
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

        var sessionInfo = _currentSessionId != null
            ? $"\n当前会话: {TruncateSessionId(_currentSessionId)}"
            : "\n当前会话: 无（将自动创建）";

        return new CommandResult
        {
            Success = true,
            Message = $"""
                📦 **当前活动仓库**
                名称: {repo.Name}
                路径: {repo.Path}
                创建时间: {repo.CreatedAt:yyyy-MM-dd HH:mm}
                会话数: {repo.SessionCount}{sessionInfo}
                """
        };
    }

    /// <summary>
    /// 列出当前仓库的所有会话（从 kimi service 获取）
    /// </summary>
    private async Task<CommandResult> HandleListSessions()
    {
        try
        {
            var repo = await _repoManager.GetActiveRepositoryAsync();
            if (repo == null)
            {
                return new CommandResult
                {
                    Success = false,
                    Message = "📦 当前没有活动仓库。请先使用 `/kimi --new-repo [名称]` 创建一个。"
                };
            }

            var allSessions = await _serviceClient.ListSessionsAsync();

            // 筛选属于当前仓库工作目录的会话
            var repoSessions = allSessions
                .Where(s => string.Equals(
                    Path.GetFullPath(s.WorkDir),
                    Path.GetFullPath(repo.Path),
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.LastActivity)
                .ToList();

            if (repoSessions.Count == 0)
            {
                return new CommandResult
                {
                    Success = true,
                    Message = $"💬 仓库 **{repo.Name}** 暂无活跃会话。发送消息将自动创建新会话。"
                };
            }

            var lines = new List<string> { $"💬 **仓库 {repo.Name} 的会话列表** (共 {repoSessions.Count} 个):\n" };

            foreach (var session in repoSessions)
            {
                var shortId = session.Id.Length > 8 ? session.Id[..8] : session.Id;
                var isCurrent = session.Id == _currentSessionId ? " ✅ [当前]" : "";
                var lastActivity = session.LastActivity.ToString("yyyy-MM-dd HH:mm");
                lines.Add($"  • {shortId}...{isCurrent} - 消息数: {session.MessageCount}, 最后活动: {lastActivity}");
            }

            lines.Add($"\n使用 `/kimi --switch-session <会话ID前缀>` 切换会话");

            return new CommandResult
            {
                Success = true,
                Message = string.Join("\n", lines)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[KimiCommand] 列出会话失败");
            return new CommandResult
            {
                Success = false,
                Message = $"❌ 获取会话列表失败: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 切换到指定会话
    /// </summary>
    private async Task<CommandResult> HandleSwitchSession(string[] args)
    {
        if (args.Length < 2)
        {
            return new CommandResult
            {
                Success = false,
                Message = "❌ 请指定会话ID: `/kimi --switch-session <会话ID或前缀>`"
            };
        }

        var sessionIdPrefix = args[1];

        try
        {
            // 先尝试精确匹配
            var session = await _serviceClient.GetSessionAsync(sessionIdPrefix);
            if (session != null)
            {
                _currentSessionId = session.Id;
                var shortId = session.Id.Length > 8 ? session.Id[..8] : session.Id;
                return new CommandResult
                {
                    Success = true,
                    Message = $"✅ 已切换到会话: {shortId}... (消息数: {session.MessageCount})"
                };
            }

            // 尝试前缀匹配
            var allSessions = await _serviceClient.ListSessionsAsync();
            var repo = await _repoManager.GetActiveRepositoryAsync();

            var matches = allSessions
                .Where(s => s.Id.StartsWith(sessionIdPrefix, StringComparison.OrdinalIgnoreCase))
                .Where(s => repo == null || string.Equals(
                    Path.GetFullPath(s.WorkDir),
                    Path.GetFullPath(repo.Path),
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                return new CommandResult
                {
                    Success = false,
                    Message = $"❌ 未找到匹配的会话: '{sessionIdPrefix}'。使用 `/kimi --list-sessions` 查看可用会话。"
                };
            }

            if (matches.Count > 1)
            {
                var matchList = string.Join("\n", matches.Select(s =>
                {
                    var shortId = s.Id.Length > 8 ? s.Id[..8] : s.Id;
                    return $"  • {shortId}...";
                }));
                return new CommandResult
                {
                    Success = false,
                    Message = $"⚠️ 前缀 '{sessionIdPrefix}' 匹配到多个会话，请提供更长的前缀:\n{matchList}"
                };
            }

            var matched = matches[0];
            _currentSessionId = matched.Id;
            var matchedShortId = matched.Id.Length > 8 ? matched.Id[..8] : matched.Id;

            return new CommandResult
            {
                Success = true,
                Message = $"✅ 已切换到会话: {matchedShortId}... (消息数: {matched.MessageCount})"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[KimiCommand] 切换会话失败");
            return new CommandResult
            {
                Success = false,
                Message = $"❌ 切换会话失败: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 处理Kimi聊天执行（带忙碌保护 + 自动Git提交 + 克隆链接）
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

            // 构建聊天消息（将所有参数合并为一条消息）
            var chatMessage = string.Join(" ", args);

            // 更新仓库使用信息
            await _repoRepository.UpdateLastUsedAsync(repo.Name);
            await _repoRepository.IncrementSessionCountAsync(repo.Name);

            // 通过 ACP 协议执行 Kimi 聊天（传递当前会话ID以维持上下文）
            var result = await _executionService.ExecuteAsync(repo.Path, chatMessage, _currentSessionId, config.DefaultTimeoutSeconds, cancellationToken);

            // 跟踪返回的会话ID，后续消息将复用同一会话
            if (result.SessionId != null)
            {
                _currentSessionId = result.SessionId;
            }

            // 自动提交 Git（如果启用且执行成功）
            if (result.Success && config.AutoCommit)
            {
                try
                {
                    var commitHash = await _gitCommitService.CommitChangesAsync(
                        repo.Path, context.OriginalUserId, chatMessage, result.DurationMs);

                    if (commitHash != null)
                    {
                        result.CommitHash = commitHash;
                        result.Committed = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[KimiCommand] 自动Git提交失败，但不影响执行结果");
                }
            }

            // 格式化输出（包含提交信息和克隆链接）
            return FormatExecutionResult(result, repo.Name);
        }
        finally
        {
            // 无论成功、失败还是取消，都必须释放状态
            _agentMonitor.EndTask();
        }
    }

    /// <summary>
    /// 格式化执行结果（包含Git提交信息和克隆链接）
    /// </summary>
    private CommandResult FormatExecutionResult(ExecutionResult result, string repoName)
    {
        const int ChunkSize = 2000;

        if (result.Success)
        {
            var header = $"🤖 **Kimi 执行结果** (仓库: {repoName}, 耗时: {result.DurationMs}ms)\n\n";

            // 添加 Git 提交信息和克隆链接
            string footer;
            if (result.Committed && result.CommitHash != null)
            {
                var shortHash = result.CommitHash.Length >= 8 ? result.CommitHash[..8] : result.CommitHash;
                footer = $"\n\n✅ 已自动提交到 Git\n提交: {shortHash}";
            }
            else
            {
                footer = "\n\nℹ️ 无文件变更";
            }
            footer += FormatCloneUrl(repoName);

            var fullMessage = header + result.Output + footer;
            var parts = SplitMessage(fullMessage, ChunkSize);

            return new CommandResult
            {
                Success = true,
                Message = parts[0],
                AdditionalMessages = parts.Skip(1).ToList()
            };
        }
        else
        {
            var error = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
            var fullMessage = $"❌ **Kimi执行失败** (仓库: {repoName}, 耗时: {result.DurationMs}ms)\n\n{error}";
            var parts = SplitMessage(fullMessage, ChunkSize);

            return new CommandResult
            {
                Success = false,
                Message = parts[0],
                AdditionalMessages = parts.Skip(1).ToList()
            };
        }
    }

    /// <summary>
    /// 将长消息拆分为不超过 chunkSize 字符的多个部分
    /// </summary>
    private static List<string> SplitMessage(string message, int chunkSize)
    {
        var parts = new List<string>();
        var offset = 0;
        while (offset < message.Length)
        {
            var length = Math.Min(chunkSize, message.Length - offset);
            parts.Add(message.Substring(offset, length));
            offset += length;
        }
        return parts;
    }

    /// <summary>
    /// 格式化克隆链接
    /// </summary>
    private string FormatCloneUrl(string repoName)
    {
        if (_gitHttpServer?.IsRunning != true)
            return "";

        var url = _gitHttpServer.GetCloneUrl(repoName);
        return $"\n\n💡 克隆仓库:\ngit clone {url}";
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalMinutes >= 1
            ? $"{(int)elapsed.TotalMinutes}分{elapsed.Seconds}秒"
            : $"{elapsed.Seconds}秒";
    }

    /// <summary>
    /// 截断会话ID用于显示
    /// </summary>
    private static string TruncateSessionId(string sessionId)
    {
        return sessionId.Length > 8 ? sessionId[..8] + "..." : sessionId;
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
                  --new-repo [名称]     创建新仓库（重置会话）
                  --list-repos          列出所有仓库
                  --switch-repo <名称>  切换到指定仓库（重置会话）
                  --current-repo        显示当前仓库和会话信息

                会话管理:
                  --list-sessions       列出当前仓库的所有会话
                  --switch-session <ID> 切换到指定会话（支持ID前缀匹配）

                聊天模式:
                  直接输入消息即可与 Kimi 对话
                  Kimi 将自动在当前仓库中执行代码操作
                  执行完成后会自动提交 Git 并返回克隆链接
                  同一仓库下会话会自动复用，保持上下文连贯

                示例:
                  /kimi --new-repo my-project
                  /kimi --switch-repo my-project
                  /kimi 用Python写个Hello World
                  /kimi 分析这个项目的代码结构
                  /kimi --list-sessions
                  /kimi --switch-session abc123
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

    /// <summary>是否自动提交Git</summary>
    public bool AutoCommit { get; set; } = true;
}
