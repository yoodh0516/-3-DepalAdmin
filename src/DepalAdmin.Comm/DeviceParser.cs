using System.Text.Json;
using DepalAdmin.Core.Models;

namespace DepalAdmin.Comm;

/// <summary>
/// server/devices 해석.
/// TODO(조시현): central_server.py의 실제 형식이 정해지면 이 파일만 고친다.
/// 지금은 아래 모양을 모두 받아들인다.
///   {"pi": {"state": "ok", "gripper": "normal"}, "yolo": "ok", ...}
///   {"devices": {...}}   또는   {"devices": [{"name": "pi", "state": "ok"}, ...]}
/// </summary>
internal static class DeviceParser
{
    private static readonly HashSet<string> SkipKeys = new() { "ts", "run_id" };

    public static Dictionary<string, DeviceInfo> Parse(string json)
    {
        var result = new Dictionary<string, DeviceInfo>();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("devices", out var inner))
            root = inner;

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in root.EnumerateObject())
            {
                if (SkipKeys.Contains(p.Name)) continue;
                Add(result, p.Name, p.Value);
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                var name = ReadString(item, "name") ?? ReadString(item, "device") ?? "?";
                Add(result, name, item);
            }
        }
        return result;
    }

    private static void Add(Dictionary<string, DeviceInfo> target, string name, JsonElement value)
    {
        var info = new DeviceInfo { Name = name };

        if (value.ValueKind == JsonValueKind.String)
        {
            info.State = value.GetString() ?? "unknown";
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in value.EnumerateObject())
            {
                var text = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.GetRawText();
                if (p.NameEquals("state") || p.NameEquals("status")) info.State = text;
                else if (!p.NameEquals("name") && !p.NameEquals("device")) info.Extra[p.Name] = text;
            }
        }
        target[name] = info;
    }

    private static string? ReadString(JsonElement item, string key) =>
        item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty(key, out var v)
        && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
