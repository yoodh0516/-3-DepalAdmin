using System.Windows.Threading;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Models;

namespace DepalAdmin.Comm;

/// <summary>
/// 브로커 없이 화면을 만들 수 있게 1초마다 가짜 데이터를 만든다.
/// settings.json에서 "UseFakeMqtt": true 일 때 쓰인다.
/// 알람 화면 시험용으로 가끔 장치 지연(WARN)과 집기 실패(ERROR → 일시정지)도 일으킨다.
/// ⚠ 등급 판정은 화면 개발용 흉내일 뿐. 실제 판정은 라인 컨트롤러가 한다 (기준값 임시).
/// </summary>
public sealed class FakeLineClient : LineClientBase
{
    // ── 수정할 값 ──────────────────────────────────────────
    private const int BatteriesPerRun = 10;       // 팔레트 2×5
    private const int AgvBatch = 8;               // 잠정값
    private const double DelayChance = 0.01;      // 1초마다 어떤 장치가 '지연'될 확률
    private const int DelaySeconds = 4;
    private const double PickFailChance = 0.06;   // 집기 실패 확률
    // ──────────────────────────────────────────────────────

    private static readonly string[] Steps = { "scan", "pick", "inspect", "measure", "home", "sort" };
    private static readonly string[] DeviceNames = { "server", "controller", "pi", "yolo", "agv", "db_saver" };
    private static readonly string[] AgvCycle = { "going", "going", "away", "away", "returning", "returning", "home" };

    private readonly Random _rnd = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Dictionary<string, int> _counts = new() { ["A"] = 0, ["B"] = 0, ["NG"] = 0, ["RESORT"] = 0 };
    private readonly Dictionary<string, int> _delayLeft = new();

    private string _mode = "idle";
    private string? _step;
    private string? _runId;
    private int _batteryId;
    private int _stepIndex;
    private int[] _cell = { 0, 0 };
    private int _agvLoaded;
    private int _agvCycleIndex = AgvCycle.Length - 1;   // home
    private string? _message = "가짜 데이터 모드입니다. 라인 현황에서 [시작]을 눌러 보세요.";

    public FakeLineClient()
    {
        _timer.Tick += (_, _) => Tick();
    }

    public override Task StartAsync(CancellationToken ct = default)
    {
        SetState(ConnectionState.Fake);
        RaiseDevices(MakeDevices());
        RaiseLine(MakeLine());
        _timer.Start();
        return Task.CompletedTask;
    }

