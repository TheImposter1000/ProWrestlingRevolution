using System.IO.Compression;
using System.Security.Cryptography;

namespace ProWrestlingRevolution;

/// <summary>
/// Installs the replacement PRW_Default(2).mdb into the application's own
/// System directory. Existing templates are backed up before upgrade; edits
/// made after upgrading to this version are not overwritten.
/// </summary>
internal static class PrwDefaultTemplate
{
    internal const string Filename = "PRWDefault.mdb";
    internal const int ExpectedLength = 434176;
    internal const string ExpectedSha256 =
        "e59c723d3919da1a9bb6927fea27d712feaa17815735ae29faa23b5087267a38";
    private const string VersionMarker = "PRWDefault.template-sha256.txt";

    internal static string EnsureInstalled(string systemDirectory)
    {
        string root = Path.GetFullPath(systemDirectory);
        Directory.CreateDirectory(root);
        string destination = Path.Combine(root, Filename);
        string marker = Path.Combine(root, VersionMarker);

        if (File.Exists(destination))
        {
            if (Checksum(destination).Equals(ExpectedSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(marker, ExpectedSha256);
                return destination;
            }

            // A local user edit to the current version is intentional.
            if (File.Exists(marker) &&
                File.ReadAllText(marker).Trim().Equals(ExpectedSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!AccessDatabase.LooksLikeMdb(destination))
                    throw new InvalidDataException(
                        "The customized System/PRWDefault.mdb is not an Access " +
                        "database. The game will not overwrite it.");
                return destination;
            }
        }

        string temporary = Path.Combine(root,
            ".prw-template-" + Guid.NewGuid().ToString("N") + ".mdb");
        try
        {
            byte[] packed = Convert.FromBase64String(PrwDefaultTemplateData.Encoded);
            using var compressed = new MemoryStream(packed, writable: false);
            using var decompressor = new BrotliStream(compressed, CompressionMode.Decompress);
            using (var output = new FileStream(temporary, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None))
                decompressor.CopyTo(output);

            if (new FileInfo(temporary).Length != ExpectedLength ||
                !AccessDatabase.LooksLikeMdb(temporary) ||
                !Checksum(temporary).Equals(ExpectedSha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "The replacement default database failed its integrity check.");

            if (File.Exists(destination))
            {
                // Preserve the previous original or user-modified template.
                // Nothing inside Databases, SaveGames, Skins or Pictures is touched.
                string backups = Path.Combine(root, "TemplateBackups");
                Directory.CreateDirectory(backups);
                string previous = Path.Combine(backups,
                    "PRWDefault-before-replacement-" +
                    DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" +
                    Guid.NewGuid().ToString("N")[..8] + ".mdb");
                File.Copy(destination, previous);
            }

            File.Move(temporary, destination, overwrite: true);
            File.WriteAllText(marker, ExpectedSha256);
            return destination;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string Checksum(string file)
    {
        using var input = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(input));
    }
}
