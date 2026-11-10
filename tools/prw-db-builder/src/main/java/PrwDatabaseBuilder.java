import com.healthmarketscience.jackcess.ColumnBuilder;
import com.healthmarketscience.jackcess.DataType;
import com.healthmarketscience.jackcess.Database;
import com.healthmarketscience.jackcess.DatabaseBuilder;
import com.healthmarketscience.jackcess.RelationshipBuilder;
import com.healthmarketscience.jackcess.Table;
import com.healthmarketscience.jackcess.TableBuilder;

import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.Date;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public final class PrwDatabaseBuilder {
    private PrwDatabaseBuilder() {}

    public static void main(String[] args) throws Exception {
        if (args.length != 1) {
            throw new IllegalArgumentException("Expected output PRW_Default.mdb path");
        }
        File out = new File(args[0]).getCanonicalFile();
        File parent = out.getParentFile();
        if (parent != null && !parent.exists() && !parent.mkdirs()) {
            throw new IOException("Cannot create output directory");
        }

        Path schemaPath = Path.of("schema.psv");
        if (!Files.exists(schemaPath)) {
            throw new IOException("schema.psv is missing; run from the builder directory");
        }
        List<String> lines = Files.readAllLines(schemaPath, StandardCharsets.UTF_8);
        List<String[]> relationships = new ArrayList<>();
        int tableCount = 0;
        int linkedCount = 0;

        try (Database db = DatabaseBuilder.create(Database.FileFormat.V2000, out)) {
            for (String line : lines) {
                line = line.trim();
                if (line.isEmpty() || line.startsWith("#")) continue;
                String[] parts = line.split("\\|", -1);
                if (parts[0].equals("FK")) {
                    if (parts.length != 5) throw new IOException("Malformed FK: " + line);
                    relationships.add(parts);
                    continue;
                }
                if (parts.length != 3) throw new IOException("Malformed table: " + line);
                TableBuilder builder = new TableBuilder(parts[0]);
                for (String column : parts[2].split(",")) {
                    String[] columnParts = column.trim().split(":");
                    if (columnParts.length < 2 || columnParts.length > 3) {
                        throw new IOException("Malformed column: " + column);
                    }
                    DataType type = switch (columnParts[1]) {
                        case "L" -> DataType.LONG;
                        case "T" -> DataType.TEXT;
                        case "M" -> DataType.MEMO;
                        case "D" -> DataType.SHORT_DATE_TIME;
                        case "Y" -> DataType.BOOLEAN;
                        case "F" -> DataType.DOUBLE;
                        case "C" -> DataType.MONEY;
                        default -> throw new IOException("Unknown column type " + columnParts[1]);
                    };
                    ColumnBuilder cb = new ColumnBuilder(columnParts[0], type);
                    if (columnParts.length == 3) {
                        if (columnParts[2].equals("A")) {
                            cb.setAutoNumber(true);
                        } else if (type == DataType.TEXT) {
                            cb.setLength(Integer.parseInt(columnParts[2]));
                        } else {
                            throw new IOException("Unexpected column modifier " + column);
                        }
                    }
                    builder.addColumn(cb);
                }
                builder.setPrimaryKey(parts[1].split(","));
                builder.toTable(db);
                tableCount++;
            }
            for (String[] fk : relationships) {
                try {
                    new RelationshipBuilder(fk[1], fk[2])
                        .addColumns(fk[3], fk[4])
                        .setReferentialIntegrity()
                        .toRelationship(db);
                    linkedCount++;
                } catch (Exception e) {
                    // The database and its real PKs remain usable if Jet rejects a
                    // supplementary relationship. The warnings are visible in CI.
                    System.err.println("RELATIONSHIP WARNING " + fk[1] + "." + fk[3] +
                                       " to " + fk[2] + "." + fk[4] + ": " + e);
                }
            }
            seed(db);
            db.flush();
        }

        try (Database reopened = DatabaseBuilder.open(out)) {
            int workers = reopened.getTable("Worker").getRowCount();
            int companies = reopened.getTable("Promotion").getRowCount();
            int contracts = reopened.getTable("WorkerContract").getRowCount();
            int universe = reopened.getTable("UniverseInfo").getRowCount();
            if (reopened.getTableNames().size() < 36 ||
                workers != 2 || companies != 2 || contracts != 2 || universe != 1) {
                throw new IOException("MDB verification failed: tables=" +
                    reopened.getTableNames().size() + ", workers=" + workers +
                    ", companies=" + companies + ", contracts=" + contracts);
            }
            System.out.println("VERIFIED native Microsoft Access 2000 MDB: " + out);
            System.out.println("Tables created: " + tableCount);
            System.out.println("Relationships created: " + linkedCount + "/" + relationships.size());
            System.out.println("Workers: " + workers + "; Promotions: " + companies +
                               "; Contracts: " + contracts);
            System.out.println("Bytes: " + out.length());
        }
    }

    private static Date day(int year, int month, int day) {
        Calendar calendar = Calendar.getInstance();
        calendar.clear();
        calendar.set(year, month - 1, day);
        return calendar.getTime();
    }

    private static int put(Database db, String tableName, String pk, Object... values)
            throws IOException {
        if (values.length % 2 != 0) {
            throw new IllegalArgumentException("Field name/value pairs required");
        }
        Map<String,Object> row = new LinkedHashMap<>();
        for (int i = 0; i < values.length; i += 2) {
            row.put((String)values[i], values[i + 1]);
        }
        Table table = db.getTable(tableName);
        if (table == null) throw new IOException("Table not found: " + tableName);
        table.addRowFromMap(row);
        Object id = row.get(pk);
        if (!(id instanceof Number)) {
            throw new IOException("No generated/explicit key for " + tableName + "." + pk);
        }
        return ((Number)id).intValue();
    }

    private static void seed(Database db) throws IOException {
        put(db,"SchemaVersion","VersionID",
            "VersionNumber","0.1.0","AppliedDate",day(2026,11,10),
            "Description","Initial native PRW worker-domain test database");
        put(db,"UniverseInfo","UniverseID",
            "Name","PRW Default Test Universe",
            "Description","Native PRW development universe with fictional worker and promotion records.",
            "StartDate",day(2026,11,10),"SchemaRevision","0.1.0","IsTemplate",true);

        int power = put(db,"Promotion","PromotionID",
            "Name","POWER Wrestling","Initials","POWER","Active",true,"CompanySize",25);
        int dna = put(db,"Promotion","PromotionID",
            "Name","DNA Wrestling","Initials","DNA","Active",true,"CompanySize",12);
        int midAtlantic = put(db,"Region","RegionID",
            "Name","Mid Atlantic","RegionTypeCode","TEWStyleRegion","CountryCode","US");
        int triState = put(db,"Region","RegionID",
            "Name","Tri State","RegionTypeCode","TEWStyleRegion","CountryCode","US");

        int jayden = put(db,"Worker","WorkerID",
            "Name","Jayden Cole","ShortName","Cole",
            "BirthDate",day(1995,8,20),"DebutDate",day(2013,1,1),
            "HeightInches",74,"WeightPounds",225,
            "NationalityCode","US","BodyTypeCode","Athletic",
            "BusinessStatusCode","Active","InRingStatusCode","Active",
            "BasedInRegionID",midAtlantic,"UserCharacterEligible",true);
        int taylor = put(db,"Worker","WorkerID",
            "Name","Taylor Vale","ShortName","Vale",
            "BirthDate",day(1987,4,11),"DebutDate",day(2006,7,1),
            "HeightInches",70,"WeightPounds",178,
            "NationalityCode","US","BodyTypeCode","Average",
            "BusinessStatusCode","Active","InRingStatusCode","Retired",
            "BasedInRegionID",triState,"UserCharacterEligible",true);

        put(db,"WorkerBiography","WorkerID","WorkerID",jayden,
            "Biography","Fictional PRW worker used for database and simulation tests.");
        put(db,"WorkerBiography","WorkerID","WorkerID",taylor,
            "Biography","Fictional PRW manager and booker used for database tests.");
        put(db,"WorkerPossibleRole","WorkerID","WorkerID",jayden,"RoleCode","Wrestler");
        put(db,"WorkerPossibleRole","WorkerID","WorkerID",taylor,"RoleCode","Manager");
        put(db,"WorkerPossibleRole","WorkerID","WorkerID",taylor,"RoleCode","Producer");
        put(db,"WorkerPhysical","WorkerID",
            "WorkerID",jayden,"HeadHealth",100,"BodyHealth",100,
            "LeftArmHealth",100,"RightArmHealth",100,
            "LeftLegHealth",100,"RightLegHealth",100);
        put(db,"WorkerPhysical","WorkerID",
            "WorkerID",taylor,"HeadHealth",100,"BodyHealth",100,
            "LeftArmHealth",100,"RightArmHealth",100,
            "LeftLegHealth",100,"RightLegHealth",100);
        put(db,"WorkerSkill","WorkerID",
            "WorkerID",jayden,"Brawling",60,"Psychology",68,"Charisma",72,
            "Microphone",70,"Basics",65,"Selling",67,"Safety",75);
        put(db,"WorkerSkill","WorkerID",
            "WorkerID",taylor,"Brawling",60,"Psychology",73,"Charisma",66,
            "Microphone",62,"Basics",63,"Selling",68,"Safety",78);
        put(db,"WorkerBusinessSkill","WorkerID",
            "WorkerID",taylor,"OwnerSkill",58,"BookingSkill",84,
            "BusinessReputation",69);
        put(db,"WorkerPopularity","WorkerPopularityID",
            "WorkerID",jayden,"RegionID",midAtlantic,"PopularityValue",55);
        put(db,"WorkerPopularity","WorkerPopularityID",
            "WorkerID",jayden,"RegionID",triState,"PopularityValue",32);
        put(db,"WorkerPopularity","WorkerPopularityID",
            "WorkerID",taylor,"RegionID",midAtlantic,"PopularityValue",25);

        int professional = put(db,"AttributeDefinition","AttributeDefinitionID",
            "Name","Professional","CategoryCode","Personality","IsActive",true);
        put(db,"WorkerAttribute","WorkerAttributeID",
            "WorkerID",jayden,"AttributeDefinitionID",professional,
            "Hidden",false,"Permanent",false);
        put(db,"ExternalCodeMap","ExternalCodeMapID",
            "SourceSystem","TEW IX","SourceVersion","9",
            "SourceField","tblAttribute.Attribute","ExternalCode","1",
            "TargetEntityType","AttributeDefinition","TargetCodeOrID","Professional",
            "Verified",true,"VerificationNote","Verified in TEW Universe code label catalog.");

        int ego = put(db,"WorkerAlterEgo","AlterEgoID",
            "WorkerID",jayden,"Name","The Vanguard","UsageWeight",100);
        put(db,"AlterEgoEligibility","EligibilityID",
            "AlterEgoID",ego,"ScopeCode","SpecificPromotion","PromotionID",dna);

        int contractPower = put(db,"WorkerContract","ContractID",
            "WorkerID",jayden,"PromotionID",power,"ContractTypeCode","Written",
            "Exclusive",false,"Touring",false,"PayBasisCode","Monthly",
            "StartDate",day(2026,1,1),"AlignmentCode","Babyface",
            "CompetesInCode","MensDivision");
        int contractDna = put(db,"WorkerContract","ContractID",
            "WorkerID",jayden,"PromotionID",dna,"ContractTypeCode","Handshake",
            "Exclusive",false,"Touring",false,"PayBasisCode","PerShow",
            "StartDate",day(2026,9,1),"AlignmentCode","Heel",
            "CompetesInCode","MensDivision");
        put(db,"ContractRole","ContractID","ContractID",contractPower,"RoleCode","Wrestler");
        put(db,"ContractRole","ContractID","ContractID",contractDna,"RoleCode","Wrestler");
        put(db,"WorkerCompanyPresentation","ContractID",
            "ContractID",contractPower,"ActiveRingName","Jayden Cole",
            "GimmickTypeCode","Realistic","GimmickName","Jayden Cole","MaskRespect",0);
        put(db,"WorkerCompanyPresentation","ContractID",
            "ContractID",contractDna,"ActiveRingName","The Vanguard",
            "AlterEgoID",ego,"GimmickTypeCode","Swagger","GimmickName","The Vanguard");
        put(db,"WorkerCompanyStanding","StandingID",
            "WorkerID",jayden,"PromotionID",power,
            "CompanyPushCode","MajorStar","MomentumValue",65);
        put(db,"WorkerCompanyStanding","StandingID",
            "WorkerID",jayden,"PromotionID",dna,
            "CompanyPushCode","Recognizable","MomentumValue",34);
        put(db,"WorkerManagerAssignment","AssignmentID",
            "PromotionID",power,"WrestlerWorkerID",jayden,
            "ManagerWorkerID",taylor,"StartDate",day(2026,1,1));

        put(db,"WorkerJobHistory","JobHistoryID",
            "WorkerID",jayden,"PromotionID",power,"EmployerInitials","POWER",
            "RecordName","POWER 2020 onward","StartDate",day(2020,1,1));
        put(db,"WorkerJobHistory","JobHistoryID",
            "WorkerID",jayden,"PromotionID",dna,"EmployerInitials","DNA",
            "RecordName","DNA 2018-2019","StartDate",day(2018,5,1),
            "EndDate",day(2019,7,1));
        put(db,"WorkerBusinessHistory","BusinessHistoryID",
            "WorkerID",taylor,"PromotionID",power,
            "EmployerInitials","POWER","PositionCode","HeadBooker",
            "StartDate",day(2024,2,1));

        int legacy = put(db,"WorkerLegacy","LegacyID",
            "LegacyName","Masked Aurora","NextGenerationNumber",2,
            "BloodRelativePolicyCode","DoesNotMatter","MaskRequired",true);
        put(db,"WorkerLegacyMembership","MembershipID",
            "LegacyID",legacy,"WorkerID",taylor,"GenerationNumber",1);
    }
}
