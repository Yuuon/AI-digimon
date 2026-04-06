using System.Diagnostics;
using DigimonBot.Core.Models;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Data.Services;

/// <summary>
/// 自定义命令执行器 - 执行用户注册的自定义二进制命令
/// </summary>
public class CustomCommandExecutor : ICustomCommandExecutor
{
    private readonly string _basePath;
    private readonly ILogger<CustomCommandExecutor> _logger;

    public CustomCommandExecutor(
        string basePath,
        ILogger<CustomCommandExecutor> logger)
    {
        _basePath = basePath;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CustomCommandResult> ExecuteAsync(
        CustomCommand command,
        string[] args,
        string userId,
        int timeoutSeconds = 30)
    {
        var stopwatch = Stopwatch.StartNew();

        // 构建完整路径
        var fullPath = Path.Combine(_basePath, command.BinaryPath);

        // 安全验证：路径必须在基目录内
        if (!ValidatePath(command.BinaryPath))
        {
            _logger.LogWarning("[CustomCmd] 路径验证失败: {Path}, 用户: {User}", command.BinaryPath, userId);
            return new CustomCommandResult
            {
                Success = false,
                Error = "非法的二进制路径",
                ExitCode = -1,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }

        // 检查文件是否存在
        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("[CustomCmd] 找不到可执行文件: {Path}", fullPath);
            return new CustomCommandResult
            {
                Success = false,
                Error = $"找不到可执行文件: {command.BinaryPath}",
                ExitCode = -1,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }

        _logger.LogInformation("[CustomCmd] 执行命令: {Name}, 路径: {Path}, 参数: {Args}, 用户: {User}",
            command.Name, fullPath, string.Join(" ", args), userId);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fullPath,
                WorkingDirectory = Path.GetDirectoryName(fullPath) ?? _basePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // 使用 ArgumentList 以安全方式传递参数（避免注入）
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = psi };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var completed = process.WaitForExit(timeoutSeconds * 1000);
            if (!completed)
            {
                process.Kill(entireProcessTree: true);
                stopwatch.Stop();
                _logger.LogWarning("[CustomCmd] 命令超时: {Name} ({Timeout}s)", command.Name, timeoutSeconds);
                return new CustomCommandResult
                {
                    Success = false,
                    Error = $"命令执行超时（{timeoutSeconds}秒）",
                    ExitCode = -1,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }

            var output = await outputTask;
            var error = await errorTask;
            stopwatch.Stop();

            _logger.LogInformation("[CustomCmd] 命令完成: {Name}, ExitCode: {Code}, 耗时: {Duration}ms",
                command.Name, process.ExitCode, stopwatch.ElapsedMilliseconds);

            return new CustomCommandResult
            {
                Success = process.ExitCode == 0,
                Output = output,
                Error = error,
                ExitCode = process.ExitCode,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[CustomCmd] 执行异常: {Name}", command.Name);
            return new CustomCommandResult
            {
                Success = false,
                Error = $"执行异常: {ex.Message}",
                ExitCode = -1,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc/>
    public bool ValidatePath(string binaryPath)
    {
        // 拒绝包含目录遍历的路径
        if (binaryPath.Contains(".."))
        {
            _logger.LogWarning("[CustomCmd] 路径包含目录遍历: {Path}", binaryPath);
            return false;
        }

        // 拒绝绝对路径
        if (Path.IsPathRooted(binaryPath))
        {
            _logger.LogWarning("[CustomCmd] 路径为绝对路径: {Path}", binaryPath);
            return false;
        }

        // 验证完整路径在基目录内
        var baseFullPath = Path.GetFullPath(_basePath);
        var targetFullPath = Path.GetFullPath(Path.Combine(_basePath, binaryPath));

        if (!targetFullPath.StartsWith(baseFullPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[CustomCmd] 路径逃逸基目录: {Path} -> {Full}", binaryPath, targetFullPath);
            return false;
        }

        return true;
    }
}
