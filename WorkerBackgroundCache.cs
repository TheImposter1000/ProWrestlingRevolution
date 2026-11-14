using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ProWrestlingRevolution;

/// <summary>
/// Decodes and downsizes skin backdrops off the UI thread. The cached image is
/// owned by the process, not by an individual worker window. Subsequent worker
/// profiles use the same prepared bitmap rather than reopening the JPG.
/// Custom skin changes produce new cache keys from the file timestamp/length.
/// </summary>
internal static class WorkerBackgroundCache
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<Bitmap?>>> Images =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Lazy<Task<Bitmap?>>> Sized =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Prepare the image once at the real page dimensions. Setting a
    /// 1366x768 image to BackgroundImageLayout.Stretch causes WinForms to
    /// resample it over and over through dozens of transparent children.
    /// An exact-size bitmap can use a cheap 1:1 tiled blit instead.
    /// </summary>
    internal static Task<Bitmap?> GetPreparedAsync(string file, Size target)
    {
        if (target.Width < 10 || target.Height < 10)
            return Task.FromResult<Bitmap?>(null);
        try
        {
            var info = new FileInfo(file);
            if (!info.Exists) return Task.FromResult<Bitmap?>(null);
            string key = info.FullName + "|" + info.Length + "|" +
                info.LastWriteTimeUtc.Ticks + "|" + target.Width + "x" + target.Height;
            return Sized.GetOrAdd(key, _ => new Lazy<Task<Bitmap?>>(async () =>
            {
                var original = await GetAsync(file).ConfigureAwait(false);
                if (original is null) return null;
                if (original.Size == target) return original;
                return await Task.Run(() =>
                {
                    var prepared = new Bitmap(target.Width, target.Height,
                        PixelFormat.Format32bppPArgb);
                    try
                    {
                        using var graphics = Graphics.FromImage(prepared);
                        graphics.CompositingQuality = CompositingQuality.HighSpeed;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        graphics.DrawImage(original,
                            new Rectangle(Point.Empty, target));
                        return prepared;
                    }
                    catch
                    {
                        prepared.Dispose();
                        throw;
                    }
                }).ConfigureAwait(false);
            }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult<Bitmap?>(null);
        }
    }

    internal static Task<Bitmap?> GetAsync(string file)
    {
        try
        {
            var info = new FileInfo(file);
            if (!info.Exists)
                return Task.FromResult<Bitmap?>(null);

            string key = info.FullName + "|" + info.Length + "|" +
                info.LastWriteTimeUtc.Ticks;
            return Images.GetOrAdd(key, _ => new Lazy<Task<Bitmap?>>(
                () => Task.Run(() => Decode(file)),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult<Bitmap?>(null);
        }
    }

    private static Bitmap? Decode(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite);
            using var original = Image.FromStream(stream);
            var bitmap = new Bitmap(SkinArt.WindowWidth, SkinArt.WindowHeight,
                PixelFormat.Format32bppPArgb);
            try
            {
                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.FromArgb(9, 18, 31));
                graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.DrawImage(original,
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or OutOfMemoryException)
        {
            // A damaged or unavailable optional skin JPG never blocks the editor.
            return null;
        }
    }
}
