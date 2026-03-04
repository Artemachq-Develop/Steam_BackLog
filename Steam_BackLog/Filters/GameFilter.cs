namespace Steam_BackLog.Filters
{
    public class GameFilter
    {
        private const double MaxHoursLimit = 20.0;
        private const double PlayedPercentageThreshold = 0.8;

        /// <summary>
        /// Применяет все правила фильтрации бэклога и сортирует результат.
        /// </summary>
        public List<Models.GameData> GetTopBacklogGames(IEnumerable<Models.GameData> games)
        {
            return games
                .Where(IsSinglePlayerGame)
                .Where(IsShortEnoughToPlay)
                .Where(HasGoodMetacriticScore)
                .Where(IsNotYetCompleted)
                .OrderByDescending(g => g.MetacriticScore)
                .ThenBy(g => g.TimeToBeatHours)
                .ToList();
        }

        // --- Specifications ---

        // Мультиплеерные игры имеют TimeToBeatHours = 0
        private bool IsSinglePlayerGame(Models.GameData game) => 
            game.TimeToBeatHours > 0;

        // Игры, которые не требуют сотен часов на прохождение
        private bool IsShortEnoughToPlay(Models.GameData game) => 
            game.TimeToBeatHours <= MaxHoursLimit;

        // Отсеиваем игры без оценки
        private bool HasGoodMetacriticScore(Models.GameData game) => 
            game.MetacriticScore > 0;

        // Если вы наиграли больше 80% от HLTB времени, игра считается пройденной/брошенной
        private bool IsNotYetCompleted(Models.GameData game)
        {
            double playedHours = game.PlaytimeForever / 60.0;
            return playedHours < (game.TimeToBeatHours * PlayedPercentageThreshold);
        }
    }
}