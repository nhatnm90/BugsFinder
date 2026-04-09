using System;
using System.Windows;

namespace MineSweeper.Services;

/// <summary>
/// Applies VS-Code-inspired colour themes at runtime by swapping a
/// <see cref="ResourceDictionary"/> in <see cref="Application.Resources"/>.
/// Persistence is delegated to <see cref="SettingsService"/>.
/// </summary>
public static class ThemeService
{
    /// <summary>The names of all available themes, in display order.</summary>
    public static readonly string[] AvailableThemes = ["Dark", "Light", "Blue"];

    /// <summary>The name of the theme that is currently active.</summary>
    public static string CurrentTheme { get; private set; } = "Dark";

    /// <summary>
    /// Activates <paramref name="themeName"/> by merging the matching
    /// <c>Themes/{name}Theme.xaml</c> ResourceDictionary into
    /// <see cref="Application.Resources"/> and persisting the choice via
    /// <see cref="SettingsService"/>.
    /// </summary>
    public static void Apply(string themeName)
    {
        if (Array.IndexOf(AvailableThemes, themeName) < 0)
            themeName = "Dark";

        var uri     = new Uri($"pack://application:,,,/Themes/{themeName}Theme.xaml");
        var newDict = new ResourceDictionary { Source = uri };

        var merged = Application.Current.Resources.MergedDictionaries;
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            if (merged[i].Source?.OriginalString.Contains("/Themes/") == true)
            {
                merged.RemoveAt(i);
                break;
            }
        }

        merged.Add(newDict);
        CurrentTheme = themeName;
        SettingsService.SaveTheme(themeName);
    }
}
