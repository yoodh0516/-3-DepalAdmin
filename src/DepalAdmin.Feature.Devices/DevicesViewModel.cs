using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using DepalAdmin.Core;
using DepalAdmin.Core.Messaging;
using DepalAdmin.Core.Models;
using DepalAdmin.Core.Pages;

namespace DepalAdmin.Feature.Devices;

/// <summary>
/// 장치별 하트비트 상태 카드 (정상·지연·끊김, 바뀐 시각).
/// 받는 것: server/devices
/// 오른쪽 라파 세부는 노원종 패널을 받아서 끼운다 (세부 값은 그쪽 담당).
/// </summary>
public partial class DevicesViewModel : ObservableObject, IHasBadge
{
    public ObservableCollection<DeviceCard> Devices { get; } = new();

    /// <summary>노원종의 라파 세부 패널 (아직 없으면 null)</summary>
    public FrameworkElement? PiPanel { get; }

    [ObservableProperty] private string _updatedText = "아직 받은 장치 상태가 없습니다.";
    [ObservableProperty] private int _badgeCount;   // 이상 장치 수

    public DevicesViewModel(ILineClient client, IEnumerable<IPanel> panels)
    {
        PiPanel = PanelSlots.Find(panels, PanelSlots.DevicesPi);

        client.DevicesReceived += (_, devices) => Apply(devices);
        if (client.LastDevices.Count > 0) Apply(client.LastDevices);
    }

    private void Apply(IReadOnlyDictionary<string, DeviceInfo> devices)
    {
        foreach (var info in devices.Values)
        {
            var card = Devices.FirstOrDefault(c => c.Name == info.Name);
            if (card == null)
            {
                card = new DeviceCard(info.Name);
                Devices.Add(card);
            }
            card.Update(info);
        }
        BadgeCount = Devices.Count(d => !d.IsNormal);
        UpdatedText = $"마지막 수신 {DateTime.Now:HH:mm:ss}";
    }
}

public partial class DeviceCard : ObservableObject
{
    public DeviceCard(string name)
    {
        Name = name;
        DisplayName = Labels.Device(name);
    }

    public string Name { get; }
    public string DisplayName { get; }

    [ObservableProperty] private string _state = "unknown";
    [ObservableProperty] private string _stateText = "-";
    [ObservableProperty] private bool _isNormal = true;
    [ObservableProperty] private string _changedAt = "";

    public void Update(DeviceInfo info)
    {
        if (State != info.State) ChangedAt = $"{DateTime.Now:HH:mm:ss}부터";
        State = info.State;
        StateText = Labels.DeviceState(info.State);
        IsNormal = StateText == "정상";
    }
}
