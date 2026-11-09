namespace DepalAdmin.Core.Models;

/// <summary>server/events — 장치·라인 이벤트. 메시지_형식 v1.2 2장.</summary>
public sealed class LineEvent
{
    public string EventId { get; set; } = "";
    public string RunId { get; set; } = "";
    public string Ts { get; set; } = "";

    /// <summary>server / controller / pi / yolo / agv (화면 자체 경고는 ui)</summary>
    public string Device { get; set; } = "";

    /// <summary>INFO / WARN / ERROR</summary>
    public string Level { get; set; } = "INFO";

    public string Message { get; set; } = "";
}
