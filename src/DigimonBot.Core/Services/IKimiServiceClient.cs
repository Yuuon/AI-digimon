using DigimonBot.Core.Models.Kimi;

namespace DigimonBot.Core.Services;

/// <summary>
/// Kimi ACP 服务客户端接口 - 通过 JSON-RPC over stdio 与 kimi acp 服务通信
/// </summary>
public interface IKimiServiceClient : IDisposable
{
    /// <summary>
    /// 确保 kimi ACP 服务已连接并初始化
    /// </summary>
    Task EnsureServiceRunningAsync(CancellationToken ct = default);

    /// <summary>
    /// 发送聊天消息并获取回复（通过 ACP 协议）
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
    /// 获取会话详情（通过列出所有会话并匹配ID）
    /// </summary>
    Task<KimiSessionInfo?> GetSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// 列出所有活跃会话
    /// </summary>
    Task<List<KimiSessionInfo>> ListSessionsAsync(CancellationToken ct = default);

    /// <summary>
    /// 删除会话（ACP 协议不支持，始终返回 false）
    /// </summary>
    Task<bool> DeleteSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// 断开 kimi ACP 服务连接
    /// </summary>
    Task StopServiceAsync(CancellationToken ct = default);
}
