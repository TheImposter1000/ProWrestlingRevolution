import com.healthmarketscience.jackcess.ColumnBuilder;
import com.healthmarketscience.jackcess.DataType;
import com.healthmarketscience.jackcess.Database;
import com.healthmarketscience.jackcess.DatabaseBuilder;
import com.healthmarketscience.jackcess.Row;
import com.healthmarketscience.jackcess.Table;
import com.healthmarketscience.jackcess.TableBuilder;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;

/** Builds a real Jet 4.0 MDB. No TEW data or commercial program files are copied. */
public final class EditorMdbGenerator {
    private static List<String[]> read(Path folder, String name) throws Exception {
        List<String[]> records = new ArrayList<>();
        List<String> lines = Files.readAllLines(folder.resolve(name), StandardCharsets.UTF_8);
        for (int i = 1; i < lines.size(); i++) {
            if (!lines.get(i).isBlank()) {
                records.add(lines.get(i).split("\\t", -1));
            }
        }
        return records;
    }

    private static ColumnBuilder text(String name, int length) {
        return new ColumnBuilder(name, DataType.TEXT).setLength(length);
    }

    public static void main(String[] args) throws Exception {
        String workspace = System.getenv("GITHUB_WORKSPACE");
        Path root = (workspace != null && !workspace.isBlank()
            ? Path.of(workspace) : Path.of("").toAbsolutePath()).normalize();
        Path manifest = root.resolve("Tools/EditorMdbGenerator/manifest");
        Path file = root.resolve("System/PRWEditor.mdb");
        Files.createDirectories(file.getParent());
        if (Files.exists(file)) Files.delete(file);

        try (Database db = DatabaseBuilder.create(Database.FileFormat.V2000, file.toFile())) {
            Table categories = new TableBuilder("tblEditorCategory")
                .addColumn(text("TableName", 80))
                .addColumn(text("DisplayName", 120))
                .addColumn(new ColumnBuilder("DisplayOrder", DataType.LONG))
                .addColumn(new ColumnBuilder("IsVisible", DataType.BOOLEAN))
                .addColumn(text("Description", 255))
                .toTable(db);
            for (String[] row : read(manifest, "categories.tsv")) {
                if (row.length != 5) throw new IllegalArgumentException("Category row size");
                categories.addRow(row[0], row[1], Integer.valueOf(row[2]),
                    Boolean.valueOf(row[3]), row[4]);
            }

            Table groups = new TableBuilder("tblEditorGroup")
                .addColumn(text("TableName", 80))
                .addColumn(text("GroupName", 80))
                .addColumn(text("DisplayName", 120))
                .addColumn(new ColumnBuilder("DisplayOrder", DataType.LONG))
                .addColumn(new ColumnBuilder("IsVisible", DataType.BOOLEAN))
                .toTable(db);
            for (String[] row : read(manifest, "groups.tsv")) {
                if (row.length != 5) throw new IllegalArgumentException("Group row size");
                groups.addRow(row[0], row[1], row[2], Integer.valueOf(row[3]),
                    Boolean.valueOf(row[4]));
            }

            Table fields = new TableBuilder("tblEditorField")
                .addColumn(text("TableName", 80))
                .addColumn(text("FieldName", 100))
                .addColumn(text("DisplayLabel", 120))
                .addColumn(new ColumnBuilder("DisplayOrder", DataType.LONG))
                .addColumn(new ColumnBuilder("IsVisible", DataType.BOOLEAN))
                .addColumn(new ColumnBuilder("IsReadOnly", DataType.BOOLEAN))
                .addColumn(text("GroupName", 80))
                .toTable(db);
            for (String[] row : read(manifest, "fields.tsv")) {
                if (row.length != 7) throw new IllegalArgumentException("Field row size");
                fields.addRow(row[0], row[1], row[2], Integer.valueOf(row[3]),
                    Boolean.valueOf(row[4]), Boolean.valueOf(row[5]), row[6]);
            }

            Table settings = new TableBuilder("tblEditorSettings")
                .addColumn(text("SettingKey", 80))
                .addColumn(text("SettingValue", 255))
                .toTable(db);
            for (String[] row : read(manifest, "settings.tsv")) {
                if (row.length != 2) throw new IllegalArgumentException("Settings row size");
                settings.addRow(row[0], row[1]);
            }
        }

        // Open the generated file independently and confirm table and row counts.
        try (Database db = DatabaseBuilder.open(file.toFile())) {
            if (db.getTableNames().size() != 4)
                throw new IllegalStateException("PRWEditor must contain exactly four tables");
            int count = 0;
            for (Row ignored : db.getTable("tblEditorCategory")) count++;
            if (count != 14) throw new IllegalStateException("Editor categories: " + count);
            count = 0;
            for (Row ignored : db.getTable("tblEditorField")) count++;
            if (count < 200) throw new IllegalStateException("Missing editor field definitions");
            System.out.println("Generated " + file + " with 14 categories and " +
                               count + " display field definitions");
        }
    }
}