    public override Task StopAsync()
    {
        OnUi(() => _timer.Stop());
        SetState(ConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    public override Task SendCommandAsync(string action, CancellationToken ct = default)
    {
        switch (action)
        {
            case UiActions.Start when _mode == "idle":
                StartRun();
                break;
            case UiActions.Pause when _mode == "running":
                _mode = "paused";
                _message = "작업자 일시정지";
                Event("INFO", "controller", "일시정지");
                break;
            case UiActions.Resume when _mode == "paused":
                _mode = "running";
                _message = null;
                Event("INFO", "controller", "재개");
                break;
            case UiActions.Stop when _mode is "running" or "paused":
                EndRun("작업자가 회차 종료");
                break;
            case UiActions.Estop:
                _mode = "estop";
                _message = "비상정지 상태 — 원인 확인 후 [비상정지 해제]";
                Event("ERROR", "controller", "비상정지");
                break;
            case UiActions.Reset when _mode == "estop":
                _mode = "idle";
                _step = null;
                _message = null;
                Event("INFO", "controller", "비상정지 해제");
                break;
            case UiActions.Skip when _mode == "paused" && _step == "wait_operator":
                Event("WARN", "controller", $"건전지 {_batteryId} 건너뜀");
                _stepIndex = 0;   // 다음 칸부터
                _mode = "running";
                _message = null;
                break;
            default:
                Event("WARN", "controller", $"지금 상태({_mode})에서는 '{action}' 명령을 받을 수 없음");
                break;
        }
        RaiseLine(MakeLine());
        return Task.CompletedTask;
    }

    // ── 1초마다 ────────────────────────────────────────────

    private void Tick()
    {
        AdvanceDelays();
        RaiseDevices(MakeDevices());
        AdvanceAgv();
        if (_mode == "running") AdvanceStep();
        RaiseLine(MakeLine());
    }

    private void AdvanceStep()
    {
        _stepIndex = (_stepIndex + 1) % Steps.Length;
        _step = Steps[_stepIndex];
        switch (_step)
        {
            case "scan":
                if (_batteryId >= BatteriesPerRun) EndRun("팔레트 비었음");
                break;
            case "pick":
                _batteryId++;
                _cell = new[] { (_batteryId - 1) / 5 % 2, (_batteryId - 1) % 5 };
                if (_rnd.NextDouble() < PickFailChance)
                {
                    _mode = "paused";
                    _step = "wait_operator";
                    _message = $"건전지 {_batteryId} 집기 실패 — [재개]로 다시 시도하거나 [건너뛰기]";
                    Event("ERROR", "pi", $"pick 실패 (grip_failed) 칸 [{_cell[0]},{_cell[1]}]");
                }
                break;
            case "sort":
                FinishBattery();
                break;
        }
    }

    private void AdvanceDelays()
    {
        foreach (var name in _delayLeft.Keys.ToList())
        {
            if (--_delayLeft[name] > 0) continue;
            _delayLeft.Remove(name);
            Event("INFO", name, "복구됨 (지연 → 정상)");
        }

        if (_rnd.NextDouble() < DelayChance)
        {
            var name = DeviceNames[_rnd.Next(1, DeviceNames.Length)];   // server 빼고
            if (_delayLeft.TryAdd(name, DelaySeconds))
                Event("WARN", name, $"하트비트 3.{_rnd.Next(10)}초 없음 → 지연");
        }
    }

    private void AdvanceAgv()
    {
        if (AgvCycle[_agvCycleIndex] == "home") return;
        _agvCycleIndex++;
        if (AgvCycle[_agvCycleIndex] == "away") Event("INFO", "agv", "AGV 목적지 도착, 하역");
        if (AgvCycle[_agvCycleIndex] == "home") Event("INFO", "agv", "AGV 복귀 완료");
    }

    private void StartRun()
    {
        _runId = $"{DateTime.Now:yyyyMMdd-HHmmss}-{_rnd.Next(0x10000):x4}";
        foreach (var key in _counts.Keys.ToList()) _counts[key] = 0;
        _batteryId = 0;
        _stepIndex = 0;
        _step = Steps[0];
        _mode = "running";
        _message = null;
        Event("INFO", "controller", "회차 시작");
    }

    private void EndRun(string reason)
    {
        if (_agvLoaded > 0) SendAgv();   // 회차 끝: 남은 A도 출발 (팀 결정)
        _mode = "idle";
        _step = null;
        _message = $"회차 종료 — {reason}";
        Event("INFO", "controller", $"회차 종료 ({reason})");
    }

    private void FinishBattery()
    {
        var result = MakeResult();
        _counts[result.Grade]++;
        RaiseResult(result);

        if (result.Dest == "AGV")
        {
            _agvLoaded++;
            if (_agvLoaded >= AgvBatch) SendAgv();
        }
    }

    private void SendAgv()
    {
        Event("INFO", "agv", $"AGV 출발 ({_agvLoaded}개 적재)");
        _agvLoaded = 0;
        _agvCycleIndex = 0;   // going
    }

    // ── 가짜 메시지 만들기 ────────────────────────────────

    private LineStatus MakeLine() => new()
    {
        RunId = _runId,
        Mode = _mode,
        Step = _mode is "running" or "paused" ? _step : null,
        BatteryId = _batteryId == 0 ? null : _batteryId,
        Counts = new Dictionary<string, int>(_counts),
        Agv = new AgvInfo { Loaded = _agvLoaded, Batch = AgvBatch, State = AgvCycle[_agvCycleIndex] },
        Message = _message,
        Ts = Json.Now(),
    };

    private Dictionary<string, DeviceInfo> MakeDevices()
    {
        var devices = new Dictionary<string, DeviceInfo>();
        foreach (var name in DeviceNames)
        {
            var info = new DeviceInfo
            {
                Name = name,
                State = _delayLeft.ContainsKey(name) ? "delayed" : "ok",
            };
            if (name == "pi")
            {
                info.Extra["gripper"] = _batteryId >= 7 ? "weak" : "normal";   // 시험용: 뒤쪽에서 집게 약해짐
                info.Extra["arm"] = _mode == "estop" ? "stopped" : "ok";
                info.Extra["arduino"] = "ok";
            }
            devices[name] = info;
        }
        return devices;
    }

    private BatteryResult MakeResult()
    {
        var r = new BatteryResult
        {
            RunId = _runId ?? "",
            BatteryId = _batteryId,
            PalletCell = _cell,
            PickedAt = Json.Now(),
            FinishedAt = Json.Now(),
            RuleVersion = "fake",
        };

        var p = _rnd.NextDouble();
        if (p < 0.08)
        {
            r.Vision = new VisionInfo { Status = "ok", Result = "scratch", Conf = 0.91, Polarity = "minus_up" };
            r.ElectricalStatus = "skipped";
            (r.Grade, r.Dest, r.Reason) = ("NG", "NG_BOX", "외관 불량(scratch)");
        }
        else if (p < 0.13)
        {
            r.Vision = new VisionInfo { Status = "ok", Result = "normal", Conf = 0.95, Polarity = "plus_up" };
            r.ElectricalStatus = "skipped";
            (r.Grade, r.Dest, r.Reason) = ("RESORT", "RESORT_BOX", "상단 카메라: +극이 위");
        }
        else
        {
            r.Vision = new VisionInfo { Status = "ok", Result = "normal", Conf = Math.Round(0.85 + _rnd.NextDouble() * 0.14, 2), Polarity = "minus_up" };
            r.ElectricalStatus = "measured";
            r.Polarity = "normal";
            var v = Math.Round(1.25 + _rnd.NextDouble() * 0.40, 3);
            var ri = Math.Round(0.10 + Math.Pow(_rnd.NextDouble(), 2), 3);
            r.VOpen = v;
            r.RInt = ri;
            r.RLoad = 3.31;
            r.VLoad = Math.Round(v * 3.31 / (3.31 + ri), 3);

            // 흉내용 임시 기준 (실제 기준은 라인 컨트롤러에 있음)
            (r.Grade, r.Dest) = v >= 1.50 && ri <= 0.35 ? ("A", "AGV")
                              : v >= 1.30 && ri <= 1.0 ? ("B", "B_BOX")
                              : ("NG", "NG_BOX");
            if (r.Grade == "NG") r.Reason = "전압 낮음 또는 내부저항 큼";
        }
        return r;
    }

    private void Event(string level, string device, string message) => RaiseEvent(new LineEvent
    {
        EventId = Guid.NewGuid().ToString("N"),
        RunId = _runId ?? "",
        Ts = Json.Now(),
        Device = device,
        Level = level,
        Message = message,
    });
}
