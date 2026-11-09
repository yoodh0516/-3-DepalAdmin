using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DepalAdmin.Feature.Handover;

public partial class HandoverView : UserControl
{
    public HandoverView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 다른 인수인계 행(DataGridRow)을 클릭한 경우를 제외하고
    /// 화면의 다른 곳을 클릭하면 선택을 해제해 즉시 인수인계 작성 모드로 돌아간다.
    /// </summary>
    private void OnRootPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not HandoverViewModel { Selected: not null } vm)
            return;

        if (e.OriginalSource is not DependencyObject source)
            return;

        // 1) 다른 인수인계 행(DataGridRow)을 클릭한 경우는 그 행 선택이 유지되도록 통과
        if (FindParent<DataGridRow>(source) != null)
            return;

        // 2) 우측 상단 [읽음] 버튼이나 스크롤바 조작 시에는 먼저 해당 동작이 수행되도록 통과
        if (FindParent<ButtonBase>(source) != null || FindParent<ScrollBar>(source) != null)
            return;

        vm.Selected = null;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T matched)
                return matched;
            child = child is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(child)
                : LogicalTreeHelper.GetParent(child);
        }
        return null;
    }
}
