using System.Text.Json;
using System.Text.RegularExpressions;
using Steam_BackLog.Filters;
using Steam_BackLog.Reports;
using Steam_BackLog.Services;

namespace Steam_BackLog;

class Program
{
    private const string ConfigFileName = "config.json";
    private const string CacheFileName = "cache.json";
    private const string HtmlFileName = "SteamReport.html";

    static async Task Main(string[] args)
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== Steam Backlog Triage ===");
            Console.WriteLine("1. Начать работу (Сбор данных из Steam и HLTB)");
            Console.WriteLine("2. Сформировать HTML страницу (из кэша)");
            Console.WriteLine("3. Выйти");
            Console.Write("\nВыберите пункт меню: ");

            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    await ExecuteDataGatheringProcessAsync();
                    Console.WriteLine("\n\nНажмите любую клавишу для возврата в меню...");
                    Console.ReadKey();
                    break;
                case "2":
                    GenerateHtmlReport();
                    Console.WriteLine("\nНажмите любую клавишу для возврата в меню...");
                    Console.ReadKey();
                    break;
                case "3":
                    return;
                default:
                    Console.WriteLine("Неверный ввод. Нажмите любую клавишу...");
                    Console.ReadKey();
                    break;
            }
        }
    }

    static async Task ExecuteDataGatheringProcessAsync()
    {
        Console.Clear();
        Console.WriteLine("=== СБОР ДАННЫХ ===\n");

        if (!File.Exists(ConfigFileName))
        {
            // Создаем пустой шаблон, если файла нет
            File.WriteAllText(ConfigFileName, JsonSerializer.Serialize(new Models.Config()));
        }

        var configJson = File.ReadAllText(ConfigFileName);
        var config = JsonSerializer.Deserialize<Models.Config>(configJson) ?? new Models.Config();

        // Интерактивный запрос ключа
        if (string.IsNullOrEmpty(config.SteamApiKey) || config.SteamApiKey == "YOUR_STEAM_API_KEY")
        {
            Console.WriteLine("Для работы программы нужен бесплатный Steam API Key.\n");
            Console.WriteLine("1. Перейдите по ссылке (можно вписать любой домен, например 'localhost'):");
            Console.WriteLine("   https://steamcommunity.com/dev/apikey");
            Console.WriteLine("2. Скопируйте полученный 32-значный ключ.");
            Console.Write("\nВставьте полученный Steam API Key: ");

            var inputKey = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(inputKey) || inputKey.Length != 32)
            {
                Console.WriteLine("Некорректный ключ. Программа завершена.");
                return;
            }

            // Сохраняем введенный ключ в файл, чтобы не спрашивать в следующий раз
            config.SteamApiKey = inputKey;
            File.WriteAllText(ConfigFileName,
                JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Ключ успешно сохранен в config.json!\n");
        }

        Console.Write("Введите ваш SteamID64 или SteamID (STEAM_X:Y:Z): ");
        var rawInput = Console.ReadLine()?.Trim();
            
        // Пропускаем через наш конвертер
        var steamId = ParseSteamId(rawInput);

        if (string.IsNullOrEmpty(steamId))
        {
            Console.WriteLine("Некорректный формат SteamID.");
            return;
        }

        if (steamId != rawInput)
        {
            Console.WriteLine($"Распознан старый формат. Конвертировано в SteamID64: {steamId}");
        }

        Console.Write("Сколько игр обработать? (Введите число или нажмите Enter для всех): ");
        var limitInput = Console.ReadLine()?.Trim();
        int? limit = int.TryParse(limitInput, out int l) && l > 0 ? l : (int?)null;

        Console.CursorVisible = false;

        // Инициализация слоя бизнес-логики
        var backlogService = new SteamBacklogService(config);

        try
        {
            // Запускаем сбор и передаем лямбду для обработки событий прогресса
            await backlogService.ProcessAccountAsync(steamId, limit, (logMessage, currentProgress, totalItems) =>
            {
                if (!string.IsNullOrEmpty(logMessage))
                {
                    LogWithProgressBar(logMessage, currentProgress, totalItems);
                }
                else
                {
                    DrawProgressBar(currentProgress, totalItems);
                }
            });

            Console.WriteLine("\n\nСбор завершен! Данные сохранены.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nКритическая ошибка: {ex.Message}");
        }
        finally
        {
            Console.CursorVisible = true;
        }
    }

    static void GenerateHtmlReport()
    {
        Console.Clear();
        Console.WriteLine("=== ГЕНЕРАЦИЯ HTML ОТЧЕТА ===\n");

        // Инициализация сервиса кэша (инфраструктура)
        var cacheService = new CacheService(CacheFileName);
        var processedGames = cacheService.LoadCache();

        if (!processedGames.Any())
        {
            Console.WriteLine("Кэш пуст или не найден. Сначала выполните Пункт 1.");
            return;
        }

        // Инициализация слоя фильтрации (бизнес-правила)
        var filter = new GameFilter();
        var topGames = filter.GetTopBacklogGames(processedGames);

        if (!topGames.Any())
        {
            Console.WriteLine("Подходящих под критерии игр не найдено.");
            return;
        }

        // Инициализация слоя представления (генерация UI)
        var reportGenerator = new HtmlReportGenerator(HtmlFileName);
        reportGenerator.GenerateAndOpen(topGames);

        Console.WriteLine("Отчет успешно сгенерирован и открыт в браузере!");
    }

    // --- МЕТОДЫ ОТРИСОВКИ КОНСОЛЬНОГО UI ---

    static void DrawProgressBar(int progress, int total)
    {
        if (total == 0) return;

        double percent = (double)progress / total;
        int width = 50;
        int filledWidth = (int)(width * percent);

        // Очистка текущей строки
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.SetCursorPosition(0, Console.CursorTop);

        string progressBar = $"[{new string('█', filledWidth)}{new string('-', width - filledWidth)}]";

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(progressBar);
        Console.ResetColor();

        Console.Write($" {Math.Round(percent * 100)}% ({progress}/{total})");
    }
    
    static string ParseSteamId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        // Если это уже число длиной 17 символов (SteamID64)
        if (input.Length >= 15 && long.TryParse(input, out _))
        {
            return input;
        }

        // Проверяем, является ли это форматом STEAM_X:Y:Z
        var regex = new Regex(@"^STEAM_[0-5]:([01]):(\d+)$", RegexOptions.IgnoreCase);
        var match = regex.Match(input);

        if (match.Success)
        {
            // Формула: (Z * 2) + V + Y
            // Где V = 76561197960265728
            long v = 76561197960265728;
            long y = long.Parse(match.Groups[1].Value); // Второе число (0 или 1)
            long z = long.Parse(match.Groups[2].Value); // Третье число

            long steamId64 = (z * 2) + v + y;
            return steamId64.ToString();
        }

        return null; // Формат не распознан
    }

    static void LogWithProgressBar(string message, int currentProgress, int total)
    {
        // Очистка строки
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.SetCursorPosition(0, Console.CursorTop);

        // Вывод лога
        Console.WriteLine(message);

        // Перерисовка бара на новой строке
        DrawProgressBar(currentProgress, total);
    }
}