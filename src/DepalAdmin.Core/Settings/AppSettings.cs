using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DepalAdmin.Core.Settings;

/// <summary>
/// 실행 파일 옆의 settings.json을 읽는다.
/// settings.local.json이 있으면 그걸 대신 읽는다 (DB 비밀번호 등 개인 값, git에 안 올라감).
/// 설정 페이지에서 저장하면 settings.local.json에 쓴다.
/// </summary>
public sealed class AppSettings
{
    private const string SharedFile = "settings.json";
    private const string LocalFile = "settings.local.json";

    /// <summary>상단에 보이는 라인 이름</summary>
    public string StationName { get; set; } = "DEPAL-01";

    /// <summary>true면 브로커 없이 가짜 데이터로 실행 (화면 개발용)</summary>
    public bool UseFakeMqtt { get; set; } = true;

    /// <summary>true면 MySQL 없이 가짜 이력 데이터로 실행</summary>
    public bool UseFakeDb { get; set; } = true;

    public string BrokerHost { get; set; } = "localhost";
    public int BrokerPort { get; set; } = 1883;
    public string ClientId { get; set; } = "ui";

    /// <summary>읽기 전용 계정으로 접속 (DB 담당과 합의)</summary>
    public string MySqlConnection { get; set; } =
        "Server=localhost;Port=3306;Database=aa_battery_inspection;User ID=ui_reader;Password=CHANGE_ME;";

    /// <summary>실시간 목록(결과·이벤트·알람)에 남겨 둘 최대 줄 수</summary>
    public int MaxRowsInMemory { get; set; } = 500;

    /// <summary>아무 조작이 없으면 자동 로그아웃 (분). 0이면 끔.</summary>
    public int AutoLogoutMinutes { get; set; } = 10;

    /// <summary>ERROR 알람 때 소리</summary>
    public bool AlarmSound { get; set; } = true;

    /// <summary>계정·작업 기록을 저장할 폴더 (실행 파일 기준 상대 경로 가능)</summary>
    public string DataFolder { get; set; } = "data";

    public string DataPath(params string[] parts)
    {
        var root = Path.IsPathRooted(DataFolder) ? DataFolder : Path.Combine(AppContext.BaseDirectory, DataFolder);
        return Path.Combine(new[] { root }.Concat(parts).ToArray());
    }

    public static AppSettings Load(string folder)
    {
        var local = Path.Combine(folder, LocalFile);
        var shared = Path.Combine(folder, SharedFile);
        var path = File.Exists(local) ? local : shared;
        if (!File.Exists(path)) return new AppSettings();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), options) ?? new AppSettings();
    }

    /// <summary>지금 값을 settings.local.json에 저장</summary>
    public void SaveLocal(string folder)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        File.WriteAllText(Path.Combine(folder, LocalFile), JsonSerializer.Serialize(this, options));
    }
}
