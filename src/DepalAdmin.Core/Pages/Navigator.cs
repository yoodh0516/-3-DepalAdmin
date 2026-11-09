namespace DepalAdmin.Core.Pages;

/// <summary>
/// 탭 이동. 다른 사람 화면으로 넘어가고 싶을 때 쓴다.
///   navigator.Navigate("알람");          // 알람 탭으로
///   navigator.CurrentTitle == "채팅"      // 지금 보고 있는 탭인지
/// 메인 창(Shell)이 탭을 바꿀 때 SetCurrent를 부르고, NavigateRequested를 듣는다.
/// </summary>
public sealed class Navigator
{
    public string? CurrentTitle { get; private set; }

    /// <summary>보고 있는 탭이 바뀌었을 때 (새 탭 이름)</summary>
    public event EventHandler<string?>? CurrentChanged;

    /// <summary>Shell 전용: 누군가 Navigate를 불렀을 때</summary>
    public event EventHandler<string>? NavigateRequested;

    /// <summary>그 이름의 탭으로 이동 (권한이 없어 탭이 숨겨져 있으면 아무 일 없음)</summary>
    public void Navigate(string title) => NavigateRequested?.Invoke(this, title);

    /// <summary>Shell 전용</summary>
    public void SetCurrent(string? title)
    {
        if (CurrentTitle == title) return;
        CurrentTitle = title;
        CurrentChanged?.Invoke(this, title);
    }
}
