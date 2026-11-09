using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Data;
using DepalAdmin.Core.Export;

namespace DepalAdmin.Feature.History.Records;

/// <summary>
/// 지난 결과 조회. 두 가지로 본다:
///   건전지별 — 한 줄에 건전지 하나
///   회차별   — 한 줄에 회차(팔레트 한 판) 하나, 등급별 개수와 걸린 시간
/// </summary>
public partial class RecordsViewModel : ObservableObject
{
    private const string All = "전체";
    private readonly IHistoryRepository _repo;
    private readonly Session _session;

    public ObservableCollection<HistoryRow> Rows { get; } = new();
    public ObservableCollection<RunSummary> Runs { get; } = new();
    public string[] Grades { get; } = { All, "A", "B", "NG", "RESORT" };

    [ObservableProperty] private DateTime _from = DateTime.Today;
    [ObservableProperty] private DateTime _to = DateTime.Today;
    [ObservableProperty] private string _grade = All;
    [ObservableProperty] private string _runFilter = "";
    [ObservableProperty] private string _status = "날짜를 고르고 [조회]를 누르세요.";
    [ObservableProperty] private int _tabIndex;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private bool _canExport;

    private List<HistoryRow> _loaded = new();

    public RecordsViewModel(IHistoryRepository repo, Session session)
    {
        _repo = repo;
        _session = session;
        _session.Changed += (_, _) => CanExport = _session.Can(Permission.ExportData);
        CanExport = _session.Can(Permission.ExportData);
    }

    partial void OnRunFilterChanged(string value) => Fill();

    [RelayCommand]
    private void Today() => (From, To) = (DateTime.Today, DateTime.Today);

    [RelayCommand]
    private void LastWeek() => (From, To) = (DateTime.Today.AddDays(-6), DateTime.Today);

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (To < From)
        {
            Status = "끝 날짜가 시작 날짜보다 빠릅니다.";
            return;
        }

        Status = "조회 중…";
        try
        {
            _loaded = (await _repo.GetResultsAsync(From.Date, To.Date.AddDays(1), Grade == All ? null : Grade)).ToList();
            Fill();
        }
        catch (Exception ex)
        {
            Status = $"조회하지 못했습니다: {ex.Message}  (settings.json의 MySQL 접속 정보를 확인하세요)";
        }
    }

    private void Fill()
    {
        var rows = string.IsNullOrWhiteSpace(RunFilter)
            ? _loaded
            : _loaded.Where(r => r.RunId.Contains(RunFilter.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        Rows.Clear();
        foreach (var r in rows) Rows.Add(r);

        Runs.Clear();
        foreach (var run in rows.GroupBy(r => r.RunId)
                                .Select(g => new RunSummary(g.Key, g.ToList()))
                                .OrderByDescending(s => s.Start))
        {
            Runs.Add(run);
        }

        Status = $"건전지 {Rows.Count}개, 회차 {Runs.Count}개";
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Export()
    {
        string? path;
        if (TabIndex == 0)
        {
            path = CsvExport.Save($"이력_{From:yyyyMMdd}-{To:yyyyMMdd}.csv", Rows,
                ("끝난 시각", r => r.FinishedAt),
                ("회차", r => r.RunId),
                ("번호", r => r.BatteryId),
                ("칸", r => r.PalletSlot),
                ("등급", r => r.GradeText),
                ("무부하 전압(V)", r => r.VOpen),
                ("내부저항(Ω)", r => r.RInt),
                ("배출 위치", r => r.DestText),
                ("사유", r => r.Reason));
        }
        else
        {
            path = CsvExport.Save($"회차_{From:yyyyMMdd}-{To:yyyyMMdd}.csv", Runs,
                ("회차", s => s.RunId),
                ("시작", s => s.Start),
                ("끝", s => s.End),
                ("걸린 시간", s => s.DurationText),
                ("개수", s => s.Count),
                ("A", s => s.A),
                ("B", s => s.B),
                ("불량", s => s.Ng),
                ("재분류", s => s.Resort),
                ("A 비율", s => s.ARatio));
        }
        if (path != null) Status = $"저장함: {path}";
    }
}

public sealed class RunSummary
{
    public RunSummary(string runId, IReadOnlyList<HistoryRow> rows)
    {
        RunId = runId;
        Start = rows.Min(r => r.StartedAt ?? r.FinishedAt);
        End = rows.Max(r => r.FinishedAt);
        Count = rows.Count;
        A = rows.Count(r => r.Grade == "A");
        B = rows.Count(r => r.Grade == "B");
        Ng = rows.Count(r => r.Grade == "NG");
        Resort = rows.Count(r => r.Grade == "RESORT");
    }

    public string RunId { get; }
    public DateTime? Start { get; }
    public DateTime? End { get; }
    public int Count { get; }
    public int A { get; }
    public int B { get; }
    public int Ng { get; }
    public int Resort { get; }

    public string ARatio => Count == 0 ? "-" : $"{A * 100.0 / Count:0}%";

    public string DurationText => Start is DateTime s && End is DateTime e ? (e - s).ToString(@"mm\:ss") : "-";
}
