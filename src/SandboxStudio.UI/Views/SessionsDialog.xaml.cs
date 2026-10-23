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
        public SessionItem(SandboxSession session)
        {
            Session = session;
            IdShort = session.Id.ToString("N")[..8];
            StateGlyph = session.State == SandboxSessionState.Running ? "\uE768" : "\uE71A";
            CanConnect = session.State is SandboxSessionState.Running or SandboxSessionState.Unknown;
            CanStop = session.State != SandboxSessionState.Stopped;
            DetailText = session switch
            {
                { State: SandboxSessionState.Running, Uptime: { } uptime } =>
                    $"Running \u00B7 up {FormatUptime(uptime)}",
                { State: SandboxSessionState.Running } => "Running",
                { State: SandboxSessionState.Stopped } => "Stopped",
                _ => "State not reported"
            };
            StateBrush = session.State == SandboxSessionState.Stopped
                ? new SolidColorBrush(Microsoft.UI.Colors.Gray)
                : new SolidColorBrush(Microsoft.UI.Colors.Green);
        }

        public SandboxSession Session { get; }

        public string IdShort { get; }

        public string StateGlyph { get; }

        public string DetailText { get; }

        public bool CanConnect { get; }

        public bool CanStop { get; }

        public Brush StateBrush { get; }

        private static string FormatUptime(TimeSpan uptime) =>
            uptime.ToString(uptime.Days > 0 ? @"d\.hh\:mm\:ss" : @"hh\:mm\:ss");
    }
}
