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
            String[] requiredTables = {
                "tblWorker","tblWorkerBio","tblWorkerBusiness","tblWorkerOver",
                "tblWorkerPhysical","tblWorkerSkills","tblContract","tblContractBio",
                "tblWorkerAttributes","tblAlternative"
            };
            if (reopened.getTableNames().size() != 10) {
                throw new IOException("Expected exactly 10 user tables, found " + reopened.getTableNames());
            }
            for (String name : requiredTables) {
                if (reopened.getTable(name) == null)
                    throw new IOException("Missing required table: " + name);
            }
            if (reopened.getTable("tblWorker").getRowCount() != 1 ||
                reopened.getTable("tblContract").getRowCount() != 1 ||
                reopened.getTable("tblWorkerAttributes").getRowCount() != 1 ||
                reopened.getTable("tblAlternative").getRowCount() != 1) {
                throw new IOException("Missing test records");
            }
            System.out.println("VERIFIED native Microsoft Access 2000 MDB: " + out);
            System.out.println("Tables created: " + tableCount);
            System.out.println("Relationships created: " + linkedCount + "/" + relationships.size());
            System.out.println("Workers: " + reopened.getTable("tblWorker").getRowCount() +
                "; Contracts: " + reopened.getTable("tblContract").getRowCount());
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
        int jayden = put(db, "tblWorker", "UID",
            "Name","Jayden Cole","ShortName","Cole",
            "BiographyType","Organic",
            "Born",day(1995,8,20),"SetToDebut",day(2013,1,1),
            "GenderID","Cisgender Male","Pronouns","He-Him","Sexuality","Heterosexual",
            "Race","White","Nationality","American","BasedIn","Mid Atlantic",
            "GameStatus","Active","InRingStatus","Active","Celebrity",false,
            "Style","Brawler","BodyType","Athletic","HeightInches",74,
            "WeightPounds",225,"MinWeightPounds",210,"MaxWeightPounds",245,
            "Freelance",false,"UserCharacter",true,"CompetesVs","Men's Division Only",
            "RoleWrestler",true,"FaceGimmick","Fighting Spirit",
            "FaceGimmickBasis","Realistic","HeelGimmick","The Vanguard",
            "HeelGimmickBasis","Swagger","Hair",true,
            "ActiveUSA",true,"ActiveCanada",true,
            "LanguageEnglish",3);

        put(db,"tblWorkerBio","WorkerUID",
            "WorkerUID",jayden,
            "Biography","Fictional PRW development worker for testing the native MDB schema.");
        put(db,"tblWorkerBusiness","WorkerUID",
            "WorkerUID",jayden,"BusinessReputation",10,
            "BookingReputation",20,"BookingSkill",35,
            "MinimumOwnerAge",40,"MinimumBookerAge",35);
        put(db,"tblWorkerPhysical","WorkerUID",
            "WorkerUID",jayden,"HeadCondition",100,"BodyCondition",100,
            "ArmsCondition",100,"LegsCondition",100);
        put(db,"tblWorkerOver","WorkerUID",
            "WorkerUID",jayden,"RegionCode","Mid Atlantic","Popularity",55);
        put(db,"tblWorkerOver","WorkerUID",
            "WorkerUID",jayden,"RegionCode","Tri State","Popularity",32);
        put(db,"tblWorkerSkills","WorkerUID",
            "WorkerUID",jayden,"Brawling",60,"Puroresu",40,
            "Technical",55,"Psychology",68,"Charisma",72,"Microphone",70,
            "Basics",65,"Selling",67,"Safety",75,"Stamina",77,
            "PotentialPrimary",70,"PotentialMental",72,
            "PotentialPerformance",80,"PotentialFundamentals",76,
            "PotentialPhysical",75);
        int contract = put(db,"tblContract","UID",
            "WorkerUID",jayden,"EmployerInitials","DNA",
            "Name","Jayden Cole","ShortName","Cole",
            "ContractType","Handshake","Exclusive",false,"Touring",false,
            "PayBasis","Per Show","StartDate",day(2026,1,1),
            "Alignment","Babyface","CompetesIn","Men's Division",
            "RoleWrestler",true,"CompanyPush","Major Star",
            "Momentum",60,"GimmickName","Fighting Spirit",
            "GimmickBasis","Realistic","MaskRespect",0);
        put(db,"tblContractBio","ContractUID",
            "ContractUID",contract,
            "Biography","Company-specific biography can be stored here without overwriting the worker's main biography.");
        put(db,"tblWorkerAttributes","UID",
            "WorkerUID",jayden,"AttributeName","Professional",
            "SourceTEWCode",1,"Hidden",false,"Permanent",false);
        put(db,"tblAlternative","UID",
            "WorkerUID",jayden,"RecordName","Jayden Cole (DNA)",
            "CompanyScope","In","CompanyInitials","DNA",
            "Usage",100,"Name","The Vanguard","ShortName","Vanguard",
            "Gimmick","The Vanguard","GimmickBasis","Swagger",
            "MinAge",18,"MaxAge",100);
    }
}
