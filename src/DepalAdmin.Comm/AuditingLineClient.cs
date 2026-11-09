using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Models;

namespace DepalAdmin.Comm;

/// <summary>
/// 진짜 클라이언트를 감싸서 (데코레이터 패턴)
///   1) 명령을 보내기 전에 권한을 확인하고
///   2) 누가 무슨 명령을 보냈는지 작업 기록에 남긴다.
/// 페이지 코드는 아무것도 바꿀 필요 없이 ILineClient를 그대로 쓰면 된다.
/// ⚠ 비상정지는 로그인·권한과 관계없이 항상 보낸다.
/// </summary>
public sealed class AuditingLineClient : ILineClient
{
    private readonly ILineClient _inner;
    private readonly Session _session;
    private readonly IAuditLog _audit;

    public AuditingLineClient(ILineClient inner, Session session, IAuditLog audit)
    {
        _inner = inner;
        _session = session;
        _audit = audit;
    }

    public ConnectionState State => _inner.State;
    public LineStatus? LastLine => _inner.LastLine;
    public IReadOnlyDictionary<string, DeviceInfo> LastDevices => _inner.LastDevices;
    public DateTime? LastMessageAt => _inner.LastMessageAt;

    public event EventHandler<ConnectionState>? ConnectionChanged
    {
        add => _inner.ConnectionChanged += value;
        remove => _inner.ConnectionChanged -= value;
    }

    public event EventHandler<LineStatus>? LineStatusReceived
    {
        add => _inner.LineStatusReceived += value;
        remove => _inner.LineStatusReceived -= value;
    }

    public event EventHandler<IReadOnlyDictionary<string, DeviceInfo>>? DevicesReceived
    {
        add => _inner.DevicesReceived += value;
        remove => _inner.DevicesReceived -= value;
    }

    public event EventHandler<LineEvent>? EventReceived
    {
        add => _inner.EventReceived += value;
        remove => _inner.EventReceived -= value;
    }

    public event EventHandler<BatteryResult>? ResultReceived
    {
        add => _inner.ResultReceived += value;
        remove => _inner.ResultReceived -= value;
    }

    public Task StartAsync(CancellationToken ct = default) => _inner.StartAsync(ct);
    public Task StopAsync() => _inner.StopAsync();

    public async Task SendCommandAsync(string action, CancellationToken ct = default)
    {
        var isEstop = action == UiActions.Estop;
        if (!isEstop && !_session.Can(Permission.OperateLine))
        {
            _audit.Write("명령 거부", $"{action} — 권한 없음");
            throw new UnauthorizedAccessException("라인 조작 권한이 없습니다. 작업자 이상 계정으로 로그인하세요.");
        }

        try
        {
            await _inner.SendCommandAsync(action, ct);
            _audit.Write(isEstop ? "비상정지" : "라인 명령", action);
        }
        catch (Exception ex)
        {
            _audit.Write("명령 실패", $"{action} — {ex.Message}");
            throw;
        }
    }
}
