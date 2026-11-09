using DepalAdmin.Core.Models;

namespace DepalAdmin.Core.Messaging;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Fake,   // 가짜 데이터 모드 (브로커 없이 화면 개발)
}

/// <summary>
/// 화면이 서버와 대화하는 유일한 창구.
/// ■ 각 페이지는 MQTT를 직접 쓰지 말고 이 인터페이스만 쓴다.
///   → 실제(MqttLineClient)와 가짜(FakeLineClient)를 설정 하나로 바꿀 수 있다.
/// ■ 모든 이벤트는 UI 스레드에서 불린다. 받은 값으로 바로 화면 속성을 바꿔도 된다.
/// </summary>
public interface ILineClient
{
    ConnectionState State { get; }

    /// <summary>마지막으로 받은 server/line (아직 없으면 null)</summary>
    LineStatus? LastLine { get; }

    /// <summary>마지막으로 받은 server/devices</summary>
    IReadOnlyDictionary<string, DeviceInfo> LastDevices { get; }

    /// <summary>마지막으로 아무 메시지나 받은 시각 (상태바 표시용)</summary>
    DateTime? LastMessageAt { get; }

    event EventHandler<ConnectionState>? ConnectionChanged;

    /// <summary>server/line — 라인 상태가 바뀔 때마다</summary>
    event EventHandler<LineStatus>? LineStatusReceived;

    /// <summary>server/devices — 장치 상태 전체</summary>
    event EventHandler<IReadOnlyDictionary<string, DeviceInfo>>? DevicesReceived;

    /// <summary>server/events — 이벤트 1건</summary>
    event EventHandler<LineEvent>? EventReceived;

    /// <summary>server/results — 건전지 결과 1건</summary>
    event EventHandler<BatteryResult>? ResultReceived;

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();

    /// <summary>ui/cmd 발행. action은 UiActions 상수 사용.</summary>
    Task SendCommandAsync(string action, CancellationToken ct = default);
}
