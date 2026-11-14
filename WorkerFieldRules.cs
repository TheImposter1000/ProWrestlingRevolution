using System.Data;
using System.Globalization;

namespace ProWrestlingRevolution;

/// <summary>
/// Presentation/validation rules over the existing native MDB columns.
/// Nothing here changes a legacy schema or silently migrates stored values.
/// </summary>
internal static class WorkerFieldRules
{
    private static readonly HashSet<string> Dates = new(StringComparer.OrdinalIgnoreCase)
    {
        "Born", "SetToDebut", "DeathDate"
    };

    private static readonly HashSet<string> ManualNumbers = new(StringComparer.OrdinalIgnoreCase)
    {
        "HeightInches", "WeightPounds", "MinWeightPounds", "MaxWeightPounds",
        "HeadCondition", "BodyCondition", "ArmsCondition", "LegsCondition"
    };

    internal static readonly string[] LanguageLevels =
        ["None", "Passable", "Basic", "Fluent"];
    internal static readonly string[] Divisions =
        ["Men's Division", "Women's Division", "Both Divisions"];
    // PRWDefault.mdb stores longer labels in the native text column.
    // Keep these exact legacy strings while showing concise UI labels.
    internal static readonly string[] DivisionStorage =
        ["Men's Division Only", "Women's Division Only", "Both Divisions"];

    internal static bool IsDate(string field) => Dates.Contains(field);
    internal static bool IsLanguage(string field) =>
        field.StartsWith("Language", StringComparison.OrdinalIgnoreCase);
    private static readonly HashSet<string> Categorical = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "BiographyType", "Regenerates", "GenderID", "Pronouns",
        "Sexuality", "Relationship", "Race", "Nationality", "BasedIn",
        "GameStatus", "InRingStatus", "Celebrity", "Style", "BodyType",
        "LoyaltyWorkerUID", "LoyaltyType", "Freelance", "UserCharacter",
        "CompetesVs", "DefaultMoveSet", "FaceGimmickBasis",
        "HeelGimmickBasis", "Mask", "Hair", "CareerGoal"
    };

    internal static bool IsManual(string field, DataColumn? column)
    {
        if (Dates.Contains(field) || ManualNumbers.Contains(field) ||
            field is "FaceGimmick" or "HeelGimmick")
            return true;
        if (IsLanguage(field) || Categorical.Contains(field) ||
            field.StartsWith("Role", StringComparison.OrdinalIgnoreCase) ||
            field.StartsWith("Active", StringComparison.OrdinalIgnoreCase))
            return false;
        // Numbers in supplementary skill, reputation and business detail
        // tables are typed directly. Numeric category IDs in tblWorker are
        // not automatically mistaken for editable numerical ratings.
        return column is not null &&
            !string.Equals(column.Table?.TableName, "tblWorker",
                StringComparison.OrdinalIgnoreCase) &&
            (column.DataType == typeof(DateTime) ||
             (column.DataType.IsPrimitive && column.DataType != typeof(bool)) ||
             column.DataType == typeof(decimal));
    }

    internal static string Display(DataRow row, string field)
    {
        if (!row.Table.Columns.Contains(field) || row[field] is DBNull) return "";
        object value = row[field];
        if (IsLanguage(field) && value is not string &&
            int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                out int level) && level is >= 0 and < 4)
            return LanguageLevels[level];
        if (field == "CompetesVs" && value is string divisionText)
        {
            for (int index = 0; index < DivisionStorage.Length; index++)
                if (divisionText.Equals(DivisionStorage[index],
                        StringComparison.OrdinalIgnoreCase) ||
                    divisionText.Equals(Divisions[index],
                        StringComparison.OrdinalIgnoreCase))
                    return Divisions[index];
        }
        if (field == "CompetesVs" && value is not string &&
            int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                out int division) && division is >= 0 and < 3)
            return Divisions[division];
        if (value is DateTime date)
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (value is bool yes) return yes ? "Yes" : "No";
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }

    internal static object ChoiceValue(DataColumn column, string field, string label,
        int index)
    {
        if (column.DataType == typeof(string))
            return field == "CompetesVs" ? DivisionStorage[index] : label;
        // Legacy PRW numeric enum storage uses zero-based codes. We keep the
        // storage type intact; unlike textual labels, numeric indices are not
        // to be changed without a database migration.
        if (IsLanguage(field) || field == "CompetesVs")
            return Convert.ChangeType(index, column.DataType, CultureInfo.InvariantCulture);
        return Convert.ChangeType(label, column.DataType, CultureInfo.InvariantCulture);
    }

    internal static bool TryManualValue(DataRow row, string field, string input,
        out object value, out string error)
    {
        value = DBNull.Value;
        error = "";
        if (!row.Table.Columns.Contains(field))
        {
            error = "This field is not available in the selected MDB.";
            return false;
        }

        var column = row.Table.Columns[field]!;
        string text = input.Trim();

        if (text.Length == 0)
        {
            if (column.AllowDBNull) return true;
            if (column.DataType == typeof(string))
            {
                value = "";
                return true;
            }
            error = field + " is required.";
            return false;
        }

        try
        {
            if (IsDate(field) || column.DataType == typeof(DateTime))
            {
                if (!DateTime.TryParseExact(text,
                        ["yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy"],
                        CultureInfo.InvariantCulture, DateTimeStyles.None,
                        out DateTime date))
                {
                    error = "Enter a valid date as YYYY-MM-DD.";
                    return false;
                }
                value = column.DataType == typeof(string)
                    ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : date;
            }
            else if (ManualNumbers.Contains(field) ||
                column.DataType.IsPrimitive && column.DataType != typeof(bool) ||
                column.DataType == typeof(decimal))
            {
                if (!decimal.TryParse(text, NumberStyles.Number,
                        CultureInfo.InvariantCulture, out decimal number))
                {
                    error = "Enter a numerical value for " + field + ".";
                    return false;
                }
                if (field.EndsWith("Condition", StringComparison.OrdinalIgnoreCase) &&
                    (number < 0 || number > 100))
                {
                    error = "Physical condition must be between 0 and 100.";
                    return false;
                }
                if (ManualNumbers.Contains(field) &&
                    !field.EndsWith("Condition", StringComparison.OrdinalIgnoreCase) &&
                    number < 0)
                {
                    error = "Measurements cannot be negative.";
                    return false;
                }
                value = column.DataType == typeof(string)
                    ? number.ToString(CultureInfo.InvariantCulture)
                    : Convert.ChangeType(number, column.DataType, CultureInfo.InvariantCulture);
            }
            else
            {
                value = column.DataType == typeof(string)
                    ? text : Convert.ChangeType(text, column.DataType,
                        CultureInfo.InvariantCulture);
            }

            if (value is string final && column.MaxLength > 0 &&
                final.Length > column.MaxLength)
            {
                error = "Maximum field length is " + column.MaxLength + ".";
                return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException
                                   or InvalidCastException or ArgumentException)
        {
            error = "The value is not valid for " + field + ".";
            return false;
        }
    }
}
