using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Settings;

namespace DepalAdmin.Feature.Chat;

/// <summary>
/// 받은 채팅을 날짜별 파일(data/chat/2026-11-12.jsonl)에 남겨 "지난 대화 불러오기"에 쓴다.
/// TODO(조시현 + DB 담당): PC마다 따로 저장되는 임시 방법. 여러 PC가 같은 기록을 보려면 MySQL로.
/// </summary>
public sealed class ChatLogFile
{
    private readonly string _folder;
    private readonly object _lock = new();

    public ChatLogFile(AppSettings settings) => _folder = settings.DataPath("chat");

    public void Append(ChatMessage message)
    {
        try
        {
            var day = Json.ParseTs(message.Ts) ?? DateTime.Now;
            lock (_lock)
            {
                Directory.CreateDirectory(_folder);
                File.AppendAllText(PathFor(day), JsonSerializer.Serialize(message, Json.Options) + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[채팅 기록] 쓰기 실패: {ex.Message}");
        }
    }

    public Task<List<ChatMessage>> ReadAsync(DateTime from, DateTime to) => Task.Run(() =>
    {
        var list = new List<ChatMessage>();
        for (var day = from.Date; day < to; day = day.AddDays(1))
        {
            var path = PathFor(day);
            if (!File.Exists(path)) continue;
            string[] lines;
            lock (_lock) lines = File.ReadAllLines(path, Encoding.UTF8);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var m = JsonSerializer.Deserialize<ChatMessage>(line, Json.Options);
                    if (m != null) list.Add(m);
                }
                catch (JsonException) { }   // 깨진 줄은 건너뜀
            }
        }
        return list;
    });

    private string PathFor(DateTime day) => Path.Combine(_folder, $"{day:yyyy-MM-dd}.jsonl");
}
