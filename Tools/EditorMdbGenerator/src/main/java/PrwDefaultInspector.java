import com.healthmarketscience.jackcess.Column;
import com.healthmarketscience.jackcess.Database;
import com.healthmarketscience.jackcess.DatabaseBuilder;
import com.healthmarketscience.jackcess.Row;
import com.healthmarketscience.jackcess.Table;

import java.io.File;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.LinkedHashSet;
import java.util.Set;

/**
 * Read-only diagnostic of the bundled native PRWDefault.mdb worker constraints.
 * Keeps testing grounded in Access field types instead of assumptions about TEW.
 */
public final class PrwDefaultInspector {
    public static void main(String[] args) throws Exception {
        if (args.length != 1)
            throw new IllegalArgumentException("Pass a path to PRWDefault.mdb.");
        Path input = Path.of(args[0]);
        if (!Files.exists(input))
        {
            String root = System.getenv("GITHUB_WORKSPACE");
            if (root != null) input = Path.of(root).resolve(args[0]);
        }
        if (!Files.exists(input))
            throw new IllegalArgumentException("Native MDB not found: " + input);
        try (Database db = DatabaseBuilder.open(input.toFile())) {
            check(db, "tblWorker", new String[] {
                "Born", "SetToDebut", "DeathDate", "HeightInches",
                "WeightPounds", "FaceGimmick", "HeelGimmick",
                "LoyaltyWorkerUID", "CompetesVs", "LanguageEnglish", "LanguageJapanese",
                "RoleWrestler", "RolePartTimeWrestler"
            });
            check(db, "tblWorkerPhysical", new String[] {
                "HeadCondition", "BodyCondition", "ArmsCondition", "LegsCondition"
            });
            check(db, "tblWorkerSkills", new String[] {
                "Brawling", "Hardcore", "Charisma", "PotentialRefereeing"
            });
            check(db, "tblFed", new String[] { "UID", "Name" });
        }
    }

    private static void check(Database db, String tableName, String[] fields)
        throws Exception
    {
        Table table = db.getTable(tableName);
        if (table == null)
            throw new IllegalStateException("Native table missing: " + tableName);
        for (String field : fields) {
            Column column = table.getColumn(field);
            if (column == null)
                throw new IllegalStateException("Native column missing: " +
                    tableName + "." + field);
            Set<String> samples = new LinkedHashSet<>();
            for (Row row : table) {
                Object raw = row.get(field);
                if (raw != null) samples.add(raw.toString());
                if (samples.size() >= 8) break;
            }
            System.out.println("NATIVE FIELD " + tableName + "." + field +
                " Type=" + column.getType() +
                " SampleValues=" + samples);
        }
    }
}
