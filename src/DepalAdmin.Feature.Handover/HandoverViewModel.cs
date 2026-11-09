using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core;
using DepalAdmin.Core.Handover;
using DepalAdmin.Core.Pages;

namespace DepalAdmin.Feature.Handover;

/// <summary>
/// 담당: 유동호 — 인수인계 화면 ViewModel (6대 점검 항목 기반 UI 껍데기).
///   1) 검사 회차(Run) 실적 및 진행 현황
///   2) 로봇팔 2대 · 컨베이어 · 비전 · 측정 · AGV 상태
///   3) 건전지 품질 및 재분류(RESORT) 이슈
///   4) 팔레트 및 분류함(B/NG/RESORT) 비우기 현황
///   5) 비상정지(E-Stop) 이력 및 안전/환경 센서(MQ-9, 온습도)
///   6) 판정 기준(rule_version) 및 후번 근무자 상세 전달사항
///   - TODO(유동호): IHandoverStore(파일/MySQL), Session, ILineClient, IAlarmService, IAuditLog, Navigator 실제 기능 연결
/// </summary>
public partial class HandoverViewModel : ObservableObject, IHasBadge
{
    public string[] CategoryFilters { get; } = { "전체", "일반 인계", "설비 주의", "기준·설정 변경", "긴급 확인" };
    public string[] ReadFilters { get; } = { "전체", "안 읽음", "읽음" };
    public string[] WriteCategories { get; } = { "일반 인계", "설비 주의", "기준·설정 변경", "긴급 확인" };
    public string[] AgvStatusOptions { get; } = { "정상 (대기/운행 가능)", "배터리 교체·충전 필요", "배터리 충전 중", "블루투스/Wi-Fi 통신 점검 필요" };

    public ObservableCollection<NoteRow> Notes { get; } = new();

    // ── 상단 조회·검색 필터 ──
    [ObservableProperty] private DateTime _from = DateTime.Today.AddDays(-6);
    [ObservableProperty] private DateTime _to = DateTime.Today;
    [ObservableProperty] private string _selectedCategoryFilter = "전체";
    [ObservableProperty] private string _selectedReadFilter = "전체";
    [ObservableProperty] private string _searchText = "";

    // ── 목록 선택 및 배지/알림 상태 ──
    [ObservableProperty] private NoteRow? _selected;
    [ObservableProperty] private bool _canWrite = true;
    [ObservableProperty] private int _badgeCount = 1;
    [ObservableProperty] private string _notice = "안 읽은 인수인계 메모가 1건 있습니다. 내용을 확인하고 [선택 메모 읽음 확인]을 눌러 주세요.";
    [ObservableProperty] private string _summary = "전체 3건  ·  미확인 1건";
    [ObservableProperty] private string _status = "UI 껍데기(미리보기) 상태입니다. (기능 연결 전)";

    // ── 1. 검사 회차 실적 및 진행 현황 ──
    [ObservableProperty] private string _currentLineSummaryText = "현재 라인: 일시정지  |  미확인 알람: 1건";
    [ObservableProperty] private string _currentRunIdText = "회차: 20261112-130500-b2c1";
    [ObservableProperty] private string _currentCountsText = "누적 분류 — A: 32개   B: 11개   불량(NG): 4개   재분류(RESORT): 3개   배출실패: 0개";
    [ObservableProperty] private bool _isRunFinishedIdle = true;
    [ObservableProperty] private bool _isRunPausedMidway;
    [ObservableProperty] private bool _isRunAborted;
    [ObservableProperty] private string _remainingPalletsText = "2";

    // ── 2. 설비 · 비전 · 측정 · AGV 상태 ──
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
    [ObservableProperty] private bool _isResortCleared = true;
    [ObservableProperty] private bool _isResortPending;
    [ObservableProperty] private string _resortRemainingCountText = "0";

    // ── 4. 팔레트 및 분류함 비우기 현황 ──
    [ObservableProperty] private string _currentAgvLoadText = "AGV 적재: 3 / 8개 (분류 위치)";
    [ObservableProperty] private bool _boxBCleared = true;
    [ObservableProperty] private bool _boxNgCleared = true;
    [ObservableProperty] private bool _boxResortChecked = true;
    [ObservableProperty] private bool _puckHomeChecked = true;

