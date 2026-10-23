using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SandboxStudio.Core.Sessions;

namespace SandboxStudio.UI.Views;

/// <summary>
/// Lists live Windows Sandbox sessions reported by the <c>wsb</c> CLI and offers
/// connect/stop actions per session.
/// </summary>
public sealed partial class SessionsDialog : ContentDialog
{
    private readonly ISandboxSessionManager _sessions;
    private readonly ObservableCollection<SessionItem> _items = new();
    private readonly CancellationTokenSource _closedCts = new();
    private bool _busy;

    /// <summary>
    /// Initializes a new instance of <see cref="SessionsDialog"/>.
    /// </summary>
    public SessionsDialog(ISandboxSessionManager sessions)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        InitializeComponent();
        SessionsList.ItemsSource = _items;
        Closed += (_, _) => _closedCts.Cancel();
    }

    private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        await RefreshAsync();
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        LoadingRing.IsActive = true;
        StatusInfoBar.IsOpen = false;
        try
        {
            _items.Clear();
            if (!_sessions.IsSupported)
            {
                ShowStatus(InfoBarSeverity.Warning, "The wsb CLI requires Windows 11 24H2 or later with Windows Sandbox enabled.");
                return;
            }

            var result = await _sessions.ListAsync(_closedCts.Token);
            if (result.ErrorMessage is not null)
            {
                ShowStatus(InfoBarSeverity.Error, result.ErrorMessage);
                return;
            }

            foreach (var session in result.Sessions)
            {
                _items.Add(new SessionItem(session));
            }

            if (_items.Count == 0)
            {
                ShowStatus(InfoBarSeverity.Informational, "No sandbox sessions are running.");
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            LoadingRing.IsActive = false;
            _busy = false;
        }
    }

    private void OnExecClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SessionItem item } button || _busy)
        {
            return;
        }

        // A Flyout rather than a nested ContentDialog — only one ContentDialog
        // can be open in a window at a time.
        var commandBox = new TextBox
        {
            Header = "Command",
            PlaceholderText = "app.exe or a built-in command",
            MinWidth = 320
        };
        var runAsBox = new ComboBox
        {
            Header = "Run as",
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        runAsBox.Items.Add(new ComboBoxItem { Tag = "System", Content = "System" });
        runAsBox.Items.Add(new ComboBoxItem { Tag = "ExistingLogin", Content = "Existing login (requires a connected session)" });
        var workDirBox = new TextBox
        {
            Header = "Working directory (optional)",
            PlaceholderText = @"C:\Tools"
        };
        var note = new TextBlock
        {
            Text = "Command output cannot be captured; only the exit code comes back.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };
        var themeKey = ActualTheme.ToString();
        if (Application.Current.Resources.ThemeDictionaries.TryGetValue(themeKey, out var themeDict) &&
            themeDict is ResourceDictionary resources &&
            resources.TryGetValue("TextFillColorSecondaryBrush", out var brushValue) &&
            brushValue is Brush secondaryBrush)
        {
            note.Foreground = secondaryBrush;
        }
        var runButton = new Button
        {
            Content = "Run",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(commandBox);
        panel.Children.Add(runAsBox);
        panel.Children.Add(workDirBox);
        panel.Children.Add(note);
        panel.Children.Add(runButton);

        var flyout = new Flyout { Content = panel };
        runButton.Click += async (_, _) =>
        {
            if (_busy)
            {
                // Keep the flyout open so the user does not lose typed input.
                ShowStatus(InfoBarSeverity.Warning, "Another operation is in progress.");
                return;
            }

            var command = commandBox.Text.Trim();
            if (command.Length == 0)
            {
                ShowStatus(InfoBarSeverity.Warning, "Enter a command to run.");
                return;
            }

            flyout.Hide();
            var runAs = runAsBox.SelectedItem is ComboBoxItem { Tag: "ExistingLogin" }
                ? WsbRunAs.ExistingLogin
                : WsbRunAs.System;
            var workingDir = string.IsNullOrWhiteSpace(workDirBox.Text) ? null : workDirBox.Text.Trim();

            _busy = true;
            button.IsEnabled = false;
            try
            {
                var result = await _sessions.ExecAsync(
                    item.Session.Id, command, runAs, workingDir, _closedCts.Token);
                if (result.ExitCode is { } exitCode)
                {
                    ShowStatus(
                        exitCode == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                        $"Command finished with exit code {exitCode}.");
                }
                else
                {
                    ShowStatus(InfoBarSeverity.Error, result.ErrorMessage ?? "The command could not be run.");
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                button.IsEnabled = true;
                _busy = false;
            }
        };

        flyout.ShowAt(button);
    }

    private async void OnIpClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SessionItem item } button || _busy)
        {
            return;
        }

        _busy = true;
        button.IsEnabled = false;
        try
        {
            var result = await _sessions.GetIpAddressAsync(item.Session.Id, _closedCts.Token);
            if (result.ErrorMessage is not null)
            {
                ShowStatus(InfoBarSeverity.Error, result.ErrorMessage);
                return;
            }

            var index = _items.IndexOf(item);
            if (index >= 0)
            {
                _items[index] = new SessionItem(
                    item.Session,
                    result.Addresses.Count == 0 ? "no IP reported" : string.Join(", ", result.Addresses));
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            button.IsEnabled = true;
            _busy = false;
        }
    }

    private async void OnConnectClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SessionItem item } button || _busy)
        {
            return;
        }

        _busy = true;
        button.IsEnabled = false;
        try
        {
            var result = await _sessions.ConnectAsync(item.Session.Id, _closedCts.Token);
            if (!result.IsSuccess)
            {
                ShowStatus(InfoBarSeverity.Error, result.ErrorMessage ?? "Could not connect to the session.");
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            button.IsEnabled = true;
            _busy = false;
        }
    }

    private async void OnStopClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SessionItem item } button || _busy)
        {
            return;
        }

        _busy = true;
        button.IsEnabled = false;
        try
        {
            var result = await _sessions.StopAsync(item.Session.Id, _closedCts.Token);
            if (!result.IsSuccess)
            {
                ShowStatus(InfoBarSeverity.Error, result.ErrorMessage ?? "Could not stop the session.");
                return;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            button.IsEnabled = true;
            _busy = false;
        }

        await RefreshAsync();
    }

    private void ShowStatus(InfoBarSeverity severity, string message)
    {
        StatusInfoBar.Severity = severity;
        StatusInfoBar.Message = message;
        StatusInfoBar.IsOpen = true;
    }

    private sealed class SessionItem
    {
        public SessionItem(SandboxSession session, string? ipText = null)
        {
            Session = session;
            IdShort = session.Id.ToString("N")[..8];
            StateGlyph = session.State == SandboxSessionState.Running ? "\uE768" : "\uE71A";
            CanConnect = session.State is SandboxSessionState.Running or SandboxSessionState.Unknown;
            CanExec = session.State != SandboxSessionState.Stopped;
            CanStop = session.State != SandboxSessionState.Stopped;
            var detail = session switch
            {
                { State: SandboxSessionState.Running, Uptime: { } uptime } =>
                    $"Running \u00B7 up {FormatUptime(uptime)}",
                { State: SandboxSessionState.Running } => "Running",
                { State: SandboxSessionState.Stopped } => "Stopped",
                _ => "State not reported"
            };
            DetailText = ipText is null ? detail : $"{detail} \u00B7 {ipText}";
            StateBrush = session.State == SandboxSessionState.Stopped
                ? new SolidColorBrush(Microsoft.UI.Colors.Gray)
                : new SolidColorBrush(Microsoft.UI.Colors.Green);
        }

        public SandboxSession Session { get; }

        public string IdShort { get; }

        public string StateGlyph { get; }

        public string DetailText { get; }

        public bool CanConnect { get; }

        public bool CanExec { get; }

        public bool CanStop { get; }

        public Brush StateBrush { get; }

        private static string FormatUptime(TimeSpan uptime) =>
            uptime.ToString(uptime.Days > 0 ? @"d\.hh\:mm\:ss" : @"hh\:mm\:ss");
    }
}
