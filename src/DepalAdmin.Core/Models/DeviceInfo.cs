namespace DepalAdmin.Core.Models;

/// <summary>
/// server/devices 안의 장치 1개.
/// TODO(조시현): central_server.py가 실제로 보내는 형식에 맞춰 State 값 목록 확정.
/// 지금은 "state" 또는 "status" 칸을 State로, 나머지 칸은 전부 Extra로 받는다.
/// </summary>
public sealed class DeviceInfo
{
    /// <summary>server / controller / pi / yolo / agv / db_saver</summary>
    public string Name { get; set; } = "";

    /// <summary>예: ok / delayed / lost / offline</summary>
    public string State { get; set; } = "unknown";

    /// <summary>나머지 값 (pi의 gripper, arm, arduino 등)</summary>
    public Dictionary<string, string> Extra { get; set; } = new();
}
