using System.Diagnostics;
using DigimonBot.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Host.Services;

/// <summary>
/// Git HTTP 服务器 - 使用 Kestrel 提供仓库的只读 HTTP 访问
/// 支持 Git "dumb" HTTP 协议，用户可通过 git clone 下载仓库
/// </summary>
public class GitHttpServer : IGitHttpServer, IHostedService
{
    private readonly ILogger<GitHttpServer> _logger;
    private readonly string _basePath;
    private readonly int _port;
    private readonly string _publicUrl;
    private IWebHost? _webHost;

    public bool IsRunning => _webHost != null;

    public GitHttpServer(
        ILogger<GitHttpServer> logger,
        string basePath,
        int port,
        string publicUrl)
    {
        _logger = logger;
        _basePath = Path.GetFullPath(basePath);
        _port = port;
        _publicUrl = publicUrl.TrimEnd('/');
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 确保基础目录存在
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }

            // 在启动前，更新所有仓库的 git server info
            await UpdateAllServerInfoAsync();

            var provider = new FileExtensionContentTypeProvider();
            // Git 专用 MIME 类型
            provider.Mappings[".pack"] = "application/x-git-packed-objects";
            provider.Mappings[".idx"] = "application/x-git-packed-objects-toc";

            _webHost = new WebHostBuilder()
                .UseKestrel(options => options.ListenAnyIP(_port))
                .Configure(app =>
                {
                    // 提供 .git 目录内容作为静态文件
                    app.UseStaticFiles(new StaticFileOptions
                    {
                        FileProvider = new PhysicalFileProvider(_basePath),
                        RequestPath = "/git",
                        ServeUnknownFileTypes = true,
                        DefaultContentType = "application/octet-stream",
                        ContentTypeProvider = provider
                    });

                    // 目录浏览（可选，用于查看仓库列表）
                    app.UseDirectoryBrowser(new DirectoryBrowserOptions
                    {
                        FileProvider = new PhysicalFileProvider(_basePath),
                        RequestPath = "/git"
                    });
                })
                .Build();

            await _webHost.StartAsync(cancellationToken);
            _logger.LogInformation("[GitHttpServer] Git HTTP 服务器已启动 - 端口: {Port}, 基础路径: {Path}", _port, _basePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GitHttpServer] Git HTTP 服务器启动失败");
            _webHost = null;
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_webHost != null)
        {
            await _webHost.StopAsync(cancellationToken);
            _webHost.Dispose();
            _webHost = null;
            _logger.LogInformation("[GitHttpServer] Git HTTP 服务器已停止");
        }
    }

    /// <inheritdoc/>
    public string GetCloneUrl(string repoName)
    {
        return $"{_publicUrl}/git/{repoName}/.git";
    }

    /// <summary>
    /// 更新所有仓库的 git update-server-info（dumb HTTP 协议需要）
    /// </summary>
    private async Task UpdateAllServerInfoAsync()
    {
        if (!Directory.Exists(_basePath)) return;

        foreach (var repoDir in Directory.GetDirectories(_basePath))
        {
            var gitDir = Path.Combine(repoDir, ".git");
            if (Directory.Exists(gitDir))
            {
                try
                {
                    await RunGitAsync(repoDir, "update-server-info");
                    _logger.LogDebug("[GitHttpServer] 已更新 server-info: {Repo}", Path.GetFileName(repoDir));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[GitHttpServer] 更新 server-info 失败: {Repo}", Path.GetFileName(repoDir));
                }
            }
        }
    }

    private static async Task RunGitAsync(string workingDirectory, params string[] arguments)
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
        if (process == null) return;

        await process.WaitForExitAsync();
    }
}
