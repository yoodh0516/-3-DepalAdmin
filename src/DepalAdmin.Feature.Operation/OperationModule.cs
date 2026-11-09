using DepalAdmin.Core.Pages;
using DepalAdmin.Feature.Operation.Panel;
using Microsoft.Extensions.DependencyInjection;

namespace DepalAdmin.Feature.Operation;

/// <summary>담당: 유동호 — 라인 조작 버튼 패널 (김초아의 라인 현황 화면 오른쪽에 들어감)</summary>
public static class OperationModule
{
    public static IServiceCollection AddOperationFeature(this IServiceCollection services)
    {
        services.AddSingleton<ControlPanelViewModel>();
        services.AddSingleton<IPanel, ControlPanel>();
        return services;
    }
}

public sealed class ControlPanel : PanelBase<ControlPanelView>
{
    public ControlPanel(ControlPanelViewModel vm) : base(vm) { }
    public override string Slot => PanelSlots.LineControls;
}
