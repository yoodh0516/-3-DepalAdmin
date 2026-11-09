using System.Windows;

namespace DepalAdmin.Core.Pages;

/// <summary>
/// 다른 사람 화면 안에 끼워 넣는 작은 화면 조각.
/// 예: 유동호의 조작 버튼 패널이 김초아의 라인 현황 화면 오른쪽에 붙는다.
/// → 한 화면에 보이지만 프로젝트가 따로라서 합칠 때 충돌이 안 난다.
///
/// 끼우는 쪽: services.AddSingleton&lt;IPanel, MyPanel&gt;();
/// 받는 쪽:   생성자에서 IEnumerable&lt;IPanel&gt;을 받아 PanelSlots.Find(panels, 자리)로 고른다.
/// </summary>
public interface IPanel
{
    /// <summary>어느 자리에 들어갈지 (PanelSlots 상수)</summary>
    string Slot { get; }

    FrameworkElement View { get; }
}

/// <summary>
/// 패널 자리 이름. 새 자리가 필요하면 공통 틀 담당(조시현)에게 요청.
/// "자리 주인"은 화면을 가진 사람, "패널 주인"은 끼우는 사람.
/// </summary>
public static class PanelSlots
{
    /// <summary>라인 현황 오른쪽 조작 버튼 — 자리: 김초아(Process) / 패널: 유동호(Operation)</summary>
    public const string LineControls = "line.controls";

    /// <summary>라인 현황 가운데 팔레트 2×5 맵 — 자리: 김초아(Process) / 패널: 김하균(Results)</summary>
    public const string LinePallet = "line.pallet";

    /// <summary>장치 상태 오른쪽 라즈베리파이 세부 — 자리: 조시현(Devices) / 패널: 노원종(PiStatus)</summary>
    public const string DevicesPi = "devices.pi";

    /// <summary>메인 창 맨 아래 알람 줄 — 자리: 조시현(Shell) / 패널: 노원종(Alarms)</summary>
    public const string ShellAlarmBar = "shell.alarmbar";

    /// <summary>자리에 맞는 패널 화면. 아직 아무도 안 끼웠으면 null (그 자리는 비어 보인다).</summary>
    public static FrameworkElement? Find(IEnumerable<IPanel> panels, string slot) =>
        panels.FirstOrDefault(p => p.Slot == slot)?.View;
}

public abstract class PanelBase<TView> : IPanel where TView : FrameworkElement, new()
{
    private readonly object _viewModel;
    private TView? _view;

    protected PanelBase(object viewModel) => _viewModel = viewModel;

    public abstract string Slot { get; }

    public FrameworkElement View => _view ??= new TView { DataContext = _viewModel };
}
