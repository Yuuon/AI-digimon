using System.Text.Json;
using System.Text.Json.Serialization;
using DigimonBot.Core.Models;

namespace DigimonBot.Data.Repositories;

/// <summary>
/// JSON 文件物品定义仓库
/// </summary>
public class JsonItemRepository : IItemRepository
{
    private readonly string _filePath;
    private Dictionary<string, ItemDefinition> _items = new();
    private readonly object _lock = new();

    public JsonItemRepository(string filePath)
    {
        _filePath = filePath;
        LoadData();
    }

    private void LoadData()
    {
        // 如果文件不存在，创建一个空的物品列表
        if (!File.Exists(_filePath))
        {
            CreateEmptyItemsFile();
        }

        var json = File.ReadAllText(_filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var definitions = JsonSerializer.Deserialize<List<ItemDefinition>>(json, options);
        
        lock (_lock)
        {
            _items = definitions?.ToDictionary(d => d.Id, d => d) 
                ?? new Dictionary<string, ItemDefinition>();
        }
    }

    private void CreateEmptyItemsFile()
    {
        // 创建空的物品列表并保存
        var emptyItems = new List<ItemDefinition>();
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        var json = JsonSerializer.Serialize(emptyItems, options);
        
        // 确保目录存在
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(_filePath, json);
    }

    public IReadOnlyDictionary<string, ItemDefinition> GetAll()
    {
        lock (_lock)
        {
            return _items.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }

    public ItemDefinition? GetById(string id)
    {
        lock (_lock)
        {
            return _items.TryGetValue(id, out var def) ? def : null;
        }
    }

    public IEnumerable<ItemDefinition> GetShopItems()
    {
        lock (_lock)
        {
            return _items.Values.Where(i => i.Price > 0).ToList();
        }
    }

    public IEnumerable<ItemDefinition> GetByType(string type)
    {
        lock (_lock)
        {
            return _items.Values.Where(i => i.Type == type).ToList();
        }
    }

    public Task ReloadAsync()
    {
        LoadData();
        return Task.CompletedTask;
    }
}
