using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Foundation.Metadata;
using WinRT.Interop;
using System.Threading.Tasks;
using ScreenRecorder.Capture;
using ScreenRecorder.Capture.Utilities;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ScreenRecorder.UI;

class ResolutionItem
{
    public string DisplayName { get; set; }
    public SizeUInt32 Resolution { get; set; }

    public bool IsZero() { return Resolution.Width == 0 || Resolution.Height == 0; }
}
class BitrateItem
{
    public string DisplayName { get; set; }
    public uint Bitrate { get; set; }
}
class FrameRateItem
{
    public string DisplayName { get; set; }
    public uint FrameRate { get; set; }
}


public sealed partial class SettingsPage : Page
{
    private IDirect3DDevice _device;
    private AppSettings _appSettings;

    private List<ResolutionItem> _resolutions;
    private List<BitrateItem> _bitrates;
    private List<FrameRateItem> _frameRates;

    private CapturePreview _preview;
    private SpriteVisual _previewVisual;
    private CompositionSurfaceBrush _previewBrush;

    private string _sessionFullPath = string.Empty;

    public SettingsPage()
    {
        this.InitializeComponent();

        if (!GraphicsCaptureSession.IsSupported())
        {
            IsEnabled = false;

            ShowUnsupportedDialogAsync();
            return;
        }

        InitializePreviewVisuals();
        InitializeCaptureSettings();


    }

