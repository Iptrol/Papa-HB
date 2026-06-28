using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nickvision.Desktop.Application;
using Nickvision.Desktop.Globalization;
using Nickvision.Desktop.Network;
using Nickvision.Desktop.Notifications;
using Nickvision.Desktop.WinUI.Helpers;
using Nickvision.Parabolic.Shared.Controllers;
using Nickvision.Parabolic.Shared.Events;
using Nickvision.Parabolic.Shared.Models;
using Nickvision.Parabolic.Shared.Services;
using Nickvision.Parabolic.WinUI.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.System;

namespace Nickvision.Parabolic.WinUI.Views;

public sealed partial class MainWindow : Window
{
    private enum Pages
    {
        Home = 0,
        Downloads,
        Custom
    }

    private readonly IServiceProvider _serviceProvider;
    private readonly MainWindowController _controller;
    private readonly AppInfo _appInfo;
    private readonly ITranslationService _translationService;
    private readonly Dictionary<int, DownloadRow> _downloadRows;
    private RoutedEventHandler? _notificationClickHandler;

    public MainWindow(IServiceProvider serviceProvider, MainWindowController controller, AppInfo appInfo, IEventsService eventsService, ITranslationService translationService)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _controller = controller;
        _appInfo = appInfo;
        _translationService = translationService;
        _downloadRows = new Dictionary<int, DownloadRow>();
        _notificationClickHandler = null;
        AppWindow.TitleBar.PreferredTheme = _controller.Theme switch
        {
            Theme.Light => TitleBarTheme.Light,
            Theme.Dark => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode
        };
        MainGrid.RequestedTheme = _controller.Theme switch
        {
            Theme.Light => ElementTheme.Light,
            Theme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        this.Geometry = _controller.WindowGeometry;
        AppWindow.SetIcon("./Assets/papa_cat.ico");
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        BtnPreview.Visibility = Visibility.Collapsed;
        AppWindow.Closing += Window_Closing;
        eventsService.AppNotificationSent += (sender, e) => DispatcherQueue.TryEnqueue(() => App_AppNotificationSent(sender, e));
        eventsService.ConfigurationSaved += App_ConfigurationSaved;
        eventsService.DownloadAdded += (sender, e) => DispatcherQueue.TryEnqueue(() => Controller_DownloadAdded(sender, e));
        eventsService.DownloadProgressChanged += (sender, e) => DispatcherQueue.TryEnqueue(() => Controller_DownloadProgressChanged(sender, e));
        eventsService.DownloadCompleted += (sender, e) => DispatcherQueue.TryEnqueue(() => Controller_DownloadCompleted(sender, e));
        eventsService.DownloadStopped += (sender, e) => DispatcherQueue.TryEnqueue(() => Controller_DownloadStopped(sender, e));
        eventsService.DownloadStartedFromQueue += (sender, e) => DispatcherQueue.TryEnqueue(() => Controller_DownloadStartedFromQueue(sender, e));
        eventsService.DownloadRetired += (sender, e) => DispatcherQueue.TryEnqueue(() => Controller_DownloadRetired(sender, e));
        eventsService.DownloadRequested += async (s, args) => await AddDownloadAsync(args.Url);
        AppWindow.Title = "Папа Качай ❤️";
        LblTitle.Text = "Папа Качай ❤️";
        MenuFile.Title = "Файл";
        MenuAddDownload.Text = "Добавить загрузку";
        MenuExit.Text = "Выход";
        MenuDownloads.Title = "Загрузки";
        MenuStopAllRemaining.Text = "Остановить все";
        MenuRetryAllFailed.Text = "Повторить неудачные";
        MenuClearAllQueued.Text = "Очистить очередь";
        MenuClearAllCompleted.Text = "Очистить завершённые";
        LblHomeTitle.Text = "Привет, папа! 👋 Ну что, давай качать интересные видосы? 🎬\n\nТвои Лена, Саша и Фреди";
        LblHomeDescription.Text = "Вставь ссылку на видео или музыку с YouTube — и скачивай!";
        LblAddDownload.Text = "Добавить ссылку";
        BtnStopAllRemaining.Label = "Остановить все";
        BtnRetryAllFailed.Label = "Повторить неудачные";
        BtnClearAllQueued.Label = "Очистить очередь";
        BtnClearAllCompleted.Label = "Очистить завершённые";
        LblDownloadsAddDownload.Text = "Добавить";
        NavDownloadsAll.Content = "Все";
        NavDownloadsRunning.Content = "Идут";
        NavDownloadsCompleted.Content = "Завершённые";
        NavDownloadsFailed.Content = "Ошибки";
        NavHistory.Content = "История";
        StatusNoneDownloads.Title = "Нет загрузок";
        StatusNoneDownloads.Description = "Загрузок этого типа пока нет";
        LblNoneAddDownload.Text = "Добавить ссылку";
        DlgCredential.Title = "Требуется авторизация";
        TxtCredentialUsername.PlaceholderText = "Введите имя пользователя";
        TxtCredentialPassword.PlaceholderText = "Введите пароль";
        DlgCredential.PrimaryButtonText = "Войти";
        DlgCredential.CloseButtonText = "Отмена";
    }

