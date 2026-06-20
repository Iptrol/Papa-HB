using CommunityToolkit.WinUI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.Storage.Pickers;
using Nickvision.Desktop.Globalization;
using Nickvision.Desktop.Keyring;
using Nickvision.Desktop.WinUI.Helpers;
using Nickvision.Parabolic.Shared.Controllers;
using Nickvision.Parabolic.Shared.Models;
using Nickvision.Parabolic.WinUI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;

namespace Nickvision.Parabolic.WinUI.Views;

public sealed partial class AddDownloadDialog : ContentDialog
{
    private enum Pages
    {
        Discover = 0,
        Loading,
        Single,
        Playlist
    }

    private enum SinglePages
    {
        General = 0,
        Advanced
    }

    private enum PlaylistPages
    {
        General = 0,
        Items,
        Advanced
    }

    private readonly AddDownloadDialogController _controller;
    private readonly ITranslationService _translationService;
    private DiscoveryContext? _discoveryContext;

    public WindowId? WindowId { get; set; }

    public AddDownloadDialog(AddDownloadDialogController controller, ITranslationService translationService)
    {
        InitializeComponent();
        _controller = controller;
        _translationService = translationService;
        _discoveryContext = null;
        Title = "Добавить загрузку";
        PrimaryButtonText = "Найти";
        CloseButtonText = "Отмена";
        DefaultButton = ContentDialogButton.Primary;
        IsPrimaryButtonEnabled = false;
        TxtUrl.Header = "Ссылка на видео";
        TxtUrl.PlaceholderText = "Вставь ссылку сюда";
        LblLoading.Text = "Загружаем информацию, подожди...";
        NavViewItemSingleGeneral.Text = "Общее";
        NavViewItemSingleAdvanced.Text = "Дополнительно";
        TxtSingleSaveFilename.Header = "Название файла";
        ToolTipService.SetToolTip(BtnSingleRevertFilename, "Вернуть оригинальное название");
        TxtSingleSaveFolder.Header = "Папка для сохранения";
        ToolTipService.SetToolTip(BtnSingleSelectSaveFolder, "Выбрать папку");
        CmbSingleFileType.Header = "Формат файла";
        TeachSingleFileType.Title = "Внимание";
        TeachSingleFileType.Subtitle = "Некоторые форматы не поддерживают встроенные субтитры и обложки.";
        CmbSingleVideoFormat.Header = "Качество видео";
        CmbSingleAudioFormat.Header = "Качество аудио";
        TxtSingleStartTime.Header = "Время начала";
        TxtSingleEndTime.Header = "Время конца";
        NavViewItemPlaylistGeneral.Text = "Общее";
        NavViewItemPlaylistItems.Text = "Видео";
        NavViewItemPlaylistAdvanced.Text = "Дополнительно";
        TxtPlaylistSaveFolder.Header = "Папка для сохранения";
        ToolTipService.SetToolTip(BtnPlaylistSelectSaveFolder, "Выбрать папку");
        CmbPlaylistFileType.Header = "Формат файла";
        TeachPlaylistFileType.Title = "Внимание";
        TeachPlaylistFileType.Subtitle = "Некоторые форматы не поддерживают встроенные субтитры и обложки.";
        CmbPlaylistSuggestedVideoResolution.Header = "Качество видео";
        CmbPlaylistSuggestedAudioBitrate.Header = "Качество аудио";
        LblPlaylistSelectAllItems.Text = "Выбрать все";
        LblPlaylistDeselectAllItems.Text = "Снять выбор";
        TglPlaylistReverseDownloadOrder.OnContent = "Обратный порядок загрузки";
        TglPlaylistReverseDownloadOrder.OffContent = "Обратный порядок загрузки";
        TglPlaylistNumberTitles.OnContent = "Нумеровать названия";
        TglPlaylistNumberTitles.OffContent = "Нумеровать названия";
        TeachPlaylistNumberTitles.Title = "Внимание";
        TeachPlaylistNumberTitles.Subtitle = "Нумерация будет применена к выбранным элементам при загрузке.";
        TglPlaylistExportM3U.OnContent = "Экспортировать M3U плейлист";
        TglPlaylistExportM3U.OffContent = "Экспортировать M3U плейлист";
    }

