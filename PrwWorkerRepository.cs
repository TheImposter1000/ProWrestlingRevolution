using System.Data;
using System.Data.OleDb;

namespace ProWrestlingRevolution;

/// <summary>
/// Small, typed entry point for workers in the native PRW MDB. Each section
/// retains its actual Access data types so dates, numbers and booleans are not
/// reduced to strings before the game can use them.
/// </summary>
public sealed record PrwWorkerSummary(object Uid, string Name, string ShortName);

public sealed record PrwProfileSection(string Table, DataTable Records);

public sealed record PrwWorkerProfile(
    PrwWorkerSummary Identity, IReadOnlyList<PrwProfileSection> Sections);

public static class PrwWorkerRepository
{
    public static IReadOnlyList<PrwWorkerSummary> List(
        string databaseFile, string? password = null)
    {
        using var connection = AccessDatabase.Open(databaseFile, password);
        EnsureSchema(connection);
        using var adapter = new OleDbDataAdapter(
            "SELECT [UID], [Name], [ShortName] FROM [tblWorker] ORDER BY [Name]",
            connection);
        var result = new DataTable();
        adapter.Fill(result);

        return result.Rows.Cast<DataRow>()
            .Select(row => new PrwWorkerSummary(
                row["UID"],
                Convert.ToString(row["Name"]) ?? "",
                Convert.ToString(row["ShortName"]) ?? ""))
            .ToArray();
    }

    public static PrwWorkerProfile Load(
        string databaseFile, object workerUid, string? password = null)
    {
        if (workerUid is null or DBNull)
            throw new ArgumentException("Select a worker with a valid UID.", nameof(workerUid));

        using var connection = AccessDatabase.Open(databaseFile, password);
        EnsureSchema(connection);

        DataTable worker = ForKey(connection, "tblWorker", "UID", workerUid);
        if (worker.Rows.Count != 1)
            throw new InvalidDataException(
                "The requested worker does not exist or has a duplicate UID.");

        DataRow identityRow = worker.Rows[0];
        var identity = new PrwWorkerSummary(
            identityRow["UID"],
            Convert.ToString(identityRow["Name"]) ?? "",
            Convert.ToString(identityRow["ShortName"]) ?? "");

        var sections = new List<PrwProfileSection>
        {
            new("tblWorker", worker)
        };

        // One-to-one worker details and one-to-many worker-linked entries.
        foreach (string table in new[]
        {
            "tblWorkerBio", "tblWorkerBusiness", "tblWorkerOver",
            "tblWorkerPhysical", "tblWorkerSkills", "tblWorkerAttributes",
            "tblAlternative", "tblContract"
        })
        {
            sections.Add(new PrwProfileSection(
                table, ForKey(connection, table, "WorkerUID", workerUid)));
        }

        // Contract biographies are keyed by ContractUID, not WorkerUID.
        // Never join on a similarly named or positional field.
        var contractBios = EmptyTable(connection, "tblContractBio");
        var contracts = sections.Single(s => s.Table == "tblContract").Records;
        foreach (DataRow contract in contracts.Rows)
        {
            if (contract["UID"] is null or DBNull) continue;
            var bios = ForKey(
                connection, "tblContractBio", "ContractUID", contract["UID"]);
            foreach (DataRow bio in bios.Rows)
                contractBios.ImportRow(bio);
        }
        sections.Add(new PrwProfileSection("tblContractBio", contractBios));

        return new PrwWorkerProfile(identity, sections);
    }

    private static void EnsureSchema(OleDbConnection connection)
    {
        var inspection = PrwSchema.Inspect(connection);
        if (!inspection.IsValid)
            throw new InvalidDataException(
                "This database is not compatible with the native PRW worker reader.\n" +
                string.Join("\n", inspection.Problems.Take(12)) +
                (inspection.Problems.Count > 12 ? "\nAdditional problems omitted." : ""));
    }

    private static DataTable EmptyTable(OleDbConnection connection, string table)
    {
        using var adapter = new OleDbDataAdapter(
            "SELECT * FROM [" + table + "] WHERE 1 = 0", connection);
        var rows = new DataTable(table);
        adapter.Fill(rows);
        return rows;
    }

    private static DataTable ForKey(
        OleDbConnection connection, string table, string key, object value)
    {
        // All table/column arguments are fixed, trusted names from PrwSchema,
        // and the value is passed as an OLE DB positional parameter.
        using var command = new OleDbCommand(
            "SELECT * FROM [" + table + "] WHERE [" + key + "] = ?", connection);
        command.Parameters.AddWithValue("lookup_value", value);
        using var adapter = new OleDbDataAdapter(command);
        var rows = new DataTable(table);
        adapter.Fill(rows);
        return rows;
    }
}
