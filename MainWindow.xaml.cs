using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LiteBrowser;

sealed class BrowserTab
{
    public WebView2 View = new() { Visibility = Visibility.Collapsed };
    public Border Header = new();
    public TextBlock Title = new();
    public DateTime LastActive = DateTime.UtcNow;
    public bool Suspended;
}

public partial class MainWindow : Window
{
    const string Home = "https://duckduckgo.com";
    static readonly TimeSpan SuspendAfter = TimeSpan.FromSeconds(30);
    static readonly Brush ActiveBg = Brush("#202025"), IdleBg = Brush("#17171B");

    readonly List<BrowserTab> tabs = new();
    readonly DispatcherTimer sweeper = new() { Interval = TimeSpan.FromSeconds(15) };
    CoreWebView2Environment? env;
    BrowserTab? current;

    static Brush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

    public MainWindow()
    {
        InitializeComponent();
        sweeper.Tick += async (_, _) => await SuspendIdleTabs();
    }

    async void Window_Loaded(object s, RoutedEventArgs e)
    {
        var opts = new CoreWebView2EnvironmentOptions
        {
            // Fewer processes, no background chatter, low-end device mode = smaller footprint.
            AdditionalBrowserArguments =
                "--enable-low-end-device-mode --renderer-process-limit=3 --process-per-site " +
                "--disable-background-networking --disable-component-update --disable-sync " +
                "--disable-features=Translate,OptimizationHints,msSmartScreenProtection"
        };
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LiteBrowser");
        env = await CoreWebView2Environment.CreateAsync(null, data, opts);
        await NewTab(Home);
        sweeper.Start();
    }

    async Task NewTab(string url)
    {
        var tab = new BrowserTab();
        tab.Title = new TextBlock { Text = "New tab", Foreground = Brush("#D8D8E0"), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var close = new Button { Content = "✕", Style = (Style)FindResource("IconBtn"), Width = 20, Height = 20, FontSize = 10, Margin = new Thickness(6, 0, 0, 0) };
        var row = new DockPanel();
        DockPanel.SetDock(close, Dock.Right);
        row.Children.Add(close); row.Children.Add(tab.Title);
        tab.Header = new Border { Child = row, Width = 190, Height = 30, Padding = new Thickness(12, 0, 6, 0), Margin = new Thickness(0, 0, 2, 0), CornerRadius = new CornerRadius(8, 8, 0, 0), Background = IdleBg, Cursor = Cursors.Hand };
        tab.Header.MouseDown += (_, _) => Select(tab);
        close.Click += (_, _) => CloseTab(tab);

        tabs.Add(tab);
        TabStrip.Children.Add(tab.Header);
        Host.Children.Add(tab.View);
        Select(tab);

        await tab.View.EnsureCoreWebView2Async(env);
        var cw = tab.View.CoreWebView2;
        cw.Settings.IsStatusBarEnabled = false;
        cw.Settings.IsGeneralAutofillEnabled = false;
        cw.Settings.IsPasswordAutosaveEnabled = false;
        cw.NewWindowRequested += async (_, a) => { a.Handled = true; await NewTab(a.Uri); };
        cw.DocumentTitleChanged += (_, _) => tab.Title.Text = string.IsNullOrWhiteSpace(cw.DocumentTitle) ? cw.Source : cw.DocumentTitle;
        cw.SourceChanged += (_, _) => { if (tab == current && !Address.IsKeyboardFocused) Address.Text = cw.Source; };
        cw.HistoryChanged += (_, _) => { if (tab == current) UpdateNav(); };
        cw.Navigate(url);
    }

    void Select(BrowserTab tab)
    {
        if (current != null && current != tab)
        {
            current.View.Visibility = Visibility.Collapsed;   // hidden WebViews stop rendering
            current.Header.Background = IdleBg;
            current.LastActive = DateTime.UtcNow;
        }
        current = tab;
        if (tab.Suspended) { tab.View.CoreWebView2?.Resume(); tab.Suspended = false; }
        tab.View.Visibility = Visibility.Visible;
        tab.Header.Background = ActiveBg;
        Address.Text = tab.View.CoreWebView2?.Source ?? "";
        UpdateNav();
        tab.View.Focus();
    }

    void CloseTab(BrowserTab tab)
    {
        int i = tabs.IndexOf(tab);
        tabs.Remove(tab);
        TabStrip.Children.Remove(tab.Header);
        Host.Children.Remove(tab.View);
        tab.View.Dispose();
        if (tabs.Count == 0) { Close(); return; }
        if (tab == current) { current = null; Select(tabs[Math.Min(i, tabs.Count - 1)]); }
    }

    async Task SuspendIdleTabs()
    {
        foreach (var t in tabs.ToList())
        {
            var cw = t.View.CoreWebView2;
            if (t == current || t.Suspended || cw == null) continue;
            if (DateTime.UtcNow - t.LastActive < SuspendAfter || cw.IsDocumentPlayingAudio) continue;
            try
            {
                cw.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
                t.Suspended = await cw.TrySuspendAsync();
            }
            catch { }
        }
        GC.Collect(1, GCCollectionMode.Optimized, false);
    }

    void UpdateNav()
    {
        var cw = current?.View.CoreWebView2;
        BackBtn.IsEnabled = cw?.CanGoBack ?? false;
        FwdBtn.IsEnabled = cw?.CanGoForward ?? false;
    }

    static string ToUrl(string input)
    {
        input = input.Trim();
        if (Uri.TryCreate(input, UriKind.Absolute, out var u) && (u.Scheme is "http" or "https" or "file" or "about" or "edge")) return input;
        if (!input.Contains(' ') && input.Contains('.')) return "https://" + input;
        return "https://duckduckgo.com/?q=" + Uri.EscapeDataString(input);
    }

    void Address_KeyDown(object s, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        current?.View.CoreWebView2?.Navigate(ToUrl(Address.Text));
        current?.View.Focus();
    }
    void Address_Focus(object s, KeyboardFocusChangedEventArgs e) => Address.Dispatcher.BeginInvoke(() => Address.SelectAll());
    void Back_Click(object s, RoutedEventArgs e) => current?.View.CoreWebView2?.GoBack();
    void Fwd_Click(object s, RoutedEventArgs e) => current?.View.CoreWebView2?.GoForward();
    void Reload_Click(object s, RoutedEventArgs e) => current?.View.CoreWebView2?.Reload();
    async void NewTab_Click(object s, RoutedEventArgs e) => await NewTab(Home);

    async void Window_KeyDown(object s, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        if (ctrl && e.Key == Key.T) { e.Handled = true; await NewTab(Home); }
        else if (ctrl && e.Key == Key.W && current != null) { e.Handled = true; CloseTab(current); }
        else if (ctrl && e.Key == Key.L) { e.Handled = true; Address.Focus(); }
        else if (e.Key == Key.F5) { e.Handled = true; current?.View.CoreWebView2?.Reload(); }
    }
}