    // ── 5. 비상정지 이력 및 현장 안전/환경 센서 ──
    [ObservableProperty] private bool _estopNone = true;
    [ObservableProperty] private bool _estopRecovered;
    [ObservableProperty] private bool _estopActive;
    [ObservableProperty] private bool _safeAgvPath = true;
    [ObservableProperty] private bool _safeArmArea = true;
    [ObservableProperty] private bool _safeEnvSensor = true;

    // ── 6. 판정 기준 및 상세 전달사항 ──
    [ObservableProperty] private string _currentRuleVersionText = "기준 버전: temp-2026-11-06";
    [ObservableProperty] private string _selectedWriteCategory = "일반 인계";
    [ObservableProperty] private string _draft = "";

    public HandoverViewModel()
    {
        LoadSampleShellData();
    }

    private void LoadSampleShellData()
    {
        Notes.Add(new NoteRow(
            new HandoverNote
            {
                WrittenAt = DateTime.Today.AddHours(14).AddMinutes(20),
                AuthorId = "op1",
                AuthorName = "김작업",
                LineMode = "paused",
                RunId = "20261112-130500-b2c1",
                UnackedAlarms = 1,
                Text = "[1. 회차/실적] 회차 진행 중 일시정지 | 이월 미검사 팔레트 2판 (A 32 / B 11 / NG 4 / 재분류 3)\n" +
                       "[2. 설비/AGV] 카메라 3대·암막 조명 점검 체크 | AGV 배터리 교체·충전 필요\n" +
                       "[3. 품질/재분류] 상단 +극 위 투입 다발, 측정 접촉 불량(abnormal) 1건 | 재분류함 3개 보관 중(재투입 요망)\n" +
                       "[4. 분류함/퍽] B 분류함·불량함 비움 완료 | AGV 적재 3/8개\n" +
                       "[5. 안전/환경] 비상정지 없음 | 주행로·로봇팔 반경·가스(MQ-9)·온습도 정상\n" +
                       "[6. 전달사항] 측면 2번 카메라 조명이 약간 흔들려 간헐적 재분류가 발생합니다. 다음 회차 시작 전 암막 부스 고정 나사 확인 부탁드립니다.",
                ReadBy = { "op1" }
            },
            category: "설비 주의",
            boxSummary: "재분류 3개 / AGV 3/8",
            isUnreadForMe: true));

        Notes.Add(new NoteRow(
            new HandoverNote
            {
                WrittenAt = DateTime.Today.AddHours(9).AddMinutes(10),
                AuthorId = "admin",
                AuthorName = "관리자",
                LineMode = "running",
                RunId = "20261112-090012-a1f4",
                UnackedAlarms = 0,
                Text = "[1. 회차/실적] 회차 완료 후 대기 | 이월 팔레트 0판\n" +
                       "[2. 설비/AGV] 전 설비 정상 | AGV 배터리 충전 완료\n" +
                       "[3. 품질/재분류] 특이 불량 없음 | 재분류함 전량 재투입 완료\n" +
                       "[4. 분류함/퍽] B·NG·재분류함 모두 비움, 퍽 원점(home) 확인\n" +
                       "[5. 안전/환경] 비상정지 없음 | 환경 센서 정상\n" +
                       "[6. 전달사항] 금일 오전 1회차 정상 가동 시작했습니다. 판정 기준 버전(temp-2026-11-06) 동일합니다.",
                ReadBy = { "admin", "op1", "op2" }
            },
            category: "일반 인계",
            boxSummary: "전량 비움 / AGV 0/8",
            isUnreadForMe: false));

        Notes.Add(new NoteRow(
            new HandoverNote
            {
                WrittenAt = DateTime.Today.AddDays(-1).AddHours(18).AddMinutes(5),
                AuthorId = "op2",
                AuthorName = "이교대",
                LineMode = "idle",
                RunId = "20261111-171022-c8d0",
                UnackedAlarms = 0,
                Text = "[1. 회차/실적] 회차 완료 후 대기 | 잔여 팔레트 1판\n" +
                       "[2. 설비/AGV] 2번 분류팔(전자석) 오프셋 미세 조정 완료 | AGV 정상\n" +
                       "[3. 품질/재분류] 외관 스크래치 불량 2건 격리 완료 | 재분류함 전량 재투입 완료\n" +
                       "[4. 분류함/퍽] 전 분류함 비우기 및 컨베이어 원점 복귀 완료\n" +
                       "[5. 안전/환경] 15:20 물리 비상정지 1회 테스트 후 원점 복귀(reset) 정상 확인\n" +
                       "[6. 전달사항] 금일 작업 종료 후 주변 정리 완료했습니다.",
                ReadBy = { "op2", "admin" }
            },
            category: "일반 인계",
            boxSummary: "전량 비움 / AGV 0/8",
            isUnreadForMe: false));

        Selected = Notes.FirstOrDefault();
    }

