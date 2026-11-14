using System.Data;
using System.Data.OleDb;
using System.Globalization;

namespace ProWrestlingRevolution;

/// <summary>
/// The Contracts screen edits native tblContract and tblContractBio only.
/// Other staged Worker Editor sections are never committed by its Save
/// action. Inserts, edits and removals are one ACID MDB transaction.
/// </summary>
internal static class PrwContractEditorService
{
    internal sealed record Company(object Uid, string Name, string Initials, string Logo)
    {
        public override string ToString() =>
            string.IsNullOrWhiteSpace(Initials) ? Name : Initials + "  |  " + Name;
    }

    internal sealed record Template(string WorkerName, object FedUid,
        string EmployerInitials, DataRow Values)
    {
        public override string ToString() => WorkerName + "  |  " + EmployerInitials;
    }

    internal sealed record WorkerChoice(object Uid, string Name)
    {
        public override string ToString() => Name;
    }

    internal static IReadOnlyList<WorkerChoice> LoadWorkers(string file, string? password)
    {
        if (!File.Exists(file)) return [];
        using var connection = AccessDatabase.Open(file, password);
        using var command = new OleDbCommand(
            "SELECT [UID], [Name] FROM [tblWorker] ORDER BY [Name]", connection);
        using var reader = command.ExecuteReader();
        var people = new List<WorkerChoice>();
        while (reader?.Read() == true && people.Count < 10000)
        {
            if (reader["UID"] is DBNull) continue;
            people.Add(new WorkerChoice(reader["UID"],
                Convert.ToString(reader["Name"]) ?? ""));
        }
        return people;
    }

    internal static IReadOnlyList<Company> LoadCompanies(string file, string? password)
    {
        if (!File.Exists(file)) return [];
        using var connection = AccessDatabase.Open(file, password);
        using var command = new OleDbCommand(
            "SELECT [UID], [Name], [Initials], [Logo] FROM [tblFed] ORDER BY [Name]",
            connection);
        using var reader = command.ExecuteReader();
        var companies = new List<Company>();
        while (reader?.Read() == true)
        {
            if (reader["UID"] is DBNull) continue;
            companies.Add(new Company(reader["UID"],
                Convert.ToString(reader["Name"]) ?? "",
                Convert.ToString(reader["Initials"]) ?? "",
                Convert.ToString(reader["Logo"]) ?? ""));
        }
        return companies;
    }

    internal static IReadOnlyList<Template> LoadTemplates(
        string file, string? password, object currentWorkerUid)
    {
        if (!File.Exists(file)) return [];
        using var connection = AccessDatabase.Open(file, password);
        // Read a limited number of existing contracts. The Contract Editor
        // never writes or changes records belonging to a different worker.
        using var command = new OleDbCommand(
            "SELECT TOP 300 c.*, w.[Name] AS [WorkerLabel] " +
            "FROM [tblContract] AS c INNER JOIN [tblWorker] AS w " +
            "ON c.[WorkerUID] = w.[UID] ORDER BY w.[Name]", connection);
        using var adapter = new OleDbDataAdapter(command);
        var data = new DataTable();
        adapter.Fill(data);
        var result = new List<Template>();
        foreach (DataRow row in data.Rows)
        {
            if (Equals(row["WorkerUID"], currentWorkerUid)) continue;
            result.Add(new Template(
                Convert.ToString(row["WorkerLabel"]) ?? "",
                row["FedUID"],
                Convert.ToString(row["EmployerInitials"]) ?? "", row));
        }
        return result;
    }

    internal static bool HasPendingChanges(PrwWorkerProfile profile) =>
        new[] { "tblContract", "tblContractBio" }.Any(name =>
            profile.Sections.FirstOrDefault(section => section.Table == name)
                ?.Records.Rows.Cast<DataRow>().Any(row =>
                    row.RowState is DataRowState.Added or DataRowState.Modified or
                        DataRowState.Deleted) == true);

