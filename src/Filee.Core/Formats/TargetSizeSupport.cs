// Copyright (c) KnifeLemon. Licensed under the MIT License.

namespace Filee.Core.Formats;

public static class TargetSizeSupport
{
    private static readonly HashSet<string> ImageFormats =
    [
        "jpg", "jpeg", "webp", "avif", "jxl", "jp2", "png", "bmp", "tiff", "tif", "gif", "apng", "mng",
    ];

    private static readonly HashSet<string> LossyAudioFormats =
    [
        "mp3", "m4a", "m4b", "aac", "ogg", "opus", "wma", "weba", "ac3", "amr",
    ];

    public static bool IsSupported(FileFormat format) =>
        format.Id == "pdf"
        || ImageFormats.Contains(format.Id)
        || format.Category == FormatCategory.Video
        || (format.Category == FormatCategory.Audio && LossyAudioFormats.Contains(format.Id));

    public static bool IsSupported(string extOrId)
    {
        var clean = extOrId.TrimStart('.').ToLowerInvariant();
        if (clean == "pdf" || ImageFormats.Contains(clean) || LossyAudioFormats.Contains(clean))
            return true;
        var format = FormatRegistry.FindById(clean) ?? FormatRegistry.FindByExtension(clean);
        return format is not null && IsSupported(format);
    }
}
