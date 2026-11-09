using System.ComponentModel;
using System.Windows;
using DepalAdmin.Core.Auth;

namespace DepalAdmin.Core.Pages;

/// <summary>
/// 위쪽 탭에 뜨는 페이지 1개.
/// 각 담당자는 자기 프로젝트에 PageBase를 상속한 클래스만 만들면
/// 메인 창(Shell)이 알아서 탭에 붙이고 F키를 매긴다.
/// </summary>
public interface IPage
{
    /// <summary>탭에 보이는 이름 (짧게). Navigator.Navigate()에도 이 이름을 쓴다.</summary>
    string Title { get; }

    /// <summary>탭 순서 (작을수록 왼쪽). 숫자는 README의 탭 순서표를 따른다.</summary>
    int Order { get; }

    /// <summary>이 권한이 없으면 탭이 숨겨진다</summary>
    Permission Required { get; }

    /// <summary>화면에 묶인 ViewModel (탭 배지를 읽을 때 쓴다)</summary>
    object ViewModel { get; }

    /// <summary>오른쪽에 그릴 화면 (처음 열 때 한 번만 만든다)</summary>
    FrameworkElement View { get; }
}

/// <summary>
/// ViewModel이 이걸 구현하면 탭 이름 옆에 숫자 배지가 뜬다 (0이면 안 보임).
/// 예: 확인 안 된 알람 수, 안 읽은 채팅 수, 안 읽은 인수인계 메모 수.
/// </summary>
public interface IHasBadge : INotifyPropertyChanged
{
    int BadgeCount { get; }
}

/// <summary>
/// 화면(TView)과 ViewModel을 묶어 주는 기본 클래스.
/// 예: public sealed class MyPage : PageBase&lt;MyView&gt; { public MyPage(MyViewModel vm) : base(vm) { } ... }
/// </summary>
public abstract class PageBase<TView> : IPage where TView : FrameworkElement, new()
{
    private TView? _view;

    protected PageBase(object viewModel) => ViewModel = viewModel;

    public abstract string Title { get; }
    public abstract int Order { get; }
    public virtual Permission Required => Permission.ViewData;
    public object ViewModel { get; }

    public FrameworkElement View => _view ??= new TView { DataContext = ViewModel };
}
