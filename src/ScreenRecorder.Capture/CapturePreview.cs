using System;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Microsoft.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Microsoft.UI.Composition;
using Windows.Devices.HumanInterfaceDevice;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Microsoft.Graphics.Canvas;
using Vortice.DXGI;
using Vortice.Direct3D11;
using WinRT;
using Microsoft.Graphics.Canvas.UI.Composition;
using Microsoft.UI;

namespace ScreenRecorder.Capture;
public sealed class CapturePreview : IDisposable
{
    private GraphicsCaptureItem _item;
    private Direct3D11CaptureFramePool _framePool;
    private GraphicsCaptureSession _session;
    private CompositionDrawingSurface _drawingSurface;
    private SpriteVisual _previewVisual;
    private SizeInt32 _lastSize;
    private bool _includeCursor = true;

    private IDirect3DDevice _device;
    private CanvasDevice _canvasDevice;

    private Rect _destRect, _srcRect;
    private CanvasBitmap _frameBitmap;

    public CapturePreview(IDirect3DDevice device, GraphicsCaptureItem item)
    {
        _item = item;
        _device = device;

        // Create a Win2D CanvasDevice from the IDirect3DDevice
        _canvasDevice = CanvasDevice.CreateFromDirect3D11Device(_device);

        SizeInt32 size = item.Size;
        _framePool = Direct3D11CaptureFramePool.Create(
            _device,
            Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
            2,
            _item.Size);

        _session = _framePool.CreateCaptureSession(_item);
        _framePool.FrameArrived += OnFrameArrived;

        _session.IsCursorCaptureEnabled = IsCursorCaptureEnabled;
    }

    public GraphicsCaptureItem Target => _item;

    public bool IsCursorCaptureEnabled
    {
        get { return _includeCursor; }
        set
        {
            if (_includeCursor != value)
            {
                _includeCursor = value;
                _session.IsCursorCaptureEnabled = _includeCursor;
            }
        }
    }

    public void StartCapture()
    {
        _session.StartCapture();
    }

    /// <summary>
    /// Creates a CompositionDrawingSurface that will serve as the target for our captured frames.
    /// No SwapChain is used; we rely on Win2D and Composition APIs.
    /// </summary>
    public ICompositionSurface CreateSurface(Compositor compositor)
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

    public void Dispose()
    {
        _session?.Dispose();
        _framePool?.Dispose();
        _canvasDevice?.Dispose();

        _drawingSurface = null;
        _session = null;
        _framePool = null;
        _canvasDevice = null;
        _item = null;
    }

    public void SetFrameSize(float width, float height)
    {
        // Suppose we want to draw into a 640x360 area, regardless of the source size:
        _destRect = new Rect(0, 0, width, height);

        var drawingSurfaceSize = new SizeInt32((int)_destRect.Width, (int)_destRect.Height);

        _drawingSurface.Resize(drawingSurfaceSize);
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        using var frame = sender.TryGetNextFrame();
        if (frame == null) return;

        var surface = frame.Surface; // This is an IDirect3DSurface containing the captured frame

        // Turn the captured frame into a CanvasBitmap
        _frameBitmap = CanvasBitmap.CreateFromDirect3D11Surface(_canvasDevice, surface, 50);

        // Draw the captured frame onto our CompositionDrawingSurface
        using var ds = CanvasComposition.CreateDrawingSession(_drawingSurface);
        ds.Clear(Colors.Transparent);
        ds.DrawImage(_frameBitmap, _destRect, _frameBitmap.Bounds);
    }
}
