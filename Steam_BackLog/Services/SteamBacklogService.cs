using System.Text.Json;
using System.Text.RegularExpressions;
using HowLongToBeatScraper;

namespace Steam_BackLog.Services;
public class SteamBacklogService
{
    private readonly Models.Config _config;
    private readonly CacheService _cacheService; // Предполагаем, что кэш тоже можно вынести в отдельный класс
    private static readonly HttpClient httpClient = new HttpClient();

    public SteamBacklogService(Models.Config config)
    {
        _config = config;
        _cacheService = new CacheService();
    }

    // Action-делегат позволяет сервису отправлять обновления прогресса "наверх" в Program.cs
    public async Task ProcessAccountAsync(string steamId, int? limit, Action<string, int, int> onProgressUpdated)
    {
        // 1. Получаем игры из Steam
        var games = await GetOwnedGamesAsync(_config.SteamApiKey, steamId);
        if (games == null || !games.Any())
        {
            throw new Exception("Игры не найдены или профиль скрыт.");
        }

        var fullBacklog = games.ToList();
        var backlogToProcess = limit.HasValue && limit > 0
            ? fullBacklog.Take(limit.Value).ToList()
            : fullBacklog;

        // 2. Инициализируем компоненты
        var cachedData = _cacheService.LoadCache();
        using var hltbScraper = new HltbScraper();
        var semaphore = new SemaphoreSlim(4);
        var tasks = new List<Task>();

        var processedGames = new List<Models.GameData>(cachedData);
        var lockObj = new object();
        int processedCount = 0;
        int totalCount = backlogToProcess.Count;

        onProgressUpdated?.Invoke("Начинаем сбор данных...", processedCount, totalCount);

        // 3. Многопоточный (лимитированный) сбор данных
        foreach (var game in backlogToProcess)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var gameData = new Models.GameData
                    {
                        AppId = game.AppId,
                        Name = game.Name,
                        PlaytimeForever = game.PlaytimeForever
                    };

                    var cachedGame = cachedData.FirstOrDefault(c => c.AppId == game.AppId);

                    if (cachedGame != null && cachedGame.TimeToBeatHours > 0)
                    {
                        gameData.MetacriticScore = cachedGame.MetacriticScore;
                        gameData.TimeToBeatHours = cachedGame.TimeToBeatHours;
                    }
                    else
                    {
                        await Task.Delay(800); // Ограничение от Cloudflare/Steam

                        gameData.MetacriticScore = await GetMetacriticScoreAsync(game.AppId);
                        gameData.TimeToBeatHours = await FetchHltbTimeAsync(hltbScraper, game.Name,
                            onProgressUpdated, processedCount, totalCount);

                        lock (lockObj)
                        {
                            processedGames.RemoveAll(g => g.AppId == gameData.AppId);
                            processedGames.Add(gameData);
                        }
                    }

                    // Обновляем счетчик и отправляем событие в UI
                    lock (lockObj)
                    {
                        processedCount++;
                        onProgressUpdated?.Invoke(string.Empty, processedCount, totalCount);

                        if (processedCount % 10 == 0)
                            _cacheService.SaveCache(processedGames);
                    }
                }
                catch (Exception ex)
                {
                    onProgressUpdated?.Invoke($"Ошибка {game.Name}: {ex.Message}", processedCount, totalCount);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
        _cacheService.SaveCache(processedGames); // Финальное сохранение
    }

    private async Task<double> FetchHltbTimeAsync(HltbScraper scraper, string? gameName,
        Action<string, int, int> onProgressUpdated, int currentCount, int totalCount)
    {
        var cleanName = CleanGameName(gameName);
        int maxRetries = 3;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var results = await scraper.Search(searchTerm: cleanName, resolveStoreIds: false);
                if (results != null && results.Any())
                {
                    var firstMatch = results.First();
                    float hltbHours = firstMatch.MainStory.HasValue && firstMatch.MainStory > 0
                        ? firstMatch.MainStory.Value
                        : (firstMatch.MainStoryWithExtras ?? 0f);

                    if (hltbHours > 0) return Math.Round((double)hltbHours, 1);

                    onProgressUpdated?.Invoke(
                        $"[HLTB] Для '{firstMatch.Title}' нет сюжетного времени (мультиплеер).", currentCount,
                        totalCount);
                    return 0;
                }

                break;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("502"))
            {
                if (i == maxRetries - 1)
                    onProgressUpdated?.Invoke($"[HLTB] Пропуск {gameName} (502 Gateway).", currentCount,
                        totalCount);
                else
                {
                    onProgressUpdated?.Invoke($"[HLTB] 502 для {gameName}. Ждем 2 сек...", currentCount,
                        totalCount);
                    await Task.Delay(2000);
                }
            }
            catch (Exception)
            {
                break;
            }
        }

        return 0;
    }

    private async Task<List<Models.SteamGame>?> GetOwnedGamesAsync(string apiKey, string steamId)
    {
        var url =
            $"http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={apiKey}&steamid={steamId}&format=json&include_appinfo=1";
        try
        {
            var response = await httpClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<Models.SteamOwnedGamesResponse>(response);
            return data?.Response?.Games;
        }
        catch
        {
            return null;
        }
    }

    private async Task<int> GetMetacriticScoreAsync(int appId)
    {
        var url = $"https://store.steampowered.com/api/appdetails?appids={appId}";
        try
        {
            var response = await httpClient.GetStringAsync(url);
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            if (root.TryGetProperty(appId.ToString(), out var appElement) &&
                appElement.TryGetProperty("success", out var success) && success.GetBoolean() &&
                appElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("metacritic", out var meta))
            {
                return meta.GetProperty("score").GetInt32();
            }
        }
        catch
        {
        }

        return 0;
    }

    private string CleanGameName(string? name)
    {
        var cleanName = Regex.Replace(name, @"(™|®|©)", "");
        cleanName = Regex.Replace(cleanName, @"(?i)(Edition|Director's Cut|Game of the Year|GOTY)", "").Trim();
        return cleanName.Split('-')[0].Trim();
    }
}
