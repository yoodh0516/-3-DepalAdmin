using System.Windows;

namespace DepalAdmin.Feature.Auth.Login;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;

    public LoginWindow(LoginViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _vm.LoggedIn += (_, _) => DialogResult = true;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LoginViewModel.IsChangeMode) && _vm.IsChangeMode)
                NewPasswordBox.Focus();
        };
        Loaded += (_, _) => IdBox.Focus();
    }

    private void OnLogin(object sender, RoutedEventArgs e)
    {
        _vm.TryLogin(PasswordBox.Password);
        PasswordBox.Clear();
        if (!_vm.IsChangeMode) PasswordBox.Focus();
    }

    private void OnChangePassword(object sender, RoutedEventArgs e)
    {
        _vm.TryChangePassword(NewPasswordBox.Password, ConfirmBox.Password);
        NewPasswordBox.Clear();
        ConfirmBox.Clear();
        NewPasswordBox.Focus();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
