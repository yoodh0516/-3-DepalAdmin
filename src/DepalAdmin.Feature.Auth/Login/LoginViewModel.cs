using CommunityToolkit.Mvvm.ComponentModel;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Settings;

namespace DepalAdmin.Feature.Auth.Login;

/// <summary>
/// 로그인 창. 비밀번호는 보안상 바인딩하지 않고 창 코드에서 직접 넘겨받는다
/// (PasswordBox.Password는 일부러 바인딩이 안 되게 만들어져 있음).
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private UserAccount? _pendingUser;   // 비밀번호를 바꿔야 하는 사용자

    public LoginViewModel(AuthService auth, AppSettings settings)
    {
        _auth = auth;
        StationName = settings.StationName;
        ConnectionHint = settings.UseFakeMqtt ? "가짜 데이터 모드" : $"브로커 {settings.BrokerHost}:{settings.BrokerPort}";
    }

    public string StationName { get; }
    public string ConnectionHint { get; }

    [ObservableProperty] private string _userId = "";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private bool _isError;
    [ObservableProperty] private bool _isChangeMode;

    /// <summary>true면 창을 닫아도 된다 (로그인 완료)</summary>
    public event EventHandler? LoggedIn;

    public void TryLogin(string password)
    {
        var result = _auth.Login(UserId, password);
        if (!result.Ok)
        {
            Show(result.Message, error: true);
            return;
        }

        if (result.MustChangePassword)
        {
            _pendingUser = result.User;
            IsChangeMode = true;
            Show(result.Message, error: false);
            return;
        }

        LoggedIn?.Invoke(this, EventArgs.Empty);
    }

    public void TryChangePassword(string newPassword, string confirm)
    {
        if (_pendingUser == null) return;
        var problem = _auth.ChangePasswordAndLogin(_pendingUser, newPassword, confirm);
        if (problem != null)
        {
            Show(problem, error: true);
            return;
        }
        LoggedIn?.Invoke(this, EventArgs.Empty);
    }

    private void Show(string message, bool error)
    {
        Message = message;
        IsError = error;
    }
}
