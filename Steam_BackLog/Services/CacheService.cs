using System.Text.Json;

namespace Steam_BackLog.Services;

public class CacheService
{
    // Имя файла вынесено как поле класса, можно передавать его через конструктор, 
    // если в будущем захотите иметь несколько кэшей (например, для разных пользователей)
    private readonly string _cacheFileName;

    public CacheService(string cacheFileName = "cache.json")
    {
        _cacheFileName = cacheFileName;
    }

    /// <summary>
    /// Читает данные из локального JSON файла. 
    /// Если файл не существует, возвращает пустой список.
    /// </summary>
    public List<Models.GameData> LoadCache()
    {
        if (File.Exists(_cacheFileName))
        {
            var json = File.ReadAllText(_cacheFileName);
            return JsonSerializer.Deserialize<List<Models.GameData>>(json) ?? new List<Models.GameData>();
        }

        return new List<Models.GameData>();
    }

    /// <summary>
    /// Сериализует список игр и сохраняет его в JSON файл.
    /// Использует WriteIndented = true для красивого форматирования (человекочитаемого).
    /// </summary>
    public void SaveCache(List<Models.GameData> data)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(_cacheFileName, json);
    }
}