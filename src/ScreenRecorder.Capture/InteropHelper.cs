using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace ScreenRecorder.Capture;
internal static class InteropHelper
{
    // The function signature as documented by Microsoft:
    // HRESULT CreateDirect3D11DeviceFromDXGIDevice(
    //    IUnknown* dxgiDevice,
    //    IInspectable** graphicsDevice
    // );
    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", PreserveSig = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(
        nint dxgiDevice,
        out nint graphicsDevice);

    /// <summary>
    /// Wraps a DXGI device (IDXGIDevice) into a IDirect3DDevice via the system-provided interop function.
    /// </summary>
    public static IDirect3DDevice CreateDirect3DDeviceFromDXGIDevice(nint dxgiDevice)
    {
        int hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out nint pUnknown);
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        // Convert the returned IInspectable to a managed object. This should implement IDirect3DDevice.
        object direct3DDeviceObj = MarshalInspectable<object>.FromAbi(pUnknown);

        // Dispose the raw pointer now that we have a managed object reference.
        MarshalInspectable<object>.DisposeAbi(pUnknown);

        // Cast to IDirect3DDevice
        return (IDirect3DDevice)direct3DDeviceObj;
    }
}
