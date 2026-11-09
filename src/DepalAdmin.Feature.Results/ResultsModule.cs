using DepalAdmin.Core.Pages;
using DepalAdmin.Feature.Results.Pallet;
using Microsoft.Extensions.DependencyInjection;

namespace DepalAdmin.Feature.Results;

/// <summary>담당: 김하균 — 검사 결과 탭 · 팔레트 2×5 맵 패널</summary>
public static class ResultsModule
{
    public static IServiceCollection AddResultsFeature(this IServiceCollection services)
    {
        services.AddSingleton<ResultsViewModel>();
        services.AddSingleton<IPage, ResultsPage>();

        // 라인 현황 화면(김초아)에 끼워 들어가는 팔레트 맵
        services.AddSingleton<PalletViewModel>();
        services.AddSingleton<IPanel, PalletPanel>();
        return services;
    }
}

public sealed class ResultsPage : PageBase<ResultsView>
{
    public ResultsPage(ResultsViewModel vm) : base(vm) { }
    public override string Title => "검사 결과";
    public override int Order => 40;
}

public sealed class PalletPanel : PanelBase<PalletView>
{
    public PalletPanel(PalletViewModel vm) : base(vm) { }
    public override string Slot => PanelSlots.LinePallet;
}
