using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Messaging;

namespace DepalAdmin.Feature.Operation.Panel;

/// <summary>
/// 담당: 유동호 — 라인 조작 버튼 (김초아의 라인 현황 화면 오른쪽에 들어감).
/// 받는 것: server/line의 mode  /  보내는 것: ui/cmd
/// </summary>
public partial class ControlPanelViewModel : ObservableObject
{
    private readonly ILineClient _client;
    private readonly Session _session;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(PauseCommand), nameof(ResumeCommand),
                                nameof(StopCommand), nameof(SkipCommand), nameof(ResetCommand))]
    private string _mode = "idle";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(PauseCommand), nameof(ResumeCommand),
                                nameof(StopCommand), nameof(SkipCommand), nameof(ResetCommand))]
    private bool _canOperate;

    [ObservableProperty] private string _permissionHint = "";
    [ObservableProperty] private string _commandResult = "";

    public ControlPanelViewModel(ILineClient client, Session session)
    {
        _client = client;
        _session = session;

        _client.LineStatusReceived += (_, line) => Mode = line.Mode;
        _session.Changed += (_, _) => UpdatePermission();
        if (_client.LastLine != null) Mode = _client.LastLine.Mode;
        UpdatePermission();
    }

    private void UpdatePermission()
    {
        CanOperate = _session.Can(Permission.OperateLine);
        PermissionHint = CanOperate ? "" : "보기 전용 계정이라 비상정지만 쓸 수 있습니다.";
    }

    // TODO(유동호): 어떤 상태에서 어떤 버튼을 켤지 팀과 확인

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task StartAsync() => SendAsync(UiActions.Start, "시작");

    [RelayCommand(CanExecute = nameof(CanPause))]
    private Task PauseAsync() => SendAsync(UiActions.Pause, "일시정지");

    [RelayCommand(CanExecute = nameof(CanResume))]
    private Task ResumeAsync() => SendAsync(UiActions.Resume, "재개");

    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopAsync() =>
        Confirm("지금 회차를 끝냅니다. 하던 건전지까지만 처리하고 멈춥니다.")
            ? SendAsync(UiActions.Stop, "회차 종료")
            : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanSkip))]
    private Task SkipAsync() =>
        Confirm("실패한 건전지를 건너뛰고 다음 칸으로 갑니다. 그 건전지는 결과가 남지 않습니다.")
            ? SendAsync(UiActions.Skip, "건너뛰기")
            : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanReset))]
    private Task ResetAsync() =>
        Confirm("비상정지를 풀고 원점으로 돌아갑니다. 라인 주변에 사람이 없는지 확인했나요?")
            ? SendAsync(UiActions.Reset, "비상정지 해제")
            : Task.CompletedTask;

    private bool CanStart() => CanOperate && Mode == "idle";
    private bool CanPause() => CanOperate && Mode == "running";
    private bool CanResume() => CanOperate && Mode == "paused";
    private bool CanStop() => CanOperate && Mode is "running" or "paused";
    private bool CanSkip() => CanOperate && Mode == "paused";
    private bool CanReset() => CanOperate && Mode == "estop";

    private static bool Confirm(string text) =>
        MessageBox.Show(text, "확인", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;

    private async Task SendAsync(string action, string name)
    {
        try
        {
            await _client.SendCommandAsync(action);
            CommandResult = $"{DateTime.Now:HH:mm:ss}  {name} 명령을 보냈습니다.";
        }
        catch (Exception ex)
        {
            CommandResult = $"{DateTime.Now:HH:mm:ss}  {name} 실패: {ex.Message}";
        }
    }
}
