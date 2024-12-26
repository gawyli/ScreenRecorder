using System;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.DirectX.Direct3D11;

namespace ScreenRecorder.Capture;
public class Direct3DDeviceHelper : IDisposable
{
    private ID3D11Device _d3dDevice;
    private ID3D11DeviceContext _d3dContext;
    private IDXGIDevice _dxgiDevice;
    private IDirect3DDevice _direct3DDevice;

    public Direct3DDeviceHelper()
    {
        CreateD3DDevice();
        CreateIDirect3DDevice();
    }

    /// <summary>
    /// Gets the underlying D3D11 device.
    /// </summary>
    public ID3D11Device D3DDevice => _d3dDevice;

    /// <summary>
    /// Gets the interop IDirect3DDevice that can be used with WinRT APIs (like GraphicsCapture).
    /// </summary>
    public IDirect3DDevice Direct3DDevice => _direct3DDevice;

    private void CreateD3DDevice()
    {
        // Create the D3D11 device with BGRA support (needed for Direct2D, Composition, etc.)
        // Also request video support if desired (optional).
        var creationFlags = DeviceCreationFlags.BgraSupport;

#if DEBUG
        creationFlags |= DeviceCreationFlags.Debug;
#endif

        var featureLevels = new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 };
        var result = D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            creationFlags,
            featureLevels,
            out _d3dDevice,
            out FeatureLevel _featureLevel,
            out _d3dContext
        );

        if (result.Failure)
        {
            throw new Exception("Failed to create D3D11 device.");
        }

        // Get the DXGI device
        _dxgiDevice = _d3dDevice.QueryInterface<IDXGIDevice>();
    }

    private void CreateIDirect3DDevice()
    {
        // Now wrap our DXGI device in a IDirect3DDevice using the Windows function.
        nint nativePtr = _dxgiDevice.NativePointer;
        _direct3DDevice = InteropHelper.CreateDirect3DDeviceFromDXGIDevice(nativePtr);
    }

    public void Dispose()
    {
        _direct3DDevice = null;
        _dxgiDevice?.Dispose();
        _d3dContext?.Dispose();
        _d3dDevice?.Dispose();
    }
}
