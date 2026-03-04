using System.Text.Json.Serialization;

namespace Steam_BackLog;

public class Models
{
    public class Config
    {
        public string SteamApiKey { get; set; } = "";
        public string SteamId { get; set; } = "";
    }

    public class GameData
    {
        public int AppId { get; set; }
        public string? Name { get; set; } = "";
        public int PlaytimeForever { get; set; }
        public int MetacriticScore { get; set; }
        public double TimeToBeatHours { get; set; }

        // Формула ценности: Оценка / Корень из времени прохождения
        public double ValueScore => TimeToBeatHours > 0 ? MetacriticScore / Math.Sqrt(TimeToBeatHours) : 0;
    }

    // Классы для десериализации ответов Steam API
    public class SteamOwnedGamesResponse
    {
        [JsonPropertyName("response")]
        public SteamOwnedGamesData? Response { get; set; }
    }

    public class SteamOwnedGamesData
    {
        [JsonPropertyName("game_count")]
        public int GameCount { get; set; }

        [JsonPropertyName("games")]
        public List<SteamGame>? Games { get; set; }
    }

    public class SteamGame
    {
        [JsonPropertyName("appid")]
        public int AppId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("playtime_forever")]
        public int PlaytimeForever { get; set; }
    }
}