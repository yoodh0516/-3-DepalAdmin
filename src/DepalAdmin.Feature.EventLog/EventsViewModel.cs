using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Export;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Models;
using DepalAdmin.Core.Settings;

namespace DepalAdmin.Feature.EventLog;

/// <summary>
/// 실시간 이벤트 로그. 등급·장치로 거르고 글자로 찾는다.
/// 받는 것: server/events
/// </summary>
public partial class EventsViewModel : ObservableObject
{
    private const string All = "전체";
    private readonly int _maxRows;
    private readonly Session _session;

    public ObservableCollection<EventRow> Rows { get; } = new();
    public ICollectionView View { get; }
    public string[] Levels { get; } = { All, "INFO", "WARN", "ERROR" };
    public ObservableCollection<string> Devices { get; } = new() { All };

    [ObservableProperty] private string _level = All;
    [ObservableProperty] private string _device = All;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _summary = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private bool _canExport;

    public EventsViewModel(ILineClient client, AppSettings settings, Session session)
    {
        _maxRows = settings.MaxRowsInMemory;
        _session = session;

        View = CollectionViewSource.GetDefaultView(Rows);
        View.Filter = Filter;

        client.EventReceived += (_, ev) => Add(ev);
        _session.Changed += (_, _) => CanExport = _session.Can(Permission.ExportData);
        CanExport = _session.Can(Permission.ExportData);
        UpdateSummary();
    }

    partial void OnLevelChanged(string value) => View.Refresh();
    partial void OnDeviceChanged(string value) => View.Refresh();
    partial void OnSearchTextChanged(string value) => View.Refresh();

    private void Add(LineEvent ev)
    {
        Rows.Insert(0, new EventRow(ev));
        while (Rows.Count > _maxRows) Rows.RemoveAt(Rows.Count - 1);

        var device = Labels.Device(ev.Device);
        if (!Devices.Contains(device)) Devices.Add(device);
        UpdateSummary();
    }

    private bool Filter(object item)
    {
        if (item is not EventRow row) return false;
        if (Level != All && row.Level != Level) return false;
        if (Device != All && row.DeviceText != Device) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        return row.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateSummary()
    {
        var error = Rows.Count(r => r.Level == "ERROR");
        var warn = Rows.Count(r => r.Level == "WARN");
        Summary = $"전체 {Rows.Count}   고장 {error}   주의 {warn}";
    }

    [RelayCommand]
    private void Clear()
    {
        Rows.Clear();
        UpdateSummary();
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Export()
    {
        CsvExport.Save($"이벤트_{DateTime.Now:yyyyMMdd_HHmm}.csv", View.Cast<EventRow>(),
            ("시각", r => r.Raw.Ts),
            ("등급", r => r.LevelText),
            ("장치", r => r.DeviceText),
            ("내용", r => r.Message),
            ("회차", r => r.Raw.RunId),
            ("이벤트 번호", r => r.Raw.EventId));
    }
}

public sealed class EventRow
{
    public EventRow(LineEvent raw) => Raw = raw;

    public LineEvent Raw { get; }
    public string Time => Json.ParseTs(Raw.Ts)?.ToString("HH:mm:ss.fff") ?? Raw.Ts;
    public string Level => Raw.Level;
    public string LevelText => Labels.Level(Raw.Level);
    public string DeviceText => Labels.Device(Raw.Device);
    public string Message => Raw.Message;
}
