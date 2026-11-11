using System.Reflection;

namespace ProWrestlingRevolution;

/// <summary>
/// Installs the separate editor metadata MDB from an embedded application
/// resource. It never replaces universe databases, saves or a custom edit
/// to System/PRWEditor.mdb.
/// </summary>
internal static class EditorMetadataTemplate
{
    internal const string Filename = "PRWEditor.mdb";

    internal static string? EnsureInstalled(string systemDirectory)
    {
        string destination = Path.Combine(Path.GetFullPath(systemDirectory), Filename);
        if (File.Exists(destination))
        {
            if (!AccessDatabase.LooksLikeMdb(destination))
                throw new InvalidDataException(
                    "System/PRWEditor.mdb is not a valid Access file. " +
                    "The game will not overwrite your custom editor metadata.");
            return destination;
        }

        // Local development builds without the Maven-generated resource keep
        // the previous native table/column labels until that resource is built.
        using Stream? embedded = typeof(EditorMetadataTemplate).Assembly
            .GetManifestResourceStream(Filename);
        if (embedded is null) return null;

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew))
                embedded.CopyTo(output);
            if (!AccessDatabase.LooksLikeMdb(temporary))
                throw new InvalidDataException(
                    "The bundled editor metadata database is not a valid MDB.");
            if (!File.Exists(destination)) File.Move(temporary, destination);
            return destination;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
