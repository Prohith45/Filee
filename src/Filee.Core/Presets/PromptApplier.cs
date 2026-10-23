// Copyright (c) KnifeLemon. Licensed under the MIT License.

using System;
using Filee.Core.Localization;

namespace Filee.Core.Presets;

/// <summary>User answers collected from a prompt window before conversion.</summary>
public sealed class PromptAnswer
{
    public PromptKind Kind { get; init; }
    public long? TargetSizeBytes { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public bool KeepAspect { get; init; } = true;
    public int? Percent { get; init; }
    public int? Quality { get; init; }
    public int? Dpi { get; init; }
}

/// <summary>Applies prompt values to a cloned preset before execution.</summary>
public static class PromptApplier
{
    /// <summary>
    /// Maps a <see cref="PromptAnswer"/> to options on a cloned <see cref="Preset"/>.
    /// Resets <see cref="Preset.Kind"/> to <see cref="PresetKind.Convert"/> so conversion engines run it.
    /// </summary>
    public static void Apply(Preset clone, PromptAnswer answer, ILocalizer? loc = null)
    {
        clone.Kind = PresetKind.Convert;
        switch (answer.Kind)
        {
            case PromptKind.TargetSize:
                clone.TargetSizeBytes = answer.TargetSizeBytes;
                if (answer.TargetSizeBytes is { } bytes)
                {
                    clone.Name = loc?.Format("preset.reduce_to", SizeText.Format(bytes))
                        ?? $"Reduce to {SizeText.Format(bytes)}";
                }
                break;

            case PromptKind.Pixels:
                clone.Image.Resize = ResizeMode.Exact;
                clone.Image.ExactWidth = answer.Width;
                clone.Image.ExactHeight = answer.Height;
                clone.Image.KeepAspect = answer.KeepAspect;
                if (answer.Height is > 0)
                {
                    clone.Media.MaxHeight = answer.Height.Value;
                }
                break;

            case PromptKind.Percent:
                clone.Image.Resize = ResizeMode.Percent;
                if (answer.Percent is > 0)
                {
                    clone.Image.ResizePercent = Math.Clamp(answer.Percent.Value, 1, 100);
                }
                break;

            case PromptKind.Quality:
                if (answer.Quality is > 0)
                {
                    var q = Math.Clamp(answer.Quality.Value, 1, 100);
                    clone.Image.Quality = q;
                    clone.Media.Quality = q switch
                    {
                        >= 80 => MediaQuality.High,
                        >= 45 => MediaQuality.Balanced,
                        _ => MediaQuality.Small,
                    };
                }
                break;

            case PromptKind.Dpi:
                if (answer.Dpi is > 0)
                {
                    clone.Image.Dpi = answer.Dpi.Value;
                    clone.Pdf.RenderDpi = answer.Dpi.Value;
                }
                break;
        }
    }
}
