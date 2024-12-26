using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Foundation;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Storage.Streams;
using Microsoft.UI.Composition;
using Microsoft.Graphics.Canvas.UI.Composition;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.DirectX;
using Windows.Graphics;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;

namespace ScreenRecorder.Capture;

public struct SizeUInt32
{
    public uint Width;
    public uint Height;
}

// Presets are made to match MediaEncodingProfile for ease of use
public static class EncoderPresets
{
    public static SizeUInt32[] Resolutions => new SizeUInt32[]
    {
        new SizeUInt32() { Width = 1280, Height = 720 },
        new SizeUInt32() { Width = 1920, Height = 1080 },
        new SizeUInt32() { Width = 3840, Height = 2160 },
        new SizeUInt32() { Width = 7680, Height = 4320 }
    };

    public static uint[] Bitrates => new uint[]
    {
        9000000,
        18000000,
        36000000,
        72000000,
    };

    public static uint[] FrameRates => new uint[]
    {
        24,
        30,
        60
    };
}

public sealed class Encoder : IDisposable
{
    public string SessionId { get; set; } = null!;

    private IDirect3DDevice _device;

    private GraphicsCaptureItem _captureItem;
    private CaptureFrameWait _frameGenerator;

    private VideoStreamDescriptor _videoDescriptor;
    private MediaStreamSource _mediaStreamSource;
    private MediaTranscoder _transcoder;
    private bool _isRecording;

    private bool _isPreviewing = false;
    private object _previewLock;
    private EncoderPreview _preview;

    private bool _closed = false;

    public Encoder(IDirect3DDevice device, GraphicsCaptureItem item)
    {
        _device = device;
        _captureItem = item;
        _isRecording = false;
        _previewLock = new object();

        CreateMediaObjects();
    }

    public ICompositionSurface CreatePreviewSurface(Compositor compositor)
    {
        if (!_isPreviewing)
        {
            lock (_previewLock)
            {
                if (!_isPreviewing)
                {
                    _preview = new EncoderPreview(_device, _captureItem);
                    _isPreviewing = true;
                }
            }
        }

        return _preview.CreateCompositionSurface(compositor);
    }

    public void SetPreviewFrameSize(float width, float height)
    {
        if (_isPreviewing)
        {
            _preview.SetFrameSize(width, height);
        }
    }

    public IAsyncAction EncodeAsync(IRandomAccessStream stream, uint width, uint height, uint bitrateInBps, uint frameRate, bool includeCursor)
    {
        return EncodeInternalAsync(stream, width, height, bitrateInBps, frameRate, includeCursor).AsAsyncAction();
    }

    public void Dispose()
    {
        if (_closed)
        {
            return;
        }
        _closed = true;

        if (!_isRecording)
        {
            DisposeInternal();
        }

        _isRecording = false;
    }

    private async Task EncodeInternalAsync(IRandomAccessStream stream, uint width, uint height, uint bitrateInBps, uint frameRate, bool includeCursor)
    {
        if (!_isRecording)
        {
            _isRecording = true;

            _frameGenerator = new CaptureFrameWait(
                _device,
                _captureItem,
                _captureItem.Size,
                includeCursor);

            var session = _frameGenerator.StartCapture();
            SessionId = $"session{Guid.NewGuid()}";

            using (_frameGenerator)
            {
                var encodingProfile = new MediaEncodingProfile();
                encodingProfile.Container.Subtype = "MPEG4";
                encodingProfile.Video.Subtype = "H264";
                encodingProfile.Video.Width = width;
                encodingProfile.Video.Height = height;
                encodingProfile.Video.Bitrate = bitrateInBps;
                encodingProfile.Video.FrameRate.Numerator = frameRate;
                encodingProfile.Video.FrameRate.Denominator = 1;
                encodingProfile.Video.PixelAspectRatio.Numerator = 1;
                encodingProfile.Video.PixelAspectRatio.Denominator = 1;

                // Start the transcoding process

                var transcode = await _transcoder.PrepareMediaStreamSourceTranscodeAsync(_mediaStreamSource, stream, encodingProfile);
                //var transcodeTask = transcode.TranscodeAsync().AsTask();

                //// Capture and save frames in a loop
                //while (_isRecording)
                //{
                //    var frame = _frameGenerator.WaitForNewFrame();
                //    if (frame != null)
                //    {
                //        await SaveFrameAsImageAsync(frame.Surface);
                //    }

                //    // Check if transcoding is complete
                //    if (transcodeTask.IsCompleted)
                //    {
                //        break;
                //    }
                //}

                //await transcodeTask;

                await transcode.TranscodeAsync();
            }

            session.Dispose();
            SessionId = string.Empty;
        }
    }

    private void DisposeInternal()
    {
        _frameGenerator.Dispose();
        _preview?.Dispose();
    }

    private void CreateMediaObjects()
    {
        // Create our encoding profile based on the size of the item
        int width = _captureItem.Size.Width;
        int height = _captureItem.Size.Height;

        // Describe our input: uncompressed BGRA8 buffers
        var videoProperties = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, (uint)width, (uint)height);
        _videoDescriptor = new VideoStreamDescriptor(videoProperties);