    private async void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        ViewStack.SelectedIndex = (int)Pages.Home;
        ViewStackDownloads.SelectedIndex = 0;
        if (_controller.ShowDisclaimerOnStartup)
        {
            var checkBox = new CheckBox() { Content = "Больше не показывать" };
            var disclaimerDialog = new ContentDialog()
            {
                Title = "Важное замечание",
                Content = new StackPanel()
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock()
                        {
                            Text = "Видео на YouTube могут быть защищены авторским правом. Пожалуйста, скачивай только то, что разрешено.",
                            TextWrapping = TextWrapping.WrapWholeWords
                        },
                        checkBox
                    }
                },
                CloseButtonText = "Понятно",
                DefaultButton = ContentDialogButton.Close,
                RequestedTheme = MainGrid.ActualTheme,
                XamlRoot = MainGrid.XamlRoot
            };
            await disclaimerDialog.ShowAsync();
            if (checkBox.IsChecked ?? false)
            {
                _controller.ShowDisclaimerOnStartup = false;
            }
        }
        if (_controller.RecoverableDownloadsCount > 0)
        {
            var recoverDialog = new ContentDialog()
            {
                Title = "Восстановить загрузки?",
                Content = "Есть незавершённые загрузки. Хочешь загрузить их снова?",
                PrimaryButtonText = "Да",
                CloseButtonText = "Нет",
                DefaultButton = ContentDialogButton.Primary,
                RequestedTheme = MainGrid.ActualTheme,
                XamlRoot = MainGrid.XamlRoot
            };
            if ((await recoverDialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                await _controller.RecoverAllDownloadsAsync();
            }
            else
            {
                await _controller.ClearRecoverableDownloadsAsync();
            }
        }
        if (_controller.UrlFromArgs is not null)
        {
            await AddDownloadAsync(_controller.UrlFromArgs);
        }
    }

    private async void Window_Closing(AppWindow sender, AppWindowClosingEventArgs e)
    {
        if (!_controller.CanShutdown)
        {
            e?.Cancel = true;
            var confirmDialog = new ContentDialog()
            {
                Title = "Папа Качай ❤️",
                Content = "Есть незавершённые загрузки. Остановить их и выйти?",
                PrimaryButtonText = "Да, выйти",
                CloseButtonText = "Нет, подождать",
                DefaultButton = ContentDialogButton.Close,
                RequestedTheme = MainGrid.ActualTheme,
                XamlRoot = MainGrid.XamlRoot
            };
            if ((await confirmDialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                _controller.WindowGeometry = this.Geometry;
                await _controller.StopAllDownloadsAsync();
                Close();
                _serviceProvider.GetRequiredService<IHostApplicationLifetime>().StopApplication();
            }
            return;
        }
        _controller.WindowGeometry = this.Geometry;
        _serviceProvider.GetRequiredService<IHostApplicationLifetime>().StopApplication();
    }

    private void App_AppNotificationSent(object? sender, AppNotificationSentEventArgs e)
    {
        if (_notificationClickHandler is not null)
        {
            BtnInfoBar.Click -= _notificationClickHandler;
            _notificationClickHandler = null;
        }
        InfoBar.Message = e.Notification.Message;
        InfoBar.Severity = e.Notification.Severity switch
        {
            NotificationSeverity.Success => InfoBarSeverity.Success,
            NotificationSeverity.Warning => InfoBarSeverity.Warning,
            NotificationSeverity.Error => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational
        };
        if (e.Notification.Action == "update-ytdlp")
        {
            BtnInfoBar.Content = "Обновить";
            _notificationClickHandler = YtdlpUpdate;
            BtnInfoBar.Click += _notificationClickHandler;
        }
        else if (e.Notification.Action == "update-deno")
        {
            BtnInfoBar.Content = "Обновить";
            _notificationClickHandler = DenoUpdate;
            BtnInfoBar.Click += _notificationClickHandler;
        }
        else if (e.Notification.Action == "error" && !string.IsNullOrEmpty(e.Notification.ActionParam))
        {
            BtnInfoBar.Content = "Подробнее";
            _notificationClickHandler = async (_, _) =>
            {
                InfoBar.IsOpen = false;
                var errorDialog = new ContentDialog()
                {
                    Title = "Ошибка",
                    Content = new ScrollViewer()
                    {
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                        Content = new TextBlock()
                        {
                            Text = e.Notification.ActionParam,
                            TextWrapping = TextWrapping.Wrap
                        }
                    },
                    CloseButtonText = "Закрыть",
                    DefaultButton = ContentDialogButton.Close,
                    RequestedTheme = MainGrid.ActualTheme,
                    XamlRoot = MainGrid.XamlRoot
                };
                await errorDialog.ShowAsync();
            };
            BtnInfoBar.Click += _notificationClickHandler;
        }
        BtnInfoBar.Visibility = _notificationClickHandler is not null ? Visibility.Visible : Visibility.Collapsed;
        InfoBar.IsOpen = true;
    }

    private void App_ConfigurationSaved(object? sender, ConfigurationSavedEventArgs args)
    {
        if (args.ChangedPropertyName == "Theme")
        {
            AppWindow.TitleBar.PreferredTheme = _controller.Theme switch
            {
                Theme.Light => TitleBarTheme.Light,
                Theme.Dark => TitleBarTheme.Dark,
                _ => TitleBarTheme.UseDefaultAppMode
            };
            MainGrid.RequestedTheme = _controller.Theme switch
            {
                Theme.Light => ElementTheme.Light,
                Theme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        TitleBar.IsBackButtonVisible = false;
        ViewStack.SelectedIndex = ViewStack.PreviousSelectedIndex;
    }

    private async void Controller_DownloadAdded(object? sender, DownloadAddedEventArgs e)
    {
        var row = _serviceProvider.GetRequiredService<DownloadRow>();
        row.PauseRequested += DownloadRow_PauseRequested;
        row.ResumeRequested += DownloadRow_ResumeRequested;
        row.StopRequested += DownloadRow_StopRequested;
        row.RetryRequested += DownloadRow_RetryRequested;
        await row.TriggerAddedStateAsync(e);
        _downloadRows[e.Id] = row;
        UpdateDownloadsList();
        ViewStack.SelectedIndex = (int)Pages.Downloads;
        TitleBar.IsBackButtonVisible = false;
    }

    private void Controller_DownloadCompleted(object? sender, DownloadCompletedEventArgs e)
    {
        if (_downloadRows.TryGetValue(e.Id, out var row))
        {
            row.TriggerCompletedState(e);
            UpdateDownloadsList();
        }
    }

    private void Controller_DownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        if (_downloadRows.TryGetValue(e.Id, out var row))
        {
            row.TriggerProgressState(e);
        }
    }

    private void Controller_DownloadStartedFromQueue(object? sender, DownloadEventArgs e)
    {
        if (_downloadRows.TryGetValue(e.Id, out var row))
        {
            row.TriggerStartedFromQueueState();
            UpdateDownloadsList();
        }
    }

    private void Controller_DownloadStopped(object? sender, DownloadEventArgs e)
    {
        if (_downloadRows.TryGetValue(e.Id, out var row))
        {
            row.TriggerStoppedState();
            UpdateDownloadsList();
        }
    }

    private void Controller_DownloadRetired(object? sender, DownloadEventArgs e)
    {
        if (_downloadRows.TryGetValue(e.Id, out var row))
        {
            _downloadRows.Remove(e.Id);
            UpdateDownloadsList();
        }
    }

    private void DownloadRow_PauseRequested(object? sender, int id)
    {
        if (_controller.PauseDownload(id) && _downloadRows.TryGetValue(id, out var row))
        {
            row.TriggerPausedState();
        }
    }

    private void DownloadRow_ResumeRequested(object? sender, int id)
    {
        if (_controller.ResumeDownload(id) && _downloadRows.TryGetValue(id, out var row))
        {
            row.TriggerResumedState();
        }
    }

    private async void DownloadRow_RetryRequested(object? sender, int id) => await _controller.RetryDownloadAsync(id);
    private async void DownloadRow_StopRequested(object? sender, int id) => await _controller.StopDownloadAsync(id);
    private void NavViewDownloads_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args) => UpdateDownloadsList();

    private void UpdateProgress_Changed(object? sender, DownloadProgress e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (e.Completed)
            {
                FlyoutUpdateProgress.Hide();
                BtnUpdateProgress.Visibility = Visibility.Collapsed;
                return;
            }
            var message = $"Скачиваем обновление: {Math.Round(e.Percentage * 100)}%";
            BtnUpdateProgress.Visibility = Visibility.Visible;
            ToolTipService.SetToolTip(BtnUpdateProgress, message);
            RingUpdateProcess.Value = e.Percentage * 100;
            LblUpdateProgress.Text = message;
        });
    }

    private async void AddDownload(object? sender, RoutedEventArgs e) => await AddDownloadAsync(null);
    private void Exit(object sender, RoutedEventArgs args) => Window_Closing(AppWindow, null!);

    private void Settings(object sender, RoutedEventArgs args)
    {
        TitleBar.IsBackButtonVisible = true;
        ViewStack.SelectedIndex = (int)Pages.Custom;
        var settings = _serviceProvider.GetRequiredService<SettingsPage>();
        settings.WindowId = AppWindow.Id;
        FrameCustom.Content = settings;
    }

    private async void History(object sender, RoutedEventArgs args)
    {
        var historyDialog = _serviceProvider.GetRequiredService<HistoryDialog>();
        historyDialog.RequestedTheme = MainGrid.ActualTheme;
        historyDialog.XamlRoot = MainGrid.XamlRoot;
        await historyDialog.ShowAsync();
    }

    private async void NavHistory_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        NavViewDownloads.SelectedItem = null;
        var historyDialog = _serviceProvider.GetRequiredService<HistoryDialog>();
        historyDialog.RequestedTheme = MainGrid.ActualTheme;
        historyDialog.XamlRoot = MainGrid.XamlRoot;
        await historyDialog.ShowAsync();
    }

    private void ClearAllCompleted(object? sender, RoutedEventArgs e)
    {
        foreach (var id in _controller.ClearCompletedDownloads())
        {
            _downloadRows.Remove(id);
        }
        UpdateDownloadsList();
    }

    private void ClearAllQueued(object? sender, RoutedEventArgs e)
    {
        foreach (var id in _controller.ClearQueuedDownloads())
        {
            _downloadRows.Remove(id);
        }
        UpdateDownloadsList();
    }

    private async void DenoUpdate(object? sender, RoutedEventArgs e)
    {
        var progress = new Progress<DownloadProgress>();
        progress.ProgressChanged += UpdateProgress_Changed;
        InfoBar.IsOpen = false;
        await _controller.DenoUpdateAsync(progress);
        progress.ProgressChanged -= UpdateProgress_Changed;
    }

    private async void RetryAllFailed(object? sender, RoutedEventArgs e) => await _controller.RetryFailedDownloadsAsync();
    private async void StopAllRemaining(object? sender, RoutedEventArgs e) => await _controller.StopAllDownloadsAsync();

    private async void WindowsUpdate(object? sender, RoutedEventArgs e)
    {
        var progress = new Progress<DownloadProgress>();
        progress.ProgressChanged += UpdateProgress_Changed;
        InfoBar.IsOpen = false;
        await _controller.WindowsUpdateAsync(progress);
        progress.ProgressChanged -= UpdateProgress_Changed;
    }

    private async void YtdlpUpdate(object? sender, RoutedEventArgs e)
    {
        var progress = new Progress<DownloadProgress>();
        progress.ProgressChanged += UpdateProgress_Changed;
        InfoBar.IsOpen = false;
        await _controller.YtdlpUpdateAsync(progress);
        progress.ProgressChanged -= UpdateProgress_Changed;
    }

    private async Task AddDownloadAsync(Uri? uri)
    {
        var addDownloadDialog = _serviceProvider.GetRequiredService<AddDownloadDialog>();
        addDownloadDialog.WindowId = AppWindow.Id;
        addDownloadDialog.RequestedTheme = MainGrid.ActualTheme;
        addDownloadDialog.XamlRoot = MainGrid.XamlRoot;
        if (uri is not null)
        {
            await addDownloadDialog.ShowAsync(uri);
        }
        else
        {
            await addDownloadDialog.ShowAsync();
        }
    }

    private async Task LaunchUriAsync(Uri? uri)
    {
        if (uri is null) return;
        await Launcher.LaunchUriAsync(uri);
    }

    private void UpdateDownloadsList()
    {
        var selectedTag = ((NavViewDownloads.SelectedItem as NavigationViewItem)?.Tag as string) ?? string.Empty;
        var rows = new List<DownloadRow>(_downloadRows.Count);
        foreach (var row in _downloadRows.Values)
        {
            if (selectedTag switch
            {
                "1" => row.Status == DownloadStatus.Running || row.Status == DownloadStatus.Paused,
                "3" => row.Status == DownloadStatus.Success || row.Status == DownloadStatus.Error || row.Status == DownloadStatus.Stopped,
                "4" => row.Status == DownloadStatus.Error,
                _ => true
            })
            {
                rows.Add(row);
            }
        }
        rows.Reverse();
        ListDownloads.ItemsSource = rows;
        ViewStackDownloads.SelectedIndex = rows.Count > 0 ? 1 : 0;
        InfoBadgeDownloadsAll.Value = _controller.RemainingDownloadsCount;
        InfoBadgeDownloadsAll.Visibility = _controller.RemainingDownloadsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        InfoBadgeDownloadsRunning.Value = _controller.RunningDownloadsCount;
        InfoBadgeDownloadsRunning.Visibility = _controller.RunningDownloadsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        InfoBadgeDownloadsCompleted.Value = _controller.CompletedDownloadsCount;
        InfoBadgeDownloadsCompleted.Visibility = _controller.CompletedDownloadsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        InfoBadgeDownloadsFailed.Value = _controller.FailedDownloadsCount;
        InfoBadgeDownloadsFailed.Visibility = _controller.FailedDownloadsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
