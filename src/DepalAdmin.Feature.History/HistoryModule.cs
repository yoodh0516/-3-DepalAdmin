using DepalAdmin.Core.Pages;
using DepalAdmin.Feature.History.Records;
using Microsoft.Extensions.DependencyInjection;

namespace DepalAdmin.Feature.History;

/// <summary>
/// 담당: 장성찬 — 이력 조회.
/// DB 연결(IHistoryRepository: 가짜/MySqlHistoryRepository)은 Shell이 골라서 등록한다.
/// 통계·보고서(조시현)도 같은 IHistoryRepository를 쓰므로, 메서드 모양을 바꿀 때는 미리 알려 주기.
/// </summary>
public static class HistoryModule
{
    public static IServiceCollection AddHistoryFeature(this IServiceCollection services)
    {
        services.AddSingleton<RecordsViewModel>();
        services.AddSingleton<IPage, RecordsPage>();
        return services;
    }
}

public sealed class RecordsPage : PageBase<RecordsView>
{
    public RecordsPage(RecordsViewModel vm) : base(vm) { }
    public override string Title => "이력 조회";
    public override int Order => 80;
}
