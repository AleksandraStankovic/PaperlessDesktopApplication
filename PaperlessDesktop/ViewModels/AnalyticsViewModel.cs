using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperlessDesktop.Helpers;
using PaperlessDesktop.Models;
using PaperlessDesktop.Services;

namespace PaperlessDesktop.ViewModels;

public partial class AnalyticsViewModel : ObservableObject, INavigationAware
{
    private const double PieRadius = 110;
    private const double PieInnerRadius = 62;
    private const double BarMaxHeight = 180;
    private const double MinTagFontSize = 12;
    private const double MaxTagFontSize = 30;
    private const int MaxPieSlices = 7;
    private const int MaxCloudTags = 30;

    private static readonly string[] SliceColors =
        { "#3F51B5", "#00897B", "#F57C00", "#C2185B", "#7B1FA2", "#388E3C", "#1976D2" };
    private const string OtherSliceColor = "#9E9E9E";
    private const string NoTypeSliceColor = "#BDBDBD";

    private readonly PaperlessApiClient _api;
    private readonly LookupCache _lookups;
    private readonly NotificationService _notifications;
    private readonly LocalizationService _localization;

    private CancellationTokenSource? _monthsCts;
    private bool _loaded;
    private bool _updatingYear;
    private bool _rebuildingMonthNames;
    private string? _monthNamesLanguage;
    private int[] _monthCounts = new int[12];
    private int _monthsYear;

    public double PieSize => PieRadius * 2;

    public ObservableCollection<PieSliceViewModel> TypeSlices { get; } = new();
    public ObservableCollection<MonthBarViewModel> MonthBars { get; } = new();
    public ObservableCollection<TagCloudItemViewModel> TagCloud { get; } = new();
    public ObservableCollection<int> AvailableYears { get; } = new();
    public ObservableCollection<string> MonthNames { get; } = new();

