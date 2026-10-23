// Copyright (c) KnifeLemon. Licensed under the MIT License.

using System.IO;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.Core.Localization;
using Filee.Core.Presets;
using Filee.Core.Settings;

namespace Filee.App.ViewModels;

public sealed partial class ValuePromptViewModel : ObservableObject
{
    private readonly PromptKind _kind;
    private readonly IReadOnlyList<string> _sources;
    private readonly ILocalizer _loc;
    private readonly UserDataStore _store;
    private readonly long _totalSourceBytes;
    private readonly long? _singleFileSize;

    public ValuePromptViewModel(PromptKind kind, IReadOnlyList<string> sources, ILocalizer loc, UserDataStore store)
    {
        _kind = kind;
        _sources = sources;
        _loc = loc;
        _store = store;

        long total = 0;
        int existingCount = 0;
        long lastSize = 0;
        foreach (var s in sources)
        {
            try
            {
                if (File.Exists(s))
                {
                    var len = new FileInfo(s).Length;
                    total += len;
                    existingCount++;
                    lastSize = len;
                }
            }
            catch { }
        }
        _totalSourceBytes = total;
        if (existingCount == 1)
            _singleFileSize = lastSize;

        Title = kind switch
        {
            PromptKind.TargetSize => _loc["prompt.title_size"],
            PromptKind.Pixels => _loc["prompt.title_pixels"],
            PromptKind.Percent => _loc["prompt.title_percent"],
            PromptKind.Quality => _loc["prompt.title_quality"],
            PromptKind.Dpi => _loc["prompt.title_dpi"],
            _ => "",
        };

        IsTargetSize = kind == PromptKind.TargetSize;
        IsPixels = kind == PromptKind.Pixels;
        IsPercent = kind == PromptKind.Percent;
        IsQuality = kind == PromptKind.Quality;
        IsDpi = kind == PromptKind.Dpi;

        if (IsTargetSize)
        {
            if (total > 0)
                CurrentSizeText = _loc.Format("prompt.current_size", SizeText.Format(total));

            var savedUnit = store.Settings.LastTargetSizeUnit;
            SelectedUnit = !string.IsNullOrEmpty(savedUnit)
                ? savedUnit
                : (total > 0 && total < 1024 * 1024 ? "KB" : "MB");

            var savedValue = store.Settings.LastTargetSizeText;
            TargetSizeText = !string.IsNullOrEmpty(savedValue) ? savedValue : "";
            ValidateTargetSize();
        }
        else if (IsPixels)
        {
            WidthText = "";
            HeightText = "";
            KeepAspect = true;
            ValidatePixels();
        }
        else if (IsPercent)
        {
            PercentText = "50";
            ValidatePercent();
        }
        else if (IsQuality)
        {
            QualityValue = 75;
            IsValid = true;
        }
        else if (IsDpi)
        {
            DpiText = "150";
            ValidateDpi();
        }
    }

    public string Title { get; }
    public bool IsTargetSize { get; }
    public bool IsPixels { get; }
    public bool IsPercent { get; }
    public bool IsQuality { get; }
    public bool IsDpi { get; }

    [ObservableProperty] private string? _currentSizeText;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isValid;

    // TargetSize
    [ObservableProperty] private string _targetSizeText = "";
    [ObservableProperty] private string _selectedUnit = "KB";
    public IReadOnlyList<string> Units { get; } = ["KB", "MB"];

    // Pixels
    [ObservableProperty] private string _widthText = "";
    [ObservableProperty] private string _heightText = "";
    [ObservableProperty] private bool _keepAspect = true;

    // Percent
    [ObservableProperty] private string _percentText = "50";

    // Quality
    [ObservableProperty] private int _qualityValue = 75;

    // Dpi
    [ObservableProperty] private string _dpiText = "150";

    partial void OnTargetSizeTextChanged(string value) => ValidateTargetSize();
    partial void OnSelectedUnitChanged(string value) => ValidateTargetSize();

