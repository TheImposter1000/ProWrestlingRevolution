using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProWrestlingRevolution;

public sealed class GameState
{
    public DateTime Date { get; set; } = new(2026, 11, 9);
    public int Week { get; set; } = 1;
    public int RandomSeed { get; set; } = 20261109;
    public int PlayerPromotionId { get; set; } = 1;
    public string NextShowName { get; set; } = "Collision Course";
    public bool ShowRunThisWeek { get; set; }
    public List<Promotion> Promotions { get; set; } = [];
    public List<Worker> Workers { get; set; } = [];
    public List<Championship> Championships { get; set; } = [];
    public List<Storyline> Storylines { get; set; } = [];
    public List<Segment> Card { get; set; } = [];
    public List<ShowReport> History { get; set; } = [];
    public List<NewsItem> News { get; set; } = [];

    [JsonIgnore] public Promotion Player => Promotions.Single(p => p.Id == PlayerPromotionId);
    public IEnumerable<Worker> Roster() => Workers.Where(w => w.EmployerId == PlayerPromotionId);
    public IEnumerable<Worker> FreeAgents() => Workers.Where(w => w.EmployerId == 0);
    public Worker Worker(int id) => Workers.Single(w => w.Id == id);

    public static GameState LoadScenario(string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Starter scenario is missing.", file);
        var game = JsonSerializer.Deserialize<GameState>(
            File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Starter universe could not be loaded.");
        if (!game.Promotions.Any(p => p.Id == game.PlayerPromotionId)
            || game.Workers.Count == 0 || game.Championships.Any(t => !game.Workers.Any(w => w.Id == t.HolderId)))
            throw new InvalidDataException("Starter universe has invalid wrestler or promotion references.");
        return game;
    }
}

public sealed class Promotion
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Initials { get; set; } = "";
    public int Popularity { get; set; }
    public int Prestige { get; set; }
    public decimal Cash { get; set; }
}

public sealed class Worker
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string Role { get; set; } = "Wrestler";
    public string Alignment { get; set; } = "Face";
    public string Style { get; set; } = "All-Rounder";
    public int Ring { get; set; }
    public int Psychology { get; set; }
    public int Microphone { get; set; }
    public int StarQuality { get; set; }
    public int Stamina { get; set; }
    public int Popularity { get; set; }
    public int Momentum { get; set; }
    public int Morale { get; set; } = 75;
    public int Fatigue { get; set; }
    public int InjuryWeeks { get; set; }
    public int EmployerId { get; set; }
    public int ContractWeeks { get; set; }
    public int WeeklyPay { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    [JsonIgnore] public string Availability => InjuryWeeks > 0 ? "Injured (" + InjuryWeeks + " weeks)" : "Available";
}

public sealed class Championship
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int HolderId { get; set; }
    public int Prestige { get; set; }
    public int Defenses { get; set; }
}

public sealed class Storyline
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int A { get; set; }
    public int B { get; set; }
    public int Heat { get; set; } = 40;
    public bool Active { get; set; } = true;
}

public sealed class Segment
{
    public int Id { get; set; }
    public string Type { get; set; } = "Match";
    public int A { get; set; }
    public int B { get; set; }
    public int? Winner { get; set; }
    public int? ChampionshipId { get; set; }
    public int Minutes { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class ShowReport
{
    public int Week { get; set; }
    public DateTime Date { get; set; }
    public string Name { get; set; } = "";
    public int Rating { get; set; }
    public int Attendance { get; set; }
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public List<string> Results { get; set; } = [];
    [JsonIgnore] public decimal Profit => Revenue - Expenses;
}

public sealed class NewsItem
{
    public int Week { get; set; }
    public string Text { get; set; } = "";
}
