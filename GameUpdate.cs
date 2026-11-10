using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Forms;

namespace ProWrestlingRevolution;

/// <summary>
/// Portable Windows updater. All network/download work happens while the game
/// is running; a temporary copy of the CURRENT executable installs the already
/// verified NEW executable after the running game closes.
/// Data folders (Databases, Skins, Pictures, System) are never read or written
/// by this installer.
/// </summary>
internal static class GameUpdate
{
    private const string ReleaseApi =
        "https://api.github.com/repos/TheImposter1000/ProWrestlingRevolution/releases/latest";
    private const string AssetName = "ProWrestlingRevolution-Windows-x64.zip";
    private const long MaxDownloadBytes = 300L * 1024 * 1024;
    private const long MaxExeBytes = 300L * 1024 * 1024;

    private static readonly HttpClient Http = CreateClient();

    internal static Version InstalledVersion =>
        typeof(LauncherForm).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

    internal static string DisplayVersion =>
        $"{InstalledVersion.Major}.{InstalledVersion.Minor}.{InstalledVersion.Build}";

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ProWrestlingRevolution-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private sealed record Release(Version Version, string Tag, Uri DownloadUrl,
        long Size, string Sha256);

    internal static async Task CheckAndInstallAsync(Form owner, Button button)
    {
        if (!button.Enabled) return;
        string priorLabel = button.Text;
        string? staging = null;
        bool handedOff = false;
        button.Enabled = false;

        try
        {
            button.Text = "Checking...";
            var release = await FetchLatestAsync();
            if (release.Version <= InstalledVersion)
            {
                MessageBox.Show(owner,
                    $"Pro Wrestling Revolution v{DisplayVersion} is up to date.",
                    "Check for Updates", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var choice = MessageBox.Show(owner,
                $"Installed version: v{DisplayVersion}\n" +
                $"Available version: {release.Tag}\n\n" +
                "Download and install this update now?\n\n" +
                "The game will close and restart. Your databases, saved careers, " +
                "skins, pictures, and system settings will not be changed.",
                "Pro Wrestling Revolution Update",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (choice != DialogResult.Yes) return;

            var runningExe = Environment.ProcessPath
                ?? throw new InvalidOperationException("Cannot identify the running application.");
            if (!Path.GetFileName(runningExe).Equals(
                "ProWrestlingRevolution.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Run ProWrestlingRevolution.exe directly to install an update.");

            staging = Path.Combine(Path.GetTempPath(),
                "PWR-Update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);

            button.Text = "Downloading...";
            var archivePath = Path.Combine(staging, "release.zip");
            await DownloadVerifiedAsync(release, archivePath);

            button.Text = "Preparing...";
            var candidate = Path.Combine(staging, "new-version.exe");
            ExtractApplication(archivePath, candidate);

            // Ensure the executable embedded in the ZIP is actually the
            // version advertised by the publisher's release metadata.
            var candidateFileVersion = FileVersionInfo.GetVersionInfo(candidate).FileVersion;
            if (!Version.TryParse(candidateFileVersion, out var embeddedVersion) ||
                embeddedVersion < release.Version)
                throw new InvalidDataException(
                    "The downloaded executable has an invalid or outdated version.");

            // The updater must not be the running executable or Windows will
            // keep it locked. Copy the current app to a temporary sibling
            // directory; copy root-level dependencies for VS debug builds.
            var helperDir = Path.Combine(staging, "helper");
            Directory.CreateDirectory(helperDir);
            var installDirectory = Path.GetDirectoryName(runningExe)!;
            var helperExe = Path.Combine(helperDir, Path.GetFileName(runningExe));
            File.Copy(runningExe, helperExe);
            foreach (var file in Directory.EnumerateFiles(
                installDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                var suffix = Path.GetExtension(file);
                if (suffix.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                    suffix.Equals(".json", StringComparison.OrdinalIgnoreCase))
                    File.Copy(file, Path.Combine(helperDir, Path.GetFileName(file)));
            }

            var start = new ProcessStartInfo
            {
                FileName = helperExe,
                WorkingDirectory = helperDir,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("--apply-update");
            start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            start.ArgumentList.Add(runningExe);
            start.ArgumentList.Add(candidate);
            start.ArgumentList.Add(release.Tag);

            if (Process.Start(start) is null)
                throw new IOException("Could not start the update installer.");

            handedOff = true;
            button.Text = "Restarting...";
            owner.Close();
        }
        catch (Exception error)
        {
            MessageBox.Show(owner,
                "The update was not installed. Your existing game and saved files are unchanged.\n\n" +
                error.Message,
                "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (!handedOff && staging is not null)
                TryDeleteDirectory(staging);
            if (!owner.IsDisposed)
            {
                button.Text = priorLabel;
                button.Enabled = true;
            }
        }
    }

    private static async Task<Release> FetchLatestAsync()
    {
        using var response = await Http.GetAsync(ReleaseApi,
            HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var jsonStream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(jsonStream);
        var root = document.RootElement;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!tag.StartsWith('v') ||
            !Version.TryParse(tag[1..], out var version))
            throw new InvalidDataException("The latest GitHub release has no supported version tag.");
        if (root.GetProperty("prerelease").GetBoolean() ||
            root.GetProperty("draft").GetBoolean())
            throw new InvalidDataException("The latest GitHub release is not a stable release.");

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != AssetName)
                continue;
            var urlText = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (!Uri.TryCreate(urlText, UriKind.Absolute, out var url) ||
                url.Scheme != Uri.UriSchemeHttps ||
                !url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
                !url.AbsolutePath.StartsWith(
                    "/TheImposter1000/ProWrestlingRevolution/releases/download/",
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The release download URL is not trusted.");

            long size = asset.GetProperty("size").GetInt64();
            if (size < 1 || size > MaxDownloadBytes)
                throw new InvalidDataException("The release archive is too large.");

            string digest = asset.GetProperty("digest").GetString() ?? "";
            if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
                digest.Length != 71 ||
                !digest.AsSpan(7).ToString().All(Uri.IsHexDigit))
                throw new InvalidDataException(
                    "GitHub did not publish a valid SHA-256 checksum for this release.");

            return new Release(version, tag, url, size, digest[7..]);
        }
        throw new InvalidDataException(
            "The latest release does not contain the Windows game download.");
    }

    private static async Task DownloadVerifiedAsync(Release release, string destination)
    {
        using var response = await Http.GetAsync(release.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length &&
            length > MaxDownloadBytes)
            throw new InvalidDataException("The release download is too large.");

        await using (var input = await response.Content.ReadAsStreamAsync())
        await using (var output = new FileStream(destination, FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 131072, useAsync: true))
        {
            var buffer = new byte[131072];
            long total = 0;
            int read;
            while ((read = await input.ReadAsync(buffer)) > 0)
            {
                total += read;
                if (total > MaxDownloadBytes)
                    throw new InvalidDataException("The download exceeded its size limit.");
                await output.WriteAsync(buffer.AsMemory(0, read));
            }
            if (total != release.Size)
                throw new InvalidDataException("The downloaded ZIP was incomplete.");
        }

        await using var verify = File.OpenRead(destination);
        string actual = Convert.ToHexString(await SHA256.HashDataAsync(verify));
        if (!actual.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("GitHub's SHA-256 checksum did not match.");
    }

    internal static void ExtractApplication(string zipPath, string destination)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        // Extract ONLY the root executable. Never extract Databases/, Skins/,
        // Pictures/, System/ or any unknown archive path into the live game.
        var candidates = zip.Entries.Where(e =>
            e.FullName.Equals("ProWrestlingRevolution.exe",
                StringComparison.OrdinalIgnoreCase)).ToList();
        if (candidates.Count != 1 || candidates[0].Length is < 1 or > MaxExeBytes)
            throw new InvalidDataException("The update archive lacks a valid application.");
        candidates[0].ExtractToFile(destination, overwrite: false);
        using var file = File.OpenRead(destination);
        if (file.ReadByte() != 'M' || file.ReadByte() != 'Z')
            throw new InvalidDataException("The update executable is not a Windows program.");
    }

    internal static int ApplyFromHelper(string[] args)
    {
        if (args.Length != 5 ||
            !int.TryParse(args[1], NumberStyles.None,
                CultureInfo.InvariantCulture, out int originalPid))
            return 2;

        string target = Path.GetFullPath(args[2]);
        string candidate = Path.GetFullPath(args[3]);
        string tag = args[4];

        if (!Path.GetFileName(target).Equals(
            "ProWrestlingRevolution.exe", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(candidate) ||
            !tag.StartsWith('v') ||
            !Version.TryParse(tag[1..], out _))
            return 2;

        try
        {
            try
            {
                using var process = Process.GetProcessById(originalPid);
                if (!process.WaitForExit(90000))
                    throw new IOException(
                        "The game is still running. Close it before installing the update.");
            }
            catch (ArgumentException)
            {
                // The original process already exited.
            }

            var directory = Path.GetDirectoryName(target)!;
            var backupDirectory = Path.Combine(directory, ".pwr-updates");
            Directory.CreateDirectory(backupDirectory);
            string backup = Path.Combine(backupDirectory,
                "ProWrestlingRevolution-before-" +
                DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) +
                ".exe");
            string next = target + ".pwr-next";

            try
            {
                File.Copy(candidate, next, overwrite: true);
                // Windows can only atomically replace a file after its process
                // has exited, and File.Replace requires the same volume.
                File.Replace(next, target, backup, ignoreMetadataErrors: true);
            }
            finally
            {
                try { File.Delete(next); } catch (IOException) { }
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = target,
                    WorkingDirectory = directory,
                    UseShellExecute = true
                }) ?? throw new IOException("The new game could not be launched.");
                return 0;
            }
            catch
            {
                // If launching fails, restore the previous executable.
                File.Copy(backup, target, overwrite: true);
                throw;
            }
        }
        catch (Exception error)
        {
            MessageBox.Show(
                "The automatic update could not be completed.\n\n" + error.Message +
                "\n\nYour save games, databases, skins and settings were not changed.",
                "Pro Wrestling Revolution Updater",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
