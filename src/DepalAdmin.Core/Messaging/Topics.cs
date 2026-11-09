namespace DepalAdmin.Core.Messaging;

/// <summary>MQTT 토픽 이름. 문자열을 직접 쓰지 말고 이 상수를 쓴다.</summary>
public static class Topics
{
    public const string ServerLine = "server/line";
    public const string ServerDevices = "server/devices";
    public const string ServerEvents = "server/events";
    public const string ServerResults = "server/results";

    public const string UiCmd = "ui/cmd";
    public const string UiStatus = "status/ui";

    /// <summary>⚠ 통신 규약에 아직 없음 (채팅 상대 확정 후 추가)</summary>
    public const string ChatMessages = "chat/messages";
}

/// <summary>ui/cmd의 action 값. 통신 규약 v0.9 5장.</summary>
public static class UiActions
{
    public const string Start = "start";
    public const string Pause = "pause";
    public const string Resume = "resume";
    public const string Stop = "stop";
    public const string Estop = "estop";
    public const string Reset = "reset";
    public const string Skip = "skip";
    public const string ManualLoaded = "manual_loaded";   // ⚠ 팀 결정 전
}
