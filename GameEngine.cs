namespace ProWrestlingRevolution;

public static class GameEngine
{
    private static int Clamp(int value) => Math.Clamp(value, 0, 100);
    private static int NextId(IEnumerable<int> ids) => ids.DefaultIfEmpty(0).Max() + 1;
    private static void BookingOpen(GameState g)
    {
        if (g.ShowRunThisWeek) throw new InvalidOperationException("Advance to the next week before booking.");
    }

    private static void CheckPair(GameState g, int a, int b)
    {
        if (a == b) throw new InvalidOperationException("Select two different wrestlers.");
        foreach (var id in new[] { a, b })
        {
            var w = g.Workers.FirstOrDefault(x => x.Id == id);
            if (w == null || w.EmployerId != g.PlayerPromotionId)
                throw new InvalidOperationException("Both participants must work for your promotion.");
            if (w.InjuryWeeks > 0)
                throw new InvalidOperationException(w.Name + " is unavailable due to injury.");
        }
    }

    public static void AddMatch(GameState g, int a, int b, int? winner, int? championship, int minutes, string notes)
    {
        BookingOpen(g);
        CheckPair(g, a, b);
        if (minutes < 3 || minutes > 90) throw new InvalidOperationException("Matches last between 3 and 90 minutes.");
        if (winner.HasValue && winner != a && winner != b) throw new InvalidOperationException("Winner must be in the match.");
        if (championship is int titleId)
        {
            var title = g.Championships.FirstOrDefault(x => x.Id == titleId)
                ?? throw new InvalidOperationException("Championship not found.");
            if (title.HolderId != a && title.HolderId != b)
                throw new InvalidOperationException("The reigning champion must be in the title match.");
            if (g.Card.Any(x => x.ChampionshipId == titleId))
                throw new InvalidOperationException("Only one defense per championship per show.");
        }
        g.Card.Add(new Segment { Id = NextId(g.Card.Select(x => x.Id)), Type = "Match",
            A = a, B = b, Winner = winner, ChampionshipId = championship,
            Minutes = minutes, Notes = notes.Trim() });
    }

    public static void AddAngle(GameState g, int a, int b, int minutes, string notes)
    {
        BookingOpen(g);
        CheckPair(g, a, b);
        if (minutes < 1 || minutes > 30) throw new InvalidOperationException("Angles last between 1 and 30 minutes.");
        g.Card.Add(new Segment { Id = NextId(g.Card.Select(x => x.Id)), Type = "Angle",
            A = a, B = b, Minutes = minutes, Notes = string.IsNullOrWhiteSpace(notes) ? "In-ring confrontation" : notes.Trim() });
    }

    public static void Remove(GameState g, int id)
    {
        BookingOpen(g);
        g.Card.RemoveAll(x => x.Id == id);
    }

    public static void Move(GameState g, int id, int delta)
    {
        BookingOpen(g);
        int i = g.Card.FindIndex(x => x.Id == id);
        if (i < 0 || i + delta < 0 || i + delta >= g.Card.Count) return;
        (g.Card[i], g.Card[i + delta]) = (g.Card[i + delta], g.Card[i]);
    }

    public static void Sign(GameState g, int workerId)
    {
        var w = g.Worker(workerId);
        if (w.EmployerId != 0) throw new InvalidOperationException("Only free agents can be signed.");
        decimal bonus = w.WeeklyPay * 4m;
        if (g.Player.Cash < bonus) throw new InvalidOperationException("Insufficient funds for the signing bonus.");
        g.Player.Cash -= bonus;
        w.EmployerId = g.PlayerPromotionId;
        w.ContractWeeks = 52;
        w.Morale = Clamp(w.Morale + 8);
        g.News.Insert(0, new NewsItem { Week = g.Week, Text = g.Player.Initials + " signed " + w.Name + " for one year." });
    }

    public static void CreateStory(GameState g, string name, int a, int b)
    {
        CheckPair(g, a, b);
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Enter a rivalry name.");
        if (g.Storylines.Any(s => s.Active && ((s.A == a && s.B == b) || (s.A == b && s.B == a))))
            throw new InvalidOperationException("These wrestlers are already in an active rivalry.");
        g.Storylines.Add(new Storyline { Id = NextId(g.Storylines.Select(s => s.Id)),
            Name = name.Trim(), A = a, B = b, Heat = 32 });
    }

