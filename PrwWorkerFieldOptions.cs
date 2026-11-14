using System.Data;
using System.Data.OleDb;
using System.Globalization;

namespace ProWrestlingRevolution;

/// <summary>
/// Provides worker field choices from the actual native PWR MDB columns.
/// The installed PRWDefault.mdb supplies base choices; the selected universe
/// contributes its own values. No TEW-specific enum codes are invented.
/// </summary>
internal static class PrwWorkerFieldOptions
{
    internal sealed record Choice(string Label, object Value);

    internal static IReadOnlyList<Choice> Get(
        AppPaths paths, string selectedDatabase, string? password,
        DataRow record, string field)
    {
        if (!record.Table.Columns.Contains(field))
            return [];
        var table = record.Table.TableName;
        if (!PrwSchema.Tables.Any(t =>
            t.Name.Equals(table, StringComparison.OrdinalIgnoreCase) &&
            t.Columns.Contains(field, StringComparer.OrdinalIgnoreCase)))
            return [];
        var targetType = record.Table.Columns[field]!.DataType;
        // Loyalty refers to the company a worker is loyal to. The original
        // PRW MDB unfortunately calls the physical column LoyaltyWorkerUID.
        // Preserve that name for compatibility, but resolve it against tblFed.
        if (field.Equals("LoyaltyWorkerUID", StringComparison.OrdinalIgnoreCase))
            return CompanyChoices(selectedDatabase, password,
                record.Table.Columns[field]!);

        if (WorkerFieldRules.IsLanguage(field) || field == "CompetesVs")
        {
            var labels = WorkerFieldRules.IsLanguage(field)
                ? WorkerFieldRules.LanguageLevels : WorkerFieldRules.Divisions;
            return labels.Select((label, index) =>
                new Choice(label, WorkerFieldRules.ChoiceValue(
                    record.Table.Columns[field]!, field, label, index))).ToArray();
        }

        if (WorkerFieldRules.IsManual(field, record.Table.Columns[field]))
            return [];

        var results = new Dictionary<string, Choice>(StringComparer.OrdinalIgnoreCase);
        void Add(object raw)
        {
            if (raw is DBNull || raw is null) return;
            string label = raw is bool boolean ? (boolean ? "Yes" : "No")
                : raw is DateTime dt ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
            if (string.IsNullOrWhiteSpace(label)) return;
            try
            {
                object typed = targetType == typeof(bool) &&
                    raw is string str ? str.Equals("Yes", StringComparison.OrdinalIgnoreCase)
                    : targetType.IsInstanceOfType(raw) ? raw
                    : Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
                results.TryAdd(label, new Choice(label, typed));
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException
                                       or OverflowException)
            {
                // Ignore incompatible values from an older imported MDB.
            }
        }

        if (targetType == typeof(bool))
        {
            Add(false);
            Add(true);
        }

        // Template is authoritative for known choices; the active database
        // allows custom universes to retain their own established values.
        if (File.Exists(paths.DefaultDatabase))
            ReadDistinct(paths.DefaultDatabase, null, table, field, Add);
        if (File.Exists(selectedDatabase) &&
            !Path.GetFullPath(paths.DefaultDatabase).Equals(
                Path.GetFullPath(selectedDatabase), StringComparison.OrdinalIgnoreCase))
            ReadDistinct(selectedDatabase, password, table, field, Add);

        if (record[field] is not DBNull)
            Add(record[field]);

        // If the MDB itself contains a Yes/No text convention, expose both
        // states rather than only the currently populated one.
        if (targetType == typeof(string) &&
            results.Count > 0 &&
            results.Keys.All(k => k.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                  k.Equals("No", StringComparison.OrdinalIgnoreCase)))
        {
            Add("No");
            Add("Yes");
        }

        return results.Values.OrderBy(c => c.Label,
            StringComparer.CurrentCultureIgnoreCase).Take(200).ToArray();
    }

    private static void ReadDistinct(string database, string? password,
        string table, string field, Action<object> add)
    {
        try
        {
            using var connection = AccessDatabase.Open(database, password);
            using var command = new OleDbCommand(
                "SELECT DISTINCT TOP 150 [" + field + "] FROM [" + table +
                "] WHERE [" + field + "] IS NOT NULL", connection);
            using var reader = command.ExecuteReader();
            while (reader?.Read() == true)
                add(reader.GetValue(0));
        }
        catch (Exception ex) when (ex is OleDbException or InvalidDataException
                                   or InvalidOperationException or IOException)
        {
            // The loaded worker's current values remain usable even when
            // an optional template cannot be read on this machine.
        }
    }

    private static IReadOnlyList<Choice> CompanyChoices(
        string database, string? password, DataColumn column)
    {
        Type targetType = column.DataType;
        var choices = new List<Choice>
        {
            new Choice("(None)", column.AllowDBNull
                ? DBNull.Value : Convert.ChangeType(0, targetType, CultureInfo.InvariantCulture))
        };
        try
        {
            using var connection = AccessDatabase.Open(database, password);
            using var command = new OleDbCommand(
                "SELECT [UID], [Name] FROM [tblFed] ORDER BY [Name]", connection);
            using var reader = command.ExecuteReader();
            while (reader?.Read() == true && choices.Count < 500)
            {
                var value = Convert.ChangeType(reader.GetValue(0), targetType,
                    CultureInfo.InvariantCulture);
                var label = Convert.ToString(reader.GetValue(1)) ?? "";
                choices.Add(new Choice(label, value));
            }
        }
        catch (Exception ex) when (ex is OleDbException or InvalidDataException
                                   or InvalidOperationException or IOException)
        {
        }
        return choices;
    }
}
