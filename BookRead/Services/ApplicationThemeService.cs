using System.Windows;
using System.Windows.Media;

namespace BookRead.Services;

/// <summary>
/// 管理应用级深浅主题，并统一更新界面使用的语义颜色资源。
/// </summary>
internal static class ApplicationThemeService
{
    /// <summary>获取当前是否使用浅色主题。</summary>
    internal static bool IsLightTheme { get; private set; }

    /// <summary>
    /// 在深色与浅色应用主题之间切换。
    /// </summary>
    /// <returns>切换后是否为浅色主题。</returns>
    /// <exception cref="InvalidOperationException">应用资源尚未初始化时抛出。</exception>
    internal static bool Toggle()
    {
        IsLightTheme = !IsLightTheme;
        ApplyTheme(IsLightTheme);
        return IsLightTheme;
    }

    /// <summary>
    /// 将指定明暗模式的语义颜色应用到整个应用程序。
    /// </summary>
    /// <param name="useLightTheme">使用浅色主题时为 <see langword="true"/>。</param>
    /// <returns>无。</returns>
    /// <exception cref="InvalidOperationException">应用资源尚未初始化时抛出。</exception>
    private static void ApplyTheme(bool useLightTheme)
    {
        if (Application.Current is null)
        {
            throw new InvalidOperationException("应用资源尚未初始化。");
        }

        IReadOnlyDictionary<string, Color> colors = useLightTheme
            ? CreateLightPalette()
            : CreateDarkPalette();

        // 所有页面只依赖语义资源，集中替换可保证主题切换不会遗漏局部区域。
        foreach ((string resourceKey, Color color) in colors)
        {
            Application.Current.Resources[resourceKey] = new SolidColorBrush(color);
        }
    }

    /// <summary>
    /// 创建浅色主题使用的语义颜色表。
    /// </summary>
    /// <returns>资源键与浅色主题颜色的映射。</returns>
    private static IReadOnlyDictionary<string, Color> CreateLightPalette()
    {
        return new Dictionary<string, Color>
        {
            ["AppBackground"] = Color.FromRgb(244, 245, 246),
            ["SurfaceBackground"] = Colors.White,
            ["Panel"] = Color.FromRgb(247, 248, 249),
            ["Line"] = Color.FromRgb(215, 220, 224),
            ["TextPrimary"] = Color.FromRgb(24, 29, 33),
            ["TextSecondary"] = Color.FromRgb(43, 48, 53),
            ["TextMuted"] = Color.FromRgb(101, 112, 119),
            ["ButtonForeground"] = Color.FromRgb(45, 51, 56),
            ["ButtonHover"] = Color.FromRgb(232, 235, 237),
            ["AccentSurface"] = Color.FromRgb(255, 240, 228),
            ["ProgressTrack"] = Color.FromRgb(217, 222, 226),
            ["ListSelected"] = Color.FromRgb(246, 233, 223),
            ["ListHover"] = Color.FromRgb(236, 239, 241),
            ["InputBorder"] = Color.FromRgb(203, 209, 214),
            ["DialogBackground"] = Colors.White,
            ["DangerSurface"] = Color.FromRgb(253, 235, 236)
        };
    }

    /// <summary>
    /// 创建深色主题使用的语义颜色表。
    /// </summary>
    /// <returns>资源键与深色主题颜色的映射。</returns>
    private static IReadOnlyDictionary<string, Color> CreateDarkPalette()
    {
        return new Dictionary<string, Color>
        {
            ["AppBackground"] = Color.FromRgb(18, 20, 22),
            ["SurfaceBackground"] = Color.FromRgb(23, 25, 28),
            ["Panel"] = Color.FromRgb(26, 29, 32),
            ["Line"] = Color.FromRgb(42, 46, 50),
            ["TextPrimary"] = Color.FromRgb(240, 242, 243),
            ["TextSecondary"] = Color.FromRgb(211, 208, 200),
            ["TextMuted"] = Color.FromRgb(127, 136, 143),
            ["ButtonForeground"] = Color.FromRgb(217, 222, 226),
            ["ButtonHover"] = Color.FromRgb(41, 46, 51),
            ["AccentSurface"] = Color.FromRgb(42, 37, 33),
            ["ProgressTrack"] = Color.FromRgb(48, 53, 58),
            ["ListSelected"] = Color.FromRgb(43, 42, 39),
            ["ListHover"] = Color.FromRgb(37, 43, 48),
            ["InputBorder"] = Color.FromRgb(52, 58, 64),
            ["DialogBackground"] = Color.FromRgb(27, 30, 33),
            ["DangerSurface"] = Color.FromRgb(56, 35, 38)
        };
    }
}
