using System.Data;
using System.Data.OleDb;
using System.Globalization;

namespace ProWrestlingRevolution;

/// <summary>
/// Contract identity operations for native PWR universes. Existing contract
/// field edits go through PrwWorkerEditorService's optimistic write path.
/// This class does not attempt to edit imported TEW databases.
/// </summary>
internal static class PrwContractStore
{
    internal sealed record Employer(object Uid, string Name, string Initials, string Logo)
    {
        public override string ToString() => Initials.Length > 0 ? Initials : Name;
    }

    internal static IReadOnlyList<Employer> Employers(string file, string? password)
    {
        using var db = AccessDatabase.Open(file, password);
        using var command = new OleDbCommand(
            "SELECT [UID], [Name], [Initials], [Logo] FROM [tblFed] ORDER BY [Initials]", db);
        using var reader = command.ExecuteReader();
        var result = new List<Employer>();
        while (reader?.Read() == true)
            result.Add(new Employer(reader["UID"],
                Convert.ToString(reader["Name"]) ?? "",
                Convert.ToString(reader["Initials"]) ?? "",
                Convert.ToString(reader["Logo"]) ?? ""));
        return result;
    }

    internal static object Create(string file, string? password, object workerUid,
        Employer employer, IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, bool> roles, string biography)
    {
        if (File.GetAttributes(file).HasFlag(FileAttributes.ReadOnly))
            throw new IOException("The selected universe is read-only.");
        using var db = AccessDatabase.Open(file, password);
        if (!PrwSchema.Inspect(db, requireExpandedTables: false).IsValid)
            throw new InvalidDataException("Contract creation requires a native PWR universe.");

        using var adapter = new OleDbDataAdapter(
            "SELECT * FROM [tblContract] WHERE 1=0", db);
        var schema = new DataTable("tblContract");
        adapter.FillSchema(schema, SchemaType.Source);
        using var transaction = db.BeginTransaction();
        try
        {
            using var next = new OleDbCommand(
                "SELECT MAX([UID]) FROM [tblContract]", db, transaction);
            var maximum = next.ExecuteScalar();
            long newUid = maximum is null or DBNull
                ? 1 : checked(Convert.ToInt64(maximum, CultureInfo.InvariantCulture) + 1);
            if (newUid > int.MaxValue) throw new OverflowException("Contract ID space is exhausted.");

            var row = schema.NewRow();
            foreach (DataColumn column in schema.Columns)
            {
                if (column.ColumnName == "UID")
                {
                    if (!column.AutoIncrement)
                        row[column] = Convert.ChangeType(newUid, column.DataType,
                            CultureInfo.InvariantCulture);
                }
                else if (column.ColumnName == "WorkerUID")
                    row[column] = Convert.ChangeType(workerUid, column.DataType,
                        CultureInfo.InvariantCulture);
                else if (column.ColumnName == "FedUID")
                    row[column] = Convert.ChangeType(employer.Uid, column.DataType,
                        CultureInfo.InvariantCulture);
                else if (column.ColumnName == "EmployerInitials")
                    row[column] = employer.Initials;
                else if (values.TryGetValue(column.ColumnName, out var value) &&
                         !string.IsNullOrWhiteSpace(value))
                    row[column] = ConvertInput(value, column);
                else if (roles.TryGetValue(column.ColumnName, out var enabled))
                    row[column] = column.DataType == typeof(bool) ? enabled
                        : Convert.ChangeType(enabled ? 1 : 0, column.DataType,
                            CultureInfo.InvariantCulture);
                else if (!column.AllowDBNull && row[column] is DBNull)
                    row[column] = Fallback(column.DataType);
            }

            var columns = schema.Columns.Cast<DataColumn>()
                .Where(c => !c.AutoIncrement && row[c] is not DBNull).ToArray();
            using var insert = new OleDbCommand(
                "INSERT INTO [tblContract] (" +
                string.Join(", ", columns.Select(c => Quote(c.ColumnName))) +
                ") VALUES (" + string.Join(", ", columns.Select(_ => "?")) + ")",
                db, transaction);
            foreach (DataColumn column in columns)
                AddValue(insert, row[column], column);
            if (insert.ExecuteNonQuery() != 1)
                throw new DBConcurrencyException("Could not create the contract.");

            object id = row.Table.Columns["UID"]!.AutoIncrement
                ? GetIdentity(db, transaction)
                : row["UID"];
            if (!string.IsNullOrWhiteSpace(biography))
            {
                using var bio = new OleDbCommand(
                    "INSERT INTO [tblContractBio] ([ContractUID], [Biography]) VALUES (?, ?)",
                    db, transaction);
                bio.Parameters.Add("uid", OleDbType.Integer).Value =
                    Convert.ToInt32(id, CultureInfo.InvariantCulture);
                bio.Parameters.Add("body", OleDbType.LongVarWChar).Value = biography;
                bio.ExecuteNonQuery();
            }
            transaction.Commit();
            return id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }


    /// <summary>
    /// Persist the editor's supported fields and biography in one transaction.
    /// Compare the changed fields against the values displayed at load time.
    /// </summary>
    internal static void Update(string file, string? password, object workerUid,
        DataRow record, IReadOnlyDictionary<string, string> fields,
        IReadOnlyDictionary<string, bool> roles, string biography)
    {
        if (File.GetAttributes(file).HasFlag(FileAttributes.ReadOnly))
            throw new IOException("The selected universe is read-only.");
        using var db = AccessDatabase.Open(file, password);
        if (!PrwSchema.Inspect(db, requireExpandedTables: false).IsValid)
            throw new InvalidDataException("Contract editing requires a native PWR universe.");
        var requested = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in fields)
        {
            var column = record.Table.Columns[item.Key];
            if (column is null || column.ColumnName is "UID" or "WorkerUID")
                continue;
            if (item.Value.Length == 0 && column.AllowDBNull)
                requested[item.Key] = DBNull.Value;
            else if (item.Value.Length == 0 && column.DataType == typeof(string))
                requested[item.Key] = "";
            else if (item.Value.Length > 0)
                requested[item.Key] = ConvertInput(item.Value, column);
        }
        foreach (var item in roles)
        {
            var column = record.Table.Columns[item.Key];
            if (column is null) continue;
            requested[item.Key] = column.DataType == typeof(bool) ? item.Value :
                Convert.ChangeType(item.Value ? 1 : 0, column.DataType,
                    CultureInfo.InvariantCulture);
        }

        using var transaction = db.BeginTransaction();
        try
        {
            using (var verify = new OleDbCommand(
                "SELECT * FROM [tblContract] WHERE [UID]=? AND [WorkerUID]=?",
                db, transaction))
            {
                verify.Parameters.AddWithValue("uid", record["UID"]);
                verify.Parameters.AddWithValue("worker", workerUid);
                using var reader = verify.ExecuteReader();
                if (reader?.Read() != true)
                    throw new DBConcurrencyException("This worker's contract was removed.");
                foreach (var item in requested)
                {
                    var before = record[item.Key, DataRowVersion.Original];
                    var inDatabase = reader[item.Key];
                    if (!Equals(before is DBNull ? null : before,
                                inDatabase is DBNull ? null : inDatabase))
                        throw new DBConcurrencyException(
                            "The contract field " + item.Key +
                            " was edited elsewhere. Reload before saving.");
                }
            }

            var changed = requested.Where(item => !Equals(
                record[item.Key] is DBNull ? null : record[item.Key],
                item.Value is DBNull ? null : item.Value)).ToArray();
            if (changed.Length != 0)
            {
                using var update = new OleDbCommand(
                    "UPDATE [tblContract] SET " +
                    string.Join(", ", changed.Select(c => Quote(c.Key) + "=?")) +
                    " WHERE [UID]=? AND [WorkerUID]=?", db, transaction);
                foreach (var item in changed)
                    AddValue(update, item.Value, record.Table.Columns[item.Key]!);
                update.Parameters.AddWithValue("uid", record["UID"]);
                update.Parameters.AddWithValue("worker", workerUid);
                if (update.ExecuteNonQuery() != 1)
                    throw new DBConcurrencyException("Contract update affected no unique record.");
            }

            using (var existing = new OleDbCommand(
                "SELECT COUNT(*) FROM [tblContractBio] WHERE [ContractUID]=?",
                db, transaction))
            {
                existing.Parameters.AddWithValue("uid", record["UID"]);
                bool hasBio = Convert.ToInt32(existing.ExecuteScalar(),
                    CultureInfo.InvariantCulture) != 0;
                using var bio = new OleDbCommand(hasBio
                    ? "UPDATE [tblContractBio] SET [Biography]=? WHERE [ContractUID]=?"
                    : "INSERT INTO [tblContractBio] ([Biography], [ContractUID]) VALUES (?, ?)",
                    db, transaction);
                bio.Parameters.Add("text", OleDbType.LongVarWChar).Value = biography;
                bio.Parameters.AddWithValue("uid", record["UID"]);
                bio.ExecuteNonQuery();
            }
            transaction.Commit();
            foreach (var item in requested) record[item.Key] = item.Value;
            record.AcceptChanges();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    internal static void Remove(string file, string? password, object workerUid, object contractUid)
    {
        if (File.GetAttributes(file).HasFlag(FileAttributes.ReadOnly))
            throw new IOException("The selected universe is read-only.");
        using var db = AccessDatabase.Open(file, password);
        if (!PrwSchema.Inspect(db, requireExpandedTables: false).IsValid)
            throw new InvalidDataException("Contract deletion requires a native PWR universe.");
        using var transaction = db.BeginTransaction();
        try
        {
            using var verify = new OleDbCommand(
                "SELECT COUNT(*) FROM [tblContract] WHERE [UID]=? AND [WorkerUID]=?",
                db, transaction);
            verify.Parameters.AddWithValue("contract_uid", contractUid);
            verify.Parameters.AddWithValue("worker_uid", workerUid);
            if (Convert.ToInt32(verify.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
                throw new DBConcurrencyException("This contract no longer belongs to the worker.");

            using var bio = new OleDbCommand(
                "DELETE FROM [tblContractBio] WHERE [ContractUID]=?", db, transaction);
            bio.Parameters.AddWithValue("contract_uid", contractUid);
            bio.ExecuteNonQuery();

            using var contract = new OleDbCommand(
                "DELETE FROM [tblContract] WHERE [UID]=? AND [WorkerUID]=?",
                db, transaction);
            contract.Parameters.AddWithValue("contract_uid", contractUid);
            contract.Parameters.AddWithValue("worker_uid", workerUid);
            if (contract.ExecuteNonQuery() != 1)
                throw new DBConcurrencyException("The contract changed during deletion.");
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    internal static object ConvertInput(string input, DataColumn column)
    {
        var type = column.DataType;
        if (type == typeof(string)) return input;
        if (type == typeof(bool))
        {
            if (input.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("True", StringComparison.OrdinalIgnoreCase) || input == "1")
                return true;
            if (input.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("False", StringComparison.OrdinalIgnoreCase) || input == "0")
                return false;
            throw new FormatException(
                column.ColumnName + " must be Yes, No, True, False, 1 or 0.");
        }
        if (type == typeof(DateTime))
            return DateTime.ParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return Convert.ChangeType(input.Replace("$", "").Replace(",", ""),
            type, CultureInfo.InvariantCulture);
    }

    private static object Fallback(Type t) =>
        t == typeof(string) ? "" :
        t == typeof(DateTime) ? DateTime.Today :
        t == typeof(bool) ? false :
        t.IsValueType ? Activator.CreateInstance(t)! :
        throw new InvalidDataException("Unknown required contract column type.");

    private static object GetIdentity(OleDbConnection db, OleDbTransaction tx)
    {
        using var command = new OleDbCommand("SELECT @@IDENTITY", db, tx);
        return command.ExecuteScalar() ?? throw new InvalidDataException(
            "The created contract did not return a UID.");
    }

    private static string Quote(string field) =>
        "[" + field.Replace("]", "]]", StringComparison.Ordinal) + "]";

    private static void AddValue(OleDbCommand cmd, object value, DataColumn column)
    {
        Type type = column.DataType;
        var oleType = type == typeof(bool) ? OleDbType.Boolean :
            type == typeof(DateTime) ? OleDbType.DBTimeStamp :
            type == typeof(string) ? (column.MaxLength < 0
                ? OleDbType.LongVarWChar : OleDbType.VarWChar) :
            type == typeof(int) ? OleDbType.Integer :
            type == typeof(short) ? OleDbType.SmallInt :
            type == typeof(long) ? OleDbType.BigInt :
            type == typeof(byte) ? OleDbType.UnsignedTinyInt :
            type == typeof(double) ? OleDbType.Double :
            type == typeof(float) ? OleDbType.Single : OleDbType.Variant;
        cmd.Parameters.Add("p" + cmd.Parameters.Count, oleType).Value = value;
    }
}
