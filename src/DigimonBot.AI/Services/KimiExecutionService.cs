using System.Diagnostics;
using DigimonBot.Core.Models.Kimi;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.AI.Services;

/// <summary>
/// Kimi CLI 执行服务 - 执行Kimi命令并捕获输出
/// </summary>
public class KimiExecutionService : IKimiExecutionService
{
    private readonly ILogger<KimiExecutionService> _logger;
    private readonly string _kimiCliPath;

    public KimiExecutionService(
        ILogger<KimiExecutionService> logger,
        string kimiCliPath = "kimi")
    {
        _logger = logger;
        _kimiCliPath = kimiCliPath;
    }

    public async Task<ExecutionResult> ExecuteAsync(string repoPath, string arguments, int timeoutSeconds)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("[KimiExec] 开始执行: {CliPath} {Args} (工作目录: {Dir}, 超时: {Timeout}s)",
            _kimiCliPath, arguments, repoPath, timeoutSeconds);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _kimiCliPath,
                Arguments = arguments,
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                stopwatch.Stop();
                return new ExecutionResult
                {
                    Success = false,
                    Error = "无法启动Kimi CLI进程",
                    ExitCode = -1,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }

            // 异步读取stdout/stderr避免死锁
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var timeoutMs = timeoutSeconds * 1000;
            var completed = process.WaitForExit(timeoutMs);

            if (!completed)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[KimiExec] 终止超时进程时出错");
                }

                stopwatch.Stop();

                _logger.LogWarning("[KimiExec] 执行超时 ({Timeout}s): {Args}", timeoutSeconds, arguments);

                return new ExecutionResult
                {
                    Success = false,
                    Output = await GetPartialOutput(outputTask),
                    Error = $"执行超时（{timeoutSeconds}秒）。进程已被终止。",
                    ExitCode = -1,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }

            var output = await outputTask;
            var error = await errorTask;

            stopwatch.Stop();

            var result = new ExecutionResult
            {
                Success = process.ExitCode == 0,
                Output = output,
                Error = error,
                ExitCode = process.ExitCode,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };

            _logger.LogInformation("[KimiExec] 执行完成 - ExitCode: {Code}, 耗时: {Duration}ms, 输出长度: {Len}",
                result.ExitCode, result.DurationMs, result.Output.Length);

            return result;
        }
        catch (Exception ex) when (ex is not TimeoutException)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "[KimiExec] 执行异常: {Args}", arguments);

            return new ExecutionResult
            {
                Success = false,
                Error = $"执行异常: {ex.Message}",
                ExitCode = -1,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// 安全获取部分输出（超时时可能还没读完）
    /// </summary>
    private static async Task<string> GetPartialOutput(Task<string> outputTask)
    {
        try
        {
            if (outputTask.IsCompleted)
            {
                return await outputTask;
            }

            // 等待最多2秒获取已有输出
            var completed = await Task.WhenAny(outputTask, Task.Delay(2000));
            if (completed == outputTask)
            {
                return await outputTask;
            }

            return "(输出读取超时)";
        }
        catch
        {
            return "(无法读取输出)";
        }
    }
}
