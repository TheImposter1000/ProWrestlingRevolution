using System.Data;
using System.Data.OleDb;

namespace ProWrestlingRevolution;

/// <summary>
/// Native PRWDefault.mdb schema, transcribed from the uploaded database.
/// This is independent of the protected TEW IX database format.
/// Column names are deliberately exact so the reader can detect schema drift.
/// </summary>
public static class PrwSchema
{
    public const string WorkerTable = "tblWorker";

    public sealed record Table(string Name, string[] KeyColumns, string[] Columns);

    public sealed record Relationship(
        string ParentTable, string ParentColumn, string ChildTable, string ChildColumn);

    public sealed record Inspection(bool IsPrwDatabase, IReadOnlyList<string> Problems)
    {
        public bool IsValid => IsPrwDatabase && Problems.Count == 0;
    }

    public static IReadOnlyList<Table> Tables { get; } =
    [
        new("tblWorker", ["UID"],
        [
            "UID", "Name", "ShortName", "Picture", "BiographyType", "Born",
            "SetToDebut", "DeathDate", "Regenerates", "GenderID", "Pronouns",
            "Sexuality", "Relationship", "Race", "Nationality", "BasedIn",
            "GameStatus", "InRingStatus", "Celebrity", "Style", "BodyType",
            "HeightInches", "WeightPounds", "MinWeightPounds", "MaxWeightPounds",
            "LoyaltyWorkerUID", "LoyaltyType", "Freelance", "UserCharacter",
            "CompetesVs", "RoleWrestler", "RolePartTimeWrestler", "RoleManager",
            "RoleOnScreenPersonality", "RolePlayByPlay", "RoleColour",
            "RoleReferee", "RoleProducer", "DefaultMoveSet", "FaceGimmick",
            "FaceGimmickBasis", "HeelGimmick", "HeelGimmickBasis", "Mask",
            "Hair", "MaturityAge", "DeclineAge", "RelevancyAge",
            "RetirementAge", "CareerGoal", "ActiveUSA", "ActiveCanada",
            "ActiveMexico", "ActiveBritishIsles", "ActiveJapan",
            "ActiveEurope", "ActiveOceania", "ActiveIndia",
            "LanguageEnglish", "LanguageJapanese", "LanguageFrench",
            "LanguageSpanish", "LanguageMediterranean", "LanguageGermanic",
            "LanguageSlavic", "LanguageHindi"
        ]),
        new("tblWorkerBio", ["WorkerUID"], ["WorkerUID", "Biography"]),
        new("tblWorkerBusiness", ["WorkerUID"],
        [
            "WorkerUID", "BusinessReputation", "MinimumOwnerAge", "PersonalWealth",
            "MinimumCompanySize", "MaximumCompanySize", "RosterSize",
            "OtherCompanies", "Patience", "RosterTurnover", "Finance",
            "StylePreference", "OwnerSpecialism", "BookingReputation",
            "BookingSkill", "MinimumBookerAge", "HiringPreference",
            "FiringPreference", "TitleReigns", "BookerSpecialism"
        ]),
        new("tblWorkerOver", ["WorkerUID", "RegionCode"],
            ["WorkerUID", "RegionCode", "Popularity"]),
        new("tblWorkerPhysical", ["WorkerUID"],
            ["WorkerUID", "HeadCondition", "BodyCondition",
             "ArmsCondition", "LegsCondition"]),
        new("tblWorkerSkills", ["WorkerUID"],
        [
            "WorkerUID", "Brawling", "Puroresu", "Hardcore", "Technical",
            "Aerial", "Flashiness", "Psychology", "Experience",
            "Respect", "Reputation", "Charisma", "Microphone", "Acting",
            "StarQuality", "SexAppeal", "Menace", "Basics", "Selling",
            "Consistency", "Safety", "Stamina", "Athleticism", "Power",
            "Toughness", "Resilience", "PlayByPlay", "Colour", "Refereeing",
            "PotentialPrimary", "PotentialMental", "PotentialPerformance",
            "PotentialFundamentals", "PotentialPhysical",
            "PotentialPlayByPlay", "PotentialColour", "PotentialRefereeing"
        ]),
        new("tblContract", ["UID"],
        [
            "UID", "WorkerUID", "FedUID", "EmployerInitials", "Name",
            "ShortName", "Picture", "ContractType", "Exclusive", "Touring",
            "PayBasis", "Wage", "Downside", "IronClad", "StartDate",
            "ExpirationDate", "TermCode", "AppearanceLimit", "OnLoan",
            "InDevelopmental", "Alignment", "CompetesIn", "Brands",
            "Divisions", "ManagerWorkerUID", "RosterUsage", "IntendedRole",
            "CompanyPush", "Momentum", "GimmickName", "GimmickBasis",
            "GimmickRating", "MoveSet", "MaskRespect", "RoleWrestler",
            "RolePartTimeWrestler", "RoleManager", "RoleOnScreenPersonality",
            "RolePlayByPlay", "RoleColour", "RoleReferee", "RoleProducer"
        ]),
        new("tblContractBio", ["ContractUID"], ["ContractUID", "Biography"]),
        new("tblWorkerAttributes", ["UID"],
            ["UID", "WorkerUID", "AttributeName", "SourceTEWCode",
             "Hidden", "Permanent"]),
        new("tblAlternative", ["UID"],
        [
            "UID", "WorkerUID", "RecordName", "CompanyScope", "FedUID",
            "CompanyInitials", "RegionCode", "Usage", "Name", "ShortName",
            "Picture", "Biography", "MoveSet", "Gimmick", "GimmickBasis",
            "Mask", "MinAge", "MaxAge"
        ])
    ];

