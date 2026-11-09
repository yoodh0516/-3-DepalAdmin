using DepalAdmin.Core.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace DepalAdmin.Feature.Report;

/// <summary>담당: 조시현 — 보고서 (일일 / 회차)</summary>
public static class ReportModule
{
    public static IServiceCollection AddReportFeature(this IServiceCollection services)
    {
        services.AddSingleton<ReportViewModel>();
        services.AddSingleton<IPage, ReportPage>();
        return services;
    }
}

public sealed class ReportPage : PageBase<ReportView>
{
    public ReportPage(ReportViewModel vm) : base(vm) { }
    public override string Title => "보고서";
    public override int Order => 100;
}
