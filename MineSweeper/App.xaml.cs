using System.Windows;
using BugsFinder.Services;

namespace BugsFinder;

/// <summary>Application entry point — loads settings then applies the saved theme.</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SettingsService.Load();
        ThemeService.Apply(SettingsService.Theme);
    }
}
