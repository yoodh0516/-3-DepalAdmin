using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Settings;

namespace DepalAdmin.Feature.Auth;

/// <summary>
/// 작업 기록을 날짜별 파일(data/audit/2026-11-12.jsonl)에 한 줄씩 덧붙인다.
/// 한 줄 = JSON 하나라서 파일 일부가 깨져도 나머지 줄은 읽힌다.
/// </summary>
public sealed class FileAuditLog : IAuditLog
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _folder;
    private readonly Session _session;
    private readonly object _lock = new();

    public FileAuditLog(AppSettings settings, Session session)
    {
        _folder = settings.DataPath("audit");
        _session = session;
    }

    public void Write(string category, string message)
    {
        try
        {
            var entry = new AuditEntry
            {
                Time = DateTime.Now,
                UserId = _session.User?.Id ?? "-",
                UserName = _session.User?.Name ?? "-",
                Category = category,
                Message = message,
            };
            var line = JsonSerializer.Serialize(entry, Options) + Environment.NewLine;
            lock (_lock)
            {
                Directory.CreateDirectory(_folder);
                File.AppendAllText(FileFor(entry.Time), line, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[작업 기록] 쓰기 실패: {ex.Message}");   // 기록 실패로 동작을 막지 않는다
        }
    }

    public Task<IReadOnlyList<AuditEntry>> ReadAsync(DateTime from, DateTime to, CancellationToken ct = default) =>
        Task.Run<IReadOnlyList<AuditEntry>>(() =>
        {
            var list = new List<AuditEntry>();
            for (var day = from.Date; day < to; day = day.AddDays(1))
            {
                ct.ThrowIfCancellationRequested();
                var path = FileFor(day);
                if (!File.Exists(path)) continue;

                string[] lines;
                lock (_lock) lines = File.ReadAllLines(path, Encoding.UTF8);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var entry = JsonSerializer.Deserialize<AuditEntry>(line, Options);
                        if (entry != null && entry.Time >= from && entry.Time < to) list.Add(entry);
                    }
                    catch (JsonException)
                    {
                        // 깨진 줄은 건너뜀
                    }
                }
            }
            return list.OrderByDescending(e => e.Time).ToList();
        }, ct);

    private string FileFor(DateTime day) => Path.Combine(_folder, $"{day:yyyy-MM-dd}.jsonl");
}
