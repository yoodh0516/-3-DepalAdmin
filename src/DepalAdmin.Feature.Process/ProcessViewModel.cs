using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using DepalAdmin.Core;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Models;
using DepalAdmin.Core.Pages;

namespace DepalAdmin.Feature.Process;

/// <summary>
/// 담당: 김초아 — 라인 현황: 공정 흐름도, 분류 개수·현재 상태, AGV.
/// 받는 것: server/line (mode · step · counts · agv), server/events 중 장치가 agv인 것(AGV 기록)
/// 이 화면에 끼워 들어오는 남의 패널:
///   가운데 팔레트 맵 — 김하균 (PanelSlots.LinePallet)
///   오른쪽 조작 버튼 — 유동호 (PanelSlots.LineControls)
/// </summary>
public partial class ProcessViewModel : ObservableObject
{
    private const int AgvLogMax = 30;

    /// <summary>
    /// 공정 순서. Step은 server/line의 step 값 (통신 규약 5장).
    /// TODO(김초아): 실제 라인 배치를 보고 이름·설명·순서를 맞추기
    /// </summary>
    public ObservableCollection<Station> Stations { get; } = new()
    {
        new Station("팔레트", "칸 인식", "scan"),
        new Station("로봇팔", "집기", "pick"),
        new Station("검사 부스", "외관·극 방향", "inspect"),
        new Station("측정", "전압·내부저항", "measure"),
        new Station("컨베이어", "원점 복귀", "home"),
        new Station("분류", "분류함에 넣기", "sort"),
        new Station("AGV", "A 운송 대기", "wait_agv", isLast: true),
    };

    public ObservableCollection<LineEvent> AgvLog { get; } = new();

    public FrameworkElement? PalletPanel { get; }
    public FrameworkElement? ControlPanel { get; }

    [ObservableProperty] private string _mode = "idle";
    [ObservableProperty] private string _modeText = "-";
    [ObservableProperty] private string _stepText = "-";
    [ObservableProperty] private string _batteryText = "-";
    [ObservableProperty] private string _runId = "-";
    [ObservableProperty] private string? _serverMessage;

    [ObservableProperty] private int _countA;
    [ObservableProperty] private int _countB;
    [ObservableProperty] private int _countNg;
    [ObservableProperty] private int _countResort;
    [ObservableProperty] private int _total;
    [ObservableProperty] private string _aRatio = "-";

    [ObservableProperty] private string _agvState = "-";
    [ObservableProperty] private string _agvStateKey = "home";
    [ObservableProperty] private string _agvLoad = "-";
    [ObservableProperty] private double _agvFill;

    public ProcessViewModel(ILineClient client, IEnumerable<IPanel> panels)
    {
        PalletPanel = PanelSlots.Find(panels, PanelSlots.LinePallet);
        ControlPanel = PanelSlots.Find(panels, PanelSlots.LineControls);

        client.LineStatusReceived += (_, line) => Apply(line);
        client.EventReceived += (_, ev) =>
        {
            if (ev.Device != "agv") return;
            AgvLog.Insert(0, ev);
            while (AgvLog.Count > AgvLogMax) AgvLog.RemoveAt(AgvLog.Count - 1);
        };
        if (client.LastLine != null) Apply(client.LastLine);
    }

    private void Apply(LineStatus s)
    {
        Mode = s.Mode;
        ModeText = Labels.Mode(s.Mode);
        StepText = Labels.Step(s.Step);
        BatteryText = s.BatteryId?.ToString() ?? "-";
        RunId = s.RunId ?? "-";
        ServerMessage = s.Message;

        // 일시정지·작업자 대기 중에도 멈춘 공정이 보이도록 running/paused 둘 다 표시
        var activeStep = s.Mode is "running" or "paused" ? s.Step : null;
        foreach (var st in Stations) st.IsActive = st.Step == activeStep;

        CountA = s.Count("A");
        CountB = s.Count("B");
        CountNg = s.Count("NG");
        CountResort = s.Count("RESORT");
        Total = CountA + CountB + CountNg + CountResort;
        ARatio = Total == 0 ? "-" : $"{CountA * 100.0 / Total:0}%";

        if (s.Agv != null)
        {
            AgvState = Labels.AgvState(s.Agv.State);
            AgvStateKey = s.Agv.State;
            AgvLoad = $"{s.Agv.Loaded} / {s.Agv.Batch}";
            AgvFill = s.Agv.Batch == 0 ? 0 : Math.Min(100, s.Agv.Loaded * 100.0 / s.Agv.Batch);
        }
    }
}

/// <summary>공정 흐름도의 한 칸</summary>
public partial class Station : ObservableObject
{
    public Station(string title, string sub, string step, bool isLast = false)
    {
        Title = title;
        Sub = sub;
        Step = step;
        IsLast = isLast;
    }

    public string Title { get; }
    public string Sub { get; }
    public string Step { get; }
    public bool IsLast { get; }

    [ObservableProperty] private bool _isActive;
}
