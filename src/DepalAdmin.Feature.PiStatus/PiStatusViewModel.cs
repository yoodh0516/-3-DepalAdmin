using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Models;

namespace DepalAdmin.Feature.PiStatus;

/// <summary>
/// 라즈베리파이 세부: 집게 · 로봇팔 · 아두이노 연결.
/// 받는 것: server/devices 안의 "pi" 칸 (hb/pi에 실어 보낸 값, 통신 규약 3-1)
///   gripper: normal / weak / broken   arm: ok / stopped   arduino: ok / offline
/// TODO(노원종): 라파 코드에서 실제로 보내는 값과 맞추고, 설명 문구를 현장에서 할 일로 다듬기
/// </summary>
public partial class PiStatusViewModel : ObservableObject
{
    private const string PiName = "pi";

    public ObservableCollection<PiItem> Items { get; } = new()
    {
        new PiItem("gripper", "집게"),
        new PiItem("arm", "로봇팔"),
        new PiItem("arduino", "아두이노 (컨베이어·측정)"),
    };

    [ObservableProperty] private string _piState = "수신 대기";
    [ObservableProperty] private string _piStateKey = "neutral";

    public PiStatusViewModel(ILineClient client)
    {
        client.DevicesReceived += (_, devices) => Apply(devices);
        if (client.LastDevices.Count > 0) Apply(client.LastDevices);
    }

    private void Apply(IReadOnlyDictionary<string, DeviceInfo> devices)
    {
        if (!devices.TryGetValue(PiName, out var pi))
        {
            PiState = "라파 정보 없음";
            PiStateKey = "warn";
            return;
        }

        PiState = Core.Labels.DeviceState(pi.State);
        PiStateKey = pi.State;
        foreach (var item in Items)
            item.Update(pi.Extra.TryGetValue(item.Key, out var v) ? v : null);
    }
}

public partial class PiItem : ObservableObject
{
    public PiItem(string key, string name)
    {
        Key = key;
        Name = name;
    }

    public string Key { get; }
    public string Name { get; }

    [ObservableProperty] private string _value = "-";
    [ObservableProperty] private string _valueText = "-";
    [ObservableProperty] private string _help = "";

    public void Update(string? value)
    {
        Value = value ?? "unknown";
        (ValueText, Help) = (Key, value) switch
        {
            ("gripper", "normal") => ("정상", ""),
            ("gripper", "weak") => ("약함", "집게 힘이 약해 전자석을 함께 씁니다. 라인은 계속 돕니다."),
            ("gripper", "broken") => ("고장", "전자석만으로 집습니다. 쉬는 시간에 집게 서보를 점검하세요."),
            ("arm", "ok") => ("정상", ""),
            ("arm", "stopped") => ("멈춤", "비상정지 중이거나 팔이 멈췄습니다. 비상정지 해제 후 원점 복귀를 확인하세요."),
            ("arduino", "ok") => ("연결됨", ""),
            ("arduino", "offline") => ("끊김", "컨베이어·전압 측정이 안 됩니다. 라파와 아두이노 USB 선을 확인하세요."),
            (_, null) => ("값 없음", "라파 하트비트에 이 값이 없습니다."),
            _ => (value!, ""),
        };
    }
}
