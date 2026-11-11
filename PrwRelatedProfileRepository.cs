using System.Data;
using System.Data.OleDb;

namespace ProWrestlingRevolution;

/// <summary>
/// Builds company and match profiles using the relationships declared in
/// PRWDefault.mdb. No inferred joins, table-name guessing or record writes.
/// </summary>
internal static class PrwRelatedProfileRepository
{
    internal static IReadOnlyList<PrwProfileSection> Load(
        string file, string primaryTable, object recordId, string? password = null)
    {
        if (primaryTable is not ("tblFed" or "tblMatch"))
            throw new ArgumentException("Only company and match profiles are supported.",
                nameof(primaryTable));
        if (recordId is null or DBNull)
            throw new ArgumentException("The selected record has no UID.", nameof(recordId));

        using var connection = AccessDatabase.Open(file, password);
        var existing = connection.GetSchema("Tables").Rows.Cast<DataRow>()
            .Where(row => string.Equals(Convert.ToString(row["TABLE_TYPE"]), "TABLE",
                StringComparison.OrdinalIgnoreCase))
            .Select(row => Convert.ToString(row["TABLE_NAME"]) ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains(primaryTable))
            throw new InvalidDataException("The requested editor category is missing.");

        DataTable root = FindRelated(connection, primaryTable, "UID", recordId);
        if (root.Rows.Count != 1)
            throw new InvalidDataException(
                "The requested record is missing or has a duplicate UID.");

        var sections = new List<PrwProfileSection> { new(primaryTable, root) };
        var queue = new Queue<PrwProfileSection>(sections);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            primaryTable
        };

        while (queue.Count > 0)
        {
            PrwProfileSection parent = queue.Dequeue();
            foreach (var link in PrwSchema.Relationships.Where(link =>
                         link.ParentTable.Equals(parent.Table,
                             StringComparison.OrdinalIgnoreCase)))
            {
                if (!existing.Contains(link.ChildTable) ||
                    !visited.Add(link.ChildTable) ||
                    !parent.Records.Columns.Contains(link.ParentColumn))
                    continue;

                // Preserve the child table's exact original Access schema,
                // even when no linked records exist for this parent.
                var child = EmptyTable(connection, link.ChildTable);
                foreach (DataRow sourceRow in parent.Records.Rows)
                {
                    object key = sourceRow[link.ParentColumn];
                    if (key is DBNull) continue;
                    DataTable linked = FindRelated(
                        connection, link.ChildTable, link.ChildColumn, key);
                    foreach (DataRow linkedRow in linked.Rows)
                        child.ImportRow(linkedRow);
                }
                var section = new PrwProfileSection(link.ChildTable, child);
                sections.Add(section);
                queue.Enqueue(section);
            }
        }

        return sections;
    }

    private static DataTable EmptyTable(OleDbConnection connection, string table)
    {
        // The table identifiers come exclusively from trusted PrwSchema constants.
        using var adapter = new OleDbDataAdapter(
            "SELECT * FROM [" + table + "] WHERE 1 = 0", connection);
        var result = new DataTable(table);
        adapter.Fill(result);
        return result;
    }

    private static DataTable FindRelated(
        OleDbConnection connection, string table, string column, object key)
    {
        using var command = new OleDbCommand(
            "SELECT * FROM [" + table + "] WHERE [" + column + "] = ?",
            connection);
        command.Parameters.AddWithValue("related_id", key);
        using var adapter = new OleDbDataAdapter(command);
        var result = new DataTable(table);
        adapter.Fill(result);
        return result;
    }
}