    partial void OnWidthTextChanged(string value) => ValidatePixels();
    partial void OnHeightTextChanged(string value) => ValidatePixels();
    partial void OnKeepAspectChanged(bool value) => ValidatePixels();

    partial void OnPercentTextChanged(string value) => ValidatePercent();
    partial void OnDpiTextChanged(string value) => ValidateDpi();

    [RelayCommand]
    private void SetDpi(string dpi) => DpiText = dpi;

    private void ValidateTargetSize()
    {
        if (string.IsNullOrWhiteSpace(TargetSizeText))
        {
            IsValid = false;
            Error = null;
            return;
        }

        var defaultUnit = SelectedUnit == "MB" ? SizeUnit.MB : SizeUnit.KB;
        if (!SizeText.TryParse(TargetSizeText, defaultUnit, out var bytes))
        {
            IsValid = false;
            Error = _loc["prompt.error_invalid"];
            return;
        }

        if (_singleFileSize.HasValue && bytes >= _singleFileSize.Value)
        {
            IsValid = false;
            Error = _loc.Format("prompt.error_not_smaller", SizeText.Format(_singleFileSize.Value));
            return;
        }

        IsValid = true;
        Error = null;
    }

    private void ValidatePixels()
    {
        var hasW = int.TryParse(WidthText.Trim(), out var w) && w > 0;
        var hasH = int.TryParse(HeightText.Trim(), out var h) && h > 0;
        if (!hasW && !hasH)
        {
            IsValid = false;
            Error = null;
            return;
        }
        IsValid = true;
        Error = null;
    }

    private void ValidatePercent()
    {
        if (int.TryParse(PercentText.Trim(), out var p) && p is >= 1 and <= 100)
        {
            IsValid = true;
            Error = null;
        }
        else
        {
            IsValid = false;
            Error = _loc["prompt.error_invalid"];
        }
    }

    private void ValidateDpi()
    {
        if (int.TryParse(DpiText.Trim(), out var d) && d >= 10)
        {
            IsValid = true;
            Error = null;
        }
        else
        {
            IsValid = false;
            Error = _loc["prompt.error_invalid"];
        }
    }

    public PromptAnswer? BuildAnswer()
    {
        if (!IsValid) return null;

        switch (_kind)
        {
            case PromptKind.TargetSize:
                var defaultUnit = SelectedUnit == "MB" ? SizeUnit.MB : SizeUnit.KB;
                if (!SizeText.TryParse(TargetSizeText, defaultUnit, out var bytes))
                    return null;
                _store.Settings.LastTargetSizeText = TargetSizeText.Trim();
                _store.Settings.LastTargetSizeUnit = SelectedUnit;
                _store.SaveLibrary();
                return new PromptAnswer
                {
                    Kind = PromptKind.TargetSize,
                    TargetSizeBytes = bytes,
                };

            case PromptKind.Pixels:
                int? w = int.TryParse(WidthText.Trim(), out var parsedW) && parsedW > 0 ? parsedW : null;
                int? h = int.TryParse(HeightText.Trim(), out var parsedH) && parsedH > 0 ? parsedH : null;
                return new PromptAnswer
                {
                    Kind = PromptKind.Pixels,
                    Width = w,
                    Height = h,
                    KeepAspect = KeepAspect,
                };

            case PromptKind.Percent:
                if (!int.TryParse(PercentText.Trim(), out var pct)) return null;
                return new PromptAnswer
                {
                    Kind = PromptKind.Percent,
                    Percent = pct,
                };

            case PromptKind.Quality:
                return new PromptAnswer
                {
                    Kind = PromptKind.Quality,
                    Quality = QualityValue,
                };

            case PromptKind.Dpi:
                if (!int.TryParse(DpiText.Trim(), out var dpi)) return null;
                return new PromptAnswer
                {
                    Kind = PromptKind.Dpi,
                    Dpi = dpi,
                };

            default:
                return null;
        }
    }
}
