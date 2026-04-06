using System.Diagnostics;
using System.Net.Http.Json;
using DigimonBot.Core.Models.Kimi;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.AI.Services;

/// <summary>
/// Kimi Web 服务客户端 - 通过 HTTP API 与 kimi web 服务通信
/// 遵循 KimiServiceClient.md 官方指南实现
/// </summary>
public class KimiServiceClient : IKimiServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly KimiServiceOptions _options;
    private readonly ILogger<KimiServiceClient> _logger;
    private Process? _kimiProcess;
    private bool _isDisposed;
    private readonly SemaphoreSlim _processLock = new(1, 1);

    public KimiServiceClient(
        KimiServiceOptions options,
        ILogger<KimiServiceClient> logger)
    {
        _options = options;
        _logger = logger;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_options.BaseUrl),
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };
    }

    #region 服务生命周期管理

    /// <inheritdoc/>
    public async Task EnsureServiceRunningAsync(CancellationToken ct = default)
    {
        if (!_options.AutoManageProcess) return;

        await _processLock.WaitAsync(ct);
        try
        {
            if (_kimiProcess?.HasExited == false) return;

            _logger.LogInformation("[KimiService] 正在启动 Kimi Web 服务...");

            var executable = FindKimiExecutable();
            var arguments = $"web --port {_options.Port} --no-open";

            if (!string.IsNullOrEmpty(_options.DefaultWorkDir))
            {
                arguments += $" --work-dir \"{_options.DefaultWorkDir}\"";
            }

            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _kimiProcess = new Process { StartInfo = psi };
            _kimiProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogDebug("[KimiWeb] {Output}", e.Data);
            };
            _kimiProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogWarning("[KimiWeb] {Error}", e.Data);
            };

            _kimiProcess.Start();
            _kimiProcess.BeginOutputReadLine();
            _kimiProcess.BeginErrorReadLine();

            // 等待服务就绪
            await WaitForServiceReadyAsync(ct);
            _logger.LogInformation("[KimiService] Kimi Web 服务已启动，端口: {Port}", _options.Port);
        }
        finally
        {
            _processLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StopServiceAsync(CancellationToken ct = default)
    {
        if (!_options.AutoManageProcess || _kimiProcess == null) return;

        await _processLock.WaitAsync(ct);
        try
        {
            if (_kimiProcess?.HasExited == false)
            {
                _logger.LogInformation("[KimiService] 正在停止 Kimi Web 服务...");
                _kimiProcess.Kill(entireProcessTree: true);
                await _kimiProcess.WaitForExitAsync(ct);
                _logger.LogInformation("[KimiService] Kimi Web 服务已停止");
            }
        }
        finally
        {
            _processLock.Release();
        }
    }

    private async Task WaitForServiceReadyAsync(CancellationToken ct)
    {
        var maxRetries = 30;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var response = await _httpClient.GetAsync("/health", ct);
                if (response.IsSuccessStatusCode) return;
            }
            catch
            {
                // 服务尚未就绪，继续等待
            }

            await Task.Delay(500, ct);
        }
        throw new TimeoutException("Kimi Web 服务在15秒内未能启动就绪");
    }

    private string FindKimiExecutable()
    {
        if (!string.IsNullOrEmpty(_options.KimiExecutablePath) && _options.KimiExecutablePath != "kimi")
        {
            if (File.Exists(_options.KimiExecutablePath))
                return _options.KimiExecutablePath;
        }

        // 尝试常见路径
        var candidates = new[]
        {
            "kimi",  // PATH 中
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "kimi"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Python", "Scripts", "kimi.exe"),
            "/usr/local/bin/kimi",
            "/usr/bin/kimi"
        };

        foreach (var candidate in candidates)
        {
            if (candidate == "kimi" || File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("无法找到 kimi 可执行文件。请安装 kimi-cli 或在配置中指定路径。");
    }

    #endregion

    #region 聊天 API

    /// <inheritdoc/>
    public async Task<KimiChatResponse> ChatAsync(
        string message,
        string? sessionId = null,
        string? workDir = null,
        bool yolo = true,
        CancellationToken ct = default)
    {
        await EnsureServiceRunningAsync(ct);

        var request = new KimiChatRequest
        {
            Message = message,
            SessionId = sessionId,
            WorkDir = workDir ?? _options.DefaultWorkDir,
            Yolo = yolo
        };

        _logger.LogInformation("[KimiService] 发送聊天请求: {Message}", 
            message.Length > 100 ? message[..100] + "..." : message);

        var response = await _httpClient.PostAsJsonAsync("/api/chat", request, ct);
        await EnsureSuccessAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<KimiChatResponse>(cancellationToken: ct);
        return result ?? throw new InvalidOperationException("Kimi 服务返回空响应");
    }

    /// <inheritdoc/>
    public async Task<string> ChatSimpleAsync(
        string message,
        string? sessionId = null,
        string? workDir = null,
        bool yolo = true,
        CancellationToken ct = default)
    {
        var response = await ChatAsync(message, sessionId, workDir, yolo, ct);
        return response.Response;
    }

    #endregion

    #region 会话管理

    /// <inheritdoc/>
    public async Task<KimiSessionInfo> CreateSessionAsync(string? workDir = null, CancellationToken ct = default)
    {
        await EnsureServiceRunningAsync(ct);

        var request = new { work_dir = workDir ?? _options.DefaultWorkDir };
        var response = await _httpClient.PostAsJsonAsync("/api/sessions", request, ct);
        await EnsureSuccessAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<KimiSessionInfo>(cancellationToken: ct);
        return result ?? throw new InvalidOperationException("创建会话返回空响应");
    }

    /// <inheritdoc/>
    public async Task<KimiSessionInfo?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await EnsureServiceRunningAsync(ct);

        var response = await _httpClient.GetAsync($"/api/sessions/{sessionId}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<KimiSessionInfo>(cancellationToken: ct);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await EnsureServiceRunningAsync(ct);

        var response = await _httpClient.DeleteAsync($"/api/sessions/{sessionId}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return false;

        await EnsureSuccessAsync(response, ct);
        return true;
    }

    #endregion

    #region 工具方法

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var content = await response.Content.ReadAsStringAsync(ct);
        throw new KimiServiceException(
            $"Kimi 服务错误: {(int)response.StatusCode} {response.ReasonPhrase}",
            (int)response.StatusCode,
            content);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _httpClient.Dispose();
        _processLock.Dispose();

        if (_kimiProcess?.HasExited == false)
        {
            try
            {
                _kimiProcess.Kill(entireProcessTree: true);
                _kimiProcess.WaitForExit(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // 忽略清理异常
            }
        }

        _kimiProcess?.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}
