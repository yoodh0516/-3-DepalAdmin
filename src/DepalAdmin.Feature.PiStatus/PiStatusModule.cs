using DepalAdmin.Core.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace DepalAdmin.Feature.PiStatus;

/// <summary>담당: 노원종 — 라즈베리파이 세부 패널 (조시현의 장치 상태 화면 오른쪽에 들어감)</summary>
public static class PiStatusModule
{
    public static IServiceCollection AddPiStatusFeature(this IServiceCollection services)
    {
        services.AddSingleton<PiStatusViewModel>();
        services.AddSingleton<IPanel, PiStatusPanel>();
        return services;
    }
}

public sealed class PiStatusPanel : PanelBase<PiStatusView>
{
    public PiStatusPanel(PiStatusViewModel vm) : base(vm) { }
    public override string Slot => PanelSlots.DevicesPi;
}
