using System.Windows;

namespace DepalAdmin.Core.Auth;

/// <summary>계정 등급. 숫자가 클수록 권한이 많다.</summary>
public enum Role
{
    Viewer = 0,     // 보기만
    Operator = 1,   // 작업자: 라인 조작, 알람 확인, 내보내기
    Admin = 2,      // 관리자: 계정·설정·작업 기록까지
}

/// <summary>화면에서 확인하는 권한 단위.</summary>
public enum Permission
{
    ViewData,
    OperateLine,
    AckAlarm,
    ExportData,
    ManageUsers,
    EditSettings,
    ViewAudit,
}

public static class Permissions
{
    /// <summary>등급별 권한표. 바꿀 때는 README의 권한표도 같이 고친다.</summary>
    public static bool Allows(Role role, Permission permission) => permission switch
    {
        Permission.ViewData => true,
        Permission.OperateLine or Permission.AckAlarm or Permission.ExportData => role >= Role.Operator,
        _ => role == Role.Admin,
    };
}

/// <summary>계정 1개 (data/users.json에 저장). 비밀번호는 PBKDF2 해시로만 저장.</summary>
public sealed class UserAccount
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Role Role { get; set; } = Role.Viewer;
    public string PasswordHash { get; set; } = "";
    public string Salt { get; set; } = "";
    public bool MustChangePassword { get; set; }
    public bool Disabled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// 지금 로그인한 사람. 모든 페이지가 같은 객체를 받는다 (DI 싱글톤).
/// 권한 확인: session.Can(Permission.OperateLine)
/// </summary>
public sealed class Session
{
    public UserAccount? User { get; private set; }
    public bool IsLoggedIn => User != null;
    public string UserName => User?.Name ?? "-";

    /// <summary>로그인·로그아웃할 때 (UI 스레드)</summary>
    public event EventHandler? Changed;

    public void SignIn(UserAccount user)
    {
        User = user;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SignOut()
    {
        if (User == null) return;
        User = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Can(Permission permission) => User != null && Permissions.Allows(User.Role, permission);
}

/// <summary>로그인 창을 띄운다. 구현: Feature.Auth의 AuthService.</summary>
public interface ILoginFlow
{
    /// <summary>로그인에 성공하면 true (Session에 사용자가 들어가 있음)</summary>
    bool ShowLogin(Window? owner);
}

/// <summary>누가 언제 무엇을 했는지 남기는 작업 기록. 구현: Feature.Auth의 FileAuditLog.</summary>
public interface IAuditLog
{
    /// <summary>기록 실패해도 예외를 던지지 않는다 (비상정지 같은 동작을 막으면 안 되므로).</summary>
    void Write(string category, string message);

    Task<IReadOnlyList<AuditEntry>> ReadAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

public sealed class AuditEntry
{
    public DateTime Time { get; set; }
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Message { get; set; } = "";
}
