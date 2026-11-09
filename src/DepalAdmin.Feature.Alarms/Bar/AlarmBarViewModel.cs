using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core;
using DepalAdmin.Core.Alarms;
using DepalAdmin.Core.Pages;

namespace DepalAdmin.Feature.Alarms.Bar;

/// <summary>
/// 메인 창 맨 아래 알람 줄. 확인 안 된 것 중 가장 급한 알람 하나를 보여 준다.
/// 고장(ERROR)이면 빨강, 주의(WARN)면 호박색, 없으면 회색.
/// </summary>
public partial class AlarmBarViewModel : ObservableObject
{
    private readonly IAlarmService _alarms;
    private readonly Navigator _navigator;

    [ObservableProperty] private string _bannerKey = "none";   // none / warn / alarm
    [ObservableProperty] private string _bannerText = "확인 안 된 알람 없음";
    [ObservableProperty] private int _unacked;

    public AlarmBarViewModel(IAlarmService alarms, Navigator navigator)
    {
        _alarms = alarms;
        _navigator = navigator;
        _alarms.Changed += (_, _) => Update();
        Update();
    }

    private void Update()
    {
        Unacked = _alarms.UnackedCount;
        var top = _alarms.MostUrgent;
        if (top == null)
        {
            BannerKey = "none";
            BannerText = "확인 안 된 알람 없음";
            return;
        }
        BannerKey = top.Level == "ERROR" ? "alarm" : "warn";
        BannerText = $"{top.LastAt:HH:mm:ss}  {Labels.Device(top.Device)}  {top.Message}" + (top.Count > 1 ? $"  ({top.Count}회)" : "");
    }

    [RelayCommand]
    private void GoToAlarms() => _navigator.Navigate("알람");
}
