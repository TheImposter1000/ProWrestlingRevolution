using System.Data;
using System.Data.OleDb;
using System.Globalization;

namespace ProWrestlingRevolution;

/// <summary>
/// One existing MDB row displayed in the Workers interface.
/// A stable, unique identifier is mandatory for editing; no row-number writes.
/// </summary>
internal sealed class WorkerRecordSection
{
    internal string Caption { get; }
    internal string TableName { get; }
    internal DataTable Data { get; }
    internal DataRow Row => Data.Rows[0];
    internal string? KeyColumn { get; }
    internal object? KeyValue { get; }
    internal bool CanEdit => KeyColumn is not null && KeyValue is not null;

    internal WorkerRecordSection(string caption, string table, DataTable data,
        string? keyColumn, object? keyValue)
    {
        Caption = caption;
        TableName = table;
        Data = data;
        KeyColumn = keyColumn;
        KeyValue = keyValue;
    }

    internal bool CanEditColumn(DataColumn column)
    {
        if (!CanEdit || column.AutoIncrement || column.ReadOnly ||
            column.ColumnName.Equals(KeyColumn, StringComparison.OrdinalIgnoreCase))
            return false;
        // Keys and relationship identifiers are displayed but never edited
        // by a generic worker form. They need category-specific selectors.
        if (column.ColumnName.EndsWith("UID", StringComparison.OrdinalIgnoreCase) ||
            column.ColumnName.EndsWith("ID", StringComparison.OrdinalIgnoreCase))
            return false;
        Type type = column.DataType;
        return type == typeof(string) || type == typeof(bool) ||
            type == typeof(DateTime) || type == typeof(byte) ||
            type == typeof(short) || type == typeof(int) || type == typeof(long) ||
            type == typeof(float) || type == typeof(double) ||
            type == typeof(decimal);
    }
}

public static partial class AccessDatabase
{
    // Only known worker-related tables are grouped into the worker editor.
    // Rows and columns are inspected at runtime; no schema data is invented.
    private static readonly (string Table, string Caption)[] WorkerTables =
    [
        ("tblWorkerBio", "Biography"),
        ("tblWorkerSkill", "Skills"),
        ("tblWorkerPhysical", "Physical"),
        ("tblWorkerOver", "Popularity"),
        ("tblWorkerBusiness", "Business")
    ];

