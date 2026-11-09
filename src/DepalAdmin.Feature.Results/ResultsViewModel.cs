using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Models;
using DepalAdmin.Core.Settings;

namespace DepalAdmin.Feature.Results;

/// <summary>
/// 담당: 김하균 — 실시간 검사 결과 표·상세.
/// 받는 것: server/results
/// </summary>
public partial class ResultsViewModel : ObservableObject
{
    private const string All = "전체";
    private readonly int _maxRows;

    public ObservableCollection<ResultRow> Rows { get; } = new();
    public ICollectionView View { get; }
    public string[] Grades { get; } = { All, "A", "B", "NG", "RESORT" };

    [ObservableProperty] private ResultRow? _selected;
    [ObservableProperty] private string _grade = All;
    [ObservableProperty] private string _summary = "결과를 기다리는 중입니다.";

    public ResultsViewModel(ILineClient client, AppSettings settings)
    {
        _maxRows = settings.MaxRowsInMemory;

        View = CollectionViewSource.GetDefaultView(Rows);
        View.Filter = o => o is ResultRow r && (Grade == All || r.Grade == Grade);

        client.ResultReceived += (_, result) => Add(result);
    }

    partial void OnGradeChanged(string value) => View.Refresh();

    private void Add(BatteryResult result)
    {
        Rows.Insert(0, new ResultRow(result));
        while (Rows.Count > _maxRows) Rows.RemoveAt(Rows.Count - 1);

        int N(string g) => Rows.Count(r => r.Grade == g);
        Summary = $"받은 결과 {Rows.Count}건   A {N("A")}   B {N("B")}   불량 {N("NG")}   재분류 {N("RESORT")}";
    }

    [RelayCommand]
    private void Clear()
    {
        Rows.Clear();
        Selected = null;
        Summary = "목록을 비웠습니다.";
    }

}

/// <summary>표 한 줄. 화면용 글자로 바꿔 둔다.</summary>
public sealed class ResultRow
{
    public ResultRow(BatteryResult raw) => Raw = raw;

    public BatteryResult Raw { get; }

    public string Time => Json.ParseTs(Raw.FinishedAt)?.ToString("HH:mm:ss") ?? "-";
    public int BatteryId => Raw.BatteryId;
    public string Cell => Raw.PalletCell is { Length: 2 } c ? $"{c[0] * 5 + c[1] + 1}" : "-";
    public string Grade => Raw.Grade;
    public string GradeText => Labels.Grade(Raw.Grade);

    public string VisionText => Raw.Vision switch
    {
        null => "-",
        { Status: "failed" } => "검사 실패",
        var v => $"{v.Result} {v.Conf:0.00}",
    };

    public string PolarityText => Raw.Vision?.Polarity switch
    {
        "minus_up" => "−극 위",
        "plus_up" => "+극 위",
        "unknown" => "모름",
        _ => "-",
    };

    public string VOpenText => Raw.VOpen is double v ? $"{v:0.000}" : "-";
    public string VLoadText => Raw.VLoad is double v ? $"{v:0.000}" : "-";
    public string RIntText => Raw.RInt is double r ? $"{r:0.000}" : "-";
    public string DestText => Labels.Dest(Raw.Dest);
    public string Reason => Raw.Reason ?? "";

    public string RawJson => JsonSerializer.Serialize(Raw, Json.Pretty);
}
