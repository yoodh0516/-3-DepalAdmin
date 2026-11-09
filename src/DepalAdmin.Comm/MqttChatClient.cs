using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Settings;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace DepalAdmin.Comm;

/// <summary>
/// 채팅용 MQTT 연결 (라인 연결과 따로 둔다 → 채팅이 문제가 생겨도 라인 감시는 그대로).
/// client_id = settings.ClientId + "-chat", 토픽 chat/messages, QoS 1, retained 안 씀.
/// ⚠ 통신 규약에 아직 없음. 채팅 상대가 정해지면 규약에 추가하고 이 파일을 맞춘다.
/// </summary>
public sealed class MqttChatClient : IChatClient
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(3);

    private readonly AppSettings _settings;
    private readonly MqttFactory _factory = new();
    private readonly IMqttClient _client;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public event EventHandler<ChatMessage>? MessageReceived;

    public MqttChatClient(AppSettings settings)
    {
        _settings = settings;
        _client = _factory.CreateMqttClient();
        _client.ApplicationMessageReceivedAsync += e =>
        {
            var seg = e.ApplicationMessage.PayloadSegment;
            if (seg.Array == null || seg.Count == 0) return Task.CompletedTask;
            try
            {
                var msg = JsonSerializer.Deserialize<ChatMessage>(Encoding.UTF8.GetString(seg.Array, seg.Offset, seg.Count), Json.Options);
                if (msg != null) OnUi(() => MessageReceived?.Invoke(this, msg));
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[채팅] 형식 오류: {ex.Message}");
            }
            return Task.CompletedTask;
        };
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loop = Task.Run(() => KeepConnectedAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_client.IsConnected)
        {
            try { await _client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), CancellationToken.None); }
            catch (Exception ex) { Debug.WriteLine($"[채팅] 종료 중 오류: {ex.Message}"); }
        }
        if (_loop != null)
        {
            try { await _loop; } catch (OperationCanceledException) { }
        }
    }

    public async Task SendAsync(ChatMessage message, CancellationToken ct = default)
    {
        if (!_client.IsConnected)
            throw new InvalidOperationException("브로커에 연결되어 있지 않아 메시지를 보내지 못했습니다.");

        var payload = JsonSerializer.Serialize(message, Json.Options);
        await _client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(Topics.ChatMessages)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), ct);
    }

    private async Task KeepConnectedAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!_client.IsConnected)
            {
                try
                {
                    await _client.ConnectAsync(new MqttClientOptionsBuilder()
                        .WithTcpServer(_settings.BrokerHost, _settings.BrokerPort)
                        .WithClientId(_settings.ClientId + "-chat")
                        .WithCleanSession(true)
                        .Build(), ct);
                    await _client.SubscribeAsync(_factory.CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(f => f.WithTopic(Topics.ChatMessages).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                        .Build(), ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[채팅] 연결 실패: {ex.Message}");
                }
            }

            try { await Task.Delay(ReconnectDelay, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private static void OnUi(Action action)
    {
        var d = Application.Current?.Dispatcher;
        if (d == null || d.CheckAccess()) action();
        else d.BeginInvoke(action);
    }
}
