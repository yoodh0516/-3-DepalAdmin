using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DepalAdmin.Core.Messaging;

/// <summary>JSON 설정과 시각 형식을 한곳에 모아 둔다.</summary>
public static class Json
{
    public const string TimeFormat = "yyyy-MM-dd HH:mm:ss.fff";

    /// <summary>서버 메시지용: C# 이름 RunId ↔ JSON 이름 run_id</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // 한글을 \uXXXX로 바꾸지 않음
    };

    /// <summary>화면에 보여 줄 때 (들여쓰기)</summary>
    public static readonly JsonSerializerOptions Pretty = new(Options) { WriteIndented = true };

    /// <summary>규약 형식의 현재 시각: 2026-11-12 09:31:00.120</summary>
    public static string Now() => DateTime.Now.ToString(TimeFormat, CultureInfo.InvariantCulture);

    public static DateTime? ParseTs(string? text) =>
        DateTime.TryParseExact(text, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? t
            : null;
}
