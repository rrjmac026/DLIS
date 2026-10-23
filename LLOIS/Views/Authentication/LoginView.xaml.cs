namespace LLOIS.Views;

using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LLOIS.Services;

public partial class LoginView : UserControl
{
    private readonly ApiClient _api;
    private readonly IAuthService _auth;

    public event Action<ApiUser, ApiClient>? LoginSucceeded;

    public LoginView(ApiClient api)
    {
        InitializeComponent();
        _api = api;
        _auth = new AuthService(api);

        Loaded += async (_, _) => await WakeServerAsync();
    }

    // Fires a quiet request so a sleeping Render instance starts waking up
    // while the user types their credentials.
    private async Task WakeServerAsync()
    {
        SetStatus("#FBBF24", "Connecting to server…");

        var ok = await _api.PingAsync();

        if (ok) SetStatus("#34D399", "System online — Secure portal");
        else    SetStatus("#F87171", "Server unreachable — check your connection");
    }

    private void SetStatus(string colorHex, string text)
    {
        StatusDot.Fill = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex)!;
        StatusText.Text = text;
    }

    private async void TryLogin()
    {
        string username = UsernameBox.Text.Trim();
        string password = PasswordBox.Password;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ErrorText.Text = "Please fill in all fields.";
            ErrorBanner.Visibility = Visibility.Visible;
            return;
        }

        LoginButton.IsEnabled = false;
        ErrorBanner.Visibility = Visibility.Collapsed;

        try
        {
            var user = await _auth.LoginAsync(username, password, rememberMe: true);
            LoginButton.IsEnabled = true;

            LoginSucceeded?.Invoke(user, _api);
        }
        catch (UnauthorizedAccessException)
        {
            LoginButton.IsEnabled = true;
            ErrorText.Text = "Invalid username or password.";
            ErrorBanner.Visibility = Visibility.Visible;
        }
        catch (InvalidOperationException ex)
        {
            LoginButton.IsEnabled = true;
            ErrorText.Text = ex.Message;
            ErrorBanner.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            LoginButton.IsEnabled = true;
            if (ConnectionFailureHandler.HandleIfApiFailure(ex))
                return;

            MessageBox.Show($"Unable to log in:\n{ex.Message}", "Login Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e) => TryLogin();

    private void WebVersionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppConfig.ApiBaseUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open the web version:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Field_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryLogin();
    }
}