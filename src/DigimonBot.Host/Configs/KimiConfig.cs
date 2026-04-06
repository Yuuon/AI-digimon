using System.Text.Json.Serialization;

namespace DigimonBot.Host.Configs;

/// <summary>
/// Kimi Agent 配置根类
/// </summary>
public class KimiConfig
{
    /// <summary>
    /// 访问控制配置
    /// </summary>
    public AccessControlConfig AccessControl { get; set; } = new();

    /// <summary>
    /// 执行配置
    /// </summary>
    public ExecutionConfig Execution { get; set; } = new();

    /// <summary>
    /// 输出配置
    /// </summary>
    public OutputConfig Output { get; set; } = new();

    /// <summary>
    /// Git配置
    /// </summary>
    public GitConfig Git { get; set; } = new();
}

/// <summary>
/// 访问控制配置
/// </summary>
public class AccessControlConfig
{
    /// <summary>
    /// 访问模式：open（所有人可访问）/ whitelist（仅白名单可访问）
    /// </summary>
    public string Mode { get; set; } = "open";

    /// <summary>
    /// 白名单用户ID列表
    /// </summary>
    public List<string> Whitelist { get; set; } = new();

    /// <summary>
    /// 非白名单用户访问权限：read-only（只读）/ restricted（完全禁止）
    /// </summary>
    public string NonWhitelistAccess { get; set; } = "read-only";
}

/// <summary>
/// 执行配置
/// </summary>
public class ExecutionConfig
{
    /// <summary>
    /// Kimi CLI 路径（默认使用系统PATH中的kimi）
    /// </summary>
    public string KimiCliPath { get; set; } = "kimi";

    /// <summary>
    /// 默认执行超时时间（秒）
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 最大允许的超时时间（秒）
    /// </summary>
    public int MaxTimeoutSeconds { get; set; } = 600;

    /// <summary>
    /// 工作空间基础路径
    /// </summary>
    public string BasePath { get; set; } = "./kimi-workspace";

    /// <summary>
    /// Kimi Web 服务基础URL
    /// </summary>
    public string KimiWebBaseUrl { get; set; } = "http://127.0.0.1:5494";

    /// <summary>
    /// Kimi Web 服务端口
    /// </summary>
    public int KimiWebPort { get; set; } = 5494;

    /// <summary>
    /// 是否自动管理 kimi web 服务进程生命周期
    /// </summary>
    public bool AutoManageKimiWeb { get; set; } = false;

    /// <summary>
    /// 是否自动批准工具调用（YOLO 模式）。启用后，AI 可以自动执行 Shell 命令和文件操作。
    /// </summary>
    public bool AutoApproveTools { get; set; } = true;
}

/// <summary>
/// 输出配置
/// </summary>
public class OutputConfig
{
    /// <summary>
    /// 默认输出模式：summary（摘要）/ full（完整）
    /// </summary>
    public string DefaultMode { get; set; } = "summary";

    /// <summary>
    /// 摘要模式最大长度
    /// </summary>
    public int MaxSummaryLength { get; set; } = 1000;

    /// <summary>
    /// 消息最大长度
    /// </summary>
    public int MaxMessageLength { get; set; } = 2000;

    /// <summary>
    /// 是否包含克隆URL
    /// </summary>
    public bool IncludeCloneUrl { get; set; } = true;
}

/// <summary>
/// Git配置
/// </summary>
public class GitConfig
{
    /// <summary>
    /// 是否自动提交
    /// </summary>
    public bool AutoCommit { get; set; } = true;

    /// <summary>
    /// 默认分支名称
    /// </summary>
    public string DefaultBranch { get; set; } = "main";

    /// <summary>
    /// 是否启用 Git HTTP 服务器
    /// </summary>
    public bool EnableHttpServer { get; set; } = true;

    /// <summary>
    /// Git HTTP 服务器端口
    /// </summary>
    public int HttpPort { get; set; } = 8080;

    /// <summary>
    /// Git 公开访问 URL
    /// </summary>
    public string PublicGitUrl { get; set; } = "http://localhost:8080";
}
