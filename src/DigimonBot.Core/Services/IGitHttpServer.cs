namespace DigimonBot.Core.Services;

/// <summary>
/// Git HTTP 服务器接口 - 提供仓库的公开克隆访问
/// </summary>
public interface IGitHttpServer
{
    /// <summary>
    /// 启动 HTTP 服务
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止 HTTP 服务
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取仓库的克隆 URL
    /// </summary>
    /// <param name="repoName">仓库名称</param>
    /// <returns>克隆 URL</returns>
    string GetCloneUrl(string repoName);

    /// <summary>
    /// 服务是否正在运行
    /// </summary>
    bool IsRunning { get; }
}
