using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Pages;
using DepalAdmin.Feature.Auth.Accounts;
using DepalAdmin.Feature.Auth.Audit;
using Microsoft.Extensions.DependencyInjection;

namespace DepalAdmin.Feature.Auth;

/// <summary>담당: 장성찬 — 로그인 · 계정 관리 · 작업 기록</summary>
public static class AuthModule
{
    public static IServiceCollection AddAuthFeature(this IServiceCollection services)
    {
        services.AddSingleton<JsonUserStore>();
        services.AddSingleton<AuthService>();
        services.AddSingleton<ILoginFlow>(sp => sp.GetRequiredService<AuthService>());
        services.AddSingleton<IAuditLog, FileAuditLog>();

        services.AddSingleton<AccountsViewModel>();
        services.AddSingleton<IPage, AccountsPage>();
        services.AddSingleton<AuditViewModel>();
        services.AddSingleton<IPage, AuditPage>();
        return services;
    }
}

public sealed class AccountsPage : PageBase<AccountsView>
{
    public AccountsPage(AccountsViewModel vm) : base(vm) { }
    public override string Title => "계정";
    public override int Order => 110;
    public override Permission Required => Permission.ManageUsers;
}

public sealed class AuditPage : PageBase<AuditView>
{
    public AuditPage(AuditViewModel vm) : base(vm) { }
    public override string Title => "작업 기록";
    public override int Order => 120;
    public override Permission Required => Permission.ViewAudit;
}
