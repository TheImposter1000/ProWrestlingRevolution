using System.Data.OleDb;
using System.Diagnostics;

namespace ProWrestlingRevolution;

/// <summary>
/// Display-only editor labels and order, kept separate from actual universe
/// MDB tables. Unknown tables and fields are never hidden or renamed in SQL.
/// </summary>
internal sealed class EditorMetadataCatalog
{
    private sealed record Category(string Label, int SortOrder, bool Visible);

    private readonly Dictionary<string, Category> _categories =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _fields =
        new(StringComparer.OrdinalIgnoreCase);

    internal static EditorMetadataCatalog Load(string file)
    {
        var catalog = new EditorMetadataCatalog();
        if (!File.Exists(file)) return catalog;
        try
        {
            using var connection = AccessDatabase.Open(file, null);
            using (var categories = new OleDbCommand(
                "SELECT TableName, DisplayName, DisplayOrder, IsVisible " +
                "FROM tblEditorCategory", connection).ExecuteReader())
            {
                if (categories is not null)
                    while (categories.Read())
                    {
                        string name = Convert.ToString(categories["TableName"]) ?? "";
                        string label = Convert.ToString(categories["DisplayName"]) ?? "";
                        if (name.Length > 0 && label.Length > 0)
                            catalog._categories[name] = new Category(
                                label, Convert.ToInt32(categories["DisplayOrder"]),
                                Convert.ToBoolean(categories["IsVisible"]));
                    }
            }
            using (var fields = new OleDbCommand(
                "SELECT TableName, FieldName, DisplayLabel " +
                "FROM tblEditorField", connection).ExecuteReader())
            {
                if (fields is not null)
                    while (fields.Read())
                    {
                        string table = Convert.ToString(fields["TableName"]) ?? "";
                        string field = Convert.ToString(fields["FieldName"]) ?? "";
                        string label = Convert.ToString(fields["DisplayLabel"]) ?? "";
                        if (table.Length > 0 && field.Length > 0 && label.Length > 0)
                            catalog._fields[table + "." + field] = label;
                    }
            }
        }
        catch (Exception error) when (error is OleDbException or InvalidOperationException
            or InvalidDataException or ArgumentException)
        {
            Trace.TraceWarning("Could not load editor MDB: " + error.Message);
            // Fall back to native names, never prevent universe browsing.
            catalog._categories.Clear();
            catalog._fields.Clear();
        }
        return catalog;
    }

    internal IReadOnlyList<string> OrderTables(IEnumerable<string> tableNames)
    {
        return tableNames.Where(name =>
            !_categories.TryGetValue(name, out var info) || info.Visible)
            .OrderBy(name => _categories.TryGetValue(name, out var info)
                ? info.SortOrder : int.MaxValue)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal string CategoryLabel(string name)
    {
        if (_categories.TryGetValue(name, out var category))
            return category.Label;

        // The three primary entity labels remain readable when a source
        // checkout has not generated the optional PRWEditor.mdb yet.
        if (name.Equals("tblFed", StringComparison.OrdinalIgnoreCase))
            return "Companies";
        if (name.Equals("tblMatch", StringComparison.OrdinalIgnoreCase))
            return "Match Types";
        if (name.Equals("tblWorker", StringComparison.OrdinalIgnoreCase))
            return "Workers";
        return name;
    }

    internal string FieldLabel(string table, string field) =>
        _fields.TryGetValue(table + "." + field, out var label) ? label : field;
}
