using DepalAdmin.Core.Data;
using DepalAdmin.Core.Handover;

namespace DepalAdmin.Feature.Report;

/// <summary>
/// 보고서 한 장에 들어갈 숫자를 모은다 (화면·인쇄 둘 다 이걸로 그림).
/// TODO(조시현): 보고서에 넣을 항목은 팀과 확정 — 지금은 처리 개수·등급 비율·사유·이벤트 수·회차 표·인수인계 메모
/// </summary>
public sealed class ReportData
{
    public required string Title { get; init; }
    public required string Station { get; init; }
    public required string Period { get; init; }
    public required string MadeBy { get; init; }
    public DateTime MadeAt { get; init; } = DateTime.Now;
    public bool IsDaily { get; init; }

    public required IReadOnlyList<HistoryRow> Rows { get; init; }
    public required IReadOnlyDictionary<string, int> EventCounts { get; init; }
    public required IReadOnlyList<HandoverNote> Notes { get; init; }

    public int Total => Rows.Count;
    public int Count(string grade) => Rows.Count(r => r.Grade == grade);
    public string Ratio(string grade) => Total == 0 ? "-" : $"{Count(grade) * 100.0 / Total:0.0}%";

    /// <summary>등급은 정해졌는데 분류함에 못 넣은 건 (sorted_destination = NULL). DB 문서: 등급 수와 실제 배출 수는 따로 본다</summary>
    public int SortFailed => Rows.Count(r => r.Dest == null);

    public string AvgCycle
    {
        get
        {
            var s = Rows.Where(r => r.StartedAt != null && r.FinishedAt != null)
                        .Select(r => (r.FinishedAt!.Value - r.StartedAt!.Value).TotalSeconds).ToList();
            return s.Count == 0 ? "-" : $"{s.Average():0.0}초";
        }
    }

    public IReadOnlyList<(string Reason, string Grade, int Count)> TopReasons =>
        Rows.Where(r => !string.IsNullOrWhiteSpace(r.Reason))
            .GroupBy(r => r.Reason!)
            .Select(g => (g.Key, g.First().Grade, g.Count()))
            .OrderByDescending(x => x.Item3)
            .Take(5)
            .ToList();

    public IReadOnlyList<(string RunId, DateTime? Start, int Count, int A, int B, int Ng, int Resort)> Runs =>
        Rows.GroupBy(r => r.RunId)
            .Select(g => (g.Key, g.Min(r => r.StartedAt ?? r.FinishedAt), g.Count(),
                          g.Count(r => r.Grade == "A"), g.Count(r => r.Grade == "B"),
                          g.Count(r => r.Grade == "NG"), g.Count(r => r.Grade == "RESORT")))
            .OrderBy(x => x.Item2)
            .ToList();

    public int Events(string level) => EventCounts.TryGetValue(level, out var n) ? n : 0;
}
