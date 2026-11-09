using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Settings;

namespace DepalAdmin.Feature.Settings;

/// <summary>
/// 설정 (관리자만). 저장하면 settings.local.json에 쓴다.
/// 자동 로그아웃·알람 소리는 바로 적용, 연결 관련 값은 다시 시작해야 적용.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly IAuditLog _audit;

    [ObservableProperty] private string _stationName = "";
    [ObservableProperty] private bool _useFakeMqtt;
    [ObservableProperty] private bool _useFakeDb;
    [ObservableProperty] private string _brokerHost = "";
    [ObservableProperty] private string _brokerPort = "";
    [ObservableProperty] private string _mySqlConnection = "";
    [ObservableProperty] private string _autoLogoutMinutes = "";
    [ObservableProperty] private bool _alarmSound;
    [ObservableProperty] private string _maxRows = "";

    [ObservableProperty] private string _message = "";
    [ObservableProperty] private bool _isError;

    public string DataFolder => _settings.DataPath();
    public string Version => Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "-";

    public SettingsViewModel(AppSettings settings, IAuditLog audit)
    {
        _settings = settings;
        _audit = audit;
        Revert();
    }

    [RelayCommand]
    private void Revert()
    {
        StationName = _settings.StationName;
        UseFakeMqtt = _settings.UseFakeMqtt;
        UseFakeDb = _settings.UseFakeDb;
        BrokerHost = _settings.BrokerHost;
        BrokerPort = _settings.BrokerPort.ToString();
        MySqlConnection = _settings.MySqlConnection;
        AutoLogoutMinutes = _settings.AutoLogoutMinutes.ToString();
        AlarmSound = _settings.AlarmSound;
        MaxRows = _settings.MaxRowsInMemory.ToString();
        Message = "";
    }

    [RelayCommand]
    private void Save()
    {
        if (!int.TryParse(BrokerPort, out var port) || port is < 1 or > 65535)
        {
            Show("브로커 포트는 1~65535 사이 숫자여야 합니다.", true);
            return;
        }
        if (!int.TryParse(AutoLogoutMinutes, out var minutes) || minutes < 0)
        {
            Show("자동 로그아웃은 0 이상의 분이어야 합니다 (0 = 끔).", true);
            return;
        }
        if (!int.TryParse(MaxRows, out var rows) || rows < 50)
        {
            Show("목록 보관 줄 수는 50 이상이어야 합니다.", true);
            return;
        }

        var needsRestart =
            UseFakeMqtt != _settings.UseFakeMqtt || UseFakeDb != _settings.UseFakeDb ||
            BrokerHost != _settings.BrokerHost || port != _settings.BrokerPort ||
            MySqlConnection != _settings.MySqlConnection || rows != _settings.MaxRowsInMemory;

        _settings.StationName = StationName.Trim();
        _settings.UseFakeMqtt = UseFakeMqtt;
        _settings.UseFakeDb = UseFakeDb;
        _settings.BrokerHost = BrokerHost.Trim();
        _settings.BrokerPort = port;
        _settings.MySqlConnection = MySqlConnection.Trim();
        _settings.AutoLogoutMinutes = minutes;
        _settings.AlarmSound = AlarmSound;
        _settings.MaxRowsInMemory = rows;

        try
        {
            _settings.SaveLocal(AppContext.BaseDirectory);
            _audit.Write("설정 변경", needsRestart ? "연결 설정 포함" : "화면 설정");
            Show(needsRestart
                ? "저장했습니다. 연결·데이터 설정은 프로그램을 다시 시작해야 적용됩니다."
                : "저장했습니다. 바로 적용됩니다.", false);
        }
        catch (Exception ex)
        {
            Show($"저장하지 못했습니다: {ex.Message}", true);
        }
    }

    private void Show(string message, bool error)
    {
        Message = message;
        IsError = error;
    }
}
