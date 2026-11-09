using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core;
using DepalAdmin.Core.Alarms;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Pages;
using DepalAdmin.Core.Settings;

namespace DepalAdmin.Shell;

/// <summary>
/// 담당: 조시현 — 메인 창.
///   위 — 라인 이름, 라인 상태, 연결, 시계, 로그인한 사람(+자동 로그아웃 남은 시간), 비상정지
///   탭 — 권한에 맞는 페이지만, 왼쪽부터 F1~F12, 배지(안 읽은 수)
///   아래 — 알람 줄 자리 (노원종 패널)
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private const int FunctionKeys = 12;

    private readonly ILineClient _client;
    private readonly Session _session;
    private readonly IAlarmService _alarms;
    private readonly ILoginFlow _login;
    private readonly IAuditLog _audit;
    private readonly AppSettings _settings;
    private readonly Navigator _navigator;
    private readonly List<IPage> _pages;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _lastActivity = DateTime.Now;

    public ObservableCollection<NavItem> Tabs { get; } = new();

    /// <summary>맨 아래 알람 줄 (노원종 패널, 없으면 null)</summary>
    public FrameworkElement? AlarmBar { get; }

    [ObservableProperty] private NavItem? _selectedTab;

    [ObservableProperty] private string _stationName = "";
    [ObservableProperty] private string _connectionKey = "disconnected";
    [ObservableProperty] private string _connectionText = "연결 안 됨";
    [ObservableProperty] private string _mode = "idle";
    [ObservableProperty] private string _modeText = "-";
    [ObservableProperty] private string _clock = "";

    [ObservableProperty] private bool _isLocked = true;
    [ObservableProperty] private string _userText = "";
    [ObservableProperty] private string _roleText = "";
    [ObservableProperty] private string _idleText = "";

    public MainViewModel(IEnumerable<IPage> pages, IEnumerable<IPanel> panels, ILineClient client, Session session,
                         IAlarmService alarms, ILoginFlow login, IAuditLog audit, AppSettings settings, Navigator navigator)
    {
        _pages = pages.OrderBy(p => p.Order).ToList();
        _client = client;
        _session = session;
        _alarms = alarms;
        _login = login;
        _audit = audit;
        _settings = settings;
        _navigator = navigator;

        AlarmBar = PanelSlots.Find(panels, PanelSlots.ShellAlarmBar);

        _client.ConnectionChanged += (_, state) => ShowConnection(state);
        _client.LineStatusReceived += (_, line) =>
        {
            Mode = line.Mode;
            ModeText = Labels.Mode(line.Mode);
        };
        _session.Changed += (_, _) => OnSessionChanged();
        _navigator.NavigateRequested += (_, title) => GoTo(title);

        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        ShowConnection(_client.State);
        OnSessionChanged();
        Tick();
    }

    partial void OnSelectedTabChanged(NavItem? value) => _navigator.SetCurrent(value?.Title);

    // ── 로그인 상태 ──────────────────────────────────────

    private void OnSessionChanged()
    {
        IsLocked = !_session.IsLoggedIn;
        UserText = _session.User?.Name ?? "로그인 안 함";
        RoleText = _session.User == null ? "" : Labels.RoleName(_session.User.Role);
        _lastActivity = DateTime.Now;
        BuildTabs();
    }

    /// <summary>권한에 맞는 페이지만 탭으로. 고르던 탭은 되도록 유지.</summary>
    private void BuildTabs()
    {
        var keepTitle = SelectedTab?.Title;
        Tabs.Clear();
        var n = 0;
        foreach (var page in _pages.Where(p => _session.Can(p.Required)))
        {
            n++;
            Tabs.Add(new NavItem(page, n <= FunctionKeys ? $"F{n}" : ""));
        }
        SelectedTab = Tabs.FirstOrDefault(t => t.Title == keepTitle) ?? Tabs.FirstOrDefault();
    }

    private void GoTo(string title)
    {
        if (IsLocked) return;
        var tab = Tabs.FirstOrDefault(t => t.Title == title);
        if (tab != null) SelectedTab = tab;
    }

    /// <summary>창에서 키·마우스 입력이 있을 때마다 부른다 (자동 로그아웃 계산용)</summary>
    public void NoteActivity() => _lastActivity = DateTime.Now;

    /// <summary>F1 = 0번 탭</summary>
    public void SelectTab(int index)
    {
        if (IsLocked || index < 0 || index >= Tabs.Count) return;
        SelectedTab = Tabs[index];
    }

    private void Tick()
    {
        Clock = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss");
        StationName = _settings.StationName;

        if (!_session.IsLoggedIn || _settings.AutoLogoutMinutes <= 0)
        {
            IdleText = "";
            return;
        }

        var left = _settings.AutoLogoutMinutes * 60 - (DateTime.Now - _lastActivity).TotalSeconds;
        if (left <= 0)
        {
            _audit.Write("자동 로그아웃", $"{_settings.AutoLogoutMinutes}분 동안 조작 없음");
            _session.SignOut();
            return;
        }
        IdleText = left <= 60 ? $"{Math.Ceiling(left)}초 뒤 자동 로그아웃" : "";
    }

    [RelayCommand]
    private void Logout()
    {
        _audit.Write("로그아웃", "");
        _session.SignOut();
    }

    [RelayCommand]
    private void Login() => _login.ShowLogin(Application.Current.MainWindow);

    // ── 연결 ────────────────────────────────────────────

    private void ShowConnection(ConnectionState state)
    {
        ConnectionKey = state.ToString().ToLowerInvariant();
        ConnectionText = state switch
        {
            ConnectionState.Fake => "가짜 데이터",
            ConnectionState.Connected => $"서버 연결 {_settings.BrokerHost}",
            ConnectionState.Connecting => "연결 중",
            _ => "서버 연결 끊김",
        };
    }

    // ── 비상정지 ─────────────────────────────────────────

    /// <summary>
    /// 확인 창 없이 바로 보낸다. 로그인 안 했어도 보낸다.
    /// 못 보내면 현장 비상정지 버튼을 누르라고 알린다 (화면 버튼은 보조 수단).
    /// </summary>
    [RelayCommand]
    private async Task EstopAsync()
    {
        try
        {
            await _client.SendCommandAsync(UiActions.Estop);
        }
        catch (Exception ex)
        {
            _alarms.Raise("ui", "ERROR", "화면에서 비상정지 명령을 보내지 못함");
            MessageBox.Show($"비상정지 명령을 보내지 못했습니다.\n현장의 비상정지 버튼을 누르세요.\n\n({ex.Message})",
                            "비상정지 실패", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

/// <summary>탭 하나. Badge는 페이지 ViewModel이 IHasBadge를 구현했을 때만 값이 있다.</summary>
public sealed class NavItem
{
    public NavItem(IPage page, string key)
    {
        Page = page;
        Key = key;
        Badge = page.ViewModel as IHasBadge;
    }

    public IPage Page { get; }
    public string Key { get; }
    public string Title => Page.Title;
    public IHasBadge? Badge { get; }
}
