using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DepalAdmin.Core.Converters;

/// <summary>
/// 화면 색 규칙 (ISA-101 고성능 HMI 방식):
///   정상은 회색·검정 글자. 색은 '이상'일 때만 쓴다 (주의 = 호박색, 고장 = 빨강).
///   그래서 정상 상태 값은 대부분 Brush.Text(검정 글자)로 돌아간다.
/// </summary>
internal static class StatusKeys
{
    public static string Classify(object? value) => (value?.ToString() ?? "").ToLowerInvariant() switch
    {
        "delayed" or "delay" or "warn" or "weak" or "paused" or "manual" or "connecting" or "fake" => "warn",
        "lost" or "offline" or "error" or "broken" or "stopped" or "estop" or "disconnected" or "alarm" => "alarm",
        "ok" or "online" or "normal" or "connected" or "running" => "ok",
        _ => "neutral",
    };

    public static object Brush(string key) => Application.Current?.TryFindResource(key) ?? Brushes.Gray;
}

/// <summary>상태 글자 → 글자색. {Binding State, Converter={StaticResource StatusToBrush}}</summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        StatusKeys.Brush(StatusKeys.Classify(value) switch
        {
            "warn" => "Brush.Warn",
            "alarm" => "Brush.Alarm",
            "ok" => "Brush.Ok",
            _ => "Brush.Text",
        });

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>상태 글자 → 배경색 (이상일 때만 칠한다). {Binding State, Converter={StaticResource StatusToFill}}</summary>
public sealed class StatusToFillConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        StatusKeys.Brush(StatusKeys.Classify(value) switch
        {
            "warn" => "Brush.WarnFill",
            "alarm" => "Brush.AlarmFill",
            _ => "Brush.Panel",
        });

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>등급(A/B/NG/RESORT) → 색. {Binding Grade, Converter={StaticResource GradeToBrush}}</summary>
public sealed class GradeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        StatusKeys.Brush(value?.ToString() switch
        {
            "A" => "Brush.GradeA",
            "B" => "Brush.GradeB",
            "NG" => "Brush.GradeNG",
            "RESORT" => "Brush.GradeResort",
            _ => "Brush.Empty",
        });

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>글자가 있으면 보임, 비었으면 숨김</summary>
public sealed class TextToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>true → 숨김, false → 보임</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
