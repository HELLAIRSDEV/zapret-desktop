using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZapretDesktop.Desktop.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ZapretDesktop.Desktop;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;

    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private System.Windows.Forms.ToolStripMenuItem? _toggleMenuItem;
    private bool _exitRequested;
    private bool _trayHintShown;
    private bool _telegramAutoFetched;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        ThemeButton.Click += OnToggleThemeClick;
        MainTabs.SelectionChanged += OnMainTabsSelectionChanged;
        DomainInput.KeyDown += OnDomainInputKeyDown;

        Loaded += OnLoaded;
        StateChanged += OnWindowStateChanged;
    }

    /// <summary>
    /// При первом открытии вкладки Telegram автоматически подбираем прокси,
    /// а содержимое вкладки «Списки» грузим лениво — это ускоряет старт.
    /// </summary>
    private void OnMainTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(MainTabs.SelectedItem, ListsTab))
            _ = _viewModel.EnsureListContentLoadedAsync();

        if (_telegramAutoFetched || !ReferenceEquals(MainTabs.SelectedItem, TelegramTab)) return;

        _telegramAutoFetched = true;

        if (_viewModel.FindProxiesCommand.CanExecute(null))
            _viewModel.FindProxiesCommand.Execute(null);
    }

    /// <summary>
    /// Ввод домена как echo: Enter — и строка сразу пишется в list-general-user.txt.
    /// </summary>
    private void OnDomainInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        e.Handled = true;

        if (_viewModel.AddDomainsCommand.CanExecute(null))
            _viewModel.AddDomainsCommand.Execute(null);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Значок в трее создаём после первого кадра — окно появляется быстрее.
        CreateTrayIcon();

        await _viewModel.InitializeAsync();
        UpdateTrayText();

        if (_viewModel.StartMinimized)
            HideToTray();

        // Проверка обновлений Flowseal идёт в фоне и не задерживает старт.
        _ = _viewModel.CheckFlowsealUpdatesInBackgroundAsync();
    }

    // ================= Трей =================

    private void CreateTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();

        var showItem = new System.Windows.Forms.ToolStripMenuItem("Открыть Zapret Desktop");
        showItem.Font = new System.Drawing.Font(showItem.Font, System.Drawing.FontStyle.Bold);
        showItem.Click += (_, _) => Dispatcher.BeginInvoke(new Action(ShowFromTray));

        _toggleMenuItem = new System.Windows.Forms.ToolStripMenuItem("Запустить обход");
        _toggleMenuItem.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_viewModel.ToggleServiceCommand.CanExecute(null))
                _viewModel.ToggleServiceCommand.Execute(null);
        }));

        var testItem = new System.Windows.Forms.ToolStripMenuItem("Подобрать стратегию");
        testItem.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            ShowFromTray();
            if (_viewModel.RunAutoTestCommand.CanExecute(null))
                _viewModel.RunAutoTestCommand.Execute(null);
        }));

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Выход");
        exitItem.Click += (_, _) => Dispatcher.BeginInvoke(new Action(ExitApplication));

        menu.Items.Add(showItem);
        menu.Items.Add(_toggleMenuItem);
        menu.Items.Add(testItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Zapret Desktop",
            Icon = LoadAppIcon(),
            Visible = true,
            ContextMenuStrip = menu
        };

        _trayIcon.DoubleClick += (_, _) => Dispatcher.BeginInvoke(new Action(ShowFromTray));
        _trayIcon.BalloonTipClicked += (_, _) => Dispatcher.BeginInvoke(new Action(ShowFromTray));

        UpdateTrayText();
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon != null) return icon;
            }
        }
        catch
        {
            // fallback ниже
        }

        return System.Drawing.SystemIcons.Shield;
    }

    private void UpdateTrayText()
    {
        if (_toggleMenuItem != null)
            _toggleMenuItem.Text = _viewModel.IsRunning ? "Остановить обход" : "Запустить обход";

        if (_trayIcon != null)
        {
            var text = $"Zapret Desktop — {_viewModel.StatusText}";
            _trayIcon.Text = text.Length > 63 ? text[..63] : text;
        }
    }

    private void ShowFromTray()
    {
        SetPriority(ProcessPriorityClass.Normal);

        ShowInTaskbar = true;
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;

        // В трее приложение должно занимать минимум: ниже приоритет + сброс рабочего набора.
        SetPriority(ProcessPriorityClass.BelowNormal);
        MemoryTrimmer.Trim(force: true);

        if (!_trayHintShown && _trayIcon != null)
        {
            _trayHintShown = true;
            _trayIcon.BalloonTipTitle = "Zapret Desktop";
            _trayIcon.BalloonTipText = "Приложение свёрнуто в трей. Обход продолжает работать.";
            _trayIcon.ShowBalloonTip(2000);
        }
    }

    private static void SetPriority(ProcessPriorityClass priority)
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            process.PriorityClass = priority;
        }
        catch
        {
            // Не удалось изменить приоритет — не критично.
        }
    }

    private void ExitApplication()
    {
        _exitRequested = true;

        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _viewModel.ShutdownEngine();
        Application.Current.Shutdown();
    }

    // ================= События окна =================

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsRunning) or nameof(MainViewModel.StatusText))
            UpdateTrayText();

        if (e.PropertyName == nameof(MainViewModel.IsFlowsealUpdateAvailable) &&
            _viewModel.IsFlowsealUpdateAvailable &&
            _trayIcon != null &&
            !IsVisible)
        {
            _trayIcon.BalloonTipTitle = "Zapret Desktop";
            _trayIcon.BalloonTipText = "Доступно обновление стратегий Flowseal. Откройте приложение, чтобы обновить.";
            _trayIcon.ShowBalloonTip(4000);
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _viewModel.MinimizeToTray)
            HideToTray();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exitRequested && _viewModel.MinimizeToTray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        _viewModel.ShutdownEngine();
        base.OnClosed(e);
    }

    private void OnToggleThemeClick(object? sender, RoutedEventArgs e)
    {
        var currentTheme = ApplicationThemeManager.GetAppTheme();
        var newTheme = currentTheme == ApplicationTheme.Dark
            ? ApplicationTheme.Light
            : ApplicationTheme.Dark;

        ApplicationThemeManager.Apply(newTheme);
    }
}

/// <summary>
/// Оставлен для совместимости с App.xaml.
/// </summary>
public class BooleanToStopStartConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is true ? "Остановить" : "Запустить";

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
