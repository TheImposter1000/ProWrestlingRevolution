using System.Data;
using System.Data.OleDb;

namespace ProWrestlingRevolution;

/// <summary>
/// Persists edits to existing native PRW worker records, preserving MDB types,
/// composite keys and referential links. It never edits the source universe
/// when given a career path, and creates a recovery copy before any writes.
/// </summary>
internal static class PrwWorkerEditorService
{
    internal static string Save(string file, PrwWorkerProfile profile, string? password)
    {
        if (File.GetAttributes(file).HasFlag(FileAttributes.ReadOnly))
            throw new IOException("This worker database is read-only.");

        var changes = profile.Sections
            .SelectMany(section => section.Records.Rows.Cast<DataRow>()
                .Where(row => row.RowState is DataRowState.Modified or DataRowState.Added)
                .Select(row => (section.Table, Row: row)))
            .ToArray();
        if (changes.Length == 0) return "";

        var identity = profile.Sections.Single(s => s.Table == "tblWorker").Records.Rows[0];
        if (string.IsNullOrWhiteSpace(Convert.ToString(identity["Name"])) ||
            string.IsNullOrWhiteSpace(Convert.ToString(identity["ShortName"])))
            throw new InvalidDataException("Worker name and short name cannot be empty.");

        using (var read = AccessDatabase.Open(file, password))
        {
            var inspection = PrwSchema.Inspect(read, requireExpandedTables: false);
            if (!inspection.IsValid)
                throw new InvalidDataException("The native PRW worker schema is invalid.");
            foreach (var change in changes)
                ValidateChange(read, change.Table, change.Row);
        }

        var backupFolder = Path.Combine(Path.GetDirectoryName(file)!, "Backups");
        Directory.CreateDirectory(backupFolder);
        var backup = Path.Combine(backupFolder, "worker-" +
            DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" +
            Guid.NewGuid().ToString("N")[..8] + ".mdb");
        File.Copy(file, backup);

        using var connection = AccessDatabase.Open(file, password);
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var change in changes)
                Apply(connection, transaction, change.Table, change.Row);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        foreach (var change in changes) change.Row.AcceptChanges();
        return backup;
    }

    private static PrwSchema.Table GetTable(string table) =>
        PrwSchema.Tables.FirstOrDefault(t =>
            string.Equals(t.Name, table, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidDataException("The worker section is not a native PRW table.");

    private static string Quote(string identifier) =>
        "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";

    private static bool IsProtectedColumn(PrwSchema.Table table, string field) =>
        table.KeyColumns.Contains(field, StringComparer.OrdinalIgnoreCase) ||
        field.EndsWith("UID", StringComparison.OrdinalIgnoreCase);

    private static string[] EditedFields(DataRow row)
    {
        if (row.RowState == DataRowState.Added)
            return row.Table.Columns.Cast<DataColumn>()
                .Where(c => row[c] is not DBNull).Select(c => c.ColumnName).ToArray();
        return row.Table.Columns.Cast<DataColumn>()
            .Where(c => !Equals(row[c, DataRowVersion.Original], row[c]))
            .Select(c => c.ColumnName).ToArray();
    }

    private static void AddValue(OleDbCommand cmd, object? value,
        DataColumn? column = null)
    {
        var dataType = column?.DataType ??
            (value is not null and not DBNull ? value.GetType() : typeof(string));
        var type = dataType == typeof(string)
            ? value is string text && text.Length > 255
                ? OleDbType.LongVarWChar : OleDbType.VarWChar
            : dataType == typeof(bool) ? OleDbType.Boolean
            : dataType == typeof(DateTime) ? OleDbType.DBTimeStamp
            : dataType == typeof(int) ? OleDbType.Integer
            : dataType == typeof(short) ? OleDbType.SmallInt
            : dataType == typeof(long) ? OleDbType.BigInt
            : dataType == typeof(byte) ? OleDbType.UnsignedTinyInt
            : dataType == typeof(float) ? OleDbType.Single
            : dataType == typeof(double) ? OleDbType.Double
            : dataType == typeof(decimal) ? OleDbType.Decimal
            : OleDbType.Variant;
        var parameter = cmd.Parameters.Add("p" + cmd.Parameters.Count, type);
        parameter.Value = value ?? DBNull.Value;
    }

    private static void ValidateChange(OleDbConnection connection, string tableName, DataRow row)
    {
        var table = GetTable(tableName);
        var fields = EditedFields(row);

        if (row.RowState == DataRowState.Added && tableName != "tblWorkerBio")
            throw new NotSupportedException("Only missing worker biographies can be created here.");

        if (row.RowState == DataRowState.Modified &&
            fields.Any(field => IsProtectedColumn(table, field)))
            throw new InvalidOperationException(
                "Worker record identities and linked UID references cannot be changed.");

        if (fields.Any(field => !table.Columns.Contains(field, StringComparer.OrdinalIgnoreCase)))
            throw new InvalidDataException("A field is not declared in the native PRW schema.");

        foreach (string field in fields)
        {
            var col = row.Table.Columns[field]!;
            if (row[field] is string s && col.MaxLength > 0 && s.Length > col.MaxLength)
                throw new InvalidDataException(field + " exceeds its maximum field length.");
        }

        if (row.RowState == DataRowState.Added)
        {
            if (row["WorkerUID"] is null or DBNull)
                throw new InvalidDataException("New biographies require a worker UID.");
            using var check = new OleDbCommand(
                "SELECT COUNT(*) FROM [tblWorkerBio] WHERE [WorkerUID] = ?", connection);
            AddValue(check, row["WorkerUID"], row.Table.Columns["WorkerUID"]);
            if (Convert.ToInt32(check.ExecuteScalar()) != 0)
                throw new DBConcurrencyException("This worker already has a biography.");
            return;
        }

        string clause = string.Join(" AND ",
            table.KeyColumns.Select(column => Quote(column) + " = ?"));
        using var current = new OleDbCommand(
            "SELECT * FROM " + Quote(table.Name) + " WHERE " + clause, connection);
        foreach (var key in table.KeyColumns) AddValue(current, row[key, DataRowVersion.Original], row.Table.Columns[key]);
        using var reader = current.ExecuteReader()
            ?? throw new IOException("Cannot verify the existing worker record.");
        if (!reader.Read())
            throw new DBConcurrencyException("The worker record was deleted externally.");

        // Prevent overwriting an external edit made after the profile was loaded.
        foreach (var field in fields)
        {
            object database = reader[field];
            object original = row[field, DataRowVersion.Original];
            if (!Equals(database is DBNull ? null : database,
                        original is DBNull ? null : original))
                throw new DBConcurrencyException(
                    "The field " + field + " changed outside PWR. Reload before saving.");
        }
        if (reader.Read())
            throw new DBConcurrencyException("Multiple records share a protected primary key.");
    }

    private static void Apply(OleDbConnection connection, OleDbTransaction transaction,
        string tableName, DataRow row)
    {
        var table = GetTable(tableName);
        if (row.RowState == DataRowState.Added)
        {
            // The parent UID is immutable, but required when creating a 1:1 bio.
            string[] columns = EditedFields(row);
            using var insert = new OleDbCommand(
                "INSERT INTO " + Quote(table.Name) +
                " (" + string.Join(", ", columns.Select(Quote)) + ") VALUES (" +
                string.Join(", ", columns.Select(_ => "?")) + ")", connection, transaction);
            foreach (string column in columns)
                AddValue(insert, row[column], row.Table.Columns[column]);
            if (insert.ExecuteNonQuery() != 1)
                throw new DBConcurrencyException("The biography could not be inserted.");
            return;
        }

        string[] edited = EditedFields(row);
        if (edited.Length == 0) return;
        using var update = new OleDbCommand(
            "UPDATE " + Quote(table.Name) + " SET " +
            string.Join(", ", edited.Select(column => Quote(column) + " = ?")) +
            " WHERE " + string.Join(" AND ", table.KeyColumns.Select(key => Quote(key) + " = ?")),
            connection, transaction);
        foreach (string column in edited)
            AddValue(update, row[column], row.Table.Columns[column]);
        foreach (string key in table.KeyColumns)
            AddValue(update, row[key, DataRowVersion.Original], row.Table.Columns[key]);
        if (update.ExecuteNonQuery() != 1)
            throw new DBConcurrencyException("A worker record could not be updated uniquely.");
    }
}