    public async new Task<ContentDialogResult> ShowAsync()
    {
        ViewStack.SelectedIndex = (int)Pages.Discover;
        if (string.IsNullOrEmpty(TxtUrl.Text))
        {
            if (Clipboard.GetContent().Contains(StandardDataFormats.Text))
            {
                if (Uri.TryCreate(await Clipboard.GetContent().GetTextAsync(), UriKind.Absolute, out var uri))
                {
                    TxtUrl.Text = uri.ToString();
                    IsPrimaryButtonEnabled = true;
                }
            }
        }
        var result = await base.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return result;
        }
        var cancellationToken = new CancellationTokenSource();
        Title = "Ищем видео...";
        PrimaryButtonText = null;
        CloseButtonText = "Отмена";
        DefaultButton = ContentDialogButton.None;
        ViewStack.SelectedIndex = (int)Pages.Loading;
        DispatcherQueue.TryEnqueue(async () => await DiscoverMediaAsync(cancellationToken.Token));
        result = await base.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (_discoveryContext is not null && _discoveryContext.Items.Count > 1)
            {
                await DownloadPlaylistAsync();
            }
            else
            {
                await DownloadSingleAsync();
            }
        }
        else
        {
            cancellationToken.Cancel();
        }
        cancellationToken.Dispose();
        return result;
    }

    public async Task<ContentDialogResult> ShowAsync(Uri url)
    {
        TxtUrl.Text = url.ToString();
        IsPrimaryButtonEnabled = true;
        return await ShowAsync();
    }

    private async Task DiscoverMediaAsync(CancellationToken cancellationToken)
    {
        _discoveryContext = await _controller.DiscoverAsync(new Uri(TxtUrl.Text), null, cancellationToken);
        if (_discoveryContext is null)
        {
            Hide();
            return;
        }
        using var thumbnailMemoryStream = await _controller.GetThumbnailImageStreamAsync(_discoveryContext);
        using var thumbnailStream = thumbnailMemoryStream.AsRandomAccessStream();
        var thumbnailDecoder = await BitmapDecoder.CreateAsync(thumbnailStream);
        var thumbnailBitmap = await thumbnailDecoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var thumbnailSource = new SoftwareBitmapSource();
        await thumbnailSource.SetBitmapAsync(thumbnailBitmap);
        Title = "Настройка загрузки";
        PrimaryButtonText = "Скачать";
        CloseButtonText = "Отмена";
        SecondaryButtonText = null;
        DefaultButton = ContentDialogButton.Primary;
        if (_discoveryContext.Items.Count == 1)
        {
            ViewStack.SelectedIndex = (int)Pages.Single;
            ViewStackSingle.SelectedIndex = (int)SinglePages.General;
            ImgSingleThumbnail.Source = thumbnailSource;
            LblSingleTitle.Text = _discoveryContext.Title;
            LblSingleUrl.Text = _discoveryContext.Url.ToString();
            TxtSingleSaveFilename.Text = _discoveryContext.Items[0].Label;
            TxtSingleSaveFolder.Text = _controller.PreviousSaveFolder;
            CmbSingleVideoFormat.ItemsSource = _discoveryContext.VideoFormats.ToBindableSelectonItems();
            CmbSingleAudioFormat.ItemsSource = _discoveryContext.AudioFormats.ToBindableSelectonItems();
            CmbSingleFileType.ItemsSource = _discoveryContext.FileTypes.ToBindableSelectonItems();
            CmbSingleFileType.SelectSelectionItem();
            TxtSingleStartTime.PlaceholderText = _discoveryContext.Items[0].StartTime;
            TxtSingleStartTime.Text = _discoveryContext.Items[0].StartTime;
            TxtSingleEndTime.PlaceholderText = _discoveryContext.Items[0].EndTime;
            TxtSingleEndTime.Text = _discoveryContext.Items[0].EndTime;
        }
        else
        {
            ViewStack.SelectedIndex = (int)Pages.Playlist;
            ViewStackPlaylist.SelectedIndex = (int)PlaylistPages.General;
            ImgPlaylistThumbnail.Source = thumbnailSource;
            LblPlaylistTitle.Text = _discoveryContext.Title;
            LblPlaylistUrl.Text = _discoveryContext.Url.ToString();
            TxtPlaylistSaveFolder.Text = _controller.PreviousSaveFolder;
            CmbPlaylistFileType.ItemsSource = _discoveryContext.FileTypes.ToBindableSelectonItems();
            CmbPlaylistFileType.SelectSelectionItem();
            CmbPlaylistSuggestedVideoResolution.ItemsSource = _discoveryContext.VideoResolutions.ToBindableSelectonItems();
            CmbPlaylistSuggestedVideoResolution.SelectSelectionItem();
            CmbPlaylistSuggestedAudioBitrate.ItemsSource = _discoveryContext.AudioBitrates.ToBindableSelectonItems();
            CmbPlaylistSuggestedAudioBitrate.SelectSelectionItem();
            LblPlaylistItemsTime.Text = $"Общая длительность: {_discoveryContext.TotalDuration}";
            TglPlaylistReverseDownloadOrder.IsOn = _controller.PreviousReverseDownloadOrder;
            TglPlaylistNumberTitles.IsOn = _controller.PreviousNumberTitles;
            ListPlaylistItems.ItemsSource = _discoveryContext.Items.ToBindableMediaSelectionItems();
            ListPlaylistItems.SelectMediaSelectionItems();
            TglPlaylistExportM3U.IsOn = _controller.PreviousExportM3U;
        }
    }

    private Task DownloadSingleAsync() => _controller.AddSingleDownloadAsync(_discoveryContext!,
        TxtSingleSaveFilename.Text,
        TxtSingleSaveFolder.Text,
        (CmbSingleFileType.SelectedItem as BindableSelectionItem)!.ToSelectionItem<MediaFileType>()!,
        (CmbSingleVideoFormat.SelectedItem as BindableSelectionItem)!.ToSelectionItem<Format>()!,
        (CmbSingleAudioFormat.SelectedItem as BindableSelectionItem)!.ToSelectionItem<Format>()!,
        Enumerable.Empty<SelectionItem<SubtitleLanguage>>(),
        false,
        false,
        false,
        new SelectionItem<PostProcessorArgument?>(null, "", true),
        TxtSingleStartTime.Text,
        TxtSingleEndTime.Text
    );

    private async Task DownloadPlaylistAsync()
    {
        var selectedPlaylistItems = new List<MediaSelectionItem>();
        foreach (var item in ListPlaylistItems.SelectedItems)
        {
            selectedPlaylistItems.Add((item as BindableMediaSelectionItem)!.SelectionItem);
        }
        await _controller.AddPlaylistDownloadsAsync(_discoveryContext!,
            selectedPlaylistItems,
            TxtPlaylistSaveFolder.Text,
            (CmbPlaylistFileType.SelectedItem as BindableSelectionItem)!.ToSelectionItem<MediaFileType>()!,
            (CmbPlaylistSuggestedVideoResolution.SelectedItem as BindableSelectionItem)!.ToSelectionItem<VideoResolution>()!,
            (CmbPlaylistSuggestedAudioBitrate.SelectedItem as BindableSelectionItem)!.ToSelectionItem<double>()!,
            TglPlaylistReverseDownloadOrder.IsOn,
            TglPlaylistNumberTitles.IsOn,
            Enumerable.Empty<SelectionItem<SubtitleLanguage>>(),
            TglPlaylistExportM3U.IsOn,
            false,
            false,
            false,
            new SelectionItem<PostProcessorArgument?>(null, "", true));
    }

    private void TxtUrl_TextChanged(object? sender, TextChangedEventArgs e) => IsPrimaryButtonEnabled = !TxtUrl.Text.StartsWith("//") && Uri.TryCreate(TxtUrl.Text, UriKind.Absolute, out var _);

    private void NavViewSingle_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        ViewStackSingle.SelectedIndex = (NavViewSingle.SelectedItem.Tag as string)! switch
        {
            "Advanced" => (int)SinglePages.Advanced,
            _ => (int)SinglePages.General
        };
    }

    private void BtnSingleRevertFilename_Click(object? sender, RoutedEventArgs e) => TxtSingleSaveFilename.Text = _discoveryContext!.Items[0].Label;

    private async void BtnSingleSelectSaveFolder_Click(object? sender, RoutedEventArgs e)
    {
        BtnSingleSelectSaveFolder.IsEnabled = false;
        try
        {
            var picker = new FolderPicker(WindowId!.Value)
            {
                SuggestedStartLocation = PickerLocationId.Downloads
            };
            await Task.Yield();
            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
            {
                TxtSingleSaveFolder.Text = folder.Path;
            }
        }
        finally
        {
            BtnSingleSelectSaveFolder.IsEnabled = true;
        }
    }

    private void CmbSingleFileType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedFileType = (CmbSingleFileType.SelectedItem as BindableSelectionItem)!.ToSelectionItem<MediaFileType>()!;
        TeachSingleFileType.IsOpen = _controller.GetShouldShowFileTypeTeach(_discoveryContext!, selectedFileType);
        CmbSingleVideoFormat.SelectSelectionItemByFormatId(_controller.PreviousVideoFormatIds[selectedFileType.Value]);
        CmbSingleAudioFormat.SelectSelectionItemByFormatId(_controller.PreviousAudioFormatIds[selectedFileType.Value]);
    }

    private void NavViewPlaylist_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        ViewStackPlaylist.SelectedIndex = (NavViewPlaylist.SelectedItem.Tag as string)! switch
        {
            "Items" => (int)PlaylistPages.Items,
            "Advanced" => (int)PlaylistPages.Advanced,
            _ => (int)PlaylistPages.General
        };
    }

    private async void BtnPlaylistSelectSaveFolder_Click(object? sender, RoutedEventArgs e)
    {
        BtnPlaylistSelectSaveFolder.IsEnabled = false;
        try
        {
            var picker = new FolderPicker(WindowId!.Value)
            {
                SuggestedStartLocation = PickerLocationId.Downloads
            };
            await Task.Yield();
            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
            {
                TxtPlaylistSaveFolder.Text = folder.Path;
            }
        }
        finally
        {
            BtnPlaylistSelectSaveFolder.IsEnabled = true;
        }
    }

    private void CmbPlaylistFileType_SelectionChanged(object? sender, SelectionChangedEventArgs e) => TeachPlaylistFileType.IsOpen = _controller.GetShouldShowFileTypeTeach(_discoveryContext!, (CmbPlaylistFileType.SelectedItem as BindableSelectionItem)!.ToSelectionItem<MediaFileType>()!);

    private void BtnPlaylistSelectAllItems_Click(object? sender, RoutedEventArgs e) => ListPlaylistItems.SelectAll();

    private void BtnPlaylistDeselectAllItems_Click(object? sender, RoutedEventArgs e) => ListPlaylistItems.DeselectAll();

    private void TglPlaylistNumberTitles_Toggled(object? sender, RoutedEventArgs e) => TeachPlaylistNumberTitles.IsOpen = _controller.GetShouldShowNumberTitlesTeach();

    private void BtnPlaylistRevertFilename_Click(object? sender, RoutedEventArgs e)
    {
        var index = (int)(sender as Button)!.Tag;
        if (ListPlaylistItems.ItemsSource is IReadOnlyList<BindableMediaSelectionItem> items)
        {
            items[index].Filename = items[index].Label;
        }
    }

    private void ListPlaylistItems_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var totalDuration = TimeSpan.Zero;
        foreach (var selectedItem in ListPlaylistItems.SelectedItems)
        {
            totalDuration += ((BindableMediaSelectionItem)selectedItem).Duration;
        }
        LblPlaylistItemsTime.Text = $"Общая длительность: {totalDuration}";
    }
}
