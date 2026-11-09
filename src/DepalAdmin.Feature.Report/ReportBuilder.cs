using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using DepalAdmin.Core;

namespace DepalAdmin.Feature.Report;

/// <summary>
/// ReportData → A4 한 장짜리 FlowDocument. 화면 미리보기와 인쇄가 같은 모양을 쓴다.
/// 인쇄 대화상자에서 "Microsoft Print to PDF"를 고르면 PDF로 저장된다 (따로 라이브러리 필요 없음).
/// 보고서는 흰 종이에 인쇄하므로 화면 색(회색 바탕)이 아니라 흑백 위주.
/// </summary>
public static class ReportBuilder
{
    private static readonly FontFamily Font = new("Malgun Gothic");
    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1C));
    private static readonly Brush Sub = new SolidColorBrush(Color.FromRgb(0x5B, 0x5A, 0x54));
    private static readonly Brush Line = new SolidColorBrush(Color.FromRgb(0x8F, 0x8E, 0x87));
    private static readonly Brush HeadFill = new SolidColorBrush(Color.FromRgb(0xE6, 0xE5, 0xE0));

    // A4 (96 dpi 기준 793 × 1122)
    private const double PageWidth = 793;
    private const double PageHeight = 1122;

    public static FlowDocument Build(ReportData d)
    {
        var doc = new FlowDocument
        {
            FontFamily = Font,
            FontSize = 12,
            Foreground = Ink,
            Background = Brushes.White,
            PagePadding = new Thickness(56, 52, 56, 52),
            PageWidth = PageWidth,
            PageHeight = PageHeight,
            ColumnWidth = PageWidth,   // 한 단
        };

        // ── 머리 ──
        doc.Blocks.Add(new Paragraph(new Run(d.Title)) { FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
        doc.Blocks.Add(new Paragraph(new Run($"{d.Station}   {d.Period}")) { Foreground = Sub, Margin = new Thickness(0, 0, 0, 2) });
        doc.Blocks.Add(new Paragraph(new Run($"작성 {d.MadeBy}   {d.MadeAt:yyyy-MM-dd HH:mm}")) { Foreground = Sub, FontSize = 11, Margin = new Thickness(0, 0, 0, 14) });

        // ── 요약 ──
        Heading(doc, "요약");
        doc.Blocks.Add(MakeTable(new[] { "처리", "A", "B", "불량", "재분류", "배출 실패", "1개당 평균" }, new[]
        {
            new[] { $"{d.Total}개", $"{d.Count("A")} ({d.Ratio("A")})", $"{d.Count("B")} ({d.Ratio("B")})",
                    $"{d.Count("NG")} ({d.Ratio("NG")})", $"{d.Count("RESORT")} ({d.Ratio("RESORT")})",
                    d.SortFailed.ToString(), d.AvgCycle },
        }));
        doc.Blocks.Add(Note("등급 기준값은 아직 임시값입니다. 기준이 바뀌면 결과의 기준 버전(rule_version)이 달라집니다. 배출 실패는 등급은 정해졌지만 분류함에 넣지 못한 건(등급 개수에 포함)."));

        // ── 불량·재분류 사유 ──
        Heading(doc, "불량·재분류 사유 (많은 순 5개)");
        var reasons = d.TopReasons;
        if (reasons.Count == 0) doc.Blocks.Add(Note("불량·재분류 건이 없습니다."));
        else doc.Blocks.Add(MakeTable(new[] { "사유", "등급", "개수" },
                 reasons.Select(r => new[] { r.Reason, Labels.Grade(r.Grade), r.Count.ToString() }).ToArray(),
                 widths: new[] { 4.0, 1.0, 1.0 }));

        // ── 장치·라인 이벤트 ──
        Heading(doc, "장치·라인 이벤트");
        doc.Blocks.Add(MakeTable(new[] { "고장 (ERROR)", "주의 (WARN)", "정보 (INFO)" }, new[]
        {
            new[] { d.Events("ERROR").ToString(), d.Events("WARN").ToString(), d.Events("INFO").ToString() },
        }));

        // ── 회차별 (일일 보고서만) ──
        if (d.IsDaily)
        {
            Heading(doc, "회차별");
            var runs = d.Runs;
            if (runs.Count == 0) doc.Blocks.Add(Note("이 날짜에 회차가 없습니다."));
            else doc.Blocks.Add(MakeTable(new[] { "회차", "시작", "개수", "A", "B", "불량", "재분류" },
                     runs.Select(r => new[] { r.RunId, r.Start?.ToString("HH:mm") ?? "-", r.Count.ToString(),
                                              r.A.ToString(), r.B.ToString(), r.Ng.ToString(), r.Resort.ToString() }).ToArray(),
                     widths: new[] { 3.0, 1, 1, 1, 1, 1, 1 }));
        }

        // ── 인수인계 메모 ──
        Heading(doc, "인수인계 메모");
        if (d.Notes.Count == 0) doc.Blocks.Add(Note("남긴 메모가 없습니다."));
        foreach (var n in d.Notes.OrderBy(n => n.WrittenAt))
        {
            var p = new Paragraph { Margin = new Thickness(0, 0, 0, 8), BorderBrush = Line, BorderThickness = new Thickness(2, 0, 0, 0), Padding = new Thickness(8, 0, 0, 0) };
            p.Inlines.Add(new Run($"{n.WrittenAt:HH:mm}  {n.AuthorName}") { FontWeight = FontWeights.SemiBold });
            p.Inlines.Add(new Run($"   {Labels.Mode(n.LineMode)}, 미확인 알람 {n.UnackedAlarms}건") { Foreground = Sub, FontSize = 11 });
            p.Inlines.Add(new LineBreak());
            p.Inlines.Add(new Run(n.Text));
            doc.Blocks.Add(p);
        }

        return doc;
    }

    private static void Heading(FlowDocument doc, string text) =>
        doc.Blocks.Add(new Paragraph(new Run(text))
        {
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 14, 0, 6),
            BorderBrush = Ink,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 3),
        });

    private static Paragraph Note(string text) =>
        new(new Run(text)) { Foreground = Sub, FontSize = 11, Margin = new Thickness(0, 2, 0, 4) };

    private static Table MakeTable(string[] headers, string[][] rows, double[]? widths = null)
    {
        var t = new Table { CellSpacing = 0, BorderBrush = Line, BorderThickness = new Thickness(1, 1, 0, 0), Margin = new Thickness(0, 0, 0, 6) };
        for (var i = 0; i < headers.Length; i++)
            t.Columns.Add(new TableColumn { Width = new GridLength(widths?[i] ?? 1, GridUnitType.Star) });

        var group = new TableRowGroup();
        group.Rows.Add(MakeRow(headers, head: true));
        foreach (var r in rows) group.Rows.Add(MakeRow(r, head: false));
        t.RowGroups.Add(group);
        return t;
    }

    private static TableRow MakeRow(string[] cells, bool head)
    {
        var row = new TableRow();
        if (head) row.Background = HeadFill;
        foreach (var c in cells)
        {
            row.Cells.Add(new TableCell(new Paragraph(new Run(c)) { Margin = new Thickness(0) })
            {
                BorderBrush = Line,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(6, 3, 6, 3),
                FontWeight = head ? FontWeights.SemiBold : FontWeights.Normal,
            });
        }
        return row;
    }
}
