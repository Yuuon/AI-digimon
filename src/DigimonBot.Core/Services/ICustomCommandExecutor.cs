using DigimonBot.Core.Models;

namespace DigimonBot.Core.Services;

/// <summary>
/// 自定义命令执行器接口
/// </summary>
public interface ICustomCommandExecutor
{
    /// <summary>
    /// 执行自定义命令
    /// </summary>
    /// <param name="command">命令实体</param>
    /// <param name="args">命令参数</param>
    /// <param name="userId">执行用户ID</param>
    /// <param name="timeoutSeconds">超时时间（秒），默认30秒</param>
    /// <returns>执行结果</returns>
    Task<CustomCommandResult> ExecuteAsync(
        CustomCommand command,
        string[] args,
        string userId,
        int timeoutSeconds = 30);

    /// <summary>
    /// 验证二进制路径是否安全（在基目录内，无目录遍历）
    /// </summary>
    bool ValidatePath(string binaryPath);
}

/// <summary>
/// 自定义命令执行结果
/// </summary>
public class CustomCommandResult
{
    /// <summary>是否执行成功</summary>
    public bool Success { get; set; }

    /// <summary>标准输出内容</summary>
    public string Output { get; set; } = "";

    /// <summary>错误输出内容</summary>
    public string Error { get; set; } = "";

    /// <summary>退出码</summary>
    public int ExitCode { get; set; }

    /// <summary>执行耗时（毫秒）</summary>
    public int DurationMs { get; set; }
}
