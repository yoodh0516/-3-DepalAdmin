using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace DepalAdmin.Shell;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = viewModel;

        // 자동 로그아웃: 키·마우스 입력이 있으면 시간을 다시 잰다
        PreviewMouseDown += (_, _) => _vm.NoteActivity();
        PreviewMouseMove += (_, _) => _vm.NoteActivity();
        PreviewKeyDown += OnKeyDown;

        // 메인 창을 닫으면 프로그램 끝 (App.xaml의 ShutdownMode가 OnExplicitShutdown이므로)
        Closed += (_, _) => Application.Current.Shutdown();
    }

    /// <summary>F1~F12로 탭 이동 (F10은 윈도우가 System 키로 보낸다)</summary>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        _vm.NoteActivity();
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key >= Key.F1 && key <= Key.F12)
        {
            _vm.SelectTab(key - Key.F1);
            e.Handled = true;
        }
    }
}

/// <summary>bool 뒤집기 (잠겨 있으면 탭 비활성)</summary>
public sealed class Not : IValueConverter
{
    public static readonly Not Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c) => value is not true;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is not true;
}
