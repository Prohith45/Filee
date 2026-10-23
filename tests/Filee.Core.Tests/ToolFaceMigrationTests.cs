// Copyright (c) KnifeLemon. Licensed under the MIT License.

using Filee.Core.Presets;
using Filee.Core.Profiles;
using Filee.Core.Settings;
using Xunit;

namespace Filee.Core.Tests;

public sealed class ToolFaceMigrationTests
{
    private static ToolbarProfile BuiltIn(string id) => BuiltInData.CreateProfiles().Single(p => p.Id == id);

    private static UserDataStore NewStore(TempDir dir)
    {
        var store = new UserDataStore(dir.Path);
        store.Load();
        return store;
    }

    private static ToolbarProfile Profile(UserDataStore store, string id) => store.Profiles.Single(p => p.Id == id);

    [Fact]
    public void Built_in_prompt_presets_have_expected_properties()
    {
        var presets = BuiltInData.CreatePresets().ToDictionary(p => p.Id);

        var promptIds = new[] { "reduce-size", "resize-pixels", "resize-percent", "set-quality", "set-dpi" };
        foreach (var id in promptIds)
        {
            Assert.True(presets.ContainsKey(id), $"Missing preset: {id}");
            var p = presets[id];
            Assert.Equal(PresetKind.Prompt, p.Kind);
            Assert.True(p.IsPrompt);
            Assert.Equal(BuiltInData.SameAsSource, p.TargetFormat);
        }

        Assert.Equal(PromptKind.TargetSize, presets["reduce-size"].Prompt);
        Assert.Equal("{name}_reduced", presets["reduce-size"].Output.FileNamePattern);

        Assert.Equal(PromptKind.Pixels, presets["resize-pixels"].Prompt);
        Assert.Equal(PromptKind.Percent, presets["resize-percent"].Prompt);
        Assert.Equal(PromptKind.Quality, presets["set-quality"].Prompt);
        Assert.Equal(PromptKind.Dpi, presets["set-dpi"].Prompt);
    }

    [Fact]
    public void Built_in_profiles_have_expected_tool_faces()
    {
        var images = BuiltIn("images");
        Assert.Equal(["reduce-size", "resize-pixels", "resize-percent", "set-quality", "set-dpi"], images.ToolPresetIds);

        var pdf = BuiltIn("pdf");
        Assert.Equal(["reduce-size"], pdf.ToolPresetIds);

        var video = BuiltIn("video");
        Assert.Equal(["reduce-size", "resize-pixels"], video.ToolPresetIds);

        var audio = BuiltIn("audio");
        Assert.Equal(["reduce-size"], audio.ToolPresetIds);

        var mixed = BuiltIn("mixed");
        Assert.Null(mixed.ToolPresetIds);

        var office = BuiltIn("office");
        Assert.Null(office.ToolPresetIds);
    }

    [Fact]
    public void Migration_from_schema_38_adds_prompt_presets_and_tool_faces()
    {
        var presets = BuiltInData.CreatePresets();
        // Remove prompt presets as in schema 38
        presets.RemoveAll(p => p.IsPrompt);

        var profiles = BuiltInData.CreateProfiles();
        foreach (var p in profiles)
            p.ToolPresetIds = null;

        var userProfile = new ToolbarProfile
        {
            Id = "user-profile",
            Name = "Custom",
            PresetIds = ["to-jpg"],
        };
        profiles.Add(userProfile);

        var changed = SettingsMigrations.ApplyToLibrary(38, presets, profiles);
        Assert.True(changed);

        // Prompt presets added
        Assert.Contains(presets, p => p.Id == "reduce-size");
        Assert.Contains(presets, p => p.Id == "resize-pixels");

        // Built-in profiles get ToolPresetIds
        var imgProfile = profiles.Single(p => p.Id == "images");
        Assert.Equal(["reduce-size", "resize-pixels", "resize-percent", "set-quality", "set-dpi"], imgProfile.ToolPresetIds);

        var pdfProfile = profiles.Single(p => p.Id == "pdf");
        Assert.Equal(["reduce-size"], pdfProfile.ToolPresetIds);

        // User profile remains untouched
        Assert.Null(userProfile.ToolPresetIds);
    }

    [Fact]
    public void ResetProfile_restores_tool_preset_ids()
    {
        using var dir = new TempDir();
        var store = NewStore(dir);

        var images = Profile(store, "images");
        images.ToolPresetIds = ["to-png"]; // customized
        store.SaveLibrary();

        Assert.True(store.ResetProfile(images));
        Assert.Equal(BuiltIn("images").ToolPresetIds, images.ToolPresetIds);
    }
}
