using System.Diagnostics;
using DigimonBot.Core.Models.Kimi;
using DigimonBot.Core.Services;
using DigimonBot.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Data.Services;

/// <summary>
/// Kimi仓库管理器 - 管理仓库生命周期（创建、切换、列出）
/// </summary>
public class KimiRepositoryManager : IKimiRepositoryManager
{
    private readonly IKimiRepositoryRepository _repository;
    private readonly ILogger<KimiRepositoryManager> _logger;
    private readonly string _basePath;
    private readonly string _defaultBranch;
    private readonly int _gitCommandTimeoutMs;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public KimiRepositoryManager(
        IKimiRepositoryRepository repository,
        ILogger<KimiRepositoryManager> logger,
        string basePath = "./kimi-workspace",
        string defaultBranch = "main",
        int gitCommandTimeoutMs = 30000)
    {
        _repository = repository;
        _logger = logger;
        _basePath = basePath;
        _defaultBranch = defaultBranch;
        _gitCommandTimeoutMs = gitCommandTimeoutMs;

        // 确保基础目录存在
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    public async Task<KimiRepository> CreateRepositoryAsync(string? name, string userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            // 自动生成名称
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"kimi-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            }

            // 验证名称格式
            name = SanitizeRepoName(name);

            // 检查是否已存在
            var existing = await _repository.GetByNameAsync(name);
            if (existing != null)
            {
                throw new InvalidOperationException($"仓库 '{name}' 已存在");
            }

            // 创建目录
            var repoPath = Path.Combine(_basePath, name);
            if (Directory.Exists(repoPath))
            {
                throw new InvalidOperationException($"目录 '{repoPath}' 已存在");
            }

            Directory.CreateDirectory(repoPath);

            // 初始化git仓库
            await RunGitCommandAsync(repoPath, "init", "--initial-branch", _defaultBranch);
            await RunGitCommandAsync(repoPath, "config", "user.email", "kimi-bot@digimon.local");
            await RunGitCommandAsync(repoPath, "config", "user.name", "Kimi Bot");

            // 创建README
            var readmePath = Path.Combine(repoPath, "README.md");
            var readmeContent = $"""
                # {name}

                Created by Kimi Bot for user `{userId}`
                Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

                ## About
                This repository was created via the `/kimi` command in DigimonBot.
                """;
            await File.WriteAllTextAsync(readmePath, readmeContent);

            // 提交README
            await RunGitCommandAsync(repoPath, "add", ".");
            await RunGitCommandAsync(repoPath, "commit", "-m", "Initial commit: Create repository");

            // 保存到数据库
            var repo = await _repository.CreateAsync(name, repoPath);

            // 设为活动仓库
            await _repository.SetActiveAsync(name);

            _logger.LogInformation("[KimiRepoManager] 仓库 '{Name}' 已创建: {Path}", name, repoPath);

            return repo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[KimiRepoManager] 创建仓库失败: {Name}", name);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IEnumerable<KimiRepository>> ListRepositoriesAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<bool> SwitchRepositoryAsync(string name)
    {
        var repo = await _repository.GetByNameAsync(name);
        if (repo == null)
        {
            _logger.LogWarning("[KimiRepoManager] 仓库不存在: {Name}", name);
            return false;
        }

        await _repository.SetActiveAsync(name);
        _logger.LogInformation("[KimiRepoManager] 已切换到仓库: {Name}", name);
        return true;
    }

    public async Task<KimiRepository?> GetActiveRepositoryAsync()
    {
        return await _repository.GetActiveAsync();
    }

    public async Task<KimiRepository> EnsureRepositoryExistsAsync(string userId)
    {
        var active = await _repository.GetActiveAsync();
        if (active != null)
        {
            return active;
        }

        // 没有活动仓库，自动创建一个
        _logger.LogInformation("[KimiRepoManager] 无活动仓库，自动创建默认仓库");
        return await CreateRepositoryAsync(null, userId);
    }

    /// <summary>
    /// 在仓库目录中执行git命令（使用安全的参数列表方式）
    /// </summary>
    private async Task<string> RunGitCommandAsync(string workingDirectory, params string[] arguments)
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
            throw new InvalidOperationException($"无法启动git进程: git {string.Join(" ", arguments)}");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var completed = process.WaitForExit(_gitCommandTimeoutMs);
        if (!completed)
        {
            process.Kill();
            throw new TimeoutException($"Git命令超时: git {string.Join(" ", arguments)}");
        }

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("[KimiRepoManager] Git命令失败: git {Args}, ExitCode: {Code}, Error: {Error}",
                string.Join(" ", arguments), process.ExitCode, error);
            throw new InvalidOperationException($"Git命令失败 (exit code {process.ExitCode}): {error}");
        }

        return output;
    }

    /// <summary>
    /// 清理仓库名称（只允许字母数字和连字符）
    /// </summary>
    private static string SanitizeRepoName(string name)
    {
        // 移除不安全字符，只保留字母、数字、连字符和下划线
        var sanitized = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = $"kimi-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        }

        // 限制长度
        if (sanitized.Length > 64)
        {
            sanitized = sanitized[..64];
        }

        return sanitized;
    }
}
