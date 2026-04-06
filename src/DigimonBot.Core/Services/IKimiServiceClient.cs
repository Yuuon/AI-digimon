using DigimonBot.Core.Models.Kimi;

namespace DigimonBot.Core.Services;

/// <summary>
/// Kimi Web 服务客户端接口 - 通过 HTTP API 与 kimi web 服务通信
/// </summary>
public interface IKimiServiceClient : IDisposable
{
    /// <summary>
    /// 确保 kimi web 服务正在运行
    /// </summary>
    Task EnsureServiceRunningAsync(CancellationToken ct = default);

    /// <summary>
    /// 发送聊天消息并获取回复
    /// </summary>
    Task<KimiChatResponse> ChatAsync(
        string message,
        string? sessionId = null,
        string? workDir = null,
        bool yolo = true,
        CancellationToken ct = default);

    /// <summary>
    /// 简单聊天，直接返回响应文本
    /// </summary>
    Task<string> ChatSimpleAsync(
        string message,
        string? sessionId = null,
        string? workDir = null,
        bool yolo = true,
        CancellationToken ct = default);

    /// <summary>
    /// 创建新会话
    /// </summary>
    Task<KimiSessionInfo> CreateSessionAsync(string? workDir = null, CancellationToken ct = default);

    /// <summary>
    /// 获取会话详情
    /// </summary>
    Task<KimiSessionInfo?> GetSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// 列出所有活跃会话
    /// </summary>
    Task<List<KimiSessionInfo>> ListSessionsAsync(CancellationToken ct = default);

    /// <summary>
    /// 删除会话
    /// </summary>
    Task<bool> DeleteSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// 停止托管的 kimi web 服务
    /// </summary>
    Task StopServiceAsync(CancellationToken ct = default);
}
