using DepalAdmin.Core.Alarms;
using DepalAdmin.Core.Pages;
using DepalAdmin.Feature.Alarms.Bar;
using Microsoft.Extensions.DependencyInjection;

namespace DepalAdmin.Feature.Alarms;

/// <summary>담당: 노원종 — 알람 서비스 · 알람 탭 · 메인 창 아래 알람 줄</summary>
public static class AlarmsModule
{
    public static IServiceCollection AddAlarmsFeature(this IServiceCollection services)
    {
        services.AddSingleton<IAlarmService, AlarmService>();

        services.AddSingleton<AlarmsViewModel>();
        services.AddSingleton<IPage, AlarmsPage>();

        // 메인 창 맨 아래 줄 (자리 주인: 조시현 Shell)
        services.AddSingleton<AlarmBarViewModel>();
        services.AddSingleton<IPanel, AlarmBarPanel>();
        return services;
    }
}

public sealed class AlarmsPage : PageBase<AlarmsView>
{
    public AlarmsPage(AlarmsViewModel vm) : base(vm) { }
    public override string Title => "알람";
    public override int Order => 20;
}

public sealed class AlarmBarPanel : PanelBase<AlarmBarView>
{
    public AlarmBarPanel(AlarmBarViewModel vm) : base(vm) { }
    public override string Slot => PanelSlots.ShellAlarmBar;
}