    // TODO(유동호): 기간·구분·확인상태·검색어 조회 기능 연결
    [RelayCommand]
    private void Search() { }

    // TODO(유동호): 오늘 날짜로 기간 설정 후 조회 연결
    [RelayCommand]
    private void Today()
    {
        From = DateTime.Today;
        To = DateTime.Today;
    }

    // TODO(유동호): 최근 7일로 기간 설정 후 조회 연결
    [RelayCommand]
    private void LastWeek()
    {
        From = DateTime.Today.AddDays(-6);
        To = DateTime.Today;
    }

    // TODO(유동호): 6대 점검 항목 + 메모를 HandoverNote로 묶어 저장 및 작업 기록(IAuditLog) 연결
    [RelayCommand]
    private void Write() { }

    // TODO(유동호): 작성 폼 입력값 기본 상태로 초기화
    [RelayCommand]
    private void ClearDraft()
    {
        IsRunFinishedIdle = true;
        IsRunPausedMidway = false;
        IsRunAborted = false;
        RemainingPalletsText = "0";

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
        IsResortCleared = true;
        IsResortPending = false;
        ResortRemainingCountText = "0";

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

    // TODO(유동호): 선택한 메모 읽음 처리 저장 연결
    [RelayCommand]
    private void MarkRead() { }

    // TODO(유동호): 미확인 메모 일괄 읽음 처리 연결
    [RelayCommand]
    private void MarkAllRead() { }
}

/// <summary>화면 표·상세 표시용 행 래퍼</summary>
public sealed class NoteRow
{
    public NoteRow(HandoverNote raw, string category, string boxSummary, bool isUnreadForMe)
    {
        Raw = raw;
        Category = category;
        BoxSummaryText = boxSummary;
        IsUnreadForMe = isUnreadForMe;
    }

    public HandoverNote Raw { get; }
    public string Category { get; }
    public string BoxSummaryText { get; }
    public bool IsUnreadForMe { get; }

    public string Time => Raw.WrittenAt.ToString("MM-dd HH:mm");
    public string FullTime => Raw.WrittenAt.ToString("yyyy-MM-dd HH:mm");
    public string Author => Raw.AuthorName;
    public string Text => Raw.Text;

    public string Preview
    {
        get
        {
            var lines = Raw.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var last = lines.LastOrDefault() ?? Raw.Text;
            return last.Replace("[6. 전달사항] ", "").Trim();
        }
    }

    public string LineModeText => Labels.Mode(Raw.LineMode);
    public string RunIdText => string.IsNullOrWhiteSpace(Raw.RunId) ? "-" : Raw.RunId;
    public string AlarmCountText => $"{Raw.UnackedAlarms}건";

    public string ReadText => IsUnreadForMe
        ? "미확인"
        : (Raw.ReadBy.Count <= 1 ? "아직 안 읽음" : $"{Raw.ReadBy.Count - 1}명 읽음");

    public string ReadDetailText => Raw.ReadBy.Count <= 1
        ? "확인 기록: 아직 다른 근무자가 읽지 않았습니다."
        : $"확인 기록: {Raw.ReadBy.Count - 1}명 확인함 ({string.Join(", ", Raw.ReadBy.Skip(1))})";
}
