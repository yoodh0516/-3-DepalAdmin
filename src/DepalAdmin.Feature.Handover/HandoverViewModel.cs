using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core;
using DepalAdmin.Core.Alarms;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Handover;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Models;
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

    private FormSnapshot _draftBackup = new();
    private bool _isRestoringForm;

    public string[] AgvStatusOptions { get; } =
    {
        "정상 (대기/운행 가능)",
        "배터리 교체·충전 필요",
        "배터리 충전 중",
        "통신 점검 필요"
    };

    public string[] WriteCategories { get; } =
    {
        "일반 인계",
        "설비 주의",
        "기준·설정 변경",
        "긴급 확인"
    };

    public ObservableCollection<NoteRow> Notes { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(WriteCommand))]
    private string _draft = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MarkReadCommand))]
    [NotifyCanExecuteChangedFor(nameof(WriteCommand))]
    [NotifyPropertyChangedFor(nameof(IsViewingSelected))]
    [NotifyPropertyChangedFor(nameof(IsWritingMode))]
    [NotifyPropertyChangedFor(nameof(CanEditForm))]
    [NotifyPropertyChangedFor(nameof(RightPanelHeader))]
    private NoteRow? _selected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(WriteCommand))]
    [NotifyPropertyChangedFor(nameof(CanEditForm))]
    private bool _canWrite;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private DateTime _from = DateTime.Today.AddDays(-(DefaultDays - 1));
    [ObservableProperty] private DateTime _to = DateTime.Today;
    [ObservableProperty] private int _badgeCount;          // 내가 안 읽은 메모 수
    [ObservableProperty] private string _notice = "";       // 로그인 직후 안내
    [ObservableProperty] private string _status = "";

    // ── 실시간 라인 수치 자동 표시 속성 ──
    [ObservableProperty] private string _liveLineStatusText = "라인 상태: 대기  |  미확인 알람: 0건";
    [ObservableProperty] private string _liveRunIdText = "현재 회차: -";
    [ObservableProperty] private string _liveCountsText = "실시간 분류 — A: 0개   B: 0개   불량(NG): 0개   재분류(RESORT): 0개";
    [ObservableProperty] private string _liveAgvText = "AGV 적재: 0 / 8개 (분류 위치)";

    // ── 1. 검사 회차(Run) 실적 및 진행 현황 ──
    [ObservableProperty] private bool _runStateIdle = true;
    [ObservableProperty] private bool _runStatePaused;
    [ObservableProperty] private bool _runStateAborted;
    [ObservableProperty] private string _remainingPallets = "0";

    // ── 2. 로봇팔 · 컨베이어 · 비전 · 측정 · AGV 상태 ──
    [ObservableProperty] private bool _checkLoadArm;
    [ObservableProperty] private bool _checkSortArm;
    [ObservableProperty] private bool _checkConveyor;
    [ObservableProperty] private bool _checkMeasureHead;
    [ObservableProperty] private bool _checkVisionBooth;
    [ObservableProperty] private bool _checkAgvLift;
    [ObservableProperty] private string _selectedAgvStatus = "정상 (대기/운행 가능)";

    // ── 3. 건전지 품질 및 재분류(RESORT) 이슈 ──
    [ObservableProperty] private bool _issueVisualDefect;
    [ObservableProperty] private bool _issuePlusUp;
    [ObservableProperty] private bool _issueContactAbnormal;
    [ObservableProperty] private bool _issueVisionOrSortFail;
    [ObservableProperty] private bool _resortCleared = true;
    [ObservableProperty] private bool _resortPending;
    [ObservableProperty] private string _resortRemainingCount = "0";

    // ── 4. 팔레트 및 분류함 비우기 확인 ──
    [ObservableProperty] private bool _boxBCleared = true;
    [ObservableProperty] private bool _boxNgCleared = true;
    [ObservableProperty] private bool _boxResortChecked = true;
    [ObservableProperty] private bool _puckHomeChecked = true;

    // ── 5. 비상정지 이력 및 안전/환경 센서 ──
    [ObservableProperty] private bool _estopNone = true;
    [ObservableProperty] private bool _estopRecovered;
    [ObservableProperty] private bool _estopActive;
    [ObservableProperty] private bool _safeAgvPath = true;
    [ObservableProperty] private bool _safeArmArea = true;
    [ObservableProperty] private bool _safeEnvSensor = true;

    // ── 6. 판정 기준 변경 및 상세 전달사항 ──
    [ObservableProperty] private string _selectedWriteCategory = "일반 인계";

    public bool IsViewingSelected => Selected != null;
    public bool IsWritingMode => Selected == null;
    public bool CanEditForm => Selected == null && CanWrite;
    public string RightPanelHeader => Selected != null
        ? "선택한 메모 상세 (다른 곳을 클릭하면 새 메모 작성으로 돌아갑니다)"
        : "메모 남기기 (교대 인수인계 작성)";

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
        _line.LineStatusReceived += (_, _) => UpdateLiveLineSnapshot();
        _alarms.Changed += (_, _) => UpdateLiveLineSnapshot();

        UpdateLiveLineSnapshot();
    }

    partial void OnSearchTextChanged(string value) => Fill();

    partial void OnSelectedChanged(NoteRow? oldValue, NoteRow? newValue)
    {
        if (oldValue == null && newValue != null)
        {
            // 작성 모드 → 선택 보기 모드로 전환될 때 작성 중이던 내용 백업
            _draftBackup = CaptureCurrentForm();
        }

        if (newValue != null)
        {
            ApplyNoteToForm(newValue.Raw);
        }
        else if (oldValue != null)
        {
            // 선택 해제 시 백업해 둔 작성 중이던 폼 복원
            RestoreForm(_draftBackup);
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        if (Selected != null)
            Selected = null;
    }

    private void UpdateLiveLineSnapshot()
    {
        var line = _line.LastLine;
        var modeText = Labels.Mode(line?.Mode ?? "idle");
        var unacked = _alarms.UnackedCount;
        LiveLineStatusText = $"라인 상태: {modeText}  |  미확인 알람: {unacked}건";
        LiveRunIdText = string.IsNullOrWhiteSpace(line?.RunId) ? "현재 회차: -" : $"현재 회차: {line.RunId}";

        var a = line?.Count("A") ?? 0;
        var b = line?.Count("B") ?? 0;
        var ng = line?.Count("NG") ?? 0;
        var resort = line?.Count("RESORT") ?? 0;
        LiveCountsText = $"실시간 분류 — A: {a}개   B: {b}개   불량(NG): {ng}개   재분류(RESORT): {resort}개";

        var agvLoaded = line?.Agv?.Loaded ?? 0;
        var agvBatch = line?.Agv?.Batch is > 0 ? line.Agv.Batch : 8;
        var agvState = Labels.AgvState(line?.Agv?.State ?? "home");
        LiveAgvText = $"AGV 적재: {agvLoaded} / {agvBatch}개 ({agvState})";
    }

    private async Task OnSessionChangedAsync()
    {
        CanWrite = _session.Can(Permission.OperateLine);
        Notice = "";
        if (!_session.IsLoggedIn) return;

        UpdateLiveLineSnapshot();
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
        Selected = keep == null ? null : Notes.FirstOrDefault(n => n.Raw.Id == keep);
        Status = $"{Notes.Count}건";
    }

    private bool CanWriteNow() => Selected == null && CanWrite && !string.IsNullOrWhiteSpace(Draft);

    [RelayCommand(CanExecute = nameof(CanWriteNow))]
    private async Task WriteAsync()
    {
        var user = _session.User;
        if (user == null) return;

        var line = _line.LastLine;
        var fullText = BuildFormattedNoteText(line);
        var note = new HandoverNote
        {
            AuthorId = user.Id,
            AuthorName = user.Name,
            Text = fullText,
            LineMode = line?.Mode ?? "",
            RunId = line?.RunId,
            UnackedAlarms = _alarms.UnackedCount,
            ReadBy = { user.Id },   // 쓴 사람은 읽은 것으로
        };
        await _store.AddAsync(note);
        _audit.Write("인수인계 작성", note.Text.Length > 40 ? note.Text[..40] + "…" : note.Text);

        ResetFormToDefaults();
        _draftBackup = CaptureCurrentForm();
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

    private string BuildFormattedNoteText(LineStatus? line)
    {
        var sb = new StringBuilder();

        // 1. 검사 회차 실적 및 진행 현황
        var runState = RunStatePaused ? "회차 진행 중 일시정지"
                     : RunStateAborted ? "비정상 중단"
                     : "회차 완료 후 대기";
        var pallets = string.IsNullOrWhiteSpace(RemainingPallets) ? "0" : RemainingPallets.Trim();
        var a = line?.Count("A") ?? 0;
        var b = line?.Count("B") ?? 0;
        var ng = line?.Count("NG") ?? 0;
        var resort = line?.Count("RESORT") ?? 0;
        sb.AppendLine($"[1. 회차/실적] 상태: {runState} | 이월 팔레트: {pallets}판 | 실적(A:{a}, B:{b}, NG:{ng}, 재분류:{resort})");

        // 2. 설비 · 비전 · 측정 · AGV 상태
        var equipChecks = new List<string>();
        if (CheckLoadArm) equipChecks.Add("1번 적재팔(집게)");
        if (CheckSortArm) equipChecks.Add("2번 분류팔(전자석)");
        if (CheckConveyor) equipChecks.Add("컨베이어·원점스위치");
        if (CheckMeasureHead) equipChecks.Add("전압 측정 헤드");
        if (CheckVisionBooth) equipChecks.Add("카메라 3대·암막조명");
        if (CheckAgvLift) equipChecks.Add("AGV 지게차·리프트");
        var equipText = equipChecks.Count == 0 ? "특이점검 없음" : string.Join(", ", equipChecks);
        sb.AppendLine($"[2. 설비/AGV] 점검체크: {equipText} | AGV 상태: {SelectedAgvStatus}");

        // 3. 건전지 품질 및 재분류(RESORT) 이슈
        var issues = new List<string>();
        if (IssueVisualDefect) issues.Add("외관 불량 다발");
        if (IssuePlusUp) issues.Add("상단 +극 위(역방향) 다발");
        if (IssueContactAbnormal) issues.Add("측정 접촉 불량(abnormal)");
        if (IssueVisionOrSortFail) issues.Add("비전 실패/배출 실패");
        var issueText = issues.Count == 0 ? "특이 이슈 없음" : string.Join(", ", issues);
        var resortCount = string.IsNullOrWhiteSpace(ResortRemainingCount) ? "0" : ResortRemainingCount.Trim();
        var resortState = ResortPending ? $"보관 중({resortCount}개)" : "전량 재투입 완료";
        sb.AppendLine($"[3. 품질/재분류] 이슈: {issueText} | 재분류함: {resortState}");

        // 4. 팔레트 및 분류함 비우기 확인
        var boxChecks = new List<string>();
        if (BoxBCleared) boxChecks.Add("B 분류함 비움");
        if (BoxNgCleared) boxChecks.Add("불량함 비움");
        if (BoxResortChecked) boxChecks.Add("재분류함 확인");
        if (PuckHomeChecked) boxChecks.Add("컨베이어 퍽 원점 확인");
        var boxText = boxChecks.Count == 0 ? "미체크" : string.Join(", ", boxChecks);
        sb.AppendLine($"[4. 분류함/퍽] 확인: {boxText}");

        // 5. 비상정지 이력 및 안전/환경 센서
        var estopText = EstopRecovered ? "발생 후 원점 복귀 완료"
                      : EstopActive ? "비상정지 유지 중"
                      : "없음";
        var safeChecks = new List<string>();
        if (SafeAgvPath) safeChecks.Add("AGV 주행로 장애물 없음");
        if (SafeArmArea) safeChecks.Add("로봇팔 반경 간섭 없음");
        if (SafeEnvSensor) safeChecks.Add("가스(MQ-9)·온습도 정상");
        var safeText = safeChecks.Count == 0 ? "미체크" : string.Join(", ", safeChecks);
        sb.AppendLine($"[5. 안전/환경] 비상정지: {estopText} | 안전체크: {safeText}");

        // 6. 판정 기준 변경 및 상세 전달사항
        sb.Append($"[6. 전달사항] 구분: {SelectedWriteCategory} | 내용: {Draft.Trim()}");

        return sb.ToString();
    }

    private void ApplyNoteToForm(HandoverNote note)
    {
        _isRestoringForm = true;
        try
        {
            var text = note.Text ?? "";
            if (!text.Contains("[1. 회차/실적]"))
            {
                // 예전 형식(단순 텍스트) 메모인 경우 기본값 + 본문만 표시
                ResetFormToDefaults();
                Draft = text;
                return;
            }

            ResetFormToDefaults();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var memoLines = new List<string>();
            var inMemo = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');
                if (inMemo)
                {
                    memoLines.Add(line);
                    continue;
                }

                if (line.StartsWith("[1. 회차/실적]"))
                {
                    RunStatePaused = line.Contains("회차 진행 중 일시정지");
                    RunStateAborted = line.Contains("비정상 중단");
                    RunStateIdle = !RunStatePaused && !RunStateAborted;
                    RemainingPallets = ExtractBetween(line, "이월 팔레트:", "판") ?? "0";
                }
                else if (line.StartsWith("[2. 설비/AGV]"))
                {
                    CheckLoadArm = line.Contains("1번 적재팔(집게)");
                    CheckSortArm = line.Contains("2번 분류팔(전자석)");
                    CheckConveyor = line.Contains("컨베이어·원점스위치");
                    CheckMeasureHead = line.Contains("전압 측정 헤드");
                    CheckVisionBooth = line.Contains("카메라 3대·암막조명");
                    CheckAgvLift = line.Contains("AGV 지게차·리프트");
                    var agv = ExtractAfter(line, "AGV 상태:");
                    if (!string.IsNullOrWhiteSpace(agv) && AgvStatusOptions.Contains(agv))
                        SelectedAgvStatus = agv;
                }
                else if (line.StartsWith("[3. 품질/재분류]"))
                {
                    IssueVisualDefect = line.Contains("외관 불량 다발");
                    IssuePlusUp = line.Contains("상단 +극 위(역방향) 다발");
                    IssueContactAbnormal = line.Contains("측정 접촉 불량(abnormal)");
                    IssueVisionOrSortFail = line.Contains("비전 실패/배출 실패");
                    ResortPending = line.Contains("보관 중(");
                    ResortCleared = !ResortPending;
                    ResortRemainingCount = ExtractBetween(line, "보관 중(", "개)") ?? "0";
                }
                else if (line.StartsWith("[4. 분류함/퍽]"))
                {
                    BoxBCleared = line.Contains("B 분류함 비움");
                    BoxNgCleared = line.Contains("불량함 비움");
                    BoxResortChecked = line.Contains("재분류함 확인");
                    PuckHomeChecked = line.Contains("컨베이어 퍽 원점 확인");
                }
                else if (line.StartsWith("[5. 안전/환경]"))
                {
                    EstopRecovered = line.Contains("발생 후 원점 복귀 완료");
                    EstopActive = line.Contains("비상정지 유지 중");
                    EstopNone = !EstopRecovered && !EstopActive;
                    SafeAgvPath = line.Contains("AGV 주행로 장애물 없음");
                    SafeArmArea = line.Contains("로봇팔 반경 간섭 없음");
                    SafeEnvSensor = line.Contains("가스(MQ-9)·온습도 정상");
                }
                else if (line.StartsWith("[6. 전달사항]"))
                {
                    var cat = ExtractBetween(line, "구분:", "| 내용:");
                    if (!string.IsNullOrWhiteSpace(cat) && WriteCategories.Contains(cat))
                        SelectedWriteCategory = cat;
                    var firstLineMemo = ExtractAfter(line, "| 내용:");
                    if (firstLineMemo != null)
                        memoLines.Add(firstLineMemo);
                    inMemo = true;
                }
            }

            Draft = string.Join(Environment.NewLine, memoLines);
        }
        finally
        {
            _isRestoringForm = false;
        }
    }

    private static string? ExtractBetween(string text, string startMarker, string endMarker)
    {
        var s = text.IndexOf(startMarker, StringComparison.Ordinal);
        if (s < 0) return null;
        s += startMarker.Length;
        var e = text.IndexOf(endMarker, s, StringComparison.Ordinal);
        if (e < 0) return null;
        return text[s..e].Trim();
    }

    private static string? ExtractAfter(string text, string marker)
    {
        var s = text.IndexOf(marker, StringComparison.Ordinal);
        if (s < 0) return null;
        return text[(s + marker.Length)..].Trim();
    }

    private void ResetFormToDefaults()
    {
        RunStateIdle = true;
        RunStatePaused = false;
        RunStateAborted = false;
        RemainingPallets = "0";

        CheckLoadArm = false;
        CheckSortArm = false;
        CheckConveyor = false;
        CheckMeasureHead = false;
        CheckVisionBooth = false;
        CheckAgvLift = false;
        SelectedAgvStatus = AgvStatusOptions[0];

        IssueVisualDefect = false;
        IssuePlusUp = false;
        IssueContactAbnormal = false;
        IssueVisionOrSortFail = false;
        ResortCleared = true;
        ResortPending = false;
        ResortRemainingCount = "0";

        BoxBCleared = true;
        BoxNgCleared = true;
        BoxResortChecked = true;
        PuckHomeChecked = true;

        EstopNone = true;
        EstopRecovered = false;
        EstopActive = false;
        SafeAgvPath = true;
        SafeArmArea = true;
        SafeEnvSensor = true;

        SelectedWriteCategory = WriteCategories[0];
        Draft = "";
    }

    private FormSnapshot CaptureCurrentForm() => new()
    {
        RunStateIdle = RunStateIdle,
        RunStatePaused = RunStatePaused,
        RunStateAborted = RunStateAborted,
        RemainingPallets = RemainingPallets,
        CheckLoadArm = CheckLoadArm,
        CheckSortArm = CheckSortArm,
        CheckConveyor = CheckConveyor,
        CheckMeasureHead = CheckMeasureHead,
        CheckVisionBooth = CheckVisionBooth,
        CheckAgvLift = CheckAgvLift,
        SelectedAgvStatus = SelectedAgvStatus,
        IssueVisualDefect = IssueVisualDefect,
        IssuePlusUp = IssuePlusUp,
        IssueContactAbnormal = IssueContactAbnormal,
        IssueVisionOrSortFail = IssueVisionOrSortFail,
        ResortCleared = ResortCleared,
        ResortPending = ResortPending,
        ResortRemainingCount = ResortRemainingCount,
        BoxBCleared = BoxBCleared,
        BoxNgCleared = BoxNgCleared,
        BoxResortChecked = BoxResortChecked,
        PuckHomeChecked = PuckHomeChecked,
        EstopNone = EstopNone,
        EstopRecovered = EstopRecovered,
        EstopActive = EstopActive,
        SafeAgvPath = SafeAgvPath,
        SafeArmArea = SafeArmArea,
        SafeEnvSensor = SafeEnvSensor,
        SelectedWriteCategory = SelectedWriteCategory,
        Draft = Draft,
    };

    private void RestoreForm(FormSnapshot s)
    {
        if (_isRestoringForm) return;
        _isRestoringForm = true;
        try
        {
            RunStateIdle = s.RunStateIdle;
            RunStatePaused = s.RunStatePaused;
            RunStateAborted = s.RunStateAborted;
            RemainingPallets = s.RemainingPallets;
            CheckLoadArm = s.CheckLoadArm;
            CheckSortArm = s.CheckSortArm;
            CheckConveyor = s.CheckConveyor;
            CheckMeasureHead = s.CheckMeasureHead;
            CheckVisionBooth = s.CheckVisionBooth;
            CheckAgvLift = s.CheckAgvLift;
            SelectedAgvStatus = s.SelectedAgvStatus;
            IssueVisualDefect = s.IssueVisualDefect;
            IssuePlusUp = s.IssuePlusUp;
            IssueContactAbnormal = s.IssueContactAbnormal;
            IssueVisionOrSortFail = s.IssueVisionOrSortFail;
            ResortCleared = s.ResortCleared;
            ResortPending = s.ResortPending;
            ResortRemainingCount = s.ResortRemainingCount;
            BoxBCleared = s.BoxBCleared;
            BoxNgCleared = s.BoxNgCleared;
            BoxResortChecked = s.BoxResortChecked;
            PuckHomeChecked = s.PuckHomeChecked;
            EstopNone = s.EstopNone;
            EstopRecovered = s.EstopRecovered;
            EstopActive = s.EstopActive;
            SafeAgvPath = s.SafeAgvPath;
            SafeArmArea = s.SafeArmArea;
            SafeEnvSensor = s.SafeEnvSensor;
            SelectedWriteCategory = s.SelectedWriteCategory;
            Draft = s.Draft;
        }
        finally
        {
            _isRestoringForm = false;
        }
    }

    private sealed class FormSnapshot
    {
        public bool RunStateIdle { get; init; } = true;
        public bool RunStatePaused { get; init; }
        public bool RunStateAborted { get; init; }
        public string RemainingPallets { get; init; } = "0";
        public bool CheckLoadArm { get; init; }
        public bool CheckSortArm { get; init; }
        public bool CheckConveyor { get; init; }
        public bool CheckMeasureHead { get; init; }
        public bool CheckVisionBooth { get; init; }
        public bool CheckAgvLift { get; init; }
        public string SelectedAgvStatus { get; init; } = "정상 (대기/운행 가능)";
        public bool IssueVisualDefect { get; init; }
        public bool IssuePlusUp { get; init; }
        public bool IssueContactAbnormal { get; init; }
        public bool IssueVisionOrSortFail { get; init; }
        public bool ResortCleared { get; init; } = true;
        public bool ResortPending { get; init; }
        public string ResortRemainingCount { get; init; } = "0";
        public bool BoxBCleared { get; init; } = true;
        public bool BoxNgCleared { get; init; } = true;
        public bool BoxResortChecked { get; init; } = true;
        public bool PuckHomeChecked { get; init; } = true;
        public bool EstopNone { get; init; } = true;
        public bool EstopRecovered { get; init; }
        public bool EstopActive { get; init; }
        public bool SafeAgvPath { get; init; } = true;
        public bool SafeArmArea { get; init; } = true;
        public bool SafeEnvSensor { get; init; } = true;
        public string SelectedWriteCategory { get; init; } = "일반 인계";
        public string Draft { get; init; } = "";
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
    public string Preview
    {
        get
        {
            var idx = Raw.Text.IndexOf("| 내용:", StringComparison.Ordinal);
            if (idx >= 0)
                return Raw.Text[(idx + "| 내용:".Length)..].Replace('\r', ' ').Replace('\n', ' ').Trim();
            return Raw.Text.Replace('\r', ' ').Replace('\n', ' ');
        }
    }
    public string LineText => $"{Labels.Mode(Raw.LineMode)}, 미확인 알람 {Raw.UnackedAlarms}건" + (Raw.RunId == null ? "" : $", 회차 {Raw.RunId}");
    public string ReadText => Raw.ReadBy.Count <= 1 ? "아직 아무도 안 읽음" : $"{Raw.ReadBy.Count - 1}명 읽음";
    public string StateKey => IsUnreadForMe ? "warn" : "neutral";
}
