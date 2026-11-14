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

        var fullLoad = System.Diagnostics.Stopwatch.StartNew();
        var opening = System.Diagnostics.Stopwatch.StartNew();
        using var connection = AccessDatabase.Open(databaseFile, password);
        PwrPerformanceLog.Record("MDB.Open", opening);
        var schema = System.Diagnostics.Stopwatch.StartNew();
        EnsureSchema(connection);
        PwrPerformanceLog.Record("MDB.Schema", schema);

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
        PwrPerformanceLog.Record("MDB.WorkerProfileTotal", fullLoad,
            "sections=" + sections.Count + "; contracts=" +
            contracts.Rows.Count);

        return new PrwWorkerProfile(identity, sections);
    }

    /// <summary>
    /// Builds an unsaved worker profile from the actual selected MDB field schema.
    /// No database record is created until the user presses Save.
    /// </summary>
    public static PrwWorkerProfile CreateDraft(string databaseFile, string? password = null)
    {
        using var connection = AccessDatabase.Open(databaseFile, password);
        EnsureSchema(connection);

        string[] tables =
        [
            "tblWorker", "tblWorkerBio", "tblWorkerBusiness",
            "tblWorkerOver", "tblWorkerPhysical", "tblWorkerSkills",
            "tblWorkerAttributes", "tblAlternative", "tblContract",
            "tblContractBio"
        ];
        var sections = new List<PrwProfileSection>();
        foreach (string tableName in tables)
        {
            using var adapter = new OleDbDataAdapter(
                "SELECT * FROM [" + tableName + "] WHERE 1 = 0", connection);
            var table = new DataTable(tableName);
            adapter.FillSchema(table, SchemaType.Source);
            if (tableName is "tblWorker" or "tblWorkerBio" or
                "tblWorkerBusiness" or "tblWorkerPhysical" or "tblWorkerSkills")
            {
                DataRow row = table.NewRow();
                foreach (DataColumn column in table.Columns)
                {
                    if (column.ColumnName is "UID" or "WorkerUID")
                    {
                        row[column] = Convert.ChangeType(0, column.DataType);
                        continue;
                    }
                    if (column.ColumnName is "Name" or "ShortName" or "Biography")
                    {
                        row[column] = "";
                        continue;
                    }
                    if (!column.AllowDBNull && row[column] is DBNull)
                    {
                        object fallback = column.DataType == typeof(string) ? ""
                            : column.DataType == typeof(bool) ? false
                            : column.DataType == typeof(DateTime) ? DateTime.Today
                            : column.DataType.IsValueType
                                ? Activator.CreateInstance(column.DataType)!
                                : throw new InvalidDataException(
                                    "A required worker field has no default: " +
                                    tableName + "." + column.ColumnName);
                        row[column] = fallback;
                    }
                }
                table.Rows.Add(row);
            }
            sections.Add(new PrwProfileSection(tableName, table));
        }
        return new PrwWorkerProfile(
            new PrwWorkerSummary(0, "", ""), sections);
    }

    private static void EnsureSchema(OleDbConnection connection)
    {
        var inspection = PrwSchema.Inspect(connection, requireExpandedTables: false);
        if (!inspection.IsValid)
            throw new InvalidDataException(
                "This database is not compatible with the native PRW worker reader.\n" +
                string.Join("\n", inspection.Problems.Take(12)) +
                (inspection.Problems.Count > 12 ? "\nAdditional problems omitted." : ""));
    }

    private static DataTable EmptyTable(OleDbConnection connection, string table)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using var adapter = new OleDbDataAdapter(
            "SELECT * FROM [" + table + "] WHERE 1 = 0", connection);
        var rows = new DataTable(table);
        adapter.Fill(rows);
        PwrPerformanceLog.Record("MDB." + table + ".SchemaOnly", stopwatch);
        return rows;
    }

    private static DataTable ForKey(
        OleDbConnection connection, string table, string key, object value)
    {
        // All table/column arguments are fixed, trusted names from PrwSchema,
        // and the value is passed as an OLE DB positional parameter.
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using var command = new OleDbCommand(
            "SELECT * FROM [" + table + "] WHERE [" + key + "] = ?", connection);
        command.Parameters.AddWithValue("lookup_value", value);
        using var adapter = new OleDbDataAdapter(command);
        var rows = new DataTable(table);
        adapter.Fill(rows);
        PwrPerformanceLog.Record("MDB." + table + ".Load", stopwatch,
            "rows=" + rows.Rows.Count);
        return rows;
    }
}