    internal static bool IsWorkerCategory(string? name) =>
        name is not null &&
        (name.Equals("Workers", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("Worker", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("tblWorker", StringComparison.OrdinalIgnoreCase) ||
         name.Equals("tblWorkers", StringComparison.OrdinalIgnoreCase));

    internal static IReadOnlyList<WorkerRecordSection> ReadWorker(
        string file, string mainTable, DataRow selected, string? password)
    {
        if (!IsWorkerCategory(mainTable))
            throw new ArgumentException("Choose the Workers category first.");

        using var connection = Open(file, password);
        ValidateTable(connection, mainTable);
        var available = connection.GetSchema("Tables").Rows.Cast<DataRow>()
            .Where(row => string.Equals(Convert.ToString(row["TABLE_TYPE"]),
                    "TABLE", StringComparison.OrdinalIgnoreCase))
            .Select(row => Convert.ToString(row["TABLE_NAME"]) ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new List<WorkerRecordSection>();
        WorkerRecordSection main = ReadSelectedSection(connection, mainTable,
            "Overview", selected);
        result.Add(main);

        // TEW-style source MDBs split their worker data into related tables
        // keyed by WorkerUID. Never assume they exist in all PWR universes.
        if (!selected.Table.Columns.Contains("WorkerUID") ||
            selected["WorkerUID"] is DBNull) return result;

        object workerUid = selected["WorkerUID"];
        foreach (var (table, caption) in WorkerTables)
        {
            if (!available.Contains(table)) continue;
            var section = ReadRelatedWorkerSection(connection, table, caption, workerUid);
            if (section is not null) result.Add(section);
        }
        return result;
    }

    private static WorkerRecordSection ReadSelectedSection(OleDbConnection connection,
        string table, string caption, DataRow selected)
    {
        var schema = ReadEmptySchema(connection, table);
        string? key = schema.PrimaryKey.Length == 1
            ? schema.PrimaryKey[0].ColumnName
            : schema.Columns.Contains("WorkerUID") ? "WorkerUID" : null;
        if (key is null || !selected.Table.Columns.Contains(key) ||
            selected[key] is DBNull)
            return CopyReadOnly(caption, table, selected);

        object value = selected[key];
        DataTable matches = SelectByKey(connection, table, key, value);
        if (matches.Rows.Count != 1)
            return CopyReadOnly(caption, table, selected);

        return new WorkerRecordSection(caption, table, matches, key, value);
    }

    private static WorkerRecordSection? ReadRelatedWorkerSection(
        OleDbConnection connection, string table, string caption, object workerUid)
    {
        var schema = ReadEmptySchema(connection, table);
        if (!schema.Columns.Contains("WorkerUID")) return null;

        DataTable matches = SelectByKey(connection, table, "WorkerUID", workerUid);
        if (matches.Rows.Count == 0) return null;

        // A one-to-many table must never be edited as though it were one row.
        // Display no tab when more than one record matches this worker UID.
        if (matches.Rows.Count != 1) return null;
        return new WorkerRecordSection(caption, table, matches, "WorkerUID", workerUid);
    }

    private static WorkerRecordSection CopyReadOnly(
        string caption, string table, DataRow source)
    {
        var data = source.Table.Clone();
        data.ImportRow(source);
        return new WorkerRecordSection(caption, table, data, null, null);
    }

    private static DataTable ReadEmptySchema(OleDbConnection connection, string table)
    {
        using var adapter = new OleDbDataAdapter(
            "SELECT * FROM " + Quote(table) + " WHERE 1=0", connection);
        var schema = new DataTable();
        adapter.FillSchema(schema, SchemaType.Source);
        return schema;
    }

    private static DataTable SelectByKey(OleDbConnection connection,
        string table, string key, object value, OleDbTransaction? transaction = null)
    {
        using var command = new OleDbCommand(
            "SELECT TOP 2 * FROM " + Quote(table) +
            " WHERE " + Quote(key) + " = ?", connection, transaction);
        command.Parameters.AddWithValue("@recordKey", value);
        using var adapter = new OleDbDataAdapter(command);
        var result = new DataTable(table);
        adapter.Fill(result);
        return result;
    }

    /// <summary>
    /// Saves only edited, supported values to exactly identified MDB records.
    /// Rechecks the original field values in the database to detect stale data,
    /// creates a backup before writing, and applies all tabs in one transaction.
    /// </summary>
    internal static string SaveWorker(
        string file,
        IReadOnlyDictionary<WorkerRecordSection, IReadOnlyDictionary<string, object?>> updates,
        string? password)
    {
        if (updates.Count == 0) throw new ArgumentException("No fields have changed.");
        if (File.GetAttributes(file).HasFlag(FileAttributes.ReadOnly))
            throw new IOException("The selected MDB is read-only.");

        // Validate all fields and uniqueness before creating the backup.
        using (var validation = Open(file, password))
            foreach (var (section, fields) in updates)
            {
                ValidateTable(validation, section.TableName);
                if (!section.CanEdit || fields.Count == 0)
                    throw new InvalidOperationException("The selected worker section cannot be edited.");
                var metadata = ReadEmptySchema(validation, section.TableName);
                if (!metadata.Columns.Contains(section.KeyColumn!))
                    throw new InvalidOperationException("The worker identifier is missing.");
                foreach (var (field, value) in fields)
                {
                    if (!metadata.Columns.Contains(field) ||
                        !section.CanEditColumn(metadata.Columns[field]!))
                        throw new InvalidOperationException("Field cannot be safely changed: " + field);
                    if (value is null && !metadata.Columns[field]!.AllowDBNull)
                        throw new InvalidOperationException(field + " cannot be empty.");
                }
                if (SelectByKey(validation, section.TableName,
                    section.KeyColumn!, section.KeyValue!).Rows.Count != 1)
                    throw new DBConcurrencyException("A worker row is no longer uniquely identifiable.");
            }

        string backupDirectory = Path.Combine(Path.GetDirectoryName(file)!, "Backups");
        Directory.CreateDirectory(backupDirectory);
        string backup = Path.Combine(backupDirectory,
            "worker-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" +
            Guid.NewGuid().ToString("N")[..8] + ".mdb");
        File.Copy(file, backup);

        using var connection = Open(file, password);
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var (section, fields) in updates)
            {
                DataTable currentRows = SelectByKey(connection, section.TableName,
                    section.KeyColumn!, section.KeyValue!, transaction);
                if (currentRows.Rows.Count != 1)
                    throw new DBConcurrencyException("The worker record changed before saving.");

                DataRow live = currentRows.Rows[0];
                foreach (string field in fields.Keys)
                {
                    object previous = section.Row[field];
                    object now = live[field];
                    if (!object.Equals(previous, now))
                        throw new DBConcurrencyException(
                            "The field '" + field + "' was changed elsewhere. " +
                            "No changes were saved. Reopen the worker record.");
                }

                string assignments = string.Join(", ",
                    fields.Keys.Select(key => Quote(key) + " = ?"));
                using var command = new OleDbCommand(
                    "UPDATE " + Quote(section.TableName) + " SET " +
                    assignments + " WHERE " + Quote(section.KeyColumn!) + " = ?",
                    connection, transaction);
                foreach (var value in fields.Values)
                    command.Parameters.AddWithValue("@value", value ?? DBNull.Value);
                command.Parameters.AddWithValue("@key", section.KeyValue!);

                if (command.ExecuteNonQuery() != 1)
                    throw new DBConcurrencyException(
                        "The worker update did not affect exactly one row.");
            }
            transaction.Commit();
            return backup;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Convert a modified input to its actual MDB column type before any save.
    /// Unchanged text is never parsed, preserving existing values exactly.
    /// </summary>
    internal static object? ParseWorkerValue(DataColumn column, string raw)
    {
        Type target = column.DataType;
        if (target == typeof(string))
        {
            if (column.MaxLength >= 0 && raw.Length > column.MaxLength)
                throw new FormatException(column.ColumnName + " exceeds its text length limit.");
            return raw;
        }
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (column.AllowDBNull) return null;
            throw new FormatException(column.ColumnName + " cannot be empty.");
        }
        try
        {
            if (target == typeof(DateTime))
                return DateTime.Parse(raw, CultureInfo.InvariantCulture);
            if (target == typeof(bool))
                return bool.Parse(raw);
            return Convert.ChangeType(raw, target, CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is FormatException or OverflowException or InvalidCastException)
        {
            throw new FormatException(
                "Invalid value for " + column.ColumnName + " (" + target.Name + ").", e);
        }
    }
}
