using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BookRead.Pages;

/// <summary>
/// 在空字符串时折叠元素、非空字符串时显示元素的转换器。
/// </summary>
internal sealed class StringToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 将字符串转换为可见状态。
    /// </summary>
    /// <param name="value">要判断的字符串。</param>
    /// <param name="targetType">绑定目标类型。</param>
    /// <param name="parameter">未使用。</param>
    /// <param name="culture">当前区域信息。</param>
    /// <returns>字符串非空时返回 <see cref="Visibility.Visible"/>，否则返回 <see cref="Visibility.Collapsed"/>。</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrWhiteSpace(value as string)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>
    /// 本转换器不支持反向转换。
    /// </summary>
    /// <param name="value">要转换的值。</param>
    /// <param name="targetType">绑定目标类型。</param>
    /// <param name="parameter">未使用。</param>
    /// <param name="culture">当前区域信息。</param>
    /// <returns>始终返回 <see cref="Binding.DoNothing"/>。</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