    public static ShowReport RunShow(GameState g)
    {
        BookingOpen(g);
        if (g.Card.Count == 0 || g.Card.Sum(x => x.Minutes) < 30)
            throw new InvalidOperationException("Book a card of at least 30 minutes before running the show.");
        foreach (var segment in g.Card)
        {
            CheckPair(g, segment.A, segment.B);
            if (segment.Winner.HasValue && segment.Winner != segment.A && segment.Winner != segment.B)
                throw new InvalidOperationException("Invalid selected winner.");
        }
        var rng = new Random(g.RandomSeed + g.Week * 9973);
        var report = new ShowReport { Week = g.Week, Date = g.Date, Name = g.NextShowName };
        double ratingSum = 0;
        double weightSum = 0;
        foreach (var seg in g.Card)
        {
            var a = g.Worker(seg.A);
            var b = g.Worker(seg.B);
            var feud = g.Storylines.FirstOrDefault(x => x.Active &&
                ((x.A == a.Id && x.B == b.Id) || (x.A == b.Id && x.B == a.Id)));
            int heatBonus = feud is null ? 0 : (feud.Heat - 35) / 8;
            bool isMatch = seg.Type == "Match";
            int rating = Clamp((int)Math.Round(isMatch
                ? (a.Ring + b.Ring) * .30 + (a.Psychology + b.Psychology) * .11 +
                  (a.Popularity + b.Popularity) * .08 + (a.Momentum + b.Momentum) * .04 +
                  heatBonus - (a.Fatigue + b.Fatigue) * .08 + rng.Next(-10, 11)
                : (a.Microphone + b.Microphone) * .34 + (a.Popularity + b.Popularity) * .12 +
                  (a.Momentum + b.Momentum) * .04 + heatBonus + rng.Next(-9, 10)));
            string description;
            if (isMatch)
            {
                var victor = seg.Winner ?? (rng.Next(2) == 0 ? a.Id : b.Id);
                var winningWorker = g.Worker(victor);
                var losingWorker = victor == a.Id ? b : a;
                winningWorker.Wins++;
                losingWorker.Losses++;
                winningWorker.Momentum = Clamp(winningWorker.Momentum + 5);
                losingWorker.Momentum = Clamp(losingWorker.Momentum - 3);
                winningWorker.Popularity = Clamp(winningWorker.Popularity + (rating > 74 ? 2 : 1));
                a.Fatigue = Clamp(a.Fatigue + Math.Max(3, seg.Minutes / 3));
                b.Fatigue = Clamp(b.Fatigue + Math.Max(3, seg.Minutes / 3));
                description = winningWorker.Name + " defeated " + losingWorker.Name;
                if (seg.ChampionshipId is int championshipId)
                {
                    var title = g.Championships.Single(x => x.Id == championshipId);
                    if (title.HolderId == victor) title.Defenses++;
                    else
                    {
                        title.HolderId = victor;
                        title.Defenses = 0;
                        g.News.Insert(0, new NewsItem { Week = g.Week,
                            Text = winningWorker.Name + " won the " + title.Name + "." });
                        description += " | NEW CHAMPION";
                    }
                }
                if (rng.Next(1000) < (a.Fatigue + b.Fatigue) / 9)
                {
                    var injured = rng.Next(2) == 0 ? a : b;
                    injured.InjuryWeeks = rng.Next(1, 5);
                    g.News.Insert(0, new NewsItem { Week = g.Week,
                        Text = injured.Name + " was injured for " + injured.InjuryWeeks + " weeks." });
                }
            }
            else
            {
                a.Momentum = Clamp(a.Momentum + 1);
                b.Momentum = Clamp(b.Momentum + 1);
                description = a.Name + " and " + b.Name + " | " + seg.Notes;
            }
            if (feud is not null) feud.Heat = Clamp(feud.Heat + (rating >= 70 ? 5 : rating >= 50 ? 3 : -2));
            double weight = isMatch ? 3 : 2;
            ratingSum += rating * weight;
            weightSum += weight;
            report.Results.Add(seg.Type + ": " + description + " | " + seg.Minutes + " min | Rating " + rating);
        }
        report.Rating = (int)Math.Round(ratingSum / weightSum);
        report.Attendance = Math.Max(100, 100 + g.Player.Popularity * 23 + (report.Rating - 50) * 11 + rng.Next(-70, 71));
        report.Revenue = report.Attendance * 32m + 6_000m + g.Player.Popularity * 190m;
        report.Expenses = 12_000m + report.Attendance * 7m + g.Roster().Sum(x => x.WeeklyPay);
        g.Player.Cash += report.Profit;
        g.Player.Popularity = Clamp(g.Player.Popularity + (report.Rating >= 75 ? 2 : report.Rating >= 55 ? 1 : -1));
        if (report.Rating >= 80) g.Player.Prestige = Clamp(g.Player.Prestige + 1);
        g.History.Insert(0, report);
        g.News.Insert(0, new NewsItem { Week = g.Week,
            Text = g.Player.Initials + " " + report.Name + ": rating " + report.Rating +
                   ", attendance " + report.Attendance.ToString("N0") + "." });
        g.Card.Clear();
        g.ShowRunThisWeek = true;
        return report;
    }

    public static void AdvanceWeek(GameState g)
    {
        if (!g.ShowRunThisWeek) throw new InvalidOperationException("Run this week's show before advancing.");
        g.Week++;
        g.Date = g.Date.AddDays(7);
        g.ShowRunThisWeek = false;
        foreach (var w in g.Workers)
        {
            if (w.InjuryWeeks > 0) w.InjuryWeeks--;
            w.Fatigue = Math.Max(0, w.Fatigue - 14);
            if (w.EmployerId != g.PlayerPromotionId) continue;
            w.ContractWeeks = Math.Max(0, w.ContractWeeks - 1);
            if (w.ContractWeeks == 0)
            {
                w.EmployerId = 0;
                g.News.Insert(0, new NewsItem { Week = g.Week, Text = w.Name + " has entered free agency." });
            }
        }
        foreach (var story in g.Storylines.Where(s => s.Active)) story.Heat = Clamp(story.Heat - 1);
        var rng = new Random(g.RandomSeed + g.Week * 7919);
        foreach (var rival in g.Promotions.Where(p => p.Id != g.PlayerPromotionId))
        {
            rival.Cash += rng.Next(-14000, 25001);
            rival.Popularity = Clamp(rival.Popularity + rng.Next(-1, 2));
        }
    }
}
