using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core;
using DepalAdmin.Core.Alarms;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Export;
using DepalAdmin.Core.Pages;

namespace DepalAdmin.Feature.Alarms;

/// <summary>담당: 노원종 — 알람 탭. 탭 배지 = 확인 안 된 알람 수.</summary>
public partial class AlarmsViewModel : ObservableObject, IHasBadge
{
    private readonly IAlarmService _service;
    private readonly Session _session;

    public ICollectionView View { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AckSelectedCommand))]
    private Alarm? _selected;

    [ObservableProperty] private bool _onlyUnacked = true;
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private int _badgeCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AckSelectedCommand), nameof(AckAllCommand), nameof(ExportCommand))]
    private bool _canAck;

    public AlarmsViewModel(IAlarmService service, Session session)
    {
        _service = service;
        _session = session;

        View = CollectionViewSource.GetDefaultView(_service.Alarms);
        View.Filter = o => o is Alarm a && (!OnlyUnacked || !a.Acked);

        _service.Changed += (_, _) => Refresh();
        _session.Changed += (_, _) => CanAck = _session.Can(Permission.AckAlarm);
        CanAck = _session.Can(Permission.AckAlarm);
        Refresh();
    }

    partial void OnOnlyUnackedChanged(bool value) => View.Refresh();

    private void Refresh()
    {
        if (OnlyUnacked) View.Refresh();   // 방금 확인한 줄이 사라지게
        var all = _service.Alarms;
        BadgeCount = _service.UnackedCount;
        Summary = $"확인 안 됨 {_service.UnackedCount}건  /  고장 {all.Count(a => !a.Acked && a.Level == "ERROR")}건  /  전체 {all.Count}건";
    }

    private bool CanAckSelected() => CanAck && Selected is { Acked: false };

    [RelayCommand(CanExecute = nameof(CanAckSelected))]
    private void AckSelected()
    {
        if (Selected != null) _service.Ack(Selected);
    }

    [RelayCommand(CanExecute = nameof(CanAck))]
    private void AckAll() => _service.AckAll();

    [RelayCommand(CanExecute = nameof(CanAck))]
    private void Export()
    {
        CsvExport.Save($"알람_{DateTime.Now:yyyyMMdd_HHmm}.csv", _service.Alarms,
            ("처음", a => a.FirstAt),
            ("마지막", a => a.LastAt),
            ("횟수", a => a.Count),
            ("등급", a => Labels.Level(a.Level)),
            ("장치", a => Labels.Device(a.Device)),
            ("내용", a => a.Message),
            ("확인자", a => a.AckedBy),
            ("확인 시각", a => a.AckedAt));
    }
}
