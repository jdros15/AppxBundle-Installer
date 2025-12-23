using System.Windows;
using System.Windows.Controls;
using AppxBundleInstaller.ViewModels;

namespace AppxBundleInstaller.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }
    
    private MainViewModel? MainVm => Window.GetWindow(this)?.DataContext as MainViewModel;
    
    private void CriticalAppsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (MainVm == null) return;
        
        // Only show warning when enabling (turning on)
        if (CriticalAppsToggle.IsOn)
        {
            var result = MessageBox.Show(
                "⚠️ WARNING: You are about to enable showing critical system apps.\n\n" +
                "These apps include:\n" +
                "• Start Menu\n" +
                "• Windows Settings\n" +
                "• Shell Experience (Taskbar)\n" +
                "• Microsoft Store\n" +
                "• Windows Security\n" +
                "• And other essential Windows components\n\n" +
                "Uninstalling ANY of these apps can cause SERIOUS SYSTEM PROBLEMS that may require a complete Windows reset to fix.\n\n" +
                "Do you understand the risks and want to proceed?",
                "⚠️ Critical Apps Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result != MessageBoxResult.Yes)
            {
                // Revert the toggle without triggering the event again
                CriticalAppsToggle.Toggled -= CriticalAppsToggle_Toggled;
                CriticalAppsToggle.IsOn = false;
                MainVm.ShowCriticalApps = false;
                CriticalAppsToggle.Toggled += CriticalAppsToggle_Toggled;
            }
        }
    }
}
