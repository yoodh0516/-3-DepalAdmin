using DepalAdmin.Core.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace DepalAdmin.Feature.EventLog;

/// <summary>담당: 장성찬 — 이벤트 로그</summary>
public static class EventLogModule
{
    public static IServiceCollection AddEventLogFeature(this IServiceCollection services)
    {
        services.AddSingleton<EventsViewModel>();
        services.AddSingleton<IPage, EventsPage>();
        return services;
    }
}

public sealed class EventsPage : PageBase<EventsView>
{
    public EventsPage(EventsViewModel vm) : base(vm) { }
    public override string Title => "이벤트 로그";
    public override int Order => 50;
}
