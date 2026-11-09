namespace DepalAdmin.Core.Messaging;

/// <summary>
/// 채팅 메시지 1건. MQTT 토픽 chat/messages (Topics.ChatMessages), JSON snake_case.
/// ⚠ 통신 규약에 아직 없음 — 채팅 상대가 정해지면(관리자 여러 대 / 작업자 메인 프로그램) 규약에 추가.
/// </summary>
public sealed class ChatMessage
{
    public string MsgId { get; set; } = Guid.NewGuid().ToString("N");
    public string FromId { get; set; } = "";
    public string FromName { get; set; } = "";

    /// <summary>보낸 프로그램 (settings의 StationName). 같은 사람이 두 PC에 있을 때 구분.</summary>
    public string Station { get; set; } = "";

    public string Text { get; set; } = "";

    /// <summary>붙여 보낸 것: "alarm" / "battery" / null</summary>
    public string? RefKind { get; set; }

    /// <summary>붙여 보낸 것의 내용 (알람 문구, "회차 #번호" 등)</summary>
    public string? RefText { get; set; }

    public string Ts { get; set; } = Json.Now();
}

/// <summary>
/// 채팅 창구. 구현: 조시현(Comm)의 MqttChatClient / FakeChatClient.
/// 이벤트는 UI 스레드에서 불린다. 내가 보낸 메시지도 MessageReceived로 한 번 돌아온다.
/// </summary>
public interface IChatClient
{
    event EventHandler<ChatMessage>? MessageReceived;

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
    Task SendAsync(ChatMessage message, CancellationToken ct = default);
}
