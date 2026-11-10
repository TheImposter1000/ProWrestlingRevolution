using System.Data;
using System.Data.OleDb;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ProWrestlingRevolution;

/// <summary>
/// Genuine Access MDB preferences store. Migrates the previous settings.ini
/// after successful database creation and keeps a recovery copy.
/// </summary>
internal sealed class GameSettings
{
    private readonly string _database;
    private readonly string _legacy;
    private int _mode; // 0 unchecked, 1 native MDB, 2 temporary INI fallback.

    internal GameSettings(string systemDirectory)
    {
        _database = Path.Combine(systemDirectory, "PWRSettings.mdb");
        _legacy = Path.Combine(systemDirectory, "settings.ini");
    }

    internal string DatabaseFile => _database;

    internal string Read(string key, string fallback)
    {
        ValidateKey(key);
        if (!UseDatabase())
            return Ini.Read(_legacy).TryGetValue(key, out var value) ? value : fallback;

        using var connection = AccessDatabase.Open(_database, null);
        using var command = new OleDbCommand(
            "SELECT [SettingValue] FROM [tblSettings] WHERE [SettingKey] = ?", connection);
        command.Parameters.Add("key", OleDbType.VarWChar, 64).Value = key;
        object? valueFromDatabase = command.ExecuteScalar();
        return valueFromDatabase is null or DBNull
            ? fallback : Convert.ToString(valueFromDatabase) ?? fallback;
    }

    internal void Write(string key, string value)
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(value);

        if (!UseDatabase())
        {
            // The database engine is not installed yet. Keep all preferences
            // in their existing file until native migration can complete.
            var previous = Ini.Read(_legacy);
            previous[key] = value;
            Ini.Write(_legacy, previous);
            return;
        }

