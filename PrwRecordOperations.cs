using System.Data;
using System.Data.OleDb;
using System.Globalization;

namespace ProWrestlingRevolution;

/// <summary>
/// Safe native PRW primary-record operations for Workers, Companies and Match Types.
/// Copy and import preserve declared dependent records, but intentionally omit
/// contracts because employment links must be negotiated, not cloned.
/// Delete follows the declared relationship graph inside one transaction.
/// </summary>
internal static class PrwRecordOperations
{
    internal sealed record Result(int Count, string Backup);

    internal static bool Supports(string table) =>
        table is "tblWorker" or "tblFed" or "tblMatch";

    internal static Result Add(string targetFile, string table, string name, string? password)
    {
        EnsureCategory(table);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Enter a record name.");
        string backup = Backup(targetFile);
        using var target = AccessDatabase.Open(targetFile, password);
        CheckSchema(target);
        using var tx = target.BeginTransaction();
        try
        {
            var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["UID"] = NewUid(target, tx, table)
            };
            if (table == "tblWorker")
            {
                fields["Name"] = name.Trim();
                fields["ShortName"] = name.Trim();
            }
            else if (table == "tblFed")
            {
                fields["Name"] = name.Trim();
                fields["Initials"] = string.Concat(
                    name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(word => char.ToUpperInvariant(word[0])))[..Math.Min(8,
                    name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)];
            }
            else
            {
                fields["RecordName"] = name.Trim();
                fields["Name"] = name.Trim();
            }
            InsertWithRequiredDefaults(target, tx, table, fields);
            if (table == "tblWorker")
                CreateWorkerDetails(target, tx, fields["UID"]!);
            tx.Commit();
            return new Result(1, backup);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    internal static Result Copy(string targetFile, string table,
        IReadOnlyList<object> identifiers, string? password)
    {
        EnsureCategory(table);
        if (identifiers.Count == 0) throw new ArgumentException("Select records to copy.");
        using var source = AccessDatabase.Open(targetFile, password);
        CheckSchema(source);
        var graphs = identifiers.Select(id => Capture(source, table, id)).ToArray();
        return InsertGraphs(targetFile, table, graphs, true, password);
    }

    internal static Result Import(string targetFile, string sourceFile, string table,
        string? nameFilter, string? password)
    {
        EnsureCategory(table);
        if (Path.GetFullPath(targetFile).Equals(Path.GetFullPath(sourceFile),
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a different database to import from.");
        using var source = AccessDatabase.Open(sourceFile, null);
        CheckSchema(source);
        using var command = new OleDbCommand(
            "SELECT [UID], " + Quote(table == "tblMatch" ? "RecordName" : "Name") +
            " FROM " + Quote(table), source);
        var identifiers = new List<object>();
        using (var reader = command.ExecuteReader())
        {
            while (reader?.Read() == true)
            {
                string name = Convert.ToString(reader.GetValue(1)) ?? "";
                if (!string.IsNullOrWhiteSpace(nameFilter) &&
                    !name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                identifiers.Add(reader.GetValue(0));
                if (identifiers.Count > 500)
                    throw new InvalidOperationException(
                        "More than 500 records matched. Supply a narrower name filter.");
            }
        }
        if (identifiers.Count == 0)
            throw new InvalidOperationException("No records match this import.");
        var graphs = identifiers.Select(id => Capture(source, table, id)).ToArray();
        return InsertGraphs(targetFile, table, graphs, false, password);
    }

    internal static Result Delete(string targetFile, string table,
        IReadOnlyList<object> identifiers, string? password)
    {
        EnsureCategory(table);
        if (identifiers.Count == 0) throw new ArgumentException("Select records to delete.");
        string backup = Backup(targetFile);
        using var connection = AccessDatabase.Open(targetFile, password);
        CheckSchema(connection);
        using var tx = connection.BeginTransaction();
        try
        {
            foreach (object id in identifiers)
            {
                var rows = ReadRows(connection, tx, table, "UID", id);
                if (rows.Rows.Count != 1)
                    throw new DBConcurrencyException("A selected record no longer exists uniquely.");
                DeleteGraph(connection, tx, table, rows.Rows[0]);
            }
            tx.Commit();
            return new Result(identifiers.Count, backup);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private sealed record Snapshot(string Table, DataRow Row,
        IReadOnlyList<(string ForeignKey, Snapshot Child)> Children);

    private static Snapshot Capture(OleDbConnection source, string table, object id)
    {
        var rows = ReadRows(source, null, table, "UID", id);
        if (rows.Rows.Count != 1)
            throw new DBConcurrencyException("The source record does not have a unique UID.");
        return CaptureRecord(source, table, rows.Rows[0]);
    }

    private static Snapshot CaptureRecord(OleDbConnection source, string table, DataRow row)
    {
        var children = new List<(string, Snapshot)>();
        foreach (var relation in PrwSchema.Relationships.Where(r => r.ParentTable == table))
        {
            // Employment ties are not part of a duplicated or imported item.
            if (relation.ChildTable == "tblContract") continue;
            var rows = ReadRows(source, null, relation.ChildTable,
                relation.ChildColumn, row[relation.ParentColumn]);
            foreach (DataRow child in rows.Rows)
                children.Add((relation.ChildColumn,
                    CaptureRecord(source, relation.ChildTable, child)));
        }
        return new Snapshot(table, row, children);
    }

    private static Result InsertGraphs(string targetFile, string table,
        IReadOnlyList<Snapshot> graphs, bool rename, string? password)
    {
        string backup = Backup(targetFile);
        using var target = AccessDatabase.Open(targetFile, password);
        CheckSchema(target);
        using var tx = target.BeginTransaction();
        try
        {
            foreach (var graph in graphs)
                InsertGraph(target, tx, graph, null, null, rename, !rename);
            tx.Commit();
            return new Result(graphs.Count, backup);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static object InsertGraph(OleDbConnection target, OleDbTransaction tx,
        Snapshot snapshot, string? overrideColumn, object? overrideValue,
        bool rename, bool isImport)
    {
        var table = GetTable(snapshot.Table);
        var data = snapshot.Row.Table.Columns.Cast<DataColumn>()
            .Where(c => table.Columns.Contains(c.ColumnName, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(c => c.ColumnName,
                c => snapshot.Row[c] is DBNull ? (object?)DBNull.Value : snapshot.Row[c],
                StringComparer.OrdinalIgnoreCase);

        if (overrideColumn is not null)
            data[overrideColumn] = overrideValue;
        if (data.ContainsKey("UID"))
            data["UID"] = NewUid(target, tx, table.Name);
        if (table.Name is "tblWorker" or "tblFed" or "tblMatch")
        {
            string labelField = table.Name == "tblMatch" ? "RecordName" : "Name";
            if (data.TryGetValue(labelField, out object? old) && old is string value)
            {
                string suffix = rename ? "Copy" : "Imported";
                var uniqueName = UniqueName(target, tx, table.Name,
                    labelField, value, suffix);
                data[labelField] = uniqueName;
                if (table.Name == "tblWorker" && data.ContainsKey("ShortName"))
                    data["ShortName"] = uniqueName;
                if (table.Name == "tblMatch" && data.ContainsKey("Name"))
                    data["Name"] = uniqueName;
            }
        }
        // Cross-universe worker and alter-ego references must not silently point
        // to unrelated records with coincidentally matching integer IDs.
        if (isImport && table.Name == "tblWorker" &&
            data.ContainsKey("LoyaltyWorkerUID"))
            data["LoyaltyWorkerUID"] = 0;
        if (isImport && table.Name == "tblAlternative")
        {
            if (data.ContainsKey("FedUID")) data["FedUID"] = 0;
            if (data.ContainsKey("CompanyInitials")) data["CompanyInitials"] = "";
        }
        Insert(target, tx, table.Name, data);
        object parentId = data.TryGetValue("UID", out object? newUid)
            ? newUid! : data[table.KeyColumns[0]]!;

        foreach (var (foreignKey, child) in snapshot.Children)
            InsertGraph(target, tx, child, foreignKey, parentId, false, isImport);
        return parentId;
    }

    private static void DeleteGraph(OleDbConnection connection, OleDbTransaction tx,
        string table, DataRow row)
    {
        foreach (var relationship in PrwSchema.Relationships.Where(r => r.ParentTable == table))
        {
            DataTable children = ReadRows(connection, tx, relationship.ChildTable,
                relationship.ChildColumn, row[relationship.ParentColumn]);
            foreach (DataRow child in children.Rows)
                DeleteGraph(connection, tx, relationship.ChildTable, child);
        }

        var schema = GetTable(table);
        using var command = new OleDbCommand(
            "DELETE FROM " + Quote(table) + " WHERE " +
            string.Join(" AND ", schema.KeyColumns.Select(column => Quote(column) + " = ?")),
            connection, tx);
        foreach (string key in schema.KeyColumns)
            AddValue(command, row[key]);
        if (command.ExecuteNonQuery() != 1)
            throw new DBConcurrencyException("Deletion failed for a linked " + table + " record.");
    }

    private static DataTable ReadRows(OleDbConnection connection, OleDbTransaction? tx,
        string table, string column, object value)
    {
        var schema = GetTable(table);
        if (!schema.Columns.Contains(column, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid native PRW relationship column.");
        using var command = new OleDbCommand(
            "SELECT * FROM " + Quote(table) + " WHERE " + Quote(column) + " = ?",
            connection, tx);
        AddValue(command, value);
        using var adapter = new OleDbDataAdapter(command);
        var result = new DataTable(table);
        adapter.Fill(result);
        return result;
    }

    private static object NewUid(OleDbConnection connection, OleDbTransaction tx, string table)
    {
        using var command = new OleDbCommand(
            "SELECT MAX([UID]) FROM " + Quote(table), connection, tx);
        object result = command.ExecuteScalar() ?? DBNull.Value;
        long next = result is DBNull ? 1 : Convert.ToInt64(result) + 1;
        if (next > int.MaxValue)
            throw new OverflowException("No more record IDs are available.");
        return Convert.ToInt32(next, CultureInfo.InvariantCulture);
    }

    private static void Insert(OleDbConnection connection, OleDbTransaction tx,
        string table, IReadOnlyDictionary<string, object?> fields)
    {
        var schema = GetTable(table);
        if (fields.Keys.Any(field =>
                !schema.Columns.Contains(field, StringComparer.OrdinalIgnoreCase)))
            throw new InvalidDataException("A source field is not part of the native PRW schema.");
        var names = fields.Keys.ToArray();
        using var command = new OleDbCommand(
            "INSERT INTO " + Quote(table) + " (" +
            string.Join(", ", names.Select(Quote)) + ") VALUES (" +
            string.Join(", ", names.Select(_ => "?")) + ")", connection, tx);
        foreach (string field in names) AddValue(command, fields[field]);
        if (command.ExecuteNonQuery() != 1)
            throw new DBConcurrencyException("Insertion failed for " + table + ".");
    }


    private static void CreateWorkerDetails(OleDbConnection connection,
        OleDbTransaction tx, object workerUid)
    {
        foreach (var table in new[]
            { "tblWorkerBio", "tblWorkerSkills", "tblWorkerPhysical", "tblWorkerBusiness" })
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["WorkerUID"] = workerUid
            };
            if (table == "tblWorkerBio") values["Biography"] = "";
            InsertWithRequiredDefaults(connection, tx, table, values);
        }
    }

    private static void InsertWithRequiredDefaults(OleDbConnection connection,
        OleDbTransaction tx, string table, Dictionary<string, object?> values)
    {
        // Respect Access defaults where present. Initialize only columns which
        // explicitly disallow null and have no database-supplied default.
        using var command = new OleDbCommand(
            "SELECT * FROM " + Quote(table) + " WHERE 1 = 0", connection, tx);
        using var adapter = new OleDbDataAdapter(command);
        var schema = new DataTable(table);
        adapter.FillSchema(schema, SchemaType.Source);
        foreach (DataColumn column in schema.Columns)
        {
            if (values.ContainsKey(column.ColumnName) || column.AutoIncrement ||
                column.AllowDBNull || column.DefaultValue is not DBNull)
                continue;
            object fallback = column.DataType == typeof(string)
                ? "" : column.DataType == typeof(DateTime)
                    ? DateTime.Today : column.DataType == typeof(bool)
                        ? false : column.DataType.IsValueType
                            ? Activator.CreateInstance(column.DataType)!
                            : throw new InvalidDataException(
                                "Cannot initialize required field: " + column.ColumnName);
            values[column.ColumnName] = fallback;
        }
        Insert(connection, tx, table, values);
    }

    private static string UniqueName(OleDbConnection connection, OleDbTransaction tx,
        string table, string field, string initial, string suffix)
    {
        string candidate = initial;
        int version = 0;
        while (true)
        {
            using var command = new OleDbCommand(
                "SELECT COUNT(*) FROM " + Quote(table) +
                " WHERE " + Quote(field) + " = ?", connection, tx);
            AddValue(command, candidate);
            if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                return candidate;
            version++;
            candidate = initial + " (" + suffix +
                (version > 1 ? " " + version.ToString(CultureInfo.InvariantCulture) : "") + ")";
            if (version > 1000)
                throw new IOException("Could not find an available name for the new record.");
        }
    }

    private static string Backup(string file)
    {
        if (File.GetAttributes(file).HasFlag(FileAttributes.ReadOnly))
            throw new IOException("The selected universe database is read-only.");
        string folder = Path.Combine(Path.GetDirectoryName(file)!, "Backups");
        Directory.CreateDirectory(folder);
        string backup = Path.Combine(folder, "records-" +
            DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" +
            Guid.NewGuid().ToString("N")[..8] + ".mdb");
        File.Copy(file, backup);
        return backup;
    }

    private static void AddValue(OleDbCommand command, object? value)
    {
        var type = value switch
        {
            string s when s.Length > 255 => OleDbType.LongVarWChar,
            string => OleDbType.VarWChar,
            bool => OleDbType.Boolean,
            int => OleDbType.Integer,
            short => OleDbType.SmallInt,
            long => OleDbType.BigInt,
            DateTime => OleDbType.DBTimeStamp,
            double => OleDbType.Double,
            float => OleDbType.Single,
            decimal => OleDbType.Decimal,
            byte => OleDbType.UnsignedTinyInt,
            _ => OleDbType.Variant
        };
        var parameter = command.Parameters.Add("p" + command.Parameters.Count, type);
        parameter.Value = value ?? DBNull.Value;
    }

    private static string Quote(string field) =>
        "[" + field.Replace("]", "]]", StringComparison.Ordinal) + "]";

    private static PrwSchema.Table GetTable(string table) =>
        PrwSchema.Tables.SingleOrDefault(t => t.Name == table)
        ?? throw new InvalidDataException("Unsupported native PRW table: " + table);

    private static void EnsureCategory(string table)
    {
        if (!Supports(table))
            throw new NotSupportedException(
                "Record operations currently support native PRW Workers, Companies and Match Types.");
    }

    private static void CheckSchema(OleDbConnection connection)
    {
        if (!PrwSchema.Inspect(connection, requireExpandedTables: false).IsValid)
            throw new InvalidDataException("A native PWR database is required.");
    }
}
