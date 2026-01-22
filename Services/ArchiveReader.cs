using SharpCompress.Archives;
using SharpCompress.Common;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace KomicViewer.Services;

/// <summary>
/// Reads images from archive files (ZIP, RAR, CBZ, CBR)
/// </summary>
public sealed partial class ArchiveReader : IDisposable
{
    private List<ArchiveImage> _images = new();
    private IArchive? _archive;
    private Stream? _archiveStream;
    private bool _disposed;

    public int PageCount => _images.Count;
    public bool IsLoaded => _images.Count > 0;

    /// <summary>
    /// Load an archive file and extract image entries
    /// </summary>
    public async Task<bool> LoadAsync(string filePath)
    {
        await Task.Run(() =>
        {
            Close();

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Archive file not found", filePath);

            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            // Open underlying stream and keep it alive while archive is used
            _archiveStream = File.OpenRead(filePath);

            try
            {
                if (ext == ".rar" || ext == ".cbr")
                {
                    // Use explicit RAR opener
                    _archive = SharpCompress.Archives.Rar.RarArchive.Open(_archiveStream);
                }
                else
                {
                    // For ZIP/CBZ and other supported formats, use factory on the stream
                    _archive = ArchiveFactory.Open(_archiveStream);
                }
            }
            catch
            {
                // If opening failed, dispose stream and rethrow
                _archiveStream?.Dispose();
                _archiveStream = null;
                throw;
            }

            var imageEntries = _archive.Entries
                .Where(e => !e.IsDirectory && IsImageFile(e.Key ?? ""))
                .OrderBy(e => NaturalSortKey(e.Key ?? ""))
                .ToList();

            _images = imageEntries.Select((e, i) => new ArchiveImage
            {
                Index = i,
                EntryKey = e.Key ?? "",
                Entry = e
            }).ToList();
        });

        return IsLoaded;
    }

    /// <summary>
    /// Get image at specified page index
    /// </summary>
    public Image? GetPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _images.Count)
            return null;

        var archiveImage = _images[pageIndex];

        using var stream = archiveImage.Entry.OpenEntryStream();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        var ext = Path.GetExtension(archiveImage.EntryKey).ToLowerInvariant();
        if (ext == ".webp")
        {
            // Decode WebP using SkiaSharp
            ms.Position = 0;
            using var skStream = new SKManagedStream(ms);
            using var codec = SKCodec.Create(skStream);
            if (codec == null) return null;
            var info = codec.Info;
            var bitmap = new SKBitmap(info.Width, info.Height, info.ColorType, info.AlphaType);
            var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput) return null;
            using var skImage = SKImage.FromBitmap(bitmap);
            using var data = skImage.Encode(SKEncodedImageFormat.Png, 100);
            using var outStream = data.AsStream();
            return Image.FromStream(outStream);
        }

        return Image.FromStream(ms);
    }

    /// <summary>
    /// Close the current archive and release resources
    /// </summary>
    public void Close()
    {
        foreach (var img in _images)
        {
            img.CachedImage?.Dispose();
        }
        _images.Clear();
        _archive?.Dispose();
        _archive = null;
        _archiveStream?.Dispose();
        _archiveStream = null;
    }

    private static bool IsImageFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp";
    }

    /// <summary>
    /// Natural sort key for proper ordering (1, 2, 10 instead of 1, 10, 2)
    /// </summary>
    private static string NaturalSortKey(string s)
    {
        return NaturalSortRegex().Replace(s, m => m.Value.PadLeft(10, '0'));
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex NaturalSortRegex();

    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _disposed = true;
        }
    }

    private sealed class ArchiveImage
    {
        public int Index { get; init; }
        public required string EntryKey { get; init; }
        public required IArchiveEntry Entry { get; init; }
        public Image? CachedImage { get; set; }
    }
}