    internal static void AssertFields(DataTable contract)
    {
        var native = PrwSchema.Tables.Single(table => table.Name == "tblContract");
        if (native.Columns.Any(column => !contract.Columns.Contains(column)))
            throw new InvalidDataException("This contract table lacks required native fields.");
    }

    private static string Quote(string name) =>
        "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";

    private static string[] ChangedColumns(DataRow row, bool added)
    {
        return row.Table.Columns.Cast<DataColumn>()
            .Where(column => added
                ? row[column] is not DBNull
                : !Equals(row[column, DataRowVersion.Original], row[column]))
            .Select(column => column.ColumnName).ToArray();
    }

    private static void Parameter(OleDbCommand command, DataColumn column, object value)
    {
        Type type = column.DataType;
        var oleType = type == typeof(string)
            ? value is string text && text.Length > 255
                ? OleDbType.LongVarWChar : OleDbType.VarWChar
            : type == typeof(DateTime) ? OleDbType.DBTimeStamp
            : type == typeof(bool) ? OleDbType.Boolean
            : type == typeof(short) ? OleDbType.SmallInt
            : type == typeof(int) ? OleDbType.Integer
            : type == typeof(long) ? OleDbType.BigInt
            : type == typeof(byte) ? OleDbType.UnsignedTinyInt
            : type == typeof(float) ? OleDbType.Single
            : type == typeof(double) ? OleDbType.Double
            : type == typeof(decimal) ? OleDbType.Decimal
            : OleDbType.Variant;
        var param = command.Parameters.Add("p" + command.Parameters.Count, oleType);
        param.Value = value is DBNull ? DBNull.Value : value;
    }

    private static void CheckExisting(OleDbConnection connection,
        OleDbTransaction transaction, DataRow row, string table)
    {
        string keyName = table == "tblContract" ? "UID" : "ContractUID";
        using var query = new OleDbCommand(
            "SELECT * FROM " + Quote(table) + " WHERE " + Quote(keyName) + " = ?",
            connection, transaction);
        Parameter(query, row.Table.Columns[keyName]!,
            row[keyName, DataRowVersion.Original]);
        using var reader = query.ExecuteReader()
            ?? throw new IOException("Cannot verify the contract record.");
        if (!reader.Read())
            throw new DBConcurrencyException(
                "This contract was deleted outside PWR. Reload before saving.");
        foreach (DataColumn col in row.Table.Columns)
        {
            // Do not silently overwrite externally modified contract details.
            object original = row[col, DataRowVersion.Original];
            object current = reader[col.ColumnName];
            if (!Equals(original is DBNull ? null : original,
                        current is DBNull ? null : current))
                throw new DBConcurrencyException(
                    "Contract field " + col.ColumnName +
                    " changed outside PWR. Reload before saving.");
        }
        if (reader.Read())
            throw new DBConcurrencyException("The contract identifier is duplicated.");
    }

    private static void Insert(OleDbConnection connection,
        OleDbTransaction transaction, string table, DataRow row)
    {
        string[] fields = ChangedColumns(row, true);
        if (fields.Length == 0)
            throw new InvalidDataException("A new contract has no fields.");
        using var command = new OleDbCommand(
            "INSERT INTO " + Quote(table) + " (" +
            string.Join(", ", fields.Select(Quote)) + ") VALUES (" +
            string.Join(", ", fields.Select(_ => "?")) + ")",
            connection, transaction);
        foreach (string field in fields)
            Parameter(command, row.Table.Columns[field]!, row[field]);
        if (command.ExecuteNonQuery() != 1)
            throw new DBConcurrencyException("Contract insertion did not affect one row.");
    }

