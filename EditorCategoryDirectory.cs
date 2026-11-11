namespace ProWrestlingRevolution;

/// <summary>
/// The native Access tables form a relational graph, not the editor's menu.
/// Show its parent entities alphabetically and expose linked detail tables
/// inside those entities. An imported, unrelated MDB remains fully browsable.
/// </summary>
internal static class EditorCategoryDirectory
{
    internal static IReadOnlyList<string> MainCategories(
        IEnumerable<string> availableTables, EditorMetadataCatalog metadata)
    {
        // Apply user choices for visibility, but do not treat individual
        // biography/skill/contract tables as separate main categories.
        var visible = metadata.OrderTables(availableTables);
        var names = visible.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var childTables = PrwSchema.Relationships
            .Where(link => names.Contains(link.ParentTable) &&
                           names.Contains(link.ChildTable))
            .Select(link => link.ChildTable)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return visible.Where(name => !childTables.Contains(name))
            .OrderBy(name => metadata.CategoryLabel(name),
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static void AssertMenuContract()
    {
        var metadata = EditorMetadataCatalog.Load("");
        string[] expected = ["tblFed", "tblMatch", "tblWorker"];
        string[] actual = MainCategories(PrwSchema.Tables.Select(t => t.Name),
            metadata).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "Main native editor categories must be Companies, Match Types, Workers.");

        var legacy = MainCategories(PrwSchema.Tables.Take(10).Select(t => t.Name),
            metadata);
        if (legacy.Count != 1 || legacy[0] != PrwSchema.WorkerTable)
            throw new InvalidDataException(
                "Older native databases must still group worker-linked records.");

        var isolated = MainCategories(["tblContract", "OtherTable"], metadata);
        if (!isolated.SequenceEqual(["OtherTable", "tblContract"],
                StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "Imported or partial MDB tables must remain browsable alphabetically.");
    }
}
