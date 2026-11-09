using DepalAdmin.Core.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace DepalAdmin.Feature.Devices;

/// <summary>담당: 조시현 — 장치 상태 카드. 오른쪽 라파 세부는 노원종 패널(PanelSlots.DevicesPi).</summary>
public static class DevicesModule
{
    public static IServiceCollection AddDevicesFeature(this IServiceCollection services)
    {
        services.AddSingleton<DevicesViewModel>();
        services.AddSingleton<IPage, DevicesPage>();
        return services;
    }
}

public sealed class DevicesPage : PageBase<DevicesView>
{
    public DevicesPage(DevicesViewModel vm) : base(vm) { }
    public override string Title => "장치 상태";
    public override int Order => 30;
}
