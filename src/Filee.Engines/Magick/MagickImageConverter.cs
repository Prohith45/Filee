// Raster image ↔ raster image conversion with ImageMagick (Magick.NET, Apache-2.0).

using System.IO;
using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Engines.Infrastructure;
using ImageMagick;

namespace Filee.Engines.Magick;

/// <summary>
/// Converts between PNG, JPG, WEBP, TIFF, BMP, GIF, ICO, AVIF, JPEG XL, JPEG 2000, PSD/PSB, TGA, PPM, APNG, MNG, QOI,
/// DDS, PCX, CUR, XPM, XBM, WBMP, PAM, PFM, SGI, Sun Raster and MIFF, and reads HEIC, XCF, camera RAW, DICOM,
/// Photo CD, OpenEXR, Radiance HDR and (on Windows) EMF/WMF.
/// </summary>
public sealed class MagickImageConverter : IConverter, ITiffMerger, IImageTransformer
{
    public string Id => "magick";
    public string DisplayName => "ImageMagick";
    public int MaxParallelism => 0;

    public bool Transforms(string from, string to) => true;

    public IReadOnlyList<ConversionEdge> Edges { get; } =
        (from source in ImageEncoder.Sources
         from target in ImageEncoder.Writable
         select ImageEncoder.Edge(source, target)).ToList();

    public EngineStatus GetStatus() =>
        EngineStatus.Available(MagickNET.ImageMagickVersion,
            $"{EngineVersions.Library("Magick.NET", typeof(MagickImage))} · ImageMagick {ImageMagickNumber()}");

    /// <summary>"7.1.2-5" out of "ImageMagick 7.1.2-5 Q8 x64 …".</summary>
    private static string ImageMagickNumber() =>
        MagickNET.ImageMagickVersion.Split(' ').FirstOrDefault(part => part.Length > 0 && char.IsAsciiDigit(part[0])) ?? "?";

