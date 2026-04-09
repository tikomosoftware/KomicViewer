using SharpCompress.Archives;
using SharpCompress.Common;
using System.Text.RegularExpressions;
using SkiaSharp;
using LibHeifSharp;
using System.Runtime.InteropServices;

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

        // Return a copy of cached image if available
        if (archiveImage.CachedImage != null)
        {
            try
            {
                // Return a copy so the caller can dispose it without affecting the cache
                return new Bitmap(archiveImage.CachedImage);
            }
            catch
            {
                // If the cached image is somehow invalid, clear it and fall through to reload
                archiveImage.CachedImage = null;
            }
        }

        try
        {
            using var entryStream = archiveImage.Entry.OpenEntryStream();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            ms.Position = 0;

            if (ms.Length == 0) return null;

            Image? result = null;
            var ext = Path.GetExtension(archiveImage.EntryKey).ToLowerInvariant();

            if (ext == ".avif")
            {
                result = LoadAvifWithLibHeif(ms);
            }
            else if (ext == ".webp")
            {
                result = LoadImageWithSkia(ms);
            }
            else
            {
                try
                {
                    // Try standard GDI+ first
                    using var img = Image.FromStream(ms);
                    // Create a copy to avoid dependency on the stream
                    result = new Bitmap(img);
                }
                catch (ArgumentException)
                {
                    // Fallback to SkiaSharp for problematic JPG/PNG/etc.
                    ms.Position = 0;
                    result = LoadImageWithSkia(ms);
                }
            }

            if (result != null)
            {
                archiveImage.CachedImage = result;
                return new Bitmap(result);
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading page {pageIndex}: {ex.Message}");
            return null;
        }
    }

    private Image? LoadAvifWithLibHeif(Stream stream)
    {
        if (!Program.IsHeifAvailable)
            return LoadImageWithSkia(stream);

        try
        {
            var bytes = (stream as MemoryStream)?.ToArray() ?? Array.Empty<byte>();
            if (bytes.Length == 0)
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                bytes = ms.ToArray();
            }

            if (bytes.Length == 0) return null;

            using var context = new HeifContext(bytes);
            using var imageHandle = context.GetPrimaryImageHandle();
            using var heifImage = imageHandle.Decode(HeifColorspace.Rgb, HeifChroma.InterleavedRgba32);
            
            var plane = heifImage.GetPlane(HeifChannel.Interleaved);
            var info = new SKImageInfo(heifImage.Width, heifImage.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            
            using var bitmap = new SKBitmap(info);
            int srcStride = plane.Stride;
            int dstStride = bitmap.RowBytes;
            int height = heifImage.Height;
            int copyWidth = Math.Min(srcStride, dstStride);
            
            IntPtr dstPtr = bitmap.GetPixels();
            if (srcStride == dstStride)
            {
                int totalSize = srcStride * height;
                byte[] buffer = new byte[totalSize];
                Marshal.Copy(plane.Scan0, buffer, 0, totalSize);
                Marshal.Copy(buffer, 0, dstPtr, totalSize);
            }
            else
            {
                for (int row = 0; row < height; row++)
                {
                    IntPtr srcRow = IntPtr.Add(plane.Scan0, row * srcStride);
                    IntPtr dstRow = IntPtr.Add(dstPtr, row * dstStride);
                    byte[] rowBuffer = new byte[copyWidth];
                    Marshal.Copy(srcRow, rowBuffer, 0, copyWidth);
                    Marshal.Copy(rowBuffer, 0, dstRow, copyWidth);
                }
            }

            using var skImage = SKImage.FromBitmap(bitmap);
            using var pngData = skImage.Encode(SKEncodedImageFormat.Png, 100);
            if (pngData == null) return null;

            using var outStream = pngData.AsStream();
            using var tempImg = Image.FromStream(outStream);
            return new Bitmap(tempImg);
        }
        catch (DllNotFoundException)
        {
            stream.Position = 0;
            return LoadImageWithSkia(stream);
        }
        catch (BadImageFormatException)
        {
            stream.Position = 0;
            return LoadImageWithSkia(stream);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AVIF Decode] ERROR: {ex.Message}");
            stream.Position = 0;
            return LoadImageWithSkia(stream);
        }
    }

    private Image? LoadImageWithSkia(Stream stream)
    {
        try
        {
            // Use SKData as it handles stream lifecycle and buffering better for some codecs
            using var data = SKData.Create(stream);
            if (data == null)
            {
                System.Diagnostics.Debug.WriteLine("SkiaSharp error: Failed to create SKData from stream");
                return null;
            }

            using var codec = SKCodec.Create(data);
            if (codec == null)
            {
                System.Diagnostics.Debug.WriteLine("SkiaSharp error: SKCodec.Create returned null (Codec not found or invalid data)");
                return null;
            }

            var info = codec.Info;
            using var bitmap = new SKBitmap(info.Width, info.Height, info.ColorType, info.AlphaType);
            var res = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
            if (res != SKCodecResult.Success && res != SKCodecResult.IncompleteInput)
            {
                System.Diagnostics.Debug.WriteLine($"SkiaSharp error: GetPixels failed with {res}");
                return null;
            }

            using var skImage = SKImage.FromBitmap(bitmap);
            using var pngData = skImage.Encode(SKEncodedImageFormat.Png, 100);
            if (pngData == null)
            {
                System.Diagnostics.Debug.WriteLine("SkiaSharp error: Failed to encode image to PNG for GDI+ conversion");
                return null;
            }

            using var outStream = pngData.AsStream();
            using var tempImg = Image.FromStream(outStream);
            return new Bitmap(tempImg);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SkiaSharp decode error: {ex.Message}");
            return null;
        }
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
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".avif";
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
