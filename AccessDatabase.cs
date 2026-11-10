using System.Data;
using System.Data.OleDb;
using System.Text;
using System.Diagnostics;

namespace ProWrestlingRevolution;

public static class AccessDatabase
{
    public static bool LooksLikeMdb(string file)
    {
        if (!File.Exists(file) || new FileInfo(file).Length < 64) return false;
        using var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bytes = new byte[64];
        int n = stream.Read(bytes);
        return Encoding.ASCII.GetString(bytes, 0, n).Contains("Standard Jet DB", StringComparison.Ordinal);
    }

    private static OleDbConnection Open(string file, string? password)
    {
        if (!LooksLikeMdb(file)) throw new InvalidDataException("Not a Microsoft Access MDB database.");
        Exception? last = null;
        foreach (var provider in new[] { "Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0" })
        {
            var builder = new OleDbConnectionStringBuilder
            {
                Provider = provider,
                DataSource = file
            };
            if (!string.IsNullOrEmpty(password)) builder["Jet OLEDB:Database Password"] = password;
            var connection = new OleDbConnection(builder.ConnectionString);
            try { connection.Open(); return connection; }
            catch (Exception ex) when (ex is OleDbException or InvalidOperationException)
            {
                last = ex;
                connection.Dispose();
            }
        }
        throw new InvalidOperationException(
            "Could not open the MDB. Check its password and ensure Microsoft Access Database Engine " +
            "matches this game's 64-bit architecture. Details: " + last?.Message, last);
    }

    public static IReadOnlyList<string> TableNames(string file, string? password)
    {
        using var connection = Open(file, password);
        DataTable? schema = connection.GetSchema("Tables");
        if (schema is null) return [];
        return schema.Rows.Cast<DataRow>()
            .Where(row => string.Equals(row["TABLE_TYPE"]?.ToString(), "TABLE", StringComparison.OrdinalIgnoreCase))
            .Select(row => row["TABLE_NAME"]?.ToString() ?? "")
            .Where(x => x.Length > 0 && !x.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static DataTable Preview(string file, string table, string? password)
    {
        using var connection = Open(file, password);
        var allowed = TableNames(file, password);
        if (!allowed.Contains(table, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("The requested table was not found.");
        var escaped = table.Replace("]", "]]", StringComparison.Ordinal);
        using var adapter = new OleDbDataAdapter("SELECT TOP 100 * FROM [" + escaped + "]", connection);
        var values = new DataTable();
        adapter.Fill(values);
        return values;
    }


    /// <summary>
    /// Loads up to maxRows actual MDB records for the editor directory, and counts
    /// the complete category separately so the UI never invents record totals.
    /// </summary>
    public static (DataTable Rows, long Total) Browse(
        string file, string table, string? password, int maxRows = 10000)
    {
        if (maxRows is < 1 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(maxRows));
        using var connection = Open(file, password);
        ValidateTable(connection, table);
        string quoted = Quote(table);
        using var count = new OleDbCommand("SELECT COUNT(*) FROM " + quoted, connection);
        long total = Convert.ToInt64(count.ExecuteScalar());
        using var adapter = new OleDbDataAdapter(
            "SELECT TOP " + maxRows + " * FROM " + quoted, connection);
        var rows = new DataTable(table);
        adapter.Fill(rows);
        return (rows, total);
    }

    /// <summary>
    /// Applies one text-field mass edit to selected records with a primary key.
    /// Rejects tables without exactly one primary-key column; no fuzzy matching,
    /// row-index updates, or guessed identifiers are permitted. The MDB is backed
    /// up before changing anything, and the updates execute in one transaction.
    /// </summary>
    public static string MassSetText(string file, string table, string field,
        IReadOnlyList<DataRow> chosen, string value, string? password)
    {
        if (chosen.Count == 0) throw new ArgumentException("Select one or more records.");
        if (File.GetAttributes(file).HasFlag(FileAttributes.ReadOnly))
            throw new IOException("This database is read-only.");
        string quotedTable = Quote(table);
        string quotedField = Quote(field);
        string keyField;

        using (var connection = Open(file, password))
        {
            ValidateTable(connection, table);
            using var adapter = new OleDbDataAdapter(
                "SELECT * FROM " + quotedTable + " WHERE 1=0", connection);
            var schema = new DataTable();
            adapter.FillSchema(schema, SchemaType.Source);
            DataColumn[] keys = schema.PrimaryKey;
            if (keys.Length != 1)
                throw new NotSupportedException(
                    "Mass Edit requires a table with one declared primary key. " +
                    "This category cannot be updated safely; use External Editing instead.");
            keyField = keys[0].ColumnName;
            if (field.Equals(keyField, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A primary key cannot be mass edited.");
            DataColumn? column = schema.Columns.Cast<DataColumn>()
                .FirstOrDefault(x => x.ColumnName.Equals(field, StringComparison.OrdinalIgnoreCase));
            if (column is null || column.DataType != typeof(string))
                throw new ArgumentException("Choose an existing text field.");
        }

        foreach (DataRow row in chosen)
            if (!row.Table.Columns.Contains(keyField) ||
                row[keyField] is null or DBNull)
                throw new InvalidOperationException(
                    "A selected record lacks a usable primary key. No changes were made.");

        string backups = Path.Combine(Path.GetDirectoryName(file)!, "Backups");
        Directory.CreateDirectory(backups);
        string backup = Path.Combine(backups,
            "editor-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" +
            Guid.NewGuid().ToString("N")[..8] + ".mdb");
        File.Copy(file, backup);

        using var writable = Open(file, password);
        using var transaction = writable.BeginTransaction();
        try
        {
            using var cmd = new OleDbCommand(
                "UPDATE " + quotedTable + " SET " + quotedField +
                " = ? WHERE " + Quote(keyField) + " = ?", writable, transaction);
            var valueParameter = cmd.Parameters.Add("updated_value", OleDbType.LongVarWChar);
            var keyParameter = cmd.Parameters.Add("record_key", OleDbType.Variant);
            valueParameter.Value = value;

            foreach (DataRow row in chosen)
            {
                keyParameter.Value = row[keyField];
                int changed = cmd.ExecuteNonQuery();
                if (changed != 1)
                    throw new DBConcurrencyException(
                        "A selected record could not be uniquely updated. All changes were rolled back.");
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

    private static string Quote(string identifier) =>
        "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";

    private static void ValidateTable(OleDbConnection connection, string table)
    {
        var schema = connection.GetSchema("Tables");
        bool found = schema.Rows.Cast<DataRow>().Any(row =>
            string.Equals(Convert.ToString(row["TABLE_TYPE"]), "TABLE",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Convert.ToString(row["TABLE_NAME"]), table,
                StringComparison.OrdinalIgnoreCase) &&
            !table.StartsWith("MSys", StringComparison.OrdinalIgnoreCase));
        if (!found) throw new ArgumentException("The requested table is not available.");
    }

    public static void OpenInMicrosoftAccess(string filename)
    {
        if (!File.Exists(filename)) throw new FileNotFoundException("The database is missing.", filename);
        Process.Start(new ProcessStartInfo(filename) { UseShellExecute = true });
    }
}