using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core;
using DepalAdmin.Core.Alarms;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Handover;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Pages;

namespace DepalAdmin.Feature.Handover;

/// <summary>
/// 담당: 유동호 — 인수인계.
///   - 메모 쓰기: 쓴 사람·시각·그때 라인 상태·회차·미확인 알람 수를 자동으로 붙인다 (작업자 이상)
///   - 목록·검색 (최근 7일 기본)
///   - 다음 근무자 '읽음' 확인 (내가 쓴 메모는 읽음 대상 아님)
///   - 로그인했는데 안 읽은 메모가 있으면 이 탭으로 데려오고 위에 안내를 띄운다
/// </summary>
public partial class HandoverViewModel : ObservableObject, IHasBadge
{
    private const int DefaultDays = 7;

    private readonly IHandoverStore _store;
    private readonly Session _session;
    private readonly ILineClient _line;
    private readonly IAlarmService _alarms;
    private readonly IAuditLog _audit;
    private readonly Navigator _navigator;
    private List<NoteRow> _loaded = new();

    public ObservableCollection<NoteRow> Notes { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(WriteCommand))]
    private string _draft = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MarkReadCommand))]
    private NoteRow? _selected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(WriteCommand))]
    private bool _canWrite;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private DateTime _from = DateTime.Today.AddDays(-(DefaultDays - 1));
    [ObservableProperty] private DateTime _to = DateTime.Today;
    [ObservableProperty] private int _badgeCount;          // 내가 안 읽은 메모 수
    [ObservableProperty] private string _notice = "";       // 로그인 직후 안내
    [ObservableProperty] private string _status = "";

    public HandoverViewModel(IHandoverStore store, Session session, ILineClient line, IAlarmService alarms,
                             IAuditLog audit, Navigator navigator)
    {
        _store = store;
        _session = session;
        _line = line;
        _alarms = alarms;
        _audit = audit;
        _navigator = navigator;

        _store.Changed += async (_, _) => await LoadAsync();
        _session.Changed += async (_, _) => await OnSessionChangedAsync();
    }

    partial void OnSearchTextChanged(string value) => Fill();

    private async Task OnSessionChangedAsync()
    {
        CanWrite = _session.Can(Permission.OperateLine);
        Notice = "";
        if (!_session.IsLoggedIn) return;

        await LoadAsync();
        if (BadgeCount == 0) return;

        Notice = $"안 읽은 인수인계 메모가 {BadgeCount}건 있습니다. 읽고 [읽음]을 눌러 주세요.";
        // 로그인 창이 닫히고 탭이 다시 만들어진 뒤에 이동
        Application.Current?.Dispatcher.InvokeAsync(() => _navigator.Navigate("인수인계"), DispatcherPriority.ApplicationIdle);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var notes = await _store.GetAsync(From.Date, To.Date.AddDays(1));
            var me = _session.User?.Id ?? "";
            _loaded = notes.Select(n => new NoteRow(n, me)).ToList();
            BadgeCount = _loaded.Count(n => n.IsUnreadForMe);
            Fill();
        }
        catch (Exception ex)
        {
            Status = $"불러오지 못했습니다: {ex.Message}";
        }
    }

    private void Fill()
    {
        var keep = Selected?.Raw.Id;
        Notes.Clear();
        foreach (var n in _loaded.Where(n =>
                     string.IsNullOrWhiteSpace(SearchText) ||
                     n.Raw.Text.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                     n.Raw.AuthorName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
        {
            Notes.Add(n);
        }
        Selected = Notes.FirstOrDefault(n => n.Raw.Id == keep);
        Status = $"{Notes.Count}건";
    }

    private bool CanWriteNow() => CanWrite && !string.IsNullOrWhiteSpace(Draft);

    [RelayCommand(CanExecute = nameof(CanWriteNow))]
    private async Task WriteAsync()
    {
        var user = _session.User;
        if (user == null) return;

        var line = _line.LastLine;
        var note = new HandoverNote
        {
            AuthorId = user.Id,
            AuthorName = user.Name,
            Text = Draft.Trim(),
            LineMode = line?.Mode ?? "",
            RunId = line?.RunId,
            UnackedAlarms = _alarms.UnackedCount,
            ReadBy = { user.Id },   // 쓴 사람은 읽은 것으로
        };
        await _store.AddAsync(note);
        _audit.Write("인수인계 작성", note.Text.Length > 40 ? note.Text[..40] + "…" : note.Text);
        Draft = "";
    }

    private bool CanMarkRead() => Selected is { IsUnreadForMe: true };

    [RelayCommand(CanExecute = nameof(CanMarkRead))]
    private async Task MarkReadAsync()
    {
        var user = _session.User;
        if (user == null || Selected == null) return;
        await _store.MarkReadAsync(Selected.Raw.Id, user.Id);
        _audit.Write("인수인계 읽음", $"{Selected.Raw.AuthorName} {Selected.Raw.WrittenAt:MM-dd HH:mm} 메모");
        if (BadgeCount <= 1) Notice = "";
    }
}

public sealed class NoteRow
{
    public NoteRow(HandoverNote raw, string myId)
    {
        Raw = raw;
        IsUnreadForMe = myId != "" && !raw.IsReadBy(myId);
    }

    public HandoverNote Raw { get; }
    public bool IsUnreadForMe { get; }

    public string Time => Raw.WrittenAt.ToString("MM-dd HH:mm");
    public string Author => Raw.AuthorName;
    public string Preview => Raw.Text.Replace('\n', ' ');
    public string LineText => $"{Labels.Mode(Raw.LineMode)}, 미확인 알람 {Raw.UnackedAlarms}건" + (Raw.RunId == null ? "" : $", 회차 {Raw.RunId}");
    public string ReadText => Raw.ReadBy.Count <= 1 ? "아직 아무도 안 읽음" : $"{Raw.ReadBy.Count - 1}명 읽음";
    public string StateKey => IsUnreadForMe ? "warn" : "neutral";
}
