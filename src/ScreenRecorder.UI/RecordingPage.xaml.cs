using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Popups;
using ScreenRecorder.Capture;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ScreenRecorder.UI;

public class RecordingOptions
{
    public GraphicsCaptureItem Target { get; }
    public SizeUInt32 Resolution { get; }
    public uint Bitrate { get; }
    public uint FrameRate { get; }
    public bool IncludeCursor { get; }

    public RecordingOptions(GraphicsCaptureItem target, SizeUInt32 resolution, uint bitrate, uint frameRate, bool includeCursor)
    {
        Target = target;
        Resolution = resolution;
        Bitrate = bitrate;
        FrameRate = frameRate;
        IncludeCursor = includeCursor;
    }
}

public enum RecordingState
{
    Recording,
    Done,
    Interrupted,
    Failed
}

public sealed partial class RecordingPage : Page
{
    private IDirect3DDevice _device;
    private Encoder _encoder;
    private CompositionSurfaceBrush _previewBrush;
    private SpriteVisual _previewVisual;
    private StorageFile _file;

    public RecordingPage()
    {
        this.InitializeComponent();

        InitializePreviewVisuals();
        InitializeCaptureSettings();

    }

    public void EndCurrentRecording()
    {
        _encoder.Dispose();
        _previewBrush.Dispose();
    }

    protected async override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _device = D3DDeviceManager.Device;

        var options = (RecordingOptions)e.Parameter;
        await StartRecordingAsync(options);
    }

    private void InitializePreviewVisuals()
    {
        var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        _previewVisual = compositor.CreateSpriteVisual();
        _previewVisual.RelativeSizeAdjustment = Vector2.One;
        _previewVisual.Size = new Vector2(-30.0f, -30.0f);
        _previewVisual.RelativeOffsetAdjustment = new Vector3(0.5f, 0.5f, 0);
        _previewVisual.AnchorPoint = new Vector2(0.5f, 0.5f);

        _previewBrush = compositor.CreateSurfaceBrush();
        _previewVisual.Brush = _previewBrush;
        ElementCompositionPreview.SetElementChildVisual(PreviewCanvas, _previewVisual);
    }

    private void InitializeCaptureSettings()
    {
    }

    private void StopRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        _encoder?.Dispose();
        _previewBrush.Dispose();

        this.Frame.Navigate(typeof(SavePage), _file);
    }

    private async Task StartRecordingAsync(RecordingOptions options)
    {
        // Find a place to put our vidoe for now
        _file = await GetTempFileAsync();

        // Kick off the encoding
        try
        {
            await StartRecording(options, _file);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            Debug.WriteLine(ex);

            var message = GetMessageForHResult(ex.HResult);
            if (message == null)
            {
                message = $"Uh-oh! Something went wrong!\n0x{ex.HResult:X8} - {ex.Message}";
            }
            var dialog = new MessageDialog(
                message,
                "Recording failed");

            await dialog.ShowAsync();

            // Go back to the main page
            Frame.GoBack();
            return;
        }

        EndCurrentRecording();
        // At this point the encoding has finished, let the user preview the file
        Frame.Navigate(typeof(SavePage), _file);
    }

    private async Task StartRecording(RecordingOptions options, StorageFile file)
    {
        // Encoders generally like even numbers
        var width = EnsureEven(options.Resolution.Width);
        var height = EnsureEven(options.Resolution.Height);

        var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;

        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        using (_encoder = new Encoder(_device, options.Target))
        {
            var surface = _encoder.CreatePreviewSurface(compositor);
            _previewBrush.Surface = surface;

            // Create or reuse a SpriteVisual
            if (_previewVisual == null)
            {
                _previewVisual = compositor.CreateSpriteVisual();

            }
            _previewVisual.Brush = _previewBrush;

            _encoder.SetPreviewFrameSize((float)PreviewCanvas.ActualWidth, (float)PreviewCanvas.ActualHeight);

            // Attach the visual to the Canvas
            ElementCompositionPreview.SetElementChildVisual(PreviewCanvas, _previewVisual);

            await _encoder.EncodeAsync(
                stream,
                width, height, options.Bitrate,
                options.FrameRate,
                options.IncludeCursor);
        }
    }

    private uint EnsureEven(uint number)
    {
        if (number % 2 == 0)
        {
            return number;
        }
        else
        {
            return number + 1;
        }
    }

    private async Task<StorageFile> GetTempFileAsync()
    {
        var folder = ApplicationData.Current.TemporaryFolder;
        var name = DateTime.Now.ToString("yyyyMMdd-HHmm-ss");
        var file = await folder.CreateFileAsync($"{name}.mp4");
        return file;
    }

    private string GetMessageForHResult(int hresult)
    {
        switch ((uint)hresult)
        {
            // MF_E_TRANSFORM_TYPE_NOT_SET
            case 0xC00D6D60:
                return "The combination of options you've chosen are not supported by your hardware.";
            default:
                return null;
        }
    }
}
