using System.Windows.Forms;

namespace ProWrestlingRevolution;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var scenario = Path.Combine(AppContext.BaseDirectory, "Data", "Scenarios", "Genesis.json");
            var save = Path.Combine(AppContext.BaseDirectory, "Saves", "career.db");
            if (args.Contains("--seed", StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(save)) File.Delete(save);
                using var seedStore = new GameStore(save);
                seedStore.Save(GameState.LoadScenario(scenario));
                return 0;
            }
            if (args.Contains("--smoke", StringComparer.OrdinalIgnoreCase))
            {
                var temp = Path.Combine(Path.GetTempPath(), "pwr-" + Guid.NewGuid().ToString("N") + ".db");
                try
                {
                    var game = GameState.LoadScenario(scenario);
                    if (game.Workers.Count < 20 || game.Promotions.Count < 3)
                        throw new Exception("Starter universe is incomplete.");
                    var roster = game.Roster().ToList();
                    GameEngine.AddMatch(game, roster[0].Id, roster[1].Id, roster[0].Id, null, 20, "Main Event");
                    GameEngine.AddMatch(game, roster[2].Id, roster[3].Id, null, null, 15, "Singles");
                    GameEngine.AddAngle(game, roster[0].Id, roster[1].Id, 8, "Face off");
                    var report = GameEngine.RunShow(game);
                    if (report.Rating <= 0 || report.Results.Count != 3) throw new Exception("Simulation failed.");
                    using var store = new GameStore(temp);
                    store.Save(game);
                    var loaded = store.LoadOrCreate(scenario);
                    if (loaded.History.Count != 1 || loaded.Card.Count != 0) throw new Exception("Save check failed.");
                    GameEngine.AdvanceWeek(loaded);
                    if (loaded.Week != 2) throw new Exception("Calendar check failed.");
                    store.Save(loaded);
                    return 0;
                }
                finally
                {
                    foreach (var suffix in new[] { "", "-wal", "-shm" })
                        try { File.Delete(temp + suffix); } catch (IOException) { }
                }
            }
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(scenario, save));
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Pro Wrestling Revolution Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
