using System.Text.Json;
using System.Text.Json.Serialization;
using DigimonBot.Core.Models.Tavern;
using DigimonBot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DigimonBot.Data.Services;

/// <summary>
/// 酒馆配置服务实现
/// </summary>
public class TavernConfigService : ITavernConfigService
{
    private TavernConfig _config = new();
    private readonly string _configFilePath;
    private readonly ILogger<TavernConfigService> _logger;
    private readonly object _lock = new();

    public TavernConfig Config
    {
        get
        {
            lock (_lock)
            {
                return _config;
            }
        }
    }

    public event EventHandler? OnConfigChanged;

    public TavernConfigService(ILogger<TavernConfigService> logger, string? configFilePath = null)
    {
        _logger = logger;
        _configFilePath = configFilePath ?? TavernConfig.DefaultConfigPath;

        // 尝试加载配置，如果不存在则创建默认配置
        if (!File.Exists(_configFilePath))
        {
            _logger.LogInformation("酒馆配置文件不存在，创建默认配置: {Path}", _configFilePath);
            CreateDefaultConfig();
        }
        else
        {
            LoadConfig(_configFilePath);
        }
    }

    public bool LoadConfig(string? filePath = null)
    {
        var path = filePath ?? _configFilePath;

        try
        {
            if (!File.Exists(path))
            {
                _logger.LogWarning("配置文件不存在: {Path}", path);
                return false;
            }

            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var config = JsonSerializer.Deserialize<TavernConfig>(json, options);
            if (config == null)
            {
                _logger.LogError("反序列化配置文件失败: {Path}", path);
                return false;
            }

            lock (_lock)
            {
                _config = config;
            }

            _logger.LogInformation("酒馆配置已加载: {Path}", path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载配置文件失败: {Path}", path);
            return false;
        }
    }

    public bool SaveConfig(string? filePath = null)
    {
        var path = filePath ?? _configFilePath;

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            lock (_lock)
            {
                var json = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(path, json);
            }

            _logger.LogInformation("酒馆配置已保存: {Path}", path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存配置文件失败: {Path}", path);
            return false;
        }
    }

    public bool ReloadConfig()
    {
        _logger.LogInformation("重新加载酒馆配置...");

        if (LoadConfig(_configFilePath))
        {
            OnConfigChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }

    public string GetConfigFilePath()
    {
        return Path.GetFullPath(_configFilePath);
    }

    /// <summary>
    /// 创建默认配置文件
    /// </summary>
    private void CreateDefaultConfig()
    {
        _config = new TavernConfig();
        SaveConfig(_configFilePath);
    }

    /// <summary>
    /// 更新配置（热更新）
    /// </summary>
    public bool UpdateConfig(Action<TavernConfig> updateAction)
    {
        try
        {
            lock (_lock)
            {
                updateAction(_config);
            }

            SaveConfig();
            OnConfigChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新配置失败");
            return false;
        }
    }
}