    public void CacheCurrentSettings()
    {
        var settings = GetCurrentSettings();
        CacheSettings(settings);
    }
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _sessionFullPath = (string)e.Parameter;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        StopPreview();
        CacheCurrentSettings();
        base.OnNavigatedFrom(e);
    }

    private void InitializePreviewVisuals()
    {
        var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        _previewBrush = compositor.CreateSurfaceBrush();
        _previewBrush.Stretch = CompositionStretch.Fill;

        var shadow = compositor.CreateDropShadow();
        shadow.Mask = _previewBrush;

        _previewVisual = compositor.CreateSpriteVisual();
        _previewVisual.RelativeSizeAdjustment = Vector2.One;
        _previewVisual.Brush = _previewBrush;
        _previewVisual.Shadow = shadow;

        ElementCompositionPreview.SetElementChildVisual(CapturePreviewCanvas, _previewVisual);
    }

    private void InitializeCaptureSettings()
    {
        _device = D3DDeviceManager.Device;

        _appSettings.GetCachedSettings();

        _resolutions = new List<ResolutionItem>();
        foreach (var resolution in EncoderPresets.Resolutions)
        {
            _resolutions.Add(new ResolutionItem()
            {
                DisplayName = $"{resolution.Width} x {resolution.Height}",
                Resolution = resolution,
            });
        }
        _resolutions.Add(new ResolutionItem()
        {
            DisplayName = "Use source size",
            Resolution = new SizeUInt32() { Width = 0, Height = 0 },
        });
        ResolutionComboBox.ItemsSource = _resolutions;
        ResolutionComboBox.SelectedIndex = GetResolutionIndex(_appSettings.Width, _appSettings.Height);

        _bitrates = new List<BitrateItem>();
        foreach (var bitrate in EncoderPresets.Bitrates)
        {
            var mbps = (float)bitrate / 1000000;
            _bitrates.Add(new BitrateItem()
            {
                DisplayName = $"{mbps:0.##} Mbps",
                Bitrate = bitrate,
            });
        }
        BitrateComboBox.ItemsSource = _bitrates;
        BitrateComboBox.SelectedIndex = GetBitrateIndex(_appSettings.Bitrate);

        _frameRates = new List<FrameRateItem>();
        foreach (var frameRate in EncoderPresets.FrameRates)
        {
            _frameRates.Add(new FrameRateItem()
            {
                DisplayName = $"{frameRate}fps",
                FrameRate = frameRate,
            });
        }
        FrameRateComboBox.ItemsSource = _frameRates;
        FrameRateComboBox.SelectedIndex = GetFrameRateIndex(_appSettings.FrameRate);


        if (ApiInformation.IsPropertyPresent(typeof(GraphicsCaptureSession).FullName, nameof(GraphicsCaptureSession.IsCursorCaptureEnabled)))
        {
            IncludeCursorCheckBox.Visibility = Visibility.Visible;
            IncludeCursorCheckBox.Checked += IncludeCursorCheckBox_Checked;
            IncludeCursorCheckBox.Unchecked += IncludeCursorCheckBox_Checked;
        }
        IncludeCursorCheckBox.IsChecked = _appSettings.IncludeCursor;
    }

    private void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            var item = await PickCaptureItemAsync(MainWindow.AppWindow);
            if (item != null)
            {
                StartPreview(item);
            }
            else
            {
                StopPreview();
            }
        });
    }

    private void IncludeCursorCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (_preview != null)
        {
            _preview.IsCursorCaptureEnabled = ((CheckBox)sender).IsChecked.Value;
        }
    }

    private void StartRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_preview == null)
        {
            throw new InvalidOperationException("There is no current preview!");
        }

        // Get our encoder properties
        var frameRateItem = (FrameRateItem)FrameRateComboBox.SelectedItem;
        var resolutionItem = (ResolutionItem)ResolutionComboBox.SelectedItem;
        var bitrateItem = (BitrateItem)BitrateComboBox.SelectedItem;

        var useSourceSize = resolutionItem.IsZero();
        var width = resolutionItem.Resolution.Width;
        var height = resolutionItem.Resolution.Height;
        var bitrate = bitrateItem.Bitrate;
        var frameRate = frameRateItem.FrameRate;
        var includeCursor = GetIncludeCursor();

        // Use the capture item's size for the encoding if desired
        if (useSourceSize)
        {
            var targetSize = _preview.Target.Size;
            width = (uint)targetSize.Width;
            height = (uint)targetSize.Height;
        }
        var resolution = new SizeUInt32() { Width = width, Height = height };

        var recordingOptions = new RecordingOptions(_preview.Target, resolution, bitrate, frameRate, includeCursor);

        this.StartRecordingButton.IsEnabled = false;

        this.Frame.Navigate(typeof(RecordingPage), recordingOptions);
    }

    private async Task<GraphicsCaptureItem> PickCaptureItemAsync(Window window)
    {
        var picker = new GraphicsCapturePicker();
        InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        return await picker.PickSingleItemAsync();
    }

    private async void ShowUnsupportedDialogAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Screen capture unsupported",
            Content = "Screen capture is not supported on this device for this release of Windows!",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot // Ensure the ContentDialog is associated with the correct XamlRoot
        };

        await dialog.ShowAsync();
    }

    private void StartPreview(GraphicsCaptureItem item)
    {
        PreviewContainerGrid.RowDefinitions[0].Height = new GridLength(2, GridUnitType.Star);
        CaptureInfoTextBlock.Text = item.DisplayName;

        var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;

        _preview?.Dispose();
        // TODO: Getting error _device - dispose object cannot be used
        _preview = new CapturePreview(_device, item);

        var surface = _preview.CreateSurface(compositor);
        _previewBrush.Surface = surface;

        // Create or reuse a SpriteVisual
        if (_previewVisual == null)
        {
            _previewVisual = compositor.CreateSpriteVisual();
            
        }
        _previewVisual.Brush = _previewBrush;

        _preview.SetFrameSize((float)CapturePreviewCanvas.ActualWidth, (float)CapturePreviewCanvas.ActualHeight);

        // Attach the visual to the Canvas
        ElementCompositionPreview.SetElementChildVisual(CapturePreviewCanvas, _previewVisual);

        _preview.StartCapture();
        var includeCursor = GetIncludeCursor();
        if (!includeCursor)
        {
            _preview.IsCursorCaptureEnabled = includeCursor;
        }

        StartRecordingButton.IsEnabled = true;
    }

    private void StopPreview()
    {
        PreviewContainerGrid.RowDefinitions[0].Height = new GridLength(0);
        //CapturePreviewCanvas.Visibility = Visibility.Collapsed;
        CaptureInfoTextBlock.Text = "Pick something to capture";
        _preview?.Dispose();
        _preview = null;

        StartRecordingButton.IsEnabled = false;
    }

    // TODO: Unit tests for these
    private bool GetIncludeCursor()
    {
        if (IncludeCursorCheckBox.Visibility == Visibility.Visible)
        {
            return IncludeCursorCheckBox.IsChecked.Value;
        }
        return true;
    }

    private int GetResolutionIndex(uint width, uint height)
    {
        for (var i = 0; i < _resolutions.Count; i++)
        {
            var resolution = _resolutions[i];
            if (resolution.Resolution.Width == width &&
                resolution.Resolution.Height == height)
            {
                return i;
            }
        }
        return 0;
    }

    private int GetBitrateIndex(uint bitrate)
    {
        for (var i = 0; i < _bitrates.Count; i++)
        {
            if (_bitrates[i].Bitrate == bitrate)
            {
                return i;
            }
        }
        return 0;
    }

    private int GetFrameRateIndex(uint frameRate)
    {
        for (var i = 0; i < _frameRates.Count; i++)
        {
            if (_frameRates[i].FrameRate == frameRate)
            {
                return i;
            }
        }
        return 0;
    }
    
    private AppSettings GetCurrentSettings()
    {
        var resolutionItem = (ResolutionItem)ResolutionComboBox.SelectedItem;
        var width = resolutionItem.Resolution.Width;
        var height = resolutionItem.Resolution.Height;
        var bitrateItem = (BitrateItem)BitrateComboBox.SelectedItem;
        var bitrate = bitrateItem.Bitrate;
        var frameRateItem = (FrameRateItem)FrameRateComboBox.SelectedItem;
        var frameRate = frameRateItem.FrameRate;
        var includeCursor = GetIncludeCursor();

        return new AppSettings { Width = width, Height = height, Bitrate = bitrate, FrameRate = frameRate, IncludeCursor = includeCursor };
    }

    private static void CacheSettings(AppSettings settings)
    {
        var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        localSettings.Values[nameof(AppSettings.Width)] = settings.Width;
        localSettings.Values[nameof(AppSettings.Height)] = settings.Height;
        localSettings.Values[nameof(AppSettings.Bitrate)] = settings.Bitrate;
        localSettings.Values[nameof(AppSettings.FrameRate)] = settings.FrameRate;
        localSettings.Values[nameof(AppSettings.IncludeCursor)] = settings.IncludeCursor;
    }
}
