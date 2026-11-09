using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core;
using DepalAdmin.Core.Alarms;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Models;
using DepalAdmin.Core.Pages;
using DepalAdmin.Core.Settings;

namespace DepalAdmin.Feature.Chat;

/// <summary>
/// 담당: 조시현 — 채팅.
///   - 접속한 사람끼리 메시지 (같은 브로커에 붙은 관리자 프로그램끼리)
///   - 알람·건전지 결과를 골라 붙여 보내기
///   - 다른 탭을 보고 있을 때 온 메시지는 탭 배지로 안 읽은 수 표시
///   - 지난 7일 대화 불러오기
/// ⚠ 누구와 누구의 채팅인지(작업자 메인 프로그램 포함 여부) 팀 확인 전. 지금은 관리자 프로그램끼리.
/// </summary>
public partial class ChatViewModel : ObservableObject, IHasBadge
{
    private const int RecentMax = 5;
    private const int HistoryDays = 7;

    private readonly IChatClient _chat;
    private readonly Session _session;
    private readonly Navigator _navigator;
    private readonly ChatLogFile _log;
    private readonly AppSettings _settings;
    private readonly HashSet<string> _seen = new();   // QoS 1 중복 수신 막기
    private readonly List<BatteryResult> _recentResults = new();

    public ObservableCollection<ChatRow> Messages { get; } = new();
    public ObservableCollection<Attachment> Attachments { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _draft = "";

    [ObservableProperty] private Attachment? _selectedAttachment;
    [ObservableProperty] private int _badgeCount;
    [ObservableProperty] private string _status = "";

    /// <summary>새 메시지가 오면 화면이 맨 아래로 스크롤하도록 알림</summary>
    public event EventHandler? ScrollToEndRequested;

    public ChatViewModel(IChatClient chat, Session session, Navigator navigator, ChatLogFile log,
                         IAlarmService alarms, ILineClient line, AppSettings settings)
    {
        _chat = chat;
        _session = session;
        _navigator = navigator;
        _log = log;
        _settings = settings;

        _chat.MessageReceived += (_, m) => Receive(m, saveToLog: true);

        // 탭을 열면 읽은 것으로
        _navigator.CurrentChanged += (_, title) => { if (title == "채팅") BadgeCount = 0; };

        // 붙여 보낼 후보: 확인 안 된 알람, 최근 건전지 결과
        alarms.Changed += (_, _) => RebuildAttachments(alarms);
        line.ResultReceived += (_, r) =>
        {
            _recentResults.Insert(0, r);
            if (_recentResults.Count > RecentMax) _recentResults.RemoveAt(RecentMax);
            RebuildAttachments(alarms);
        };
        RebuildAttachments(alarms);
    }

    private void Receive(ChatMessage m, bool saveToLog)
    {
        if (!_seen.Add(m.MsgId)) return;
        if (saveToLog) _log.Append(m);

        Messages.Add(new ChatRow(m, isMine: m.FromId == _session.User?.Id && m.Station == _settings.StationName));
        if (_navigator.CurrentTitle != "채팅" && saveToLog) BadgeCount++;
        ScrollToEndRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildAttachments(IAlarmService alarms)
    {
        var keep = SelectedAttachment?.Text;
        Attachments.Clear();
        Attachments.Add(Attachment.None);
        foreach (var a in alarms.Alarms.Where(a => !a.Acked).Take(RecentMax))
            Attachments.Add(new Attachment("alarm", $"[{Labels.Level(a.Level)}] {Labels.Device(a.Device)} {a.Message}"));
        foreach (var r in _recentResults)
            Attachments.Add(new Attachment("battery", $"건전지 #{r.BatteryId} ({Labels.Grade(r.Grade)}) 회차 {r.RunId}"));
        SelectedAttachment = Attachments.FirstOrDefault(x => x.Text == keep) ?? Attachment.None;
    }

    private bool CanSend() => !string.IsNullOrWhiteSpace(Draft);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var user = _session.User;
        if (user == null) return;

        var att = SelectedAttachment ?? Attachment.None;
        var msg = new ChatMessage
        {
            FromId = user.Id,
            FromName = user.Name,
            Station = _settings.StationName,
            Text = Draft.Trim(),
            RefKind = att.Kind,
            RefText = att.Kind == null ? null : att.Text,
        };

        try
        {
            await _chat.SendAsync(msg);
            Draft = "";
            SelectedAttachment = Attachment.None;
            Status = "";
        }
        catch (Exception ex)
        {
            Status = $"보내지 못했습니다: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        Status = "불러오는 중…";
        var old = await _log.ReadAsync(DateTime.Today.AddDays(-(HistoryDays - 1)), DateTime.Today.AddDays(1));
        var merged = old.Concat(Messages.Select(r => r.Raw))
                        .GroupBy(m => m.MsgId).Select(g => g.First())
                        .OrderBy(m => m.Ts, StringComparer.Ordinal)
                        .ToList();
        Messages.Clear();
        _seen.Clear();
        foreach (var m in merged) Receive(m, saveToLog: false);
        Status = $"최근 {HistoryDays}일 대화 {merged.Count}건";
    }
}

public sealed class ChatRow
{
    public ChatRow(ChatMessage raw, bool isMine)
    {
        Raw = raw;
        IsMine = isMine;
    }

    public ChatMessage Raw { get; }
    public bool IsMine { get; }
    public string Who => IsMine ? "나" : $"{Raw.FromName} ({Raw.Station})";
    public string Time => Json.ParseTs(Raw.Ts)?.ToString("MM-dd HH:mm") ?? Raw.Ts;
    public string Text => Raw.Text;
    public string? RefText => Raw.RefText;
    public string RefKind => Raw.RefKind ?? "";
}

public sealed record Attachment(string? Kind, string Text)
{
    public static readonly Attachment None = new(null, "붙이지 않음");
}
