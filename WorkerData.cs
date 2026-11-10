using System.Data;
using System.Data.OleDb;
using System.Globalization;

namespace ProWrestlingRevolution;

/// <summary>
/// Schema-driven windows for the user's PRW_Default MDB. The table and parent
/// field names here come from that MDB, not the older TEW schema. Data values,
/// column types, primary keys and actual row counts come from ACE at runtime.
/// </summary>
internal sealed record WorkerSection(string Label, string Table, string OwnerField,
    string Description, bool IsMany = true, string? ChildKey = null)
{
    internal bool IsCore => Table == "Worker";
}

internal sealed record WorkerListEntry(int Id, string Name, string ShortName)
{
    public override string ToString() => Name;
}

internal static class WorkerData
{
    internal static readonly WorkerSection Core =
        new("Personal Details", "Worker", "WorkerID", "Identity, dates, size, status, and basic profile", false);

    internal static readonly WorkerSection[] Sections =
    [
        new("Biography", "WorkerBiography", "WorkerID", "Full biography and written background", false),
        new("Physical Health", "WorkerPhysical", "WorkerID", "Health ratings, injuries and physical condition", false),
        new("In-Ring Skills", "WorkerSkill", "WorkerID", "Brawling, technical, aerial, charisma and other skills", false),
        new("Skill Potential", "WorkerSkillPotential", "WorkerID", "Potential by skill category"),
        new("Business Skills", "WorkerBusinessSkill", "WorkerID", "Ownership, booking and business ability", false),
        new("Popularity", "WorkerPopularity", "WorkerID", "Regional popularity values"),
        new("Languages", "WorkerLanguage", "WorkerID", "Languages and fluency"),
        new("Active Areas", "WorkerActiveArea", "WorkerID", "Regional availability"),
        new("Possible Roles", "WorkerPossibleRole", "WorkerID", "Available professional roles"),
        new("Attributes", "WorkerAttribute", "WorkerID", "Defined attributes, hidden and permanent flags"),
        new("Contracts", "WorkerContract", "WorkerID", "Company agreements and contractual conditions", true, "ContractID"),
        new("Alter Egos", "WorkerAlterEgo", "WorkerID", "Alternative identities, presentation and portraits", true, "AlterEgoID"),
        new("Company Standing", "WorkerCompanyStanding", "WorkerID", "Company push and momentum"),
        new("Job History", "WorkerJobHistory", "WorkerID", "In-ring employment and history"),
        new("Business History", "WorkerBusinessHistory", "WorkerID", "Administrative and business employment"),
        new("Managing", "WorkerManagerAssignment", "ManagerWorkerID", "Wrestlers managed by this worker"),
        new("Managed By", "WorkerManagerAssignment", "WrestlerWorkerID", "Managers assigned to this worker"),
        new("Legacies", "WorkerLegacyMembership", "WorkerID", "Legacy and generation memberships")
    ];

    internal static readonly WorkerSection[] ContractChildren =
    [
        new("Company Presentation", "WorkerCompanyPresentation", "ContractID",
            "Ring name, gimmick, picture and related presentation", false),
        new("Contract Roles", "ContractRole", "ContractID", "Roles attached to this agreement"),
        new("Brands", "ContractBrand", "ContractID", "Brand assignments"),
        new("Divisions", "ContractDivision", "ContractID", "Division assignments")
    ];

    internal static readonly WorkerSection[] AlterEgoChildren =
    [
        new("Eligibility", "AlterEgoEligibility", "AlterEgoID",
            "Promotion and region eligibility for this identity")
    ];

    private static OleDbConnection Connect(string path)
    {
        if (!AccessDatabase.LooksLikeMdb(path))
            throw new InvalidDataException("The selected universe is not a valid Access MDB.");
        Exception? failure = null;
        foreach (string provider in new[] { "Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0" })
        {
            var connection = new OleDbConnection(
                new OleDbConnectionStringBuilder { Provider = provider, DataSource = path }.ConnectionString);
            try { connection.Open(); return connection; }
            catch (Exception e) when (e is OleDbException or InvalidOperationException)
            {
                failure = e;
                connection.Dispose();
            }
        }
        throw new InvalidOperationException(
            "Microsoft Access Database Engine (64-bit) is required to edit workers. " +
            (failure?.Message ?? ""), failure);
    }

