using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using DepalAdmin.Core;

namespace DepalAdmin.Feature.Alarms;

public partial class AlarmsView : UserControl
{
    public AlarmsView()
    {
        InitializeComponent();
    }
}

/// <summary>ERROR → 고장, WARN → 주의</summary>
public sealed class LevelText : IValueConverter
{
    public static readonly LevelText Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c) => Labels.Level(value as string);
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>pi → 라즈베리파이</summary>
public sealed class DeviceText : IValueConverter
{
    public static readonly DeviceText Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c) => Labels.Device(value as string);
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}
