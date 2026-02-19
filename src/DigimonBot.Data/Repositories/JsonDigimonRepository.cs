using System.Text.Json;
using System.Text.Json.Serialization;
using DigimonBot.Core.Models;

namespace DigimonBot.Data.Repositories;

/// <summary>
/// JSON文件数码宝贝数据仓库
/// </summary>
public class JsonDigimonRepository : IDigimonRepository
{
    private readonly string _filePath;
    private Dictionary<string, DigimonDefinition> _digimons = new();
    private readonly object _lock = new();

    public JsonDigimonRepository(string filePath)
    {
        _filePath = filePath;
        LoadData();
    }

    private void LoadData()
    {
        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException($"Digimon database file not found: {_filePath}");
        }

        var json = File.ReadAllText(_filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var definitions = JsonSerializer.Deserialize<List<DigimonDefinition>>(json, options);
        
        lock (_lock)
        {
            _digimons = definitions?.ToDictionary(d => d.Id, d => d) 
                ?? new Dictionary<string, DigimonDefinition>();
        }
    }

    public IReadOnlyDictionary<string, DigimonDefinition> GetAll()
    {
        lock (_lock)
        {
            return _digimons.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }

    public DigimonDefinition? GetById(string id)
    {
        lock (_lock)
        {
            return _digimons.TryGetValue(id, out var def) ? def : null;
        }
    }

    public List<DigimonDefinition> GetByStage(DigimonStage stage)
    {
        lock (_lock)
        {
            return _digimons.Values.Where(d => d.Stage == stage).ToList();
        }
    }

    public DigimonDefinition GetDefaultEgg()
    {
        lock (_lock)
        {
            // 优先返回幼年期I
            var baby1 = _digimons.Values.FirstOrDefault(d => d.Stage == DigimonStage.Baby1);
            if (baby1 != null) return baby1;
            
            // 否则返回第一个
            return _digimons.Values.First();
        }
    }

    public Task ReloadAsync()
    {
        LoadData();
        return Task.CompletedTask;
    }
}
