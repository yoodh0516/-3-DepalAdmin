using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DepalAdmin.Core.Alarms;

/// <summary>
/// 알람 1건. WARN·ERROR 이벤트가 오면 만들어지고, 작업자가 '확인'할 때까지 남는다.
/// 같은 장치·같은 내용이 짧은 시간에 반복되면 새로 만들지 않고 Count만 올린다.
/// </summary>
public partial class Alarm : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime FirstAt { get; init; }
    public string Device { get; init; } = "";
    public string Level { get; init; } = "WARN";
    public string Message { get; init; } = "";

    [ObservableProperty] private DateTime _lastAt;
    [ObservableProperty] private int _count = 1;
    [ObservableProperty] private bool _acked;
    [ObservableProperty] private string? _ackedBy;
    [ObservableProperty] private DateTime? _ackedAt;
}

/// <summary>
/// 알람 목록을 관리한다. 구현: Feature.Alarms의 AlarmService.
/// 메인 창 아래 알람 줄과 알람 페이지가 같이 쓴다.
/// </summary>
public interface IAlarmService
{
    /// <summary>최신이 위. UI 스레드에서만 바뀐다.</summary>
    ObservableCollection<Alarm> Alarms { get; }

    int UnackedCount { get; }

    /// <summary>확인 안 된 알람 중 가장 급한 것 (ERROR 우선, 그다음 최신)</summary>
    Alarm? MostUrgent { get; }

    /// <summary>알람이 생기거나 확인될 때</summary>
    event EventHandler? Changed;

    /// <summary>권한이 없으면 false</summary>
    bool Ack(Alarm alarm);

    /// <summary>확인한 개수 (권한이 없으면 0)</summary>
    int AckAll();

    /// <summary>화면 자체에서 알람을 낼 때 (예: 브로커 연결 끊김)</summary>
    void Raise(string device, string level, string message);
}
