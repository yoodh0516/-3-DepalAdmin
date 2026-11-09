using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Data;
using DepalAdmin.Core.Handover;
using DepalAdmin.Core.Settings;

namespace DepalAdmin.Feature.Report;

/// <summary>
/// 담당: 조시현 — 보고서.
///   일일: 고른 날짜 하루 전체 / 회차: 그날의 회차 하나
///   데이터: IHistoryRepository(장성찬, 결과·이벤트 수) + IHandoverStore(유동호, 인수인계 메모)
///   인쇄: 작업자 이상 (PDF는 인쇄 대화상자에서 "Microsoft Print to PDF")
/// </summary>
public partial class ReportViewModel : ObservableObject
{
    private const string Daily = "일일";
    private const string PerRun = "회차";

    private readonly IHistoryRepository _repo;
    private readonly IHandoverStore _handover;
    private readonly Session _session;
    private readonly IAuditLog _audit;
    private readonly AppSettings _settings;
    private ReportData? _data;

    public string[] Kinds { get; } = { Daily, PerRun };
    public ObservableCollection<string> RunIds { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPerRun))]
    private string _kind = Daily;

    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private string? _runId;
    [ObservableProperty] private FlowDocument? _document;
    [ObservableProperty] private string _status = "날짜를 고르고 [만들기]를 누르세요.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrintCommand))]
    private bool _canPrint;

    public bool IsPerRun => Kind == PerRun;

    public ReportViewModel(IHistoryRepository repo, IHandoverStore handover, Session session, IAuditLog audit, AppSettings settings)
    {
        _repo = repo;
        _handover = handover;
        _session = session;
        _audit = audit;
        _settings = settings;
        _session.Changed += (_, _) => CanPrint = _data != null && _session.Can(Permission.ExportData);
    }

    partial void OnDateChanged(DateTime value) => _ = LoadRunsAsync();
    partial void OnKindChanged(string value) { if (IsPerRun) _ = LoadRunsAsync(); }

    private async Task LoadRunsAsync()
    {
        try
        {
            var rows = await _repo.GetResultsAsync(Date.Date, Date.Date.AddDays(1), null);
            RunIds.Clear();
            foreach (var id in rows.Select(r => r.RunId).Distinct().OrderBy(x => x)) RunIds.Add(id);
            RunId = RunIds.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Status = $"회차 목록을 못 읽었습니다: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task MakeAsync()
    {
        Status = "만드는 중…";
        try
        {
            var from = Date.Date;
            var to = from.AddDays(1);
            var rows = await _repo.GetResultsAsync(from, to, null);
            string title, period;

            if (IsPerRun)
            {
                if (string.IsNullOrEmpty(RunId))
                {
                    Status = "이 날짜에 회차가 없습니다.";
                    return;
                }
                rows = rows.Where(r => r.RunId == RunId).ToList();
                // 회차 보고서의 이벤트·메모는 그 회차가 돌던 시간만
                from = rows.Min(r => r.StartedAt ?? r.FinishedAt) ?? from;
                to = (rows.Max(r => r.FinishedAt) ?? to).AddSeconds(1);
                title = "회차 보고서";
                period = $"회차 {RunId}  ({from:yyyy-MM-dd HH:mm} ~ {to:HH:mm})";
            }
            else
            {
                title = "일일 보고서";
                period = $"{from:yyyy-MM-dd (ddd)}";
            }

            var events = await _repo.GetEventCountsAsync(from, to);
            var notes = await _handover.GetAsync(from, to);

            _data = new ReportData
            {
                Title = title,
                Station = _settings.StationName,
                Period = period,
                MadeBy = _session.UserName,
                IsDaily = !IsPerRun,
                Rows = rows,
                EventCounts = events,
                Notes = notes,
            };
            Document = ReportBuilder.Build(_data);
            CanPrint = _session.Can(Permission.ExportData);
            Status = $"건전지 {rows.Count}개, 인수인계 메모 {notes.Count}건";
        }
        catch (Exception ex)
        {
            Status = $"만들지 못했습니다: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private void Print()
    {
        if (_data == null) return;
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;

        // 미리보기에 쓰는 문서는 화면에 붙어 있어서 인쇄용으로 새로 만든다
        var doc = ReportBuilder.Build(_data);
        doc.PageWidth = dialog.PrintableAreaWidth;
        doc.PageHeight = dialog.PrintableAreaHeight;
        doc.ColumnWidth = dialog.PrintableAreaWidth;
        dialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"{_data.Title} {_data.Period}");

        _audit.Write("보고서 인쇄", $"{_data.Title} {_data.Period}");
        Status = "인쇄를 보냈습니다.";
    }
}
