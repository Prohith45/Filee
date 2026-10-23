// Copyright (c) KnifeLemon. Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.App.Views;
using Filee.Core.Localization;
using Filee.Core.Presets;
using Filee.Core.Settings;

namespace Filee.App.Tests;

public class ValuePromptTests
{
    [AvaloniaFact]
    public void TargetSize_validates_single_file_must_be_smaller()
    {
        TestServices.EnsureInitialized();
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, new byte[100 * 1024]); // 100 KB
            var store = AppHost.Get<UserDataStore>();
            var loc = AppHost.Get<ILocalizer>();
            var vm = new ValuePromptViewModel(PromptKind.TargetSize, [tempFile], loc, store);

            Assert.True(vm.IsTargetSize);
            Assert.False(string.IsNullOrEmpty(vm.CurrentSizeText));

            // Enter 200 KB -> not smaller!
            vm.SelectedUnit = "KB";
            vm.TargetSizeText = "200";
            Assert.False(vm.IsValid);
            Assert.NotNull(vm.Error);

            // Enter 50 KB -> valid!
            vm.TargetSizeText = "50";
            Assert.True(vm.IsValid);
            Assert.Null(vm.Error);

            var answer = vm.BuildAnswer();
            Assert.NotNull(answer);
            Assert.Equal(50 * 1024, answer.TargetSizeBytes);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [AvaloniaFact]
    public void TargetSize_allows_larger_target_for_multiple_files()
    {
        TestServices.EnsureInitialized();
        var f1 = Path.GetTempFileName();
        var f2 = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(f1, new byte[50 * 1024]);
            File.WriteAllBytes(f2, new byte[50 * 1024]);
            var store = AppHost.Get<UserDataStore>();
            var loc = AppHost.Get<ILocalizer>();
            var vm = new ValuePromptViewModel(PromptKind.TargetSize, [f1, f2], loc, store);

            vm.SelectedUnit = "KB";
            vm.TargetSizeText = "80"; // Larger than f1 and f2 individually
            Assert.True(vm.IsValid);
            Assert.Null(vm.Error);
        }
        finally
        {
            if (File.Exists(f1)) File.Delete(f1);
            if (File.Exists(f2)) File.Delete(f2);
        }
    }

    [AvaloniaFact]
    public void Prompt_slice_closes_donut_opens_prompt_and_starts_no_job_on_cancel()
    {
        TestServices.EnsureInitialized();
        var store = AppHost.Get<UserDataStore>();
        var conversions = AppHost.Get<ConversionService>();
        var radial = AppHost.Get<RadialController>();
        var windows = AppHost.Get<WindowService>();
        var before = conversions.Jobs.Select(j => j.Job.Id).ToHashSet();

        var temp = Path.Combine(Path.GetTempPath(), "test-photo.jpg");
        try
        {
            File.WriteAllBytes(temp, new byte[50 * 1024]);
            radial.ShowForFiles(400, 400, [temp]);
            RenderTests.Pump();
            var donut = radial.Window!.DonutControl;

            // Switch to Tools face
            Assert.True(donut.ShowTools());
            donut.FinishAnimations();
            RenderTests.Pump();

            // Find reduce-size slice
            var vm = (RadialViewModel)radial.Window.DataContext!;
            var reduceIndex = vm.Items.ToList().FindIndex(i => i.Id == "reduce-size");
            Assert.True(reduceIndex >= 0);

            // Select and trigger reduce-size
            donut.SetHover(reduceIndex);
            Assert.True(donut.HandleKey(Key.Enter));
            RenderTests.Pump();

            Assert.False(radial.IsVisible); // donut closed
            var promptWindow = Assert.IsType<ValuePromptWindow>(windows.ValuePrompt);
            Assert.True(promptWindow.IsVisible);

            // Cancel
            promptWindow.Close();
            RenderTests.Pump();

            // No conversion job started
            Assert.DoesNotContain(conversions.Jobs, j => !before.Contains(j.Job.Id));
        }
        finally
        {
            windows.ValuePrompt?.Close();
            radial.Close(animate: false);
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    [AvaloniaFact]
    public void Prompt_slice_submits_value_and_starts_conversion_job_with_toast()
    {
        TestServices.EnsureInitialized();
        var conversions = AppHost.Get<ConversionService>();
        var radial = AppHost.Get<RadialController>();
        var windows = AppHost.Get<WindowService>();
        var before = conversions.Jobs.Select(j => j.Job.Id).ToHashSet();

        var temp = Path.Combine(Path.GetTempPath(), "test-photo.jpg");
        try
        {
            File.WriteAllBytes(temp, new byte[50 * 1024]);
            radial.ShowForFiles(400, 400, [temp]);
            RenderTests.Pump();
            var donut = radial.Window!.DonutControl;

            Assert.True(donut.ShowTools());
            donut.FinishAnimations();
            RenderTests.Pump();

            var vm = (RadialViewModel)radial.Window.DataContext!;
            var reduceIndex = vm.Items.ToList().FindIndex(i => i.Id == "reduce-size");
            Assert.True(reduceIndex >= 0);

            donut.SetHover(reduceIndex);
            Assert.True(donut.HandleKey(Key.Enter));
            RenderTests.Pump();

            var promptWindow = Assert.IsType<ValuePromptWindow>(windows.ValuePrompt);
            Assert.True(promptWindow.IsVisible);

            var promptVm = Assert.IsType<ValuePromptViewModel>(promptWindow.DataContext);
            promptVm.SelectedUnit = "KB";
            promptVm.TargetSizeText = "30";
            Assert.True(promptVm.IsValid);

            promptWindow.Confirm();
            RenderTests.Pump();

            // Job started and added to Jobs and ToastJobs
            var jobVm = conversions.Jobs.FirstOrDefault(j => !before.Contains(j.Job.Id));
            Assert.NotNull(jobVm);
            Assert.Contains(conversions.ToastJobs, t => t.Job.Id == jobVm.Job.Id);
            Assert.Equal(30 * 1024, jobVm.Job.Preset.TargetSizeBytes);
            Assert.False(jobVm.Job.Preset.IsPrompt);
            Assert.Equal(PresetKind.Convert, jobVm.Job.Preset.Kind);
            Assert.Equal("Reduce to 30 KB", jobVm.Job.PresetDisplayName);
        }
        finally
        {
            windows.ValuePrompt?.Close();
            radial.Close(animate: false);
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
