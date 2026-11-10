using System.IO.Compression;
using System.Security.Cryptography;

namespace ProWrestlingRevolution;

/// <summary>
/// Ships the original, unmodified PRWDefault.mdb as a compact embedded resource.
/// The running game only reads its installed copy under its own System folder.
/// No working-directory, source-tree or GitHub repository lookup is performed.
/// </summary>
internal static class PrwDefaultTemplate
{
    internal const string Filename = "PRWDefault.mdb";
    private const string OriginalSha256 =
        "b4f728d091aa4113d3ce03bbfc51de230df7845a4df69646177d857ae6054130";

    internal static string EnsureInstalled(string systemDirectory)
    {
        string destination = Path.Combine(Path.GetFullPath(systemDirectory), Filename);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
        {
            if (!AccessDatabase.LooksLikeMdb(destination))
                throw new InvalidDataException(
                    "The installed System/PRWDefault.mdb is not an Access database. " +
                    "Correct or move that file; the game will not overwrite it.");
            return destination;
        }

        string temporary = Path.Combine(Path.GetDirectoryName(destination)!,
            ".prw-template-" + Guid.NewGuid().ToString("N") + ".mdb");
        try
        {
            byte[] packed = Convert.FromBase64String(PrwDefaultTemplateData.Encoded);
            using var compressed = new MemoryStream(packed, writable: false);
            using var decompressor = new BrotliStream(compressed, CompressionMode.Decompress);
            using (var output = new FileStream(temporary, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None))
                decompressor.CopyTo(output);

            if (!AccessDatabase.LooksLikeMdb(temporary) ||
                !Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(temporary)))
                    .Equals(OriginalSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "The bundled default database did not pass its integrity check.");

            // Never replace a modified or already-installed user template.
            if (!File.Exists(destination))
                File.Move(temporary, destination);
            return destination;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
