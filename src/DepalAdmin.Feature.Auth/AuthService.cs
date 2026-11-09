using System.Windows;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Settings;
using DepalAdmin.Feature.Auth.Login;

namespace DepalAdmin.Feature.Auth;

public sealed record LoginResult(bool Ok, string Message, UserAccount? User = null, bool MustChangePassword = false);

/// <summary>
/// 로그인 규칙:
///   - 5번 틀리면 30초 동안 로그인 막힘
///   - 사용 중지된 계정은 로그인 불가
///   - 임시 비밀번호(처음·초기화)면 새 비밀번호를 정해야 들어갈 수 있음
/// </summary>
public sealed class AuthService : ILoginFlow
{
    // ── 수정할 값 ──────────────────────────────────────────
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockTime = TimeSpan.FromSeconds(30);
    // ──────────────────────────────────────────────────────

    private readonly JsonUserStore _store;
    private readonly Session _session;
    private readonly IAuditLog _audit;
    private readonly AppSettings _settings;

    private int _failures;
    private DateTime _lockedUntil = DateTime.MinValue;

    public AuthService(JsonUserStore store, Session session, IAuditLog audit, AppSettings settings)
    {
        _store = store;
        _session = session;
        _audit = audit;
        _settings = settings;
    }

    public bool ShowLogin(Window? owner)
    {
        var window = new LoginWindow(new LoginViewModel(this, _settings));
        if (owner != null && owner.IsVisible) window.Owner = owner;
        return window.ShowDialog() == true;
    }

    public LoginResult Login(string id, string password)
    {
        var wait = _lockedUntil - DateTime.Now;
        if (wait > TimeSpan.Zero)
            return new LoginResult(false, $"여러 번 틀려서 잠겼습니다. {Math.Ceiling(wait.TotalSeconds)}초 뒤에 다시 시도하세요.");

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrEmpty(password))
            return new LoginResult(false, "아이디와 비밀번호를 입력하세요.");

        var user = _store.Find(id);
        if (user == null || !_store.CheckPassword(user, password))
        {
            _failures++;
            _audit.Write("로그인 실패", $"아이디 '{id.Trim()}' ({_failures}/{MaxFailures})");
            if (_failures >= MaxFailures)
            {
                _failures = 0;
                _lockedUntil = DateTime.Now + LockTime;
                return new LoginResult(false, $"{MaxFailures}번 틀렸습니다. {LockTime.TotalSeconds:0}초 동안 로그인할 수 없습니다.");
            }
            return new LoginResult(false, $"아이디 또는 비밀번호가 맞지 않습니다. ({_failures}/{MaxFailures})");
        }

        if (user.Disabled)
        {
            _audit.Write("로그인 실패", $"'{user.Id}' 사용 중지된 계정");
            return new LoginResult(false, "사용 중지된 계정입니다. 관리자에게 문의하세요.");
        }

        _failures = 0;
        if (user.MustChangePassword)
            return new LoginResult(true, "임시 비밀번호입니다. 새 비밀번호를 정하세요.", user, MustChangePassword: true);

        Complete(user);
        return new LoginResult(true, "", user);
    }

    /// <summary>문제가 있으면 이유, 성공하면 null (그리고 로그인 완료)</summary>
    public string? ChangePasswordAndLogin(UserAccount user, string newPassword, string confirm)
    {
        if (newPassword.Length < JsonUserStore.MinPasswordLength)
            return $"비밀번호는 {JsonUserStore.MinPasswordLength}자 이상이어야 합니다.";
        if (newPassword != confirm)
            return "두 칸의 비밀번호가 다릅니다.";
        if (_store.CheckPassword(user, newPassword))
            return "지금 비밀번호와 다른 비밀번호를 정하세요.";

        _store.SetPassword(user, newPassword, mustChangePassword: false);
        Complete(user);
        _audit.Write("비밀번호 변경", "임시 비밀번호를 새 비밀번호로 바꿈");
        return null;
    }

    private void Complete(UserAccount user)
    {
        user.LastLoginAt = DateTime.Now;
        _store.Update(user);
        _session.SignIn(user);
        _audit.Write("로그인", $"{Core.Labels.RoleName(user.Role)} 권한");
    }
}