        using var connection = AccessDatabase.Open(_database, null);
        using var transaction = connection.BeginTransaction();
        using (var check = new OleDbCommand(
            "SELECT COUNT(*) FROM [tblSettings] WHERE [SettingKey] = ?",
            connection, transaction))
        {
            check.Parameters.Add("key", OleDbType.VarWChar, 64).Value = key;
            bool exists = Convert.ToInt32(check.ExecuteScalar()) > 0;
            using var save = new OleDbCommand(exists
                ? "UPDATE [tblSettings] SET [SettingValue] = ? WHERE [SettingKey] = ?"
                : "INSERT INTO [tblSettings] ([SettingValue], [SettingKey]) VALUES (?, ?)",
                connection, transaction);
            save.Parameters.Add("value", OleDbType.LongVarWChar).Value = value;
            save.Parameters.Add("key", OleDbType.VarWChar, 64).Value = key;
            if (save.ExecuteNonQuery() != 1)
                throw new DBConcurrencyException("The MDB setting could not be saved.");
        }
        transaction.Commit();
    }

    private bool UseDatabase()
    {
        if (_mode != 0) return _mode == 1;
        Directory.CreateDirectory(Path.GetDirectoryName(_database)!);

        if (File.Exists(_database))
        {
            // Never replace an existing MDB, even when it is unopenable.
            if (!AccessDatabase.LooksLikeMdb(_database))
                throw new InvalidDataException("System/PWRSettings.mdb is not a valid Access MDB.");
            using var existing = AccessDatabase.Open(_database, null);
            EnsureTable(existing);
            if (IsEmpty(existing))
            {
                ImportPreviousValues(existing);
                ArchivePreviousIni();
            }
            _mode = 1;
            return true;
        }

        // No new dependency for CI smoke tests or game installations without
        // ACE: the legacy preference file remains active until ACE is present.
        try
        {
            CreateNewDatabase();
            _mode = 1;
            return true;
        }
        catch (InvalidOperationException)
        {
            _mode = 2;
            return false;
        }
        catch (COMException)
        {
            _mode = 2;
            return false;
        }
    }

    private void CreateNewDatabase()
    {
        string temporary = Path.Combine(Path.GetDirectoryName(_database)!,
            ".settings-" + Guid.NewGuid().ToString("N") + ".mdb");
        try
        {
            CreateBlankAccessDatabase(temporary);
            using (var connection = AccessDatabase.Open(temporary, null))
            {
                EnsureTable(connection);
                ImportPreviousValues(connection);
            }
            if (!File.Exists(_database))
                File.Move(temporary, _database);
            ArchivePreviousIni();
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private void ImportPreviousValues(OleDbConnection connection)
    {
        var values = Ini.Read(_legacy);
        if (!values.ContainsKey("Skin")) values["Skin"] = "Obsidian";
        if (!values.ContainsKey("Database")) values["Database"] = "";
        foreach (var entry in values)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length > 64)
                continue;
            using var command = new OleDbCommand(
                "INSERT INTO [tblSettings] ([SettingKey], [SettingValue]) VALUES (?, ?)",
                connection);
            command.Parameters.Add("key", OleDbType.VarWChar, 64).Value = entry.Key;
            command.Parameters.Add("value", OleDbType.LongVarWChar).Value = entry.Value;
            command.ExecuteNonQuery();
        }
    }

    private static bool IsEmpty(OleDbConnection connection)
    {
        using var command = new OleDbCommand(
            "SELECT COUNT(*) FROM [tblSettings]", connection);
        return Convert.ToInt32(command.ExecuteScalar()) == 0;
    }

    private static void EnsureTable(OleDbConnection connection)
    {
        var schema = connection.GetSchema("Tables");
        bool exists = schema.Rows.Cast<DataRow>().Any(row =>
            string.Equals(Convert.ToString(row["TABLE_NAME"]), "tblSettings",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Convert.ToString(row["TABLE_TYPE"]), "TABLE",
                StringComparison.OrdinalIgnoreCase));
        if (exists) return;

        using var command = new OleDbCommand(
            "CREATE TABLE [tblSettings] (" +
            "[SettingKey] TEXT(64) NOT NULL, " +
            "[SettingValue] MEMO, " +
            "CONSTRAINT [PK_tblSettings] PRIMARY KEY ([SettingKey]))",
            connection);
        command.ExecuteNonQuery();
    }

    private static void CreateBlankAccessDatabase(string path)
    {
        Type? type = Type.GetTypeFromProgID("ADOX.Catalog");
        if (type is null)
            throw new InvalidOperationException(
                "ADOX is not installed. The 64-bit Microsoft Access Database Engine " +
                "is needed to create the settings MDB.");
        Exception? last = null;
        foreach (var provider in new[]
            { "Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0" })
        {
            object? catalog = null;
            try
            {
                catalog = Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException("ADOX could not be created.");
                var builder = new OleDbConnectionStringBuilder
                {
                    Provider = provider,
                    DataSource = path
                };
                builder["Jet OLEDB:Engine Type"] = 5;
                type.InvokeMember("Create", BindingFlags.InvokeMethod,
                    null, catalog, [builder.ConnectionString]);

                // ADOX Create opens an ADO connection. Close the file lock.
                object? active = type.InvokeMember("ActiveConnection",
                    BindingFlags.GetProperty, null, catalog, null);
                if (active is not null && Marshal.IsComObject(active))
                {
                    try
                    {
                        active.GetType().InvokeMember("Close", BindingFlags.InvokeMethod,
                            null, active, null);
                    }
                    finally { Marshal.FinalReleaseComObject(active); }
                }
                return;
            }
            catch (Exception ex) when (ex is COMException or TargetInvocationException)
            {
                last = ex;
                if (File.Exists(path)) File.Delete(path);
            }
            finally
            {
                if (catalog is not null && Marshal.IsComObject(catalog))
                    Marshal.FinalReleaseComObject(catalog);
            }
        }
        throw new InvalidOperationException(
            "No compatible 64-bit Access MDB provider was found. " + last?.Message,
            last);
    }

    private void ArchivePreviousIni()
    {
        if (!File.Exists(_legacy)) return;
        try { File.Move(_legacy, _legacy + ".migrated"); }
        catch (IOException) { /* Keep the original backup unchanged. */ }
        catch (UnauthorizedAccessException) { /* The new MDB is already saved. */ }
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 64)
            throw new ArgumentException("Setting names must be 1 to 64 characters.", nameof(key));
    }
}
