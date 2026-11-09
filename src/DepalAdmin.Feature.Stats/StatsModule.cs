using DepalAdmin.Core.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace DepalAdmin.Feature.Stats;

/// <summary>담당: 조시현 — 통계</summary>
public static class StatsModule
{
    public static IServiceCollection AddStatsFeature(this IServiceCollection services)
    {
        services.AddSingleton<StatsViewModel>();
        services.AddSingleton<IPage, StatsPage>();
        return services;
    }
}

public sealed class StatsPage : PageBase<StatsView>
{
    public StatsPage(StatsViewModel vm) : base(vm) { }
    public override string Title => "통계";
    public override int Order => 90;
}
