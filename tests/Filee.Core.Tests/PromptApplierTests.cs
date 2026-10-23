// Copyright (c) KnifeLemon. Licensed under the MIT License.

using System.Text.Json;
using Filee.Core.Presets;
using Filee.Core.Settings;
using Xunit;

namespace Filee.Core.Tests;

public sealed class PromptApplierTests
{
    [Fact]
    public void Applies_TargetSize_answer()
    {
        var preset = new Preset { Kind = PresetKind.Prompt, Prompt = PromptKind.TargetSize };
        var answer = new PromptAnswer { Kind = PromptKind.TargetSize, TargetSizeBytes = 300 * 1024L };

        PromptApplier.Apply(preset, answer);

        Assert.Equal(300 * 1024L, preset.TargetSizeBytes);
    }

    [Fact]
    public void Applies_Pixels_answer()
    {
        var preset = new Preset { Kind = PresetKind.Prompt, Prompt = PromptKind.Pixels };
        var answer = new PromptAnswer
        {
            Kind = PromptKind.Pixels,
            Width = 1920,
            Height = 1080,
            KeepAspect = true,
        };

        PromptApplier.Apply(preset, answer);

        Assert.Equal(ResizeMode.Exact, preset.Image.Resize);
        Assert.Equal(1920, preset.Image.ExactWidth);
        Assert.Equal(1080, preset.Image.ExactHeight);
        Assert.True(preset.Image.KeepAspect);
        Assert.Equal(1080, preset.Media.MaxHeight);
    }

    [Fact]
    public void Applies_Percent_answer()
    {
        var preset = new Preset { Kind = PresetKind.Prompt, Prompt = PromptKind.Percent };
        var answer = new PromptAnswer { Kind = PromptKind.Percent, Percent = 65 };

        PromptApplier.Apply(preset, answer);

        Assert.Equal(ResizeMode.Percent, preset.Image.Resize);
        Assert.Equal(65, preset.Image.ResizePercent);
    }

    [Fact]
    public void Applies_Quality_answer()
    {
        var preset = new Preset { Kind = PresetKind.Prompt, Prompt = PromptKind.Quality };
        var answer = new PromptAnswer { Kind = PromptKind.Quality, Quality = 85 };

        PromptApplier.Apply(preset, answer);

        Assert.Equal(85, preset.Image.Quality);
        Assert.Equal(MediaQuality.High, preset.Media.Quality);

        var answerMid = new PromptAnswer { Kind = PromptKind.Quality, Quality = 60 };
        PromptApplier.Apply(preset, answerMid);
        Assert.Equal(60, preset.Image.Quality);
        Assert.Equal(MediaQuality.Balanced, preset.Media.Quality);

        var answerLow = new PromptAnswer { Kind = PromptKind.Quality, Quality = 30 };
        PromptApplier.Apply(preset, answerLow);
        Assert.Equal(30, preset.Image.Quality);
        Assert.Equal(MediaQuality.Small, preset.Media.Quality);
    }

    [Fact]
    public void Applies_Dpi_answer()
    {
        var preset = new Preset { Kind = PresetKind.Prompt, Prompt = PromptKind.Dpi };
        var answer = new PromptAnswer { Kind = PromptKind.Dpi, Dpi = 300 };

        PromptApplier.Apply(preset, answer);

        Assert.Equal(300, preset.Image.Dpi);
        Assert.Equal(300, preset.Pdf.RenderDpi);
    }

    [Fact]
    public void Preset_Clone_copies_new_fields()
    {
        var preset = new Preset
        {
            Kind = PresetKind.Prompt,
            Prompt = PromptKind.TargetSize,
            TargetSizeBytes = 500 * 1024L,
            Image = new ImageOptions
            {
                Resize = ResizeMode.Exact,
                ExactWidth = 800,
                ExactHeight = 600,
                KeepAspect = false,
            },
        };

        var clone = preset.Clone();

        Assert.True(clone.IsPrompt);
        Assert.Equal(PresetKind.Prompt, clone.Kind);
        Assert.Equal(PromptKind.TargetSize, clone.Prompt);
        Assert.Equal(500 * 1024L, clone.TargetSizeBytes);
        Assert.Equal(ResizeMode.Exact, clone.Image.Resize);
        Assert.Equal(800, clone.Image.ExactWidth);
        Assert.Equal(600, clone.Image.ExactHeight);
        Assert.False(clone.Image.KeepAspect);
    }

    [Fact]
    public void Serializes_and_deserializes_prompt_preset_with_context()
    {
        var preset = new Preset
        {
            Id = "test-prompt",
            Name = "Reduce to size",
            Kind = PresetKind.Prompt,
            Prompt = PromptKind.TargetSize,
            TargetSizeBytes = 1024 * 1024L,
        };

        var list = new List<Preset> { preset };
        var json = JsonSerializer.Serialize(list, typeof(List<Preset>), FileeJsonContext.Default);
        var restored = (List<Preset>?)JsonSerializer.Deserialize(json, typeof(List<Preset>), FileeJsonContext.Default);

        Assert.NotNull(restored);
        var restoredPreset = Assert.Single(restored);
        Assert.Equal(PresetKind.Prompt, restoredPreset.Kind);
        Assert.Equal(PromptKind.TargetSize, restoredPreset.Prompt);
        Assert.Equal(1024 * 1024L, restoredPreset.TargetSizeBytes);
        Assert.True(restoredPreset.IsPrompt);
    }
}
