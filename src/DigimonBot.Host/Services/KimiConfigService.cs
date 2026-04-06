using System.Text.Json;
using DigimonBot.Host.Configs;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Host.Services;

/// <summary>
/// Kimi配置服务 - 支持热重载
/// </summary>
public class KimiConfigService : IDisposable
{
    private readonly ILogger<KimiConfigService> _logger;
    private FileSystemWatcher? _watcher;
    private KimiConfig _currentConfig;
    private readonly string _configPath;
    private DateTime _lastReload = DateTime.MinValue;
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// 当前配置（只读访问）
    /// </summary>
    public KimiConfig CurrentConfig => _currentConfig;

    /// <summary>
    /// 构造函数
    /// </summary>
    public KimiConfigService(ILogger<KimiConfigService> logger)
    {
        _logger = logger;
        _configPath = Path.Combine("Data", "kimi_config.json");
        _currentConfig = new KimiConfig();
        
        LoadConfiguration();
        SetupWatcher();
    }

    /// <summary>
    /// 设置文件系统监视器
    /// </summary>
    private void SetupWatcher()
    {
        _watcher = new FileSystemWatcher("Data", "kimi_config.json")
        {
            NotifyFilter = NotifyFilters.LastWrite
        };
        
        _watcher.Changed += OnConfigChanged;
        _watcher.EnableRaisingEvents = true;
        
        _logger.LogInformation("[KimiConfig] 文件监视器已启动，正在监视: {Path}", _configPath);
    }

    /// <summary>
    /// 配置文件变更事件处理
    /// </summary>
    private void OnConfigChanged(object sender, FileSystemEventArgs e)
    {
        // 防抖处理：忽略100ms内的连续事件
        var now = DateTime.Now;
        if (now - _lastReload < _debounceInterval)
            return;
        
        _lastReload = now;
        
        _logger.LogInformation("[KimiConfig] 配置文件已更改，正在重新加载...");
        LoadConfiguration();
    }

    /// <summary>
    /// 加载配置文件
    /// </summary>
    private void LoadConfiguration()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                _logger.LogWarning("[KimiConfig] 配置文件不存在: {Path}，使用默认配置", _configPath);
                _currentConfig = new KimiConfig();
                return;
            }

            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<KimiConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (config != null)
            {
                _currentConfig = config;
                _logger.LogInformation("[KimiConfig] 配置加载成功 - 模式: {Mode}, 白名单: {WhitelistCount}人",
                    _currentConfig.AccessControl.Mode,
                    _currentConfig.AccessControl.Whitelist.Count);
            }
            else
            {
                _logger.LogWarning("[KimiConfig] 配置文件解析为空，使用默认配置");
                _currentConfig = new KimiConfig();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[KimiConfig] 配置文件JSON解析失败，使用默认配置");
            _currentConfig = new KimiConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[KimiConfig] 加载配置文件失败，使用默认配置");
            _currentConfig = new KimiConfig();
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _watcher?.Dispose();
        _logger.LogInformation("[KimiConfig] 文件监视器已停止");
    }
}
