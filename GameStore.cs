using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace ProWrestlingRevolution;

public sealed class GameStore(string path) : IDisposable
{
    public string FilePath { get; } = path;

    public GameState LoadOrCreate(string scenarioFile)
    {
        using var connection = Open();
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT Json FROM Career WHERE Id = 1";
        var json = query.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(json))
        {
            var initial = GameState.LoadScenario(scenarioFile);
            Save(initial);
            return initial;
        }
        return JsonSerializer.Deserialize<GameState>(json)
            ?? throw new InvalidDataException("Career save is invalid.");
    }

    public void Save(GameState game)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Career (Id, Json) VALUES (1, $json) ON CONFLICT(Id) DO UPDATE SET Json = excluded.Json";
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(game));
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS Career (Id INTEGER PRIMARY KEY, Json TEXT NOT NULL);";
        command.ExecuteNonQuery();
        return connection;
    }

    public void Dispose() { }
}