    /// <summary>
    /// Writes the pages into one multi-page TIFF. Each page is flattened onto the preset's background (no alpha) at 8 bits
    /// and marked as a page of a multi-page document (SubfileType 2), so older viewers (Microsoft Office Document
    /// Imaging, fax software) show it page by page; compression is the preset's.
    /// </summary>
    public Task MergeAsync(IReadOnlyList<string> inputPaths, string outputPath, Filee.Core.Presets.ImageOptions options,
        CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var compression = options.TiffCompression switch
            {
                Filee.Core.Presets.TiffCompression.Lzw => CompressionMethod.LZW,
                Filee.Core.Presets.TiffCompression.Zip => CompressionMethod.Zip,
                _ => CompressionMethod.NoCompression,
            };
            using var pages = new MagickImageCollection();
            foreach (var path in inputPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var source = new MagickImageCollection(path);
                foreach (var frame in source)
                {
                    var page = frame.Clone();
                    page.BackgroundColor = new MagickColor(string.IsNullOrWhiteSpace(options.Background) ? "#FFFFFF" : options.Background);
                    page.Alpha(AlphaOption.Remove);
                    page.Depth = 8;
                    page.Settings.Compression = compression;
                    page.SetAttribute("tiff:subfiletype", "2");
                    pages.Add(page);
                }
            }
            pages.Write(outputPath, MagickFormat.Tiff);
        }, cancellationToken);

    public Task<IReadOnlyList<string>> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<string>>(() =>
        {
            // Animated PNGs are often saved as .png: read them as APNG, so they stay animated as GIF or WebP.
            var from = step.From == "png" && Apng.IsAnimated(step.InputPath) ? "apng" : step.From;
            using var images = ImageReader.Read(step.InputPath, from, step.Preset);
            return Write(images, from, step, progress, cancellationToken);
        }, cancellationToken);

    /// <summary>
    /// The rest of a conversion once the picture is read (also for pictures another engine decoded, see
    /// Wic/WicImageConverter): the frames kept for the target, the preset's options, the files.
    /// </summary>
    /// <param name="from">Format the frames were read from (animations and icons keep different frames).</param>
    internal static IReadOnlyList<string> Write(MagickImageCollection images, string from, ConversionStep step,
        IProgress<double>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (step.Preset.TargetSizeBytes is { } targetBytes)
            return WriteTargetSize(images, from, step, targetBytes, progress, cancellationToken);

        progress?.Report(0.3);

        // A JPG has no transparent border to trim: re-encoding it would only cost a JPEG generation.
        if (step.Preset.Image.TrimTransparent && !ImageEncoder.HoldsAlpha(step.To))
            throw new ConversionRefusedException("error.no_transparency", step.To.ToUpperInvariant());

        var animation = ImageEncoder.KeepsAnimation(from, step.To);
        var frames = ImageEncoder.SelectFrames(images, from, step.To);
        // Before the options: frames dropped for speed aren't resized for nothing.
        if (animation && step.Preset.Image.ChangesSpeed)
            frames = AnimationTiming.ChangeSpeed(frames, step.Preset.Image.PlaybackSpeed, step.To);
        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageEncoder.ApplyOptions(frame, step.Preset.Image, step.To);
            // Animations aren't trimmed: each frame trimmed alone would shift. Trimming all frames to one box could be added.
            if (step.Preset.Image.TrimTransparent && !(animation && frames.Count > 1))
                ImageEncoder.TrimTransparentBorders(frame);
        }
        progress?.Report(0.7);

        var written = ImageEncoder.Write(frames, step.To, step.Output, step.Preset.Image, step.InputPath, animation);
        progress?.Report(1);
        return written;
    }

    private static IReadOnlyList<string> WriteTargetSize(MagickImageCollection images, string from, ConversionStep step,
        long targetBytes, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(0.1);

        // Multi-frame or animation refusal
        var animation = ImageEncoder.KeepsAnimation(from, step.To);
        if (animation || images.Count > 1 || (step.From == "png" && Apng.IsAnimated(step.InputPath)) || from is "gif" or "apng")
            throw new ConversionRefusedException("error.size_target_animation");

        // Target larger than source: write nothing, report notice
        if (File.Exists(step.InputPath))
        {
            var srcSize = new FileInfo(step.InputPath).Length;
            if (srcSize <= targetBytes)
            {
                step.Notice?.Invoke("notice.size_already_small");
                return [];
            }
        }

        var to = step.To.ToLowerInvariant();
        var isLossy = to is "jpg" or "jpeg" or "webp" or "avif" or "jxl";
        var isIndexedOrLossless = to is "png" or "bmp" or "tiff";
        if (!isLossy && !isIndexedOrLossless)
            throw new ConversionRefusedException("error.size_target_unsupported", step.To.ToUpperInvariant());

        var frames = ImageEncoder.SelectFrames(images, from, step.To);
        var frame = frames[0];
        ImageEncoder.ApplyOptions(frame, step.Preset.Image, step.To);
        if (step.Preset.Image.TrimTransparent)
            ImageEncoder.TrimTransparentBorders(frame);

        var magickFormat = ImageEncoder.ToMagickFormat(step.To);
        var origWidth = frame.Width;
        var origHeight = frame.Height;
        var pixelCount = (long)origWidth * origHeight;

        // Proxy scaling for very large images (> 100 MP)
        var useProxy = pixelCount > 100_000_000;

        if (isLossy)
        {
            var minQ = to is "jpg" or "jpeg" ? 40 : 30;
            var maxQ = (int)Math.Min(95, Math.Max(minQ, frame.Quality > 0 ? (int)frame.Quality : 92));

            long ProbeQuality(int q, IMagickImage<byte> img)
            {
                using var clone = img.Clone();
                clone.Quality = (uint)q;
                using var ms = new MemoryStream();
                clone.Write(ms, magickFormat);
                return ms.Length;
            }

            int bestQ = -1;
            long bestQSize = 0;

            if (useProxy)
            {
                using var proxy = frame.Clone();
                proxy.Resize((uint)(origWidth / 2), (uint)(origHeight / 2));
                var proxyTarget = (long)(targetBytes * 0.3);
                var proxyFit = SizeFitter.FitInteger(minQ, maxQ, proxyTarget, (q, ct) => ProbeQuality(q, proxy), cancellationToken);
                var testQ = proxyFit.BestValue ?? minQ;
                var fullSize = ProbeQuality(testQ, frame);
                if (fullSize <= targetBytes)
                {
                    bestQ = testQ;
                    bestQSize = fullSize;
                }
            }

            if (bestQ < 0)
            {
                var qFit = SizeFitter.FitInteger(minQ, maxQ, targetBytes, (q, ct) => ProbeQuality(q, frame), cancellationToken);
                if (qFit.BestValue is { } qVal)
                {
                    bestQ = qVal;
                    bestQSize = qFit.BestSize;
                }
            }

            if (bestQ >= minQ && bestQSize <= targetBytes)
            {
                frame.Quality = (uint)bestQ;
            }
            else
            {
                // Phase B: Scale bisection (5..100) at base quality 60
                int baseQ = 60;
                frame.Quality = (uint)baseQ;

                long ProbeScale(int percent)
                {
                    using var clone = frame.Clone();
                    var w = Math.Max(1, (int)Math.Round(origWidth * percent / 100.0));
                    var h = Math.Max(1, (int)Math.Round(origHeight * percent / 100.0));
                    clone.Resize((uint)w, (uint)h);
                    using var ms = new MemoryStream();
                    clone.Write(ms, magickFormat);
                    return ms.Length;
                }

                var scaleFit = SizeFitter.FitInteger(5, 100, targetBytes, (p, ct) => ProbeScale(p), cancellationToken);
                if (scaleFit.BestValue is { } bestPct)
                {
                    var w = Math.Max(1, (int)Math.Round(origWidth * bestPct / 100.0));
                    var h = Math.Max(1, (int)Math.Round(origHeight * bestPct / 100.0));
                    frame.Resize((uint)w, (uint)h);
                    frame.Quality = (uint)baseQ;
                }
                else
                {
                    frame.Quality = (uint)minQ;
                    var w = Math.Max(1, (int)Math.Round(origWidth * 0.05));
                    var h = Math.Max(1, (int)Math.Round(origHeight * 0.05));
                    frame.Resize((uint)w, (uint)h);
                    using var ms = new MemoryStream();
                    frame.Write(ms, magickFormat);
                    if (ms.Length > targetBytes)
                        throw new ConversionRefusedException("error.size_target_too_small", SizeText.Format(targetBytes));
                }
            }
        }
        else // Lossless / Indexed: png, bmp, tiff
        {
            bool quantized = false;
            using (var qTest = frame.Clone())
            {
                qTest.Quantize(new QuantizeSettings { Colors = 256 });
                using var ms = new MemoryStream();
                qTest.Write(ms, magickFormat);
                if (ms.Length <= targetBytes)
                {
                    frame.Quantize(new QuantizeSettings { Colors = 256 });
                    quantized = true;
                    step.Notice?.Invoke("notice.size_quantized");
                }
            }

            using (var testMs = new MemoryStream())
            {
                frame.Write(testMs, magickFormat);
                if (testMs.Length > targetBytes)
                {
                    long ProbeScale(int percent)
                    {
                        using var clone = frame.Clone();
                        var w = Math.Max(1, (int)Math.Round(origWidth * percent / 100.0));
                        var h = Math.Max(1, (int)Math.Round(origHeight * percent / 100.0));
                        clone.Resize((uint)w, (uint)h);
                        if (!quantized)
                            clone.Quantize(new QuantizeSettings { Colors = 256 });
                        using var s = new MemoryStream();
                        clone.Write(s, magickFormat);
                        return s.Length;
                    }

                    var scaleFit = SizeFitter.FitInteger(5, 100, targetBytes, (p, ct) => ProbeScale(p), cancellationToken);
                    if (scaleFit.BestValue is { } bestPct)
                    {
                        var w = Math.Max(1, (int)Math.Round(origWidth * bestPct / 100.0));
                        var h = Math.Max(1, (int)Math.Round(origHeight * bestPct / 100.0));
                        frame.Resize((uint)w, (uint)h);
                        if (!quantized)
                        {
                            frame.Quantize(new QuantizeSettings { Colors = 256 });
                            step.Notice?.Invoke("notice.size_quantized");
                        }
                    }
                    else
                    {
                        throw new ConversionRefusedException("error.size_target_too_small", SizeText.Format(targetBytes));
                    }
                }
            }
        }

        progress?.Report(0.7);
        var written = ImageEncoder.Write([frame], step.To, step.Output, step.Preset.Image, step.InputPath, animation: false);

        if (written.Count > 0 && File.Exists(written[0]))
        {
            var len = new FileInfo(written[0]).Length;
            if (len > targetBytes)
            {
                for (int attempt = 0; attempt < 5 && len > targetBytes; attempt++)
                {
                    var newW = Math.Max(1, (int)(frame.Width * 0.9));
                    var newH = Math.Max(1, (int)(frame.Height * 0.9));
                    frame.Resize((uint)newW, (uint)newH);
                    ImageEncoder.WriteImage(frame, written[0], step.To);
                    len = new FileInfo(written[0]).Length;
                }

                if (len > targetBytes)
                    step.Notice?.Invoke("notice.size_target_not_reached");
            }
        }

        progress?.Report(1.0);
        return written;
    }
}
