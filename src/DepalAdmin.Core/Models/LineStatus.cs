namespace DepalAdmin.Core.Models;

/// <summary>
/// server/line (retained) — 라인 현재 상태. 통신 규약 v0.9 5장.
/// JSON 이름은 snake_case로 자동 변환된다 (RunId ↔ run_id).
/// </summary>
public sealed class LineStatus
{
    public string? RunId { get; set; }

    /// <summary>idle / running / paused / estop / manual</summary>
    public string Mode { get; set; } = "idle";

    /// <summary>scan / pick / inspect / measure / home / sort / wait_agv / wait_operator</summary>
    public string? Step { get; set; }

    public int? BatteryId { get; set; }

    /// <summary>키: "A", "B", "NG", "RESORT"</summary>
    public Dictionary<string, int> Counts { get; set; } = new();

    public AgvInfo? Agv { get; set; }

    public string? Message { get; set; }

    public string? Ts { get; set; }

    public int Count(string grade) => Counts.TryGetValue(grade, out var n) ? n : 0;
}

public sealed class AgvInfo
{
    public int Loaded { get; set; }
    public int Batch { get; set; }

    /// <summary>home / going / away / returning</summary>
    public string State { get; set; } = "home";
}