    private static string Quote(string identifier) =>
        "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";

    private static bool ValidRelation(WorkerSection section) =>
        section == Core || Sections.Contains(section) ||
        ContractChildren.Contains(section) || AlterEgoChildren.Contains(section);

    private static void Validate(OleDbConnection db, WorkerSection section)
    {
        if (!ValidRelation(section))
            throw new ArgumentException("Unsupported worker data section.");
        var names = db.GetSchema("Tables").Rows.Cast<DataRow>()
            .Where(x => string.Equals(Convert.ToString(x["TABLE_TYPE"]), "TABLE",
                StringComparison.OrdinalIgnoreCase))
            .Select(x => Convert.ToString(x["TABLE_NAME"]));
        if (!names.Contains(section.Table, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "The selected MDB does not contain the " + section.Table + " table.");
    }

    private static DataTable Schema(OleDbConnection db, WorkerSection section)
    {
        Validate(db, section);
        using var adapter = new OleDbDataAdapter(
            "SELECT * FROM " + Quote(section.Table) + " WHERE 1 = 0", db);
        var schema = new DataTable(section.Table);
        adapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;
        adapter.FillSchema(schema, SchemaType.Source);
        if (!schema.Columns.Contains(section.OwnerField))
            throw new InvalidDataException(section.Table + " has no " + section.OwnerField + " field.");
        return schema;
    }

    internal static DataTable LoadRows(string path, WorkerSection section, int parentId)
    {
        using var db = Connect(path);
        var table = Schema(db, section);
        using var command = new OleDbCommand(
            "SELECT TOP 3000 * FROM " + Quote(section.Table) + " WHERE " +
            Quote(section.OwnerField) + " = ?", db);
        command.Parameters.Add("parent", OleDbType.Integer).Value = parentId;
        using var adapter = new OleDbDataAdapter(command);
        adapter.Fill(table);
        return table;
    }

    internal static DataTable EmptySchema(string path, WorkerSection section)
    {
        using var db = Connect(path);
        return Schema(db, section);
    }

    internal static IReadOnlyList<WorkerListEntry> LoadWorkers(string path)
    {
        using var db = Connect(path);
        Validate(db, Core);
        using var cmd = new OleDbCommand(
            "SELECT TOP 25000 [WorkerID], [Name], [ShortName] FROM [Worker] ORDER BY [Name]", db);
        using var reader = cmd.ExecuteReader();
        var workers = new List<WorkerListEntry>();
        if (reader is null) return workers;
        while (reader.Read())
        {
            int id = Convert.ToInt32(reader["WorkerID"], CultureInfo.InvariantCulture);
            string name = Convert.ToString(reader["Name"]) ?? "(Unnamed)";
            string shortName = Convert.ToString(reader["ShortName"]) ?? "";
            workers.Add(new WorkerListEntry(id, name, shortName));
        }
        return workers;
    }

    internal static string DisplayRow(DataRow row, WorkerSection section)
    {
        string[] candidates = section.Table switch
        {
            "WorkerContract" => ["PromotionID", "ContractTypeCode", "ContractID"],
            "WorkerPopularity" => ["RegionID", "PopularityValue"],
            "WorkerLanguage" => ["LanguageCode", "FluencyLevel"],
            "WorkerPossibleRole" => ["RoleCode"],
            "WorkerAlterEgo" => ["Name", "GimmickTypeCode"],
            "WorkerAttribute" => ["AttributeDefinitionID"],
            "WorkerActiveArea" => ["RegionID", "Active"],
            "WorkerJobHistory" => ["RecordName", "EmployerInitials", "StartDate"],
            "WorkerBusinessHistory" => ["EmployerInitials", "PositionCode", "StartDate"],
            "WorkerSkillPotential" => ["PotentialCategoryCode", "PotentialValue"],
            "WorkerLegacyMembership" => ["LegacyID", "GenerationNumber"],
            _ => ["Name", "GimmickName", "PromotionID", "AssignmentID", "WorkerID"]
        };
        var values = candidates
            .Where(c => row.Table.Columns.Contains(c) && row[c] is not DBNull)
            .Select(c => c + ": " + Convert.ToString(row[c], CultureInfo.InvariantCulture))
            .Take(3).ToArray();
        if (values.Length > 0) return string.Join("    ", values);
        DataColumn? pk = row.Table.PrimaryKey.FirstOrDefault();
        return pk is not null ? "#" + Convert.ToString(row[pk]) : section.Table;
    }

    private static object DbValue(DataColumn column, object? value)
    {
        if (value is null || value is DBNull) return DBNull.Value;
        if (value is string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) && column.DataType != typeof(string))
                return column.AllowDBNull ? DBNull.Value :
                    throw new InvalidOperationException(column.ColumnName + " is required.");
            if (column.DataType == typeof(string))
            {
                if (column.MaxLength >= 0 && raw.Length > column.MaxLength)
                    throw new ArgumentException(column.ColumnName + " exceeds its maximum length.");
                return raw;
            }
        }
        try
        {
            if (column.DataType == typeof(DateTime))
                return Convert.ToDateTime(value, CultureInfo.InvariantCulture);
            if (column.DataType == typeof(bool))
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            if (column.DataType == typeof(Guid))
                return Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!);
            return Convert.ChangeType(value, column.DataType, CultureInfo.InvariantCulture)!;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or InvalidCastException)
        {
            throw new ArgumentException("Invalid value for " + column.ColumnName + ".", ex);
        }
    }

    private static void AddParameter(OleDbCommand command, DataColumn column, object? value)
    {
        OleDbType type = column.DataType switch
        {
            var t when t == typeof(bool) => OleDbType.Boolean,
            var t when t == typeof(byte) => OleDbType.UnsignedTinyInt,
            var t when t == typeof(short) => OleDbType.SmallInt,
            var t when t == typeof(int) => OleDbType.Integer,
            var t when t == typeof(long) => OleDbType.BigInt,
            var t when t == typeof(float) => OleDbType.Single,
            var t when t == typeof(double) => OleDbType.Double,
            var t when t == typeof(decimal) => OleDbType.Decimal,
            var t when t == typeof(DateTime) => OleDbType.Date,
            var t when t == typeof(string) =>
                column.MaxLength > 0 && column.MaxLength <= 255 ? OleDbType.VarWChar : OleDbType.LongVarWChar,
            _ => OleDbType.Variant
        };
        var parameter = command.Parameters.Add("field" + command.Parameters.Count, type);
        parameter.Value = DbValue(column, value);
    }

    private static string Backup(string path)
    {
        string folder = Path.Combine(Path.GetDirectoryName(path)!, "Backups");
        Directory.CreateDirectory(folder);
        string target = Path.Combine(folder, "worker-edit-" +
            DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) +
            "-" + Guid.NewGuid().ToString("N")[..8] + ".mdb");
        File.Copy(path, target);
        return target;
    }

    /// <summary>
    /// Writes changed fields only. Primary and relationship keys are immutable.
    /// A transaction is used and a full MDB backup is made before each write.
    /// The command's WHERE clause uses all declared primary-key columns and
    /// the expected parent ID. No record is matched on row order or name.
    /// </summary>
    internal static (string BackupPath, int? NewId) Save(
        string file, WorkerSection section, int? ownerId,
        DataRow? existing, IReadOnlyDictionary<string, object?> fields)
    {
        if (File.GetAttributes(file).HasFlag(FileAttributes.ReadOnly))
            throw new IOException("The selected database is read-only.");
        if (fields.Count == 0 && existing is not null)
            return ("No changes", null);

        using var db = Connect(file);
        DataTable schema = Schema(db, section);
        DataColumn[] primary = schema.PrimaryKey;
        if (existing is not null && primary.Length == 0)
            throw new NotSupportedException(section.Table +
                " has no declared primary key. Editing is disabled to prevent accidental multi-row changes.");

        var updates = new List<(DataColumn Column, object Value)>();
        foreach (var pair in fields)
        {
            DataColumn? column = schema.Columns.Cast<DataColumn>()
                .FirstOrDefault(c => c.ColumnName.Equals(pair.Key, StringComparison.OrdinalIgnoreCase));
            if (column is null || column.ReadOnly || column.AutoIncrement ||
                primary.Any(pk => pk.ColumnName == column.ColumnName) ||
                column.ColumnName.Equals(section.OwnerField, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This field cannot be changed: " + pair.Key);
            updates.Add((column, DbValue(column, pair.Value)));
        }

        if (existing is null && section != Core && ownerId is null)
            throw new InvalidOperationException("A parent record must be selected.");

        string sql;
        if (existing is null)
        {
            var cols = new List<string>();
            if (section != Core) cols.Add(Quote(section.OwnerField));
            cols.AddRange(updates.Select(p => Quote(p.Column.ColumnName)));
            if (cols.Count == 0)
                throw new InvalidOperationException("Enter at least one value.");
            sql = "INSERT INTO " + Quote(section.Table) + " (" +
                string.Join(", ", cols) + ") VALUES (" +
                string.Join(", ", cols.Select(_ => "?")) + ")";
        }
        else
        {
            if (updates.Count == 0) return ("No changes", null);
            sql = "UPDATE " + Quote(section.Table) + " SET " +
                string.Join(", ", updates.Select(p => Quote(p.Column.ColumnName) + " = ?")) +
                " WHERE " + string.Join(" AND ",
                    primary.Select(p => Quote(p.ColumnName) + " = ?"));
            if (section != Core) sql += " AND " + Quote(section.OwnerField) + " = ?";
        }

        // Finish all validation before any backup or change.
        string backup = Backup(file);
        using var tx = db.BeginTransaction();
        try
        {
            using var cmd = new OleDbCommand(sql, db, tx);
            if (existing is null && section != Core)
                AddParameter(cmd, schema.Columns[section.OwnerField]!, ownerId!.Value);
            foreach (var update in updates) AddParameter(cmd, update.Column, update.Value);
            if (existing is not null)
            {
                foreach (var key in primary)
                {
                    if (!existing.Table.Columns.Contains(key.ColumnName) || existing.IsNull(key.ColumnName))
                        throw new InvalidDataException("A primary-key value is missing.");
                    AddParameter(cmd, key, existing[key.ColumnName]);
                }
                if (section != Core)
                    AddParameter(cmd, schema.Columns[section.OwnerField]!, ownerId!.Value);
            }
            int changed = cmd.ExecuteNonQuery();
            if (changed != 1)
                throw new DBConcurrencyException("The selected record was not uniquely updated.");
            int? newId = null;
            if (existing is null && section == Core)
            {
                using var identity = new OleDbCommand("SELECT @@IDENTITY", db, tx);
                newId = Convert.ToInt32(identity.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
            tx.Commit();
            return (backup, newId);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    internal static string Delete(string file, WorkerSection section, int ownerId, DataRow record)
    {
        if (section == Core)
            throw new InvalidOperationException("Deleting entire workers is disabled to protect dependent data.");
        if (File.GetAttributes(file).HasFlag(FileAttributes.ReadOnly))
            throw new IOException("This database is read-only.");

        using var db = Connect(file);
        DataTable schema = Schema(db, section);
        if (schema.PrimaryKey.Length == 0)
            throw new NotSupportedException("This table does not declare a primary key. Delete is unavailable.");
        string where = string.Join(" AND ",
            schema.PrimaryKey.Select(k => Quote(k.ColumnName) + " = ?"));
        string sql = "DELETE FROM " + Quote(section.Table) +
            " WHERE " + where + " AND " + Quote(section.OwnerField) + " = ?";
        string backup = Backup(file);
        using var tx = db.BeginTransaction();
        try
        {
            using var command = new OleDbCommand(sql, db, tx);
            foreach (DataColumn key in schema.PrimaryKey)
            {
                if (!record.Table.Columns.Contains(key.ColumnName) || record.IsNull(key.ColumnName))
                    throw new InvalidDataException("A primary-key value is missing.");
                AddParameter(command, key, record[key.ColumnName]);
            }
            AddParameter(command, schema.Columns[section.OwnerField]!, ownerId);
            if (command.ExecuteNonQuery() != 1)
                throw new DBConcurrencyException("The selected row was not uniquely deleted.");
            tx.Commit();
            return backup;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
