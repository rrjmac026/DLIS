namespace LLOIS.Views;

using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LLOIS.Services;

//Main View Updates file
public partial class MainView
{
    private System.Windows.Threading.DispatcherTimer? _updateCheckTimer;
    private bool _updatePromptShown;

    private void StartUpdateChecks()
    {
        CheckForUpdateAndUpdateBadge();

        _updateCheckTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(15)
        };
        _updateCheckTimer.Tick += (s, args) => CheckForUpdateAndUpdateBadge();
        _updateCheckTimer.Start();
    }

   private async void CheckForUpdateAndUpdateBadge()
    {
        try
        {
            var update = await UpdateService.CheckForUpdateAsync();

            SettingsBtnRef.ApplyTemplate();
            var settingsBadge = SettingsBtnRef.Template.FindName("UpdateBadge", SettingsBtnRef) as Border;
            if (settingsBadge is not null)
                settingsBadge.Visibility = update is not null ? Visibility.Visible : Visibility.Collapsed;

            // Popup only once per app session, so the 15-minute timer doesn't nag
            if (update is not null && !_updatePromptShown)
            {
                _updatePromptShown = true;
                await PromptAndApplyUpdateAsync(update);
            }
        }
        catch
        {
            // Silent — offline is fine, just skip this check cycle
        }
    }

    /// <summary>One-time confirmation shown if we just relaunched from an update.</summary>
    private void ShowUpdateCompleteNotice()
    {
        var previousVersion = UpdateService.ConsumeUpdateMarker();
        if (previousVersion is null) return;

        MessageBox.Show(
            $"DLIS was successfully updated to version {App.CurrentVersion}.",
            "Update Complete",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async Task PromptAndApplyUpdateAsync(UpdateService.UpdateInfo update)
    {
        var result = MessageBox.Show(
            $"A new version ({update.Version}) is available. Update now?\n\nThe app will restart.",
            "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var progressWindow = new UpdateProgressWindow { Owner = Window.GetWindow(this) };
            progressWindow.Show();

            var progress = new Progress<double>(percent => progressWindow.SetProgress(percent));
            var path = await UpdateService.DownloadUpdateAsync(update, progress);

            progressWindow.SetStatus("Restarting...");
            await Task.Delay(500);

            UpdateService.ApplyUpdateAndRestart(path);
        }
        catch (Exception ex)
        {
            if (!ConnectionFailureHandler.HandleIfApiFailure(ex))
                MessageBox.Show($"Update failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var update = await UpdateService.CheckForUpdateAsync();
            if (update is null)
            {
                MessageBox.Show($"You're on the latest version ({App.CurrentVersion}).",
                    "No Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await PromptAndApplyUpdateAsync(update);
        }
        catch (Exception ex)
        {
            if (!ConnectionFailureHandler.HandleIfApiFailure(ex))
                MessageBox.Show($"Update check failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}