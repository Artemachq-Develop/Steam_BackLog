using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Steam_BackLog.Reports
{
    public class HtmlReportGenerator
    {
        private readonly string _outputFileName;

        public HtmlReportGenerator(string outputFileName = "SteamReport.html")
        {
            _outputFileName = outputFileName;
        }

        public void GenerateAndOpen(List<Models.GameData> games)
        {
            var html = GenerateHtmlString(games);
            
            File.WriteAllText(_outputFileName, html, Encoding.UTF8);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Path.GetFullPath(_outputFileName),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Не удалось открыть браузер: {ex.Message}");
            }
        }

        private string GenerateHtmlString(List<Models.GameData> topGames)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html><html lang='ru'><head><meta charset='UTF-8'><title>Мой Steam Бэклог</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #1b2838; color: #c7d5e0; margin: 0; padding: 20px; }");
            html.AppendLine("h1 { text-align: center; color: #ffffff; margin-bottom: 30px; }");
            html.AppendLine(".container { max-width: 1050px; margin: 0 auto; }");
            html.AppendLine("table { width: 100%; border-collapse: collapse; background-color: #171a21; box-shadow: 0 4px 8px rgba(0,0,0,0.3); }");
            
            html.AppendLine("th { background-color: #2a475e; color: #ffffff; text-transform: uppercase; font-size: 14px; padding: 15px; text-align: left; position: relative; }");
            html.AppendLine("th.sortable { cursor: pointer; transition: background-color 0.2s; }");
            html.AppendLine("th.sortable:hover { background-color: #3b5c77; }");
            html.AppendLine("th.sortable::after { content: ' \\25B2\\25BC'; font-size: 10px; color: #8f98a0; position: absolute; right: 8px; top: 50%; transform: translateY(-50%); }");
            
            html.AppendLine("td { padding: 15px; text-align: left; border-bottom: 1px solid #2a475e; vertical-align: middle; }");
            html.AppendLine("tr:hover { background-color: #202a36; }");
            html.AppendLine(".cover { width: 120px; border-radius: 4px; box-shadow: 0 2px 4px rgba(0,0,0,0.5); }");
            html.AppendLine(".game-link { color: #ffffff; text-decoration: none; font-size: 18px; font-weight: 500; transition: color 0.2s; display: block; margin-bottom: 5px; }");
            html.AppendLine(".game-link:hover { color: #66c0f4; }");
            
            html.AppendLine(".hltb-link { font-size: 12px; color: #8f98a0; text-decoration: none; display: inline-block; padding: 3px 6px; background-color: #1b2838; border-radius: 3px; border: 1px solid #2a475e; transition: all 0.2s; }");
            html.AppendLine(".hltb-link:hover { color: #ffffff; background-color: #2a475e; border-color: #66c0f4; }");
            
            html.AppendLine(".meta-box { display: inline-block; width: 40px; height: 40px; line-height: 40px; text-align: center; border-radius: 4px; font-weight: bold; font-size: 18px; color: #fff; }");
            html.AppendLine(".meta-green { background-color: #66cc33; }");
            html.AppendLine(".meta-yellow { background-color: #ffcc33; color: #333; }");
            html.AppendLine(".meta-red { background-color: #ff0000; }");
            html.AppendLine(".rank { font-size: 18px; font-weight: bold; color: #4f94bc; }");
            html.AppendLine("</style></head><body><div class='container'>");
            html.AppendLine($"<h1>Топ игр на прохождение — Найдено: {topGames.Count}</h1>");
            
            html.AppendLine("<table id='gamesTable'>");
            html.AppendLine("<thead><tr>");
            html.AppendLine("<th>#</th>");
            html.AppendLine("<th>Обложка</th>");
            html.AppendLine("<th class='sortable' onclick='sortTable(2, \"string\")'>Название</th>");
            html.AppendLine("<th class='sortable' onclick='sortTable(3, \"number\")'>Metascore</th>");
            html.AppendLine("<th class='sortable' onclick='sortTable(4, \"number\")'>HLTB (Часы)</th>");
            html.AppendLine("<th class='sortable' onclick='sortTable(5, \"number\")'>Наиграно (Часы)</th>");
            html.AppendLine("</tr></thead><tbody>");

            int rank = 1;
            foreach (var g in topGames)
            {
                string imgUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{g.AppId}/capsule_184x69.jpg";
                string storeUrl = $"https://store.steampowered.com/app/{g.AppId}/";
                
                string safeName = Uri.EscapeDataString(CleanGameNameForUrl(g.Name));
                string hltbUrl = $"https://howlongtobeat.com/?q={safeName}";
                
                double playedHours = Math.Round(g.PlaytimeForever / 60.0, 1);

                string metaClass = "meta-red";
                if (g.MetacriticScore >= 75) metaClass = "meta-green";
                else if (g.MetacriticScore >= 50) metaClass = "meta-yellow";

                html.AppendLine("<tr>");
                html.AppendLine($"<td class='rank'>{rank}</td>");
                html.AppendLine($"<td><a href='{storeUrl}' target='_blank'><img class='cover' src='{imgUrl}' alt='Cover' onerror=\"this.style.display='none'\"></a></td>");
                html.AppendLine($"<td><a href='{storeUrl}' target='_blank' class='game-link'>{g.Name}</a></td>");
                html.AppendLine($"<td><div class='meta-box {metaClass}'>{g.MetacriticScore}</div></td>");
                html.AppendLine($"<td><div style='font-size: 16px; font-weight: 500; margin-bottom: 5px;'>{g.TimeToBeatHours:F1} ч.</div>");
                html.AppendLine($"<a href='{hltbUrl}' target='_blank' class='hltb-link' title='Открыть на HowLongToBeat'>HLTB &#8599;</a></td>");
                html.AppendLine($"<td style='color: #8f98a0;'>{playedHours:F1} ч.</td>");
                html.AppendLine("</tr>");
                rank++;
            }

            html.AppendLine("</tbody></table></div>");
            html.AppendLine("<script>");
            html.AppendLine("let sortDirections = [true, true, true, true, true, true];");
            html.AppendLine("function sortTable(columnIndex, type) {");
            html.AppendLine("  var table = document.getElementById('gamesTable');");
            html.AppendLine("  var tbody = table.tBodies[0];");
            html.AppendLine("  var rows = Array.from(tbody.rows);");
            html.AppendLine("  var dir = sortDirections[columnIndex] ? -1 : 1;");
            html.AppendLine("  rows.sort(function(a, b) {");
            html.AppendLine("    var aCol = a.cells[columnIndex].innerText.replace(/[^0-9.]/g, '').trim();");
            html.AppendLine("    var bCol = b.cells[columnIndex].innerText.replace(/[^0-9.]/g, '').trim();");
            html.AppendLine("    if(type === 'number') {");
            html.AppendLine("      var aNum = parseFloat(aCol) || 0;");
            html.AppendLine("      var bNum = parseFloat(bCol) || 0;");
            html.AppendLine("      return (aNum - bNum) * dir;");
            html.AppendLine("    } else {");
            html.AppendLine("      var aText = a.cells[columnIndex].innerText.trim().toLowerCase();");
            html.AppendLine("      var bText = b.cells[columnIndex].innerText.trim().toLowerCase();");
            html.AppendLine("      if (aText < bText) return -1 * dir;");
            html.AppendLine("      if (aText > bText) return 1 * dir;");
            html.AppendLine("      return 0;");
            html.AppendLine("    }");
            html.AppendLine("  });");
            html.AppendLine("  sortDirections[columnIndex] = !sortDirections[columnIndex];");
            html.AppendLine("  rows.forEach(function(row) { tbody.appendChild(row); });");
            html.AppendLine("}");
            html.AppendLine("</script>");
            html.AppendLine("</body></html>");
            
            return html.ToString();
        }

        private string CleanGameNameForUrl(string? name)
        {
            var cleanName = Regex.Replace(name ?? string.Empty, @"(™|®|©)", "");
            cleanName = Regex.Replace(cleanName, @"(?i)(Edition|Director's Cut|Game of the Year|GOTY)", "").Trim();
            return cleanName.Split('-')[0].Trim();
        }
    }
}
