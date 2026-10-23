// Copyright (c) KnifeLemon. Licensed under the MIT License.

using System.Globalization;

namespace Filee.Core.Presets;

/// <summary>Unit of data measurement (1 KB = 1024 bytes).</summary>
public enum SizeUnit
{
    Bytes = 1,
    KB = 1024,
    MB = 1024 * 1024,
    GB = 1024 * 1024 * 1024,
}

/// <summary>Parses human-entered file size strings into bytes.</summary>
public static class SizeText
{
    public const long OneKb = 1024;

    /// <summary>
    /// Parses size text such as "500", "500kb", "1.5 MB", "1,5mb".
    /// 1 KB = 1024 bytes (Windows Explorer standard). Minimum valid size is 1 KB (1024 bytes).
    /// Returns false for negative, zero, NaN, or sizes smaller than 1 KB.
    /// </summary>
    public static bool TryParse(string? text, SizeUnit defaultUnit, out long bytes)
    {
        bytes = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();

        // Extract unit suffix if present
        var multiplier = (long)defaultUnit;
        var numPart = trimmed;

        if (trimmed.EndsWith("gb", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = (long)SizeUnit.GB;
            numPart = trimmed[..^2].Trim();
        }
        else if (trimmed.EndsWith("g", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = (long)SizeUnit.GB;
            numPart = trimmed[..^1].Trim();
        }
        else if (trimmed.EndsWith("mb", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = (long)SizeUnit.MB;
            numPart = trimmed[..^2].Trim();
        }
        else if (trimmed.EndsWith("m", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = (long)SizeUnit.MB;
            numPart = trimmed[..^1].Trim();
        }
        else if (trimmed.EndsWith("kb", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = (long)SizeUnit.KB;
            numPart = trimmed[..^2].Trim();
        }
        else if (trimmed.EndsWith("k", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = (long)SizeUnit.KB;
            numPart = trimmed[..^1].Trim();
        }
        else if (trimmed.EndsWith("bytes", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = (long)SizeUnit.Bytes;
            numPart = trimmed[..^5].Trim();
        }
        else if (trimmed.EndsWith("b", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = (long)SizeUnit.Bytes;
            numPart = trimmed[..^1].Trim();
        }

        // Allow comma as decimal separator
        numPart = numPart.Replace(',', '.');

        if (!double.TryParse(numPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return false;

        if (!double.IsFinite(number) || number <= 0)
            return false;

        var rawBytes = number * multiplier;
        if (rawBytes < OneKb || rawBytes > long.MaxValue)
            return false;

        bytes = (long)Math.Round(rawBytes);
        return true;
    }

    /// <summary>Parses size text assuming KB if no unit is specified.</summary>
    public static bool TryParse(string? text, out long bytes) =>
        TryParse(text, SizeUnit.KB, out bytes);

    /// <summary>Formats a byte count into a compact human-readable string (e.g. "500 KB", "1.5 MB").</summary>
    public static string Format(long bytes)
    {
        if (bytes >= (long)SizeUnit.GB)
            return $"{(double)bytes / (long)SizeUnit.GB:0.##} GB";
        if (bytes >= (long)SizeUnit.MB)
            return $"{(double)bytes / (long)SizeUnit.MB:0.##} MB";
        return $"{(double)bytes / (long)SizeUnit.KB:0.##} KB";
    }
}