    private static void Update(OleDbConnection connection,
        OleDbTransaction transaction, string table, DataRow row)
    {
        string keyName = table == "tblContract" ? "UID" : "ContractUID";
        string[] fields = ChangedColumns(row, false);
        if (fields.Length == 0) return;
        if (fields.Contains(keyName, StringComparer.OrdinalIgnoreCase) ||
            (table == "tblContract" &&
             fields.Contains("WorkerUID", StringComparer.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "Contract identities cannot be reassigned in this editor.");
        using var command = new OleDbCommand(
            "UPDATE " + Quote(table) + " SET " +
            string.Join(", ", fields.Select(field => Quote(field) + " = ?")) +
            " WHERE " + Quote(keyName) + " = ?", connection, transaction);
        foreach (string field in fields)
            Parameter(command, row.Table.Columns[field]!, row[field]);
        Parameter(command, row.Table.Columns[keyName]!,
            row[keyName, DataRowVersion.Original]);
        if (command.ExecuteNonQuery() != 1)
            throw new DBConcurrencyException(
                "Contract update did not uniquely affect one record.");
    }

    private static void Delete(OleDbConnection connection,
        OleDbTransaction transaction, string table, DataRow row)
    {
        string keyName = table == "tblContract" ? "UID" : "ContractUID";
        using var command = new OleDbCommand(
            "DELETE FROM " + Quote(table) + " WHERE " + Quote(keyName) + " = ?",
            connection, transaction);
        Parameter(command, row.Table.Columns[keyName]!,
            row[keyName, DataRowVersion.Original]);
        if (command.ExecuteNonQuery() != 1)
            throw new DBConcurrencyException(
                "Contract removal did not uniquely affect one record.");
    }

    internal static bool Save(string databaseFile, PrwWorkerProfile profile,
        string? password)
    {
        if (string.IsNullOrWhiteSpace(databaseFile) || !File.Exists(databaseFile))
            throw new FileNotFoundException("Select a writable MDB before saving contracts.");
        if (File.GetAttributes(databaseFile).HasFlag(FileAttributes.ReadOnly))
            throw new IOException("This contract database is read-only.");

        DataTable contracts = profile.Sections.Single(section =>
            section.Table == "tblContract").Records;
        DataTable? bios = profile.Sections.FirstOrDefault(section =>
            section.Table == "tblContractBio")?.Records;
        AssertFields(contracts);
        var changedContracts = contracts.Rows.Cast<DataRow>().Where(row =>
            row.RowState is DataRowState.Added or DataRowState.Modified or
                DataRowState.Deleted).ToArray();
        var changedBios = bios?.Rows.Cast<DataRow>().Where(row =>
            row.RowState is DataRowState.Added or DataRowState.Modified or
                DataRowState.Deleted).ToArray() ?? [];
        if (changedContracts.Length == 0 && changedBios.Length == 0) return false;

        // Always validate local changes before opening the transaction.
        foreach (DataRow row in changedContracts.Where(row =>
                     row.RowState != DataRowState.Deleted))
        {
            if (row["WorkerUID"] is DBNull ||
                !Equals(Convert.ToInt64(row["WorkerUID"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(profile.Identity.Uid, CultureInfo.InvariantCulture)))
                throw new InvalidDataException("A contract must belong to this worker.");
            if (row.RowState == DataRowState.Added &&
                (row["FedUID"] is DBNull ||
                 Convert.ToInt64(row["FedUID"], CultureInfo.InvariantCulture) <= 0 ||
                 string.IsNullOrWhiteSpace(Convert.ToString(row["Name"]))))
                throw new InvalidDataException(
                    "Choose an employer and enter a contract name before saving.");
        }

        using var connection = AccessDatabase.Open(databaseFile, password);
        if (!PrwSchema.Inspect(connection, requireExpandedTables: false).IsValid)
            throw new InvalidDataException("This is not a compatible native PWR database.");
        using var transaction = connection.BeginTransaction();
        var draftKeys = changedContracts
            .Where(row => row.RowState == DataRowState.Added)
            .ToDictionary(row => row, row => row["UID"]);
        var bioDraftKeys = changedBios
            .Where(row => row.RowState == DataRowState.Added)
            .ToDictionary(row => row, row => row["ContractUID"]);
        try
        {
            foreach (DataRow row in changedContracts.Where(row =>
                         row.RowState is DataRowState.Modified or DataRowState.Deleted))
                CheckExisting(connection, transaction, row, "tblContract");
            foreach (DataRow row in changedBios.Where(row =>
                         row.RowState is DataRowState.Modified or DataRowState.Deleted))
                CheckExisting(connection, transaction, row, "tblContractBio");

            // Deleting the contract must remove its child biography first.
            var deletedIds = changedContracts
                .Where(row => row.RowState == DataRowState.Deleted)
                .Select(row => Convert.ToInt64(row["UID", DataRowVersion.Original],
                    CultureInfo.InvariantCulture)).ToHashSet();
            foreach (long deletedId in deletedIds)
            {
                using var bioDelete = new OleDbCommand(
                    "DELETE FROM [tblContractBio] WHERE [ContractUID] = ?",
                    connection, transaction);
                Parameter(bioDelete, bios?.Columns["ContractUID"] ??
                    contracts.Columns["UID"]!, Convert.ChangeType(
                        deletedId, (bios?.Columns["ContractUID"] ??
                        contracts.Columns["UID"]!).DataType, CultureInfo.InvariantCulture));
                bioDelete.ExecuteNonQuery();
            }
            foreach (DataRow row in changedBios.Where(row =>
                         row.RowState == DataRowState.Deleted &&
                         !deletedIds.Contains(Convert.ToInt64(
                             row["ContractUID", DataRowVersion.Original],
                             CultureInfo.InvariantCulture))))
                Delete(connection, transaction, "tblContractBio", row);
            foreach (DataRow row in changedContracts.Where(row =>
                         row.RowState == DataRowState.Deleted))
                Delete(connection, transaction, "tblContract", row);

            using var next = new OleDbCommand(
                "SELECT MAX([UID]) FROM [tblContract]", connection, transaction);
            object max = next.ExecuteScalar() ?? DBNull.Value;
            long nextUid = max is DBNull ? 1 : Convert.ToInt64(max) + 1;
            var keyMap = new Dictionary<long, long>();
            foreach (DataRow row in changedContracts.Where(row =>
                         row.RowState == DataRowState.Added))
            {
                long tempKey = Convert.ToInt64(row["UID"], CultureInfo.InvariantCulture);
                if (nextUid > int.MaxValue)
                    throw new OverflowException("No contract identifiers remain.");
                row["UID"] = Convert.ChangeType(nextUid,
                    contracts.Columns["UID"]!.DataType, CultureInfo.InvariantCulture);
                keyMap[tempKey] = nextUid++;
                Insert(connection, transaction, "tblContract", row);
            }
            foreach (DataRow row in changedContracts.Where(row =>
                         row.RowState == DataRowState.Modified))
                Update(connection, transaction, "tblContract", row);

            foreach (DataRow row in changedBios.Where(row =>
                         row.RowState != DataRowState.Deleted))
            {
                long contractId = Convert.ToInt64(row["ContractUID"],
                    CultureInfo.InvariantCulture);
                if (keyMap.TryGetValue(contractId, out long actual))
                    row["ContractUID"] = Convert.ChangeType(actual,
                        row.Table.Columns["ContractUID"]!.DataType,
                        CultureInfo.InvariantCulture);
                if (row.RowState == DataRowState.Added)
                    Insert(connection, transaction, "tblContractBio", row);
                else Update(connection, transaction, "tblContractBio", row);
            }
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            // An INSERT may fail after temporary IDs are translated to
            // actual MAX(UID)+1 values. Restore the staged keys so the
            // player can correct the error and retry without broken links.
            foreach (var (row, original) in draftKeys)
                if (row.RowState == DataRowState.Added) row["UID"] = original;
            foreach (var (row, original) in bioDraftKeys)
                if (row.RowState == DataRowState.Added)
                    row["ContractUID"] = original;
            throw;
        }

        contracts.AcceptChanges();
        bios?.AcceptChanges();
        return true;
    }
}
