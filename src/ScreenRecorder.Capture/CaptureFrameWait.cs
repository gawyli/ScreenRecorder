using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using System.Threading;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Core;
using Microsoft.UI;

namespace ScreenRecorder.Capture;

public sealed class SurfaceWithInfo : IDisposable
{
    public IDirect3DSurface Surface { get; internal set; }
    public TimeSpan SystemRelativeTime { get; internal set; }

    public void Dispose()
    {
        Surface?.Dispose();
        Surface = null;
    }
}

public sealed class CaptureFrameWait : IDisposable
{
    private readonly object _frameLock = new object();

    private IDirect3DDevice _device;

    private WaitHandle[] _events;
    private ManualResetEvent _frameEvent;
    private ManualResetEvent _closedEvent;
    private Direct3D11CaptureFrame _currentFrame;
    private CanvasDevice _canvasDevice;
    private GraphicsCaptureItem _item;
    private GraphicsCaptureSession _session;
    private Direct3D11CaptureFramePool _framePool;
    private CanvasRenderTarget _renderTarget;

    private bool _isDisposed;

    public CaptureFrameWait(
        IDirect3DDevice device,
        GraphicsCaptureItem item,
        SizeInt32 size,
        bool includeCursor)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _item = item ?? throw new ArgumentNullException(nameof(item));
        _frameEvent = new ManualResetEvent(false);
        _closedEvent = new ManualResetEvent(false);
        _events = new[] { _closedEvent, _frameEvent };
        _canvasDevice = CanvasDevice.CreateFromDirect3D11Device(device);

        InitializeCapture(size, includeCursor);
    }

    public GraphicsCaptureSession StartCapture()
    {
        _session.StartCapture();
        return _session;
    }

    public SurfaceWithInfo WaitForNewFrame()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(CaptureFrameWait));

        // Let's get a fresh one.
        _currentFrame?.Dispose();
        _frameEvent.Reset();

        var signaledEvent = _events[WaitHandle.WaitAny(_events)];
        if (signaledEvent == _closedEvent)
        {
            Dispose();
            return null;
        }

        var result = new SurfaceWithInfo();
        result.SystemRelativeTime = _currentFrame.SystemRelativeTime;
        result.Surface = _currentFrame.Surface;

        return result;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _item.Closed -= OnClosed;
        _framePool.FrameArrived -= OnFrameArrived;

        //_session?.Dispose();
        _framePool?.Dispose();
        _frameEvent.Dispose();
        _closedEvent.Dispose();
        _canvasDevice.Dispose();

    }

    private void InitializeCapture(SizeInt32 size, bool includeCursor)
    {
        _item.Closed += OnClosed;
        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _device,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            1,
            size);

        _session = _framePool.CreateCaptureSession(_item);
        _framePool.FrameArrived += OnFrameArrived;

        _session.IsCursorCaptureEnabled = includeCursor;
    }

    private void SetResult(Direct3D11CaptureFrame frame)
    {
        lock (_frameLock)
        {
            _currentFrame = frame;
            _frameEvent.Set();
        }
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        try
        {
            var frame = sender.TryGetNextFrame();
            if (frame != null)
            {
                SetResult(frame);

            }
        }
        catch (Exception)
        {
            Stop();
        }
    }

    private void OnClosed(GraphicsCaptureItem sender, object args)
    {
        Stop();
    }

    private void Stop()
    {
        _closedEvent.Set();
    }

    private void Cleanup()
    {
        _framePool?.Dispose();
        _session?.Dispose();
        if (_item != null)
        {
            _item.Closed -= OnClosed;
        }
        _item = null;
        _device = null;

        lock (_frameLock)
        {
            _currentFrame?.Dispose();
        }
    }

}
