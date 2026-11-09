using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core;
using DepalAdmin.Core.Data;

namespace DepalAdmin.Feature.Stats;

/// <summary>
/// 기간 통계. 차트 라이브러리 없이 막대를 직접 그린다 (높이·너비를 여기서 계산).
///   - 등급 비율
///   - 시간대별 처리량
///   - 불량·재분류 사유 순위
///   - 무부하 전압 분포 (+ 임시 기준선)
/// </summary>
public partial class StatsViewModel : ObservableObject
{
    // ── 수정할 값 ──────────────────────────────────────────
    private const double BarMaxHeight = 120;
    private const double VoltFrom = 1.20;
    private const double VoltStep = 0.05;
    private const int VoltBins = 10;          // 1.20 ~ 1.70
    private const double TempLimitA = 1.50;   // ⚠ 임시 기준값 (서버의 판정 기준과 맞출 것)
    private const double TempLimitB = 1.30;
    // ──────────────────────────────────────────────────────

    private static readonly string[] GradeOrder = { "A", "B", "NG", "RESORT" };
    private readonly IHistoryRepository _repo;

    public ObservableCollection<GradeBar> Grades { get; } = new();
    public ObservableCollection<Bar> Hours { get; } = new();
    public ObservableCollection<Bar> Volts { get; } = new();
    public ObservableCollection<ReasonRow> Reasons { get; } = new();

    [ObservableProperty] private DateTime _from = DateTime.Today.AddDays(-6);
    [ObservableProperty] private DateTime _to = DateTime.Today;
    [ObservableProperty] private string _status = "";

    [ObservableProperty] private int _total;
    [ObservableProperty] private string _aRatio = "-";
    [ObservableProperty] private string _avgCycle = "-";
    [ObservableProperty] private string _avgVOpen = "-";
    [ObservableProperty] private string _limitNote = $"기준선 {TempLimitB:0.00} V / {TempLimitA:0.00} V는 임시값";

    public StatsViewModel(IHistoryRepository repo)
    {
        _repo = repo;
        _ = LoadAsync();   // 처음 열 때 한 번
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        Status = "불러오는 중…";
        try
        {
            var rows = await _repo.GetResultsAsync(From.Date, To.Date.AddDays(1), null);
            Build(rows);
            Status = $"{From:MM-dd} ~ {To:MM-dd}, 건전지 {rows.Count}개";
        }
        catch (Exception ex)
        {
            Status = $"불러오지 못했습니다: {ex.Message}";
        }
    }

    private void Build(IReadOnlyList<HistoryRow> rows)
    {
        Total = rows.Count;
        var a = rows.Count(r => r.Grade == "A");
        ARatio = Total == 0 ? "-" : $"{a * 100.0 / Total:0.0}%";

        var cycles = rows.Where(r => r.StartedAt != null && r.FinishedAt != null)
                         .Select(r => (r.FinishedAt!.Value - r.StartedAt!.Value).TotalSeconds)
                         .ToList();
        AvgCycle = cycles.Count == 0 ? "-" : $"{cycles.Average():0.0}초";

        var volts = rows.Where(r => r.VOpen != null).Select(r => r.VOpen!.Value).ToList();
        AvgVOpen = volts.Count == 0 ? "-" : $"{volts.Average():0.000} V";

        // 등급 비율
        Grades.Clear();
        foreach (var g in GradeOrder)
        {
            var n = rows.Count(r => r.Grade == g);
            Grades.Add(new GradeBar(g, Labels.Grade(g), n, Total == 0 ? 0 : n * 100.0 / Total));
        }

        // 시간대별 (데이터가 있는 시간 앞뒤만)
        Hours.Clear();
        var byHour = rows.Where(r => r.FinishedAt != null).GroupBy(r => r.FinishedAt!.Value.Hour)
                         .ToDictionary(g => g.Key, g => g.Count());
        if (byHour.Count > 0)
        {
            var max = byHour.Values.Max();
            for (var h = byHour.Keys.Min(); h <= byHour.Keys.Max(); h++)
            {
                var n = byHour.TryGetValue(h, out var c) ? c : 0;
                Hours.Add(new Bar($"{h}시", n, n * BarMaxHeight / max, Highlight: false));
            }
        }

        // 전압 분포
        Volts.Clear();
        var bins = new int[VoltBins];
        foreach (var v in volts)
        {
            var i = (int)Math.Floor((v - VoltFrom) / VoltStep);
            if (i >= 0 && i < VoltBins) bins[i]++;
        }
        var vmax = Math.Max(1, bins.Max());
        for (var i = 0; i < VoltBins; i++)
        {
            var low = VoltFrom + i * VoltStep;
            Volts.Add(new Bar($"{low:0.00}", bins[i], bins[i] * BarMaxHeight / vmax,
                              Highlight: low + 1e-9 >= TempLimitA));   // A 기준 이상 구간은 진하게
        }

        // 사유 순위
        Reasons.Clear();
        var reasons = rows.Where(r => !string.IsNullOrWhiteSpace(r.Reason))
                          .GroupBy(r => r.Reason!)
                          .Select(g => (Reason: g.Key, Count: g.Count(), Grade: g.First().Grade))
                          .OrderByDescending(x => x.Count)
                          .Take(8)
                          .ToList();
        var rmax = reasons.Count == 0 ? 1 : reasons.Max(x => x.Count);
        var rank = 1;
        foreach (var r in reasons)
            Reasons.Add(new ReasonRow(rank++, r.Reason, r.Grade, r.Count, r.Count * 100.0 / rmax));
    }
}

public sealed record GradeBar(string Grade, string Label, int Count, double Percent)
{
    public string Text => $"{Count}개  {Percent:0.0}%";
}

/// <summary>세로 막대 하나. Height는 픽셀.</summary>
public sealed record Bar(string Label, int Count, double Height, bool Highlight);

/// <summary>사유 순위 한 줄. Width는 1위 대비 %</summary>
public sealed record ReasonRow(int Rank, string Reason, string Grade, int Count, double Width);
