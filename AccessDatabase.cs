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

    public static void OpenInMicrosoftAccess(string filename)
    {
        if (!File.Exists(filename)) throw new FileNotFoundException("The database is missing.", filename);
        Process.Start(new ProcessStartInfo(filename) { UseShellExecute = true });
    }
}