        // Create our MediaStreamSource
        _mediaStreamSource = new MediaStreamSource(_videoDescriptor);
        _mediaStreamSource.BufferTime = TimeSpan.FromSeconds(0);
        _mediaStreamSource.Starting += OnMediaStreamSourceStarting;
        _mediaStreamSource.SampleRequested += OnMediaStreamSourceSampleRequested;

        // Create our transcoder
        _transcoder = new MediaTranscoder();
        _transcoder.HardwareAccelerationEnabled = true;
    }

    private void OnMediaStreamSourceSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
    {
        if (_isRecording && !_closed)
        {
            try
            {
                using (var frame = _frameGenerator.WaitForNewFrame())
                {
                    if (frame == null)
                    {
                        args.Request.Sample = null;
                        DisposeInternal();
                        return;
                    }

                    if (_isPreviewing)
                    {
                        lock (_previewLock)
                        {
                            _preview.PresentSurface(frame.Surface);
                        }
                    }

                    var timeStamp = frame.SystemRelativeTime;
                    var sample = MediaStreamSample.CreateFromDirect3D11Surface(frame.Surface, timeStamp);
                    args.Request.Sample = sample;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);
                Debug.WriteLine(e);
                args.Request.Sample = null;
                DisposeInternal();
            }
        }
        else
        {
            args.Request.Sample = null;
            DisposeInternal();
        }
    }

    private void OnMediaStreamSourceStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
    {
        using (var frame = _frameGenerator.WaitForNewFrame())
        {
            args.Request.SetActualStartPosition(frame.SystemRelativeTime);
        }
    }

    private async Task SaveFrameAsImageAsync(IDirect3DSurface surface)
    {
        var canvasDevice = CanvasDevice.CreateFromDirect3D11Device(_device);
        var bitmap = CanvasBitmap.CreateFromDirect3D11Surface(canvasDevice, surface);

        // Create the folder structure
        var rootFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
        var sessionsFolder = await rootFolder.CreateFolderAsync("Sessions", Windows.Storage.CreationCollisionOption.OpenIfExists);
        var sessionFolder = await sessionsFolder.CreateFolderAsync(SessionId, Windows.Storage.CreationCollisionOption.OpenIfExists);
        var imagesFolder = await sessionFolder.CreateFolderAsync("Images", Windows.Storage.CreationCollisionOption.OpenIfExists);

        // Generate the file name
        var fileName = $"image{Guid.NewGuid()}.jpg";
        var file = await imagesFolder.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.GenerateUniqueName);

        using var fileStream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
        await bitmap.SaveAsync(fileStream, CanvasBitmapFileFormat.Png);
    }

    private class EncoderPreview : IDisposable
    {
        private IDirect3DDevice _device;
        private CanvasDevice _canvasDevice;
        private readonly GraphicsCaptureItem _item;
        private CompositionDrawingSurface _drawingSurface;

        private Rect _destRect;
        private CanvasBitmap _frameBitmap;

        public EncoderPreview(IDirect3DDevice device, GraphicsCaptureItem item)
        {
            _item = item;
            _device = device;

            _canvasDevice = CanvasDevice.CreateFromDirect3D11Device(_device);
        }

        public ICompositionSurface CreateCompositionSurface(Compositor compositor)
        {
            if (compositor == null) throw new ArgumentNullException(nameof(compositor));

            // Create a CompositionGraphicsDevice from our CanvasDevice
            var compGraphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(compositor, _canvasDevice);

            var directXPixelFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized;
            var directXAlphaMode = DirectXAlphaMode.Premultiplied;

            // Create a CompositionDrawingSurface sized to the capture item
            _drawingSurface = compGraphicsDevice.CreateDrawingSurface(
                new Size(_item.Size.Width, _item.Size.Height),
                directXPixelFormat,
                directXAlphaMode);

            return _drawingSurface;
        }

        public void PresentSurface(IDirect3DSurface surface)
        {
            // Turn the captured frame into a CanvasBitmap
            _frameBitmap = CanvasBitmap.CreateFromDirect3D11Surface(_canvasDevice, surface, 50);

            // Draw the captured frame onto our CompositionDrawingSurface
            using var ds = CanvasComposition.CreateDrawingSession(_drawingSurface);
            ds.Clear(Colors.Transparent);
            ds.DrawImage(_frameBitmap, _destRect, _frameBitmap.Bounds);
        }

        public void SetFrameSize(float width, float height)
        {
            // Suppose we want to draw into a 640x360 area, regardless of the source size:
            _destRect = new Rect(0, 0, width, height);

            var drawingSurfaceSize = new SizeInt32((int)_destRect.Width, (int)_destRect.Height);

            _drawingSurface.Resize(drawingSurfaceSize);
        }

        public void Dispose()
        {
            _device.Dispose();
            //_drawingSurface.Dispose();
        }

    }

}