    public event EventHandler<FilterChip>? FilterRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingMonths;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasData))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private int _totalDocuments;

    [ObservableProperty]
    private int _tagCount;

    [ObservableProperty]
    private int _correspondentCount;

    [ObservableProperty]
    private int _documentTypeCount;

    [ObservableProperty]
    private int? _selectedYear;

    [ObservableProperty]
    private int _fromMonthIndex;

    [ObservableProperty]
    private int _toMonthIndex = 11;

    [ObservableProperty]
    private int _periodTotal;

    public bool HasData => TotalDocuments > 0;
    public bool ShowEmptyState => !IsLoading && _loaded && TotalDocuments == 0;

    public AnalyticsViewModel(PaperlessApiClient api, LookupCache lookups,
                              NotificationService notifications, LocalizationService localization)
    {
        _api = api;
        _lookups = lookups;
        _notifications = notifications;
        _localization = localization;
        UpdateMonthNames();
    }

    public void OnNavigatedTo()
    {
        if (!_loaded)
        {
            _ = LoadAsync();
            return;
        }

        UpdateMonthNames();
        BuildTypeSlices();
        RebuildMonthBars();
    }

    public void Invalidate() => _loaded = false;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await _lookups.LoadAsync();

            var newestTask = _api.GetDocumentsAsync(new DocumentQuery { PageSize = 1, Ordering = "-created" });
            var oldestTask = _api.GetDocumentsAsync(new DocumentQuery { PageSize = 1, Ordering = "created" });
            await Task.WhenAll(newestTask, oldestTask);
            var newest = await newestTask;
            var oldest = await oldestTask;

            TotalDocuments = newest.Count;
            TagCount = _lookups.Tags.Count;
            CorrespondentCount = _lookups.Correspondents.Count;
            DocumentTypeCount = _lookups.DocumentTypes.Count;
            _loaded = true;

            BuildTypeSlices();
            BuildTagCloud();

            int currentYear = DateTime.Now.Year;
            int newestYear = newest.Results.FirstOrDefault()?.Created?.Year ?? currentYear;
            int oldestYear = oldest.Results.FirstOrDefault()?.Created?.Year ?? newestYear;
            int topYear = Math.Max(newestYear, currentYear);
            int bottomYear = Math.Max(Math.Min(oldestYear, topYear), topYear - 30);

            _updatingYear = true;
            AvailableYears.Clear();
            for (int y = topYear; y >= bottomYear; y--)
                AvailableYears.Add(y);

            int year = SelectedYear is int chosen && AvailableYears.Contains(chosen) ? chosen : newestYear;
            SelectedYear = year;
            _updatingYear = false;

            await LoadMonthsAsync(year);
        }
        catch (Exception ex)
        {
            _notifications.ShowError(ex);
        }
        finally
        {
            _updatingYear = false;
            IsLoading = false;
        }
    }

    private async Task LoadMonthsAsync(int year)
    {
        _monthsCts?.Cancel();
        var cts = new CancellationTokenSource();
        _monthsCts = cts;

        IsLoadingMonths = true;
        try
        {
            var requests = Enumerable.Range(1, 12)
                .Select(month => _api.GetDocumentsAsync(
                    new DocumentQuery { PageSize = 1, CreatedYear = year, CreatedMonth = month }, cts.Token))
                .ToList();

            var results = await Task.WhenAll(requests);

            _monthCounts = results.Select(r => r.Count).ToArray();
            _monthsYear = year;
            RebuildMonthBars();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _notifications.ShowError(ex);
        }
        finally
        {
            if (_monthsCts == cts)
            {
                _monthsCts = null;
                IsLoadingMonths = false;
            }
            cts.Dispose();
        }
    }

    [RelayCommand]
    private void SelectType(PieSliceViewModel? slice)
    {
        if (slice?.DocumentTypeId is int id)
            FilterRequested?.Invoke(this, new FilterChip(FilterKind.DocumentType, id, slice.Name));
    }

    [RelayCommand]
    private void SelectMonth(MonthBarViewModel? bar)
    {
        if (bar is { HasDocuments: true })
            FilterRequested?.Invoke(this, FilterChip.ForMonth(bar.Year, bar.Month, $"{bar.FullName} {bar.Year}"));
    }

    [RelayCommand]
    private void SelectTag(TagCloudItemViewModel? item)
    {
        if (item is not null)
            FilterRequested?.Invoke(this, FilterChip.ForTag(item.Tag));
    }

    partial void OnSelectedYearChanged(int? value)
    {
        if (_updatingYear || value is not int year)
            return;
        _ = LoadMonthsAsync(year);
    }

    partial void OnFromMonthIndexChanged(int value)
    {
        if (_rebuildingMonthNames)
            return;
        if (value < 0)
        {
            FromMonthIndex = 0;
            return;
        }
        if (value > ToMonthIndex)
            ToMonthIndex = value;
        RebuildMonthBars();
    }

    partial void OnToMonthIndexChanged(int value)
    {
        if (_rebuildingMonthNames)
            return;
        if (value < 0)
        {
            ToMonthIndex = 11;
            return;
        }
        if (value < FromMonthIndex)
            FromMonthIndex = value;
        RebuildMonthBars();
    }

    private void BuildTypeSlices()
    {
        TypeSlices.Clear();

        var types = _lookups.DocumentTypes
            .Where(t => t.DocumentCount > 0)
            .OrderByDescending(t => t.DocumentCount)
            .ToList();

        var entries = new List<(string Name, int Count, int? Id, string Color)>();
        for (int i = 0; i < types.Count && i < MaxPieSlices; i++)
            entries.Add((types[i].Name, types[i].DocumentCount, types[i].Id, SliceColors[i % SliceColors.Length]));

        int otherCount = types.Skip(MaxPieSlices).Sum(t => t.DocumentCount);
        if (otherCount > 0)
            entries.Add((_localization.Get("Analytics_OtherTypes"), otherCount, null, OtherSliceColor));

        int withoutType = TotalDocuments - types.Sum(t => t.DocumentCount);
        if (withoutType > 0)
            entries.Add((_localization.Get("Analytics_NoType"), withoutType, FilterChip.NoDocumentTypeId, NoTypeSliceColor));

        int total = entries.Sum(e => e.Count);
        if (total == 0)
            return;

        double angle = 0;
        foreach (var entry in entries)
        {
            double sweep = entry.Count / (double)total * 360;
            var geometry = PieGeometry.CreateSlice(angle, sweep, PieRadius, PieInnerRadius);
            TypeSlices.Add(new PieSliceViewModel(entry.Name, entry.Count, entry.Count * 100.0 / total,
                                                 entry.Color, geometry, entry.Id));
            angle += sweep;
        }
    }

    private void RebuildMonthBars()
    {
        MonthBars.Clear();
        if (_monthsYear == 0)
            return;

        var culture = Culture;
        int from = Math.Clamp(FromMonthIndex, 0, 11) + 1;
        int to = Math.Clamp(ToMonthIndex, 0, 11) + 1;
        if (to < from)
            (from, to) = (to, from);

        int max = Math.Max(1, Enumerable.Range(from, to - from + 1).Max(m => _monthCounts[m - 1]));
        int total = 0;

        for (int month = from; month <= to; month++)
        {
            int count = _monthCounts[month - 1];
            total += count;
            double height = count == 0 ? 0 : Math.Max(4, count / (double)max * BarMaxHeight);

            MonthBars.Add(new MonthBarViewModel(
                _monthsYear, month,
                culture.DateTimeFormat.GetAbbreviatedMonthName(month),
                Capitalize(culture.DateTimeFormat.GetMonthName(month), culture),
                count, height));
        }
        PeriodTotal = total;
    }

    private void BuildTagCloud()
    {
        TagCloud.Clear();

        var tags = _lookups.Tags
            .Where(t => t.DocumentCount > 0)
            .OrderByDescending(t => t.DocumentCount)
            .Take(MaxCloudTags)
            .ToList();
        if (tags.Count == 0)
            return;

        int max = tags.Max(t => t.DocumentCount);
        int min = tags.Min(t => t.DocumentCount);

        foreach (var tag in tags.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            double ratio = max == min ? 1 : (tag.DocumentCount - min) / (double)(max - min);
            TagCloud.Add(new TagCloudItemViewModel(tag, MinTagFontSize + ratio * (MaxTagFontSize - MinTagFontSize)));
        }
    }

    private CultureInfo Culture
    {
        get
        {
            try
            {
                return CultureInfo.GetCultureInfo(_localization.CurrentLanguage == "en" ? "en-US" : "sr-Latn-RS");
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }
    }

    private void UpdateMonthNames()
    {
        if (_monthNamesLanguage == _localization.CurrentLanguage)
            return;

        int from = FromMonthIndex;
        int to = ToMonthIndex;

        _rebuildingMonthNames = true;
        var culture = Culture;
        MonthNames.Clear();
        for (int month = 1; month <= 12; month++)
            MonthNames.Add(Capitalize(culture.DateTimeFormat.GetMonthName(month), culture));
        FromMonthIndex = from;
        ToMonthIndex = to;
        _rebuildingMonthNames = false;

        _monthNamesLanguage = _localization.CurrentLanguage;
    }

    private static string Capitalize(string text, CultureInfo culture)
        => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0], culture) + text[1..];
}