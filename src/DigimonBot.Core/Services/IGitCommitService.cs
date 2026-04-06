namespace DigimonBot.Core.Services;

/// <summary>
/// Git 提交服务接口 - 自动提交 Kimi 执行后的文件变更
/// </summary>
public interface IGitCommitService
{
    /// <summary>
    /// 提交工作目录中的所有变更
    /// </summary>
    /// <param name="repoPath">仓库路径</param>
    /// <param name="userId">发起用户ID</param>
    /// <param name="command">执行的命令内容</param>
    /// <param name="durationMs">执行耗时（毫秒）</param>
    /// <returns>提交哈希，如果无变更则返回null</returns>
    Task<string?> CommitChangesAsync(string repoPath, string userId, string command, int durationMs);

    /// <summary>
    /// 生成提交信息
    /// </summary>
    string GenerateCommitMessage(string userId, string command, int durationMs);
}
