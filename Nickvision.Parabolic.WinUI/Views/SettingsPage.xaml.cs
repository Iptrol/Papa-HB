using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using Nickvision.Desktop.Globalization;
using Nickvision.Desktop.WinUI.Helpers;
using Nickvision.Parabolic.Shared.Controllers;
using Nickvision.Parabolic.Shared.Models;
using Nickvision.Parabolic.WinUI.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    private readonly PreferencesViewController _controller;
    private readonly ITranslationService _translationService;
    private bool _constructing;

    public WindowId? WindowId { get; set; }

    public SettingsPage(PreferencesViewController controller, ITranslationService translationService)
    {
        InitializeComponent();
        _controller = controller;
        _translationService = translationService;
        _constructing = true;
        RowTheme.Header = _translationService._("Theme");
        CmbTheme.ItemsSource = _controller.Themes.ToBindableSelectonItems();
        RowPreventSuspend.Header = _translationService._("Prevent Suspend");
        RowPreventSuspend.Description = _translationService._("Prevent the computer from sleeping while downloads are running");
        TglPreventSuspend.OnContent = _translationService._("On");
        TglPreventSuspend.OffContent = _translationService._("Off");
        RowHistoryLength.Header = _translationService._("Download History Length");
        RowHistoryLength.Description = _translationService._("The amount of time to keep past downloads in the app's history");
        CmbHistoryLength.ItemsSource = _controller.HistoryLengths.ToBindableSelectonItems();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        CmbTheme.SelectSelectionItem();
        TglPreventSuspend.IsOn = _controller.PreventSuspend;
        CmbHistoryLength.SelectSelectionItem();
        _constructing = false;
    }

    private async void Cmb_SelectionChanged(object? sender, SelectionChangedEventArgs e) => await ApplyChangesAsync();

    private async void Tgl_Toggled(object? sender, RoutedEventArgs e) => await ApplyChangesAsync();

    private async Task ApplyChangesAsync()
    {
        if (_constructing)
        {
            return;
        }
        _controller.Theme = (CmbTheme.SelectedItem as BindableSelectionItem)!.ToSelectionItem<Theme>()!;
        _controller.PreventSuspend = TglPreventSuspend.IsOn;
        _controller.HistoryLength = (CmbHistoryLength.SelectedItem as BindableSelectionItem)!.ToSelectionItem<HistoryLength>()!;
    }
}