    // Relationships explicitly present in the uploaded MDB. Not every UID-like
    // field is a declared foreign key; do not infer additional relationships.
    public static IReadOnlyList<Relationship> Relationships { get; } =
    [
        new("tblWorker", "UID", "tblWorkerBio", "WorkerUID"),
        new("tblWorker", "UID", "tblWorkerBusiness", "WorkerUID"),
        new("tblWorker", "UID", "tblWorkerOver", "WorkerUID"),
        new("tblWorker", "UID", "tblWorkerPhysical", "WorkerUID"),
        new("tblWorker", "UID", "tblWorkerSkills", "WorkerUID"),
        new("tblWorker", "UID", "tblWorkerAttributes", "WorkerUID"),
        new("tblWorker", "UID", "tblAlternative", "WorkerUID"),
        new("tblWorker", "UID", "tblContract", "WorkerUID"),
        new("tblContract", "UID", "tblContractBio", "ContractUID")
    ];

    public static Inspection Inspect(string file, string? password)
    {
        using var connection = AccessDatabase.Open(file, password);
        return Inspect(connection);
    }

    internal static Inspection Inspect(OleDbConnection connection)
    {
        var tableNames = connection.GetSchema("Tables").Rows.Cast<DataRow>()
            .Where(row => string.Equals(Convert.ToString(row["TABLE_TYPE"]),
                "TABLE", StringComparison.OrdinalIgnoreCase))
            .Select(row => Convert.ToString(row["TABLE_NAME"]) ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!tableNames.Contains(WorkerTable))
            return new(false, ["The native tblWorker table is missing."]);

        var problems = new List<string>();
        foreach (var table in Tables)
        {
            if (!tableNames.Contains(table.Name))
            {
                problems.Add("Missing table: " + table.Name);
                continue;
            }

            try
            {
                using var command = new OleDbCommand(
                    "SELECT * FROM [" + table.Name + "] WHERE 1 = 0", connection);
                using var reader = command.ExecuteReader(CommandBehavior.SchemaOnly)
                    ?? throw new InvalidDataException("Could not read the column list.");

                var actual = Enumerable.Range(0, reader.FieldCount)
                    .Select(reader.GetName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var name in table.Columns)
                    if (!actual.Contains(name))
                        problems.Add(table.Name + " is missing column " + name);
            }
            catch (Exception ex) when (ex is OleDbException or InvalidDataException)
            {
                problems.Add("Cannot inspect " + table.Name + ": " + ex.Message);
            }
        }
        return new(true, problems);
    }

    /// <summary>
    /// Checks the in-code map itself without depending on ACE or the operating system.
    /// Used by the existing smoke-test runner.
    /// </summary>
    public static void AssertDefinitionIntegrity()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in Tables)
        {
            if (!names.Add(table.Name))
                throw new InvalidDataException("Duplicate schema table: " + table.Name);
            if (table.Columns.Length != table.Columns.Distinct(
                    StringComparer.OrdinalIgnoreCase).Count())
                throw new InvalidDataException("Duplicate columns: " + table.Name);
            if (table.KeyColumns.Any(key => !table.Columns.Contains(
                    key, StringComparer.OrdinalIgnoreCase)))
                throw new InvalidDataException("Unmapped key: " + table.Name);
        }

        foreach (var relationship in Relationships)
        {
            var parent = Tables.Single(t => t.Name == relationship.ParentTable);
            var child = Tables.Single(t => t.Name == relationship.ChildTable);
            if (!parent.Columns.Contains(relationship.ParentColumn) ||
                !child.Columns.Contains(relationship.ChildColumn))
                throw new InvalidDataException("Invalid relation: " +
                    relationship.ParentTable + " -> " + relationship.ChildTable);
        }
    }
}
