using System.Windows;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Models;

namespace DepalAdmin.Comm;

/// <summary>
/// 실제·가짜 클라이언트의 공통 부분.
/// 받은 메시지를 UI 스레드로 옮겨서 이벤트를 울린다 → 페이지는 Dispatcher를 신경 쓸 필요 없음.
/// </summary>
public abstract class LineClientBase : ILineClient
{
    private IReadOnlyDictionary<string, DeviceInfo> _lastDevices = new Dictionary<string, DeviceInfo>();

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public LineStatus? LastLine { get; private set; }
    public IReadOnlyDictionary<string, DeviceInfo> LastDevices => _lastDevices;
    public DateTime? LastMessageAt { get; private set; }

    public event EventHandler<ConnectionState>? ConnectionChanged;
    public event EventHandler<LineStatus>? LineStatusReceived;
    public event EventHandler<IReadOnlyDictionary<string, DeviceInfo>>? DevicesReceived;
    public event EventHandler<LineEvent>? EventReceived;
    public event EventHandler<BatteryResult>? ResultReceived;

    public abstract Task StartAsync(CancellationToken ct = default);
    public abstract Task StopAsync();
    public abstract Task SendCommandAsync(string action, CancellationToken ct = default);

    protected static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }

    protected void SetState(ConnectionState state) => OnUi(() =>
    {
        if (State == state) return;
        State = state;
        ConnectionChanged?.Invoke(this, state);
    });

    protected void RaiseLine(LineStatus line) => OnUi(() =>
    {
        LastMessageAt = DateTime.Now;
        LastLine = line;
        LineStatusReceived?.Invoke(this, line);
    });

    protected void RaiseDevices(IReadOnlyDictionary<string, DeviceInfo> devices) => OnUi(() =>
    {
        LastMessageAt = DateTime.Now;
        _lastDevices = devices;
        DevicesReceived?.Invoke(this, devices);
    });

    protected void RaiseEvent(LineEvent ev) => OnUi(() =>
    {
        LastMessageAt = DateTime.Now;
        EventReceived?.Invoke(this, ev);
    });

    protected void RaiseResult(BatteryResult result) => OnUi(() =>
    {
        LastMessageAt = DateTime.Now;
        ResultReceived?.Invoke(this, result);
    });

    /// <summary>화면 자체에서 생긴 경고를 이벤트 로그에 남긴다 (device = "ui")</summary>
    protected void RaiseLocalEvent(string level, string message) => OnUi(() => EventReceived?.Invoke(this, new LineEvent
    {
        EventId = Guid.NewGuid().ToString("N"),
        Ts = Json.Now(),
        Device = "ui",
        Level = level,
        Message = message,
    }));
}
