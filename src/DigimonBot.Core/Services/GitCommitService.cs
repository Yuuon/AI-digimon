using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Core.Services;

/// <summary>
/// Git 提交服务 - 自动提交 Kimi 执行后的文件变更
/// 使用 git CLI 通过 Process 执行（安全使用 ArgumentList）
/// </summary>
public class GitCommitService : IGitCommitService
{
    private readonly ILogger<GitCommitService> _logger;

    public GitCommitService(ILogger<GitCommitService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string?> CommitChangesAsync(string repoPath, string userId, string command, int durationMs)
    {
        try
        {
            // 1. Stage all changes
            await RunGitAsync(repoPath, "add", "-A");

            // 2. Check if there are any changes to commit
            var status = await RunGitAsync(repoPath, "status", "--porcelain");
            if (string.IsNullOrWhiteSpace(status))
            {
                _logger.LogInformation("[GitCommit] 无文件变更，跳过提交");
                return null;
            }

            // 3. Commit with descriptive message
            var commitMessage = GenerateCommitMessage(userId, command, durationMs);
            await RunGitAsync(repoPath, "commit", "-m", commitMessage);

            // 4. Get the commit hash
            var commitHash = (await RunGitAsync(repoPath, "rev-parse", "HEAD")).Trim();

            _logger.LogInformation("[GitCommit] 已自动提交: {Hash} (用户: {User})", 
                commitHash[..Math.Min(8, commitHash.Length)], userId);

            return commitHash;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GitCommit] 自动提交失败，但不影响执行结果");
            return null;
        }
    }

    /// <inheritdoc/>
    public string GenerateCommitMessage(string userId, string command, int durationMs)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var truncatedCommand = command.Length > 200 ? command[..200] + "..." : command;

        return $"""
            kimi execution at {timestamp} by {userId}

            Command: {truncatedCommand}
            Duration: {durationMs}ms
            """;
    }

    /// <summary>
    /// 在仓库目录中执行 git 命令（使用安全的参数列表方式）
    /// </summary>
    private async Task<string> RunGitAsync(string workingDirectory, params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException($"无法启动 git 进程: git {string.Join(" ", arguments)}");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var completed = process.WaitForExit(30000); // 30秒超时
        if (!completed)
        {
            process.Kill();
            throw new TimeoutException($"Git 命令超时: git {string.Join(" ", arguments)}");
        }

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("[GitCommit] Git 命令失败: git {Args}, ExitCode: {Code}, Error: {Error}",
                string.Join(" ", arguments), process.ExitCode, error);
            throw new InvalidOperationException($"Git 命令失败 (exit code {process.ExitCode}): {error}");
        }

        return output;
    }
}
