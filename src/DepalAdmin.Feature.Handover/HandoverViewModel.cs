using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core;
using DepalAdmin.Core.Handover;
using DepalAdmin.Core.Pages;

namespace DepalAdmin.Feature.Handover;

/// <summary>
/// 담당: 유동호 — 인수인계 화면 ViewModel (UI 껍데기).
///   - 교대 메모 쓰기 (작성자·시각·라인 상태·회차·미확인 알람 수 자동 첨부)
///   - 기간·상태·검색어 조회 목록
///   - 다음 근무자 '읽음' 확인 및 안 읽은 메모 배지/상단 안내
///   - TODO(유동호): IHandoverStore(파일/MySQL), Session, ILineClient, IAlarmService, IAuditLog, Navigator 실제 기능 연결
/// </summary>
public partial class HandoverViewModel : ObservableObject, IHasBadge
{
    public string[] ReadFilters { get; } = { "전체", "안 읽음", "읽음" };

    public ObservableCollection<NoteRow> Notes { get; } = new();

    [ObservableProperty] private string _selectedReadFilter = "전체";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private DateTime _from = DateTime.Today.AddDays(-6);
    [ObservableProperty] private DateTime _to = DateTime.Today;

    [ObservableProperty] private NoteRow? _selected;
    [ObservableProperty] private string _draft = "";
    [ObservableProperty] private bool _canWrite = true;

    [ObservableProperty] private int _badgeCount = 1;
    [ObservableProperty] private string _notice = "안 읽은 인수인계 메모가 1건 있습니다. 내용을 확인하고 [읽음 확인]을 눌러 주세요.";
    [ObservableProperty] private string _summary = "전체 3건  ·  미확인 1건";
    [ObservableProperty] private string _status = "UI 미리보기 상태입니다. (기능 연결 전)";
    [ObservableProperty] private string _currentSnapshotText = "자동 첨부 미리보기 — 라인 상태: 대기  |  회차: 20261112-093015-a3f9  |  미확인 알람: 0건";

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
                Text = "측면 2번 카메라 조명이 약간 흔들려 간헐적으로 재분류(RESORT) 판정이 나옵니다.\n다음 회차 시작 전에 암막 부스 고정 나사 한 번 확인 부탁드립니다.",
                ReadBy = { "op1" }
            },
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
                Text = "오전 1회차 팔레트 정상 가동 시작했습니다. AGV 배터리 충전 완료 상태입니다.",
                ReadBy = { "admin", "op1", "op2" }
            },
            isUnreadForMe: false));

        Notes.Add(new NoteRow(
            new HandoverNote
            {
                WrittenAt = DateTime.Today.AddDays(-1).AddHours(18).AddMinutes(5),
                AuthorId = "op2",
                AuthorName = "이교대",
                LineMode = "idle",
                RunId = null,
                UnackedAlarms = 0,
                Text = "금일 작업 종료 후 컨베이어 원점 복귀(home) 및 재분류함 비우기 완료했습니다.",
                ReadBy = { "op2", "admin" }
            },
            isUnreadForMe: false));

        Selected = Notes.FirstOrDefault();
    }

    // TODO(유동호): 기간·상태·검색어 조회 기능 연결
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

    // TODO(유동호): 교대 메모 저장 및 작업 기록(IAuditLog) 연결
    [RelayCommand]
    private void Write() { }

    // TODO(유동호): 작성 중인 입력칸 초기화
    [RelayCommand]
    private void ClearDraft() => Draft = "";

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
    public NoteRow(HandoverNote raw, bool isUnreadForMe)
    {
        Raw = raw;
        IsUnreadForMe = isUnreadForMe;
    }

    public HandoverNote Raw { get; }
    public bool IsUnreadForMe { get; }

    public string Time => Raw.WrittenAt.ToString("MM-dd HH:mm");
    public string FullTime => Raw.WrittenAt.ToString("yyyy-MM-dd HH:mm");
    public string Author => Raw.AuthorName;
    public string Text => Raw.Text;
    public string Preview => Raw.Text.Replace("\r\n", " ").Replace('\n', ' ');

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
