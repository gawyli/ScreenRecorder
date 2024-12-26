using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.DirectX.Direct3D11;
using System.Runtime.InteropServices;
using ScreenRecorder.Capture;

namespace ScreenRecorder.UI;
public static class D3DDeviceManager
{
    private static IDirect3DDevice GlobalDevice;
    public static IDirect3DDevice Device
    {
        get
        {
            // This initialization isn't thread safe, so make sure this 
            // happens well before everyone starts needing it.
            if (GlobalDevice == null)
            {
                var device = new Direct3DDeviceHelper();

                GlobalDevice = device.Direct3DDevice;

            }
            return GlobalDevice;
        }

    }

}
