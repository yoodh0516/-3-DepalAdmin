using System.Collections.ObjectModel;
using System.Media;
using DepalAdmin.Core.Alarms;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Settings;

namespace DepalAdmin.Feature.Alarms;

/// <summary>
/// WARN·ERROR 이벤트와 브로커 끊김을 알람으로 만든다.
///   - 같은 장치·같은 내용이 MergeWindow 안에 또 오면 새 줄 대신 횟수만 올린다 (알람 홍수 방지)
///   - 확인(Ack)은 작업자 이상만, 누가 언제 확인했는지 남긴다
///   - 고장(ERROR)이면 소리 (설정에서 끌 수 있음)
/// </summary>
public sealed class AlarmService : IAlarmService
{
    // ── 수정할 값 ──────────────────────────────────────────
    private static readonly TimeSpan MergeWindow = TimeSpan.FromSeconds(30);
    // ──────────────────────────────────────────────────────

    private readonly Session _session;
    private readonly IAuditLog _audit;
    private readonly AppSettings _settings;
    private bool _wasConnected;

    public ObservableCollection<Alarm> Alarms { get; } = new();
    public event EventHandler? Changed;

    public AlarmService(ILineClient client, Session session, IAuditLog audit, AppSettings settings)
    {
        _session = session;
        _audit = audit;
        _settings = settings;

        client.EventReceived += (_, ev) =>
        {
            if (ev.Level is "WARN" or "ERROR") Raise(ev.Device, ev.Level, ev.Message);
        };

        client.ConnectionChanged += (_, state) =>
        {
            if (state == ConnectionState.Connected)
            {
                _wasConnected = true;
            }
            else if (state == ConnectionState.Disconnected && _wasConnected)
            {
                Raise("ui", "ERROR", "브로커 연결이 끊겼습니다. 서버 PC와 Mosquitto를 확인하세요.");
            }
        };
    }

    public int UnackedCount => Alarms.Count(a => !a.Acked);

    public Alarm? MostUrgent =>
        Alarms.FirstOrDefault(a => !a.Acked && a.Level == "ERROR") ??
        Alarms.FirstOrDefault(a => !a.Acked);

    public void Raise(string device, string level, string message)
    {
        var now = DateTime.Now;
        var same = Alarms.FirstOrDefault(a =>
            !a.Acked && a.Device == device && a.Message == message && now - a.LastAt < MergeWindow);

        if (same != null)
        {
            same.Count++;
            same.LastAt = now;
        }
        else
        {
            Alarms.Insert(0, new Alarm { FirstAt = now, LastAt = now, Device = device, Level = level, Message = message });
            while (Alarms.Count > _settings.MaxRowsInMemory) Alarms.RemoveAt(Alarms.Count - 1);
        }

        if (level == "ERROR" && _settings.AlarmSound) SystemSounds.Hand.Play();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Ack(Alarm alarm)
    {
        if (alarm.Acked) return true;
        if (!_session.Can(Permission.AckAlarm)) return false;

        alarm.Acked = true;
        alarm.AckedBy = _session.UserName;
        alarm.AckedAt = DateTime.Now;
        _audit.Write("알람 확인", $"[{alarm.Level}] {alarm.Device}: {alarm.Message}");
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public int AckAll()
    {
        if (!_session.Can(Permission.AckAlarm)) return 0;

        var targets = Alarms.Where(a => !a.Acked).ToList();
        var now = DateTime.Now;
        foreach (var a in targets)
        {
            a.Acked = true;
            a.AckedBy = _session.UserName;
            a.AckedAt = now;
        }
        if (targets.Count > 0)
        {
            _audit.Write("알람 확인", $"한꺼번에 {targets.Count}건");
            Changed?.Invoke(this, EventArgs.Empty);
        }
        return targets.Count;
    }
}
