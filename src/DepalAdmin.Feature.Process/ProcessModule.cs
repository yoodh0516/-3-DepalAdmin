using DepalAdmin.Core.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace DepalAdmin.Feature.Process;

/// <summary>담당: 김초아 — 라인 현황 탭 (팔레트 자리: 김하균, 조작 버튼 자리: 유동호)</summary>
public static class ProcessModule
{
    public static IServiceCollection AddProcessFeature(this IServiceCollection services)
    {
        services.AddSingleton<ProcessViewModel>();
        services.AddSingleton<IPage, ProcessPage>();
        return services;
    }
}

public sealed class ProcessPage : PageBase<ProcessView>
{
    public ProcessPage(ProcessViewModel vm) : base(vm) { }
    public override string Title => "라인 현황";
    public override int Order => 10;
}
