using System.Diagnostics;
using DigimonBot.Core.Models.Kimi;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.AI.Services;

/// <summary>
/// Kimi 执行服务 - 通过 HTTP API 与 Kimi Web 服务交互
/// 替代原有的 CLI Process 调用方式，遵循 KimiServiceClient.md 官方指南
/// </summary>
public class KimiExecutionService : IKimiExecutionService
{
    private readonly IKimiServiceClient _serviceClient;
    private readonly ILogger<KimiExecutionService> _logger;

    public KimiExecutionService(
        IKimiServiceClient serviceClient,
        ILogger<KimiExecutionService> logger)
    {
        _serviceClient = serviceClient;
        _logger = logger;
    }

    public async Task<ExecutionResult> ExecuteAsync(string workDir, string message, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("[KimiExec] 开始执行: 消息={Message}, 工作目录={Dir}, 超时={Timeout}s",
            message.Length > 100 ? message[..100] + "..." : message, workDir, timeoutSeconds);

        try
        {
            // 使用超时和外部取消的联合令牌
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // 通过 HTTP API 发送聊天请求
            var chatResponse = await _serviceClient.ChatAsync(
                message: message,
                workDir: workDir,
                yolo: true,
                ct: linkedCts.Token);

            stopwatch.Stop();

            var result = new ExecutionResult
            {
                Success = true,
                Output = chatResponse.Response,
                SessionId = chatResponse.SessionId,
                ExitCode = 0,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };

            _logger.LogInformation("[KimiExec] 执行完成 - 会话: {Session}, 耗时: {Duration}ms, 输出长度: {Len}",
                result.SessionId, result.DurationMs, result.Output.Length);

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();

            bool isExternalCancel = cancellationToken.IsCancellationRequested;
            if (isExternalCancel)
            {
                _logger.LogInformation("[KimiExec] 任务已被用户取消");
                return new ExecutionResult
                {
                    Success = false,
                    Output = "",
                    Error = "任务已被用户中断。",
                    ExitCode = -1,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
            else
            {
                _logger.LogWarning("[KimiExec] 执行超时 ({Timeout}s)", timeoutSeconds);
                return new ExecutionResult
                {
                    Success = false,
                    Output = "",
                    Error = $"执行超时（{timeoutSeconds}秒）。请求已被取消。",
                    ExitCode = -1,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
        }
        catch (KimiServiceException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[KimiExec] Kimi 服务错误: StatusCode={StatusCode}", ex.StatusCode);

            return new ExecutionResult
            {
                Success = false,
                Error = $"Kimi 服务错误 ({ex.StatusCode}): {ex.Message}",
                ExitCode = ex.StatusCode,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[KimiExec] HTTP 请求失败 - Kimi Web 服务可能未运行");

            return new ExecutionResult
            {
                Success = false,
                Error = $"无法连接到 Kimi Web 服务: {ex.Message}\n请确认 kimi web 服务已启动。",
                ExitCode = -1,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[KimiExec] 执行异常");

            return new ExecutionResult
            {
                Success = false,
                Error = $"执行异常: {ex.Message}",
                ExitCode = -1,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }
}
