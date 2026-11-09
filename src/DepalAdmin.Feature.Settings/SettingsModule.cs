using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace DepalAdmin.Feature.Settings;

/// <summary>담당: 노원종 — 설정</summary>
public static class SettingsModule
{
    public static IServiceCollection AddSettingsFeature(this IServiceCollection services)
    {
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<IPage, SettingsPage>();
        return services;
    }
}

public sealed class SettingsPage : PageBase<SettingsView>
{
    public SettingsPage(SettingsViewModel vm) : base(vm) { }
    public override string Title => "설정";
    public override int Order => 130;
    public override Permission Required => Permission.EditSettings;
}
