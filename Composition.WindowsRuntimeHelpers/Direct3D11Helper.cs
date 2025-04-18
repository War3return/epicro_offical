﻿//  ---------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// 
//  The MIT License (MIT)
// 
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
// 
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.
//  ---------------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;

namespace Composition.WindowsRuntimeHelpers
{
    public static class Direct3D11Helper
    {
        static Guid IInspectable = new Guid("AF86E2E0-B12D-4c6a-9C5A-D7AA65101E90");
        static Guid ID3D11Resource = new Guid("dc8e63f3-d12b-4952-b47b-5e45026a862d");
        static Guid IDXGIAdapter3 = new Guid("645967A4-1392-4310-A798-8053CE3E93FD");
        static Guid ID3D11Device = new Guid("db6f6ddb-ac77-4e88-8253-819df9bbf140");
        static Guid ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

        [ComImport]
        [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [ComVisible(true)]
        interface IDirect3DDxgiInterfaceAccess
        {
            IntPtr GetInterface([In] ref Guid iid);
        };

        [DllImport(
            "d3d11.dll",
            EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall
            )]
        static extern UInt32 CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

        [DllImport(
            "d3d11.dll",
            EntryPoint = "CreateDirect3D11SurfaceFromDXGISurface",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall
            )]
        static extern UInt32 CreateDirect3D11SurfaceFromDXGISurface(IntPtr dxgiSurface, out IntPtr graphicsSurface);

        public static IDirect3DDevice CreateDevice()
        {
            return CreateDevice(false);
        }

        public static IDirect3DDevice CreateDevice(bool useWARP)
        {
            var d3dDevice = new SharpDX.Direct3D11.Device(
                useWARP ? SharpDX.Direct3D.DriverType.Software : SharpDX.Direct3D.DriverType.Hardware,
                SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport);
            var device = CreateDirect3DDeviceFromSharpDXDevice(d3dDevice);
            return device;
        }

        public static IDirect3DDevice CreateDirect3DDeviceFromSharpDXDevice(SharpDX.Direct3D11.Device d3dDevice)
        {
            IDirect3DDevice device = null;

            // Acquire the DXGI interface for the Direct3D device.
            using (var dxgiDevice = d3dDevice.QueryInterface<SharpDX.DXGI.Device3>())
            {
                // Wrap the native device using a WinRT interop object.
                uint hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr pUnknown);

                if (hr == 0)
                {
                    device = Marshal.GetObjectForIUnknown(pUnknown) as IDirect3DDevice;
                    Marshal.Release(pUnknown);
                }
            }

            return device;
        }

        public static IDirect3DSurface CreateDirect3DSurfaceFromSharpDXTexture(SharpDX.Direct3D11.Texture2D texture)
        {
            IDirect3DSurface surface = null;

            // Acquire the DXGI interface for the Direct3D surface.
            using (var dxgiSurface = texture.QueryInterface<SharpDX.DXGI.Surface>())
            {
                // Wrap the native device using a WinRT interop object.
                uint hr = CreateDirect3D11SurfaceFromDXGISurface(dxgiSurface.NativePointer, out IntPtr pUnknown);

                if (hr == 0)
                {
                    surface = Marshal.GetObjectForIUnknown(pUnknown) as IDirect3DSurface;
                    Marshal.Release(pUnknown);
                }
            }

            return surface;
        }

        public static SharpDX.Direct3D11.Device CreateSharpDXDevice(IDirect3DDevice device)
        {
            var access = (IDirect3DDxgiInterfaceAccess)device;
            var d3dPointer = access.GetInterface(ID3D11Device);
            var d3dDevice = new SharpDX.Direct3D11.Device(d3dPointer);
            return d3dDevice;
        }

        public static SharpDX.Direct3D11.Texture2D CreateSharpDXTexture2D(IDirect3DSurface surface)
        {
            var access = (IDirect3DDxgiInterfaceAccess)surface;
            var d3dPointer = access.GetInterface(ID3D11Texture2D);
            var d3dSurface = new SharpDX.Direct3D11.Texture2D(d3dPointer);
            return d3dSurface;
        }
       

        public static SoftwareBitmap CopyToSoftwareBitmap(SharpDX.Direct3D11.Texture2D texture)
        {
            var device = texture.Device;
            var desc = texture.Description;

            using (var staging = new SharpDX.Direct3D11.Texture2D(device, new SharpDX.Direct3D11.Texture2DDescription
            {
                CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.Read,
                BindFlags = SharpDX.Direct3D11.BindFlags.None,
                Format = desc.Format,
                Width = desc.Width,
                Height = desc.Height,
                OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = SharpDX.Direct3D11.ResourceUsage.Staging
            }))
            {
                device.ImmediateContext.CopyResource(texture, staging);
                var dataBox = device.ImmediateContext.MapSubresource(staging, 0, SharpDX.Direct3D11.MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, desc.Width, desc.Height, BitmapAlphaMode.Premultiplied);

                // Buffer로 변환
                var length = desc.Width * desc.Height * 4;
                byte[] buffer = new byte[length];
                Marshal.Copy(dataBox.DataPointer, buffer, 0, length);

                // IBuffer로 변환
                var ibuffer = WindowsRuntimeBufferExtensions.AsBuffer(buffer, 0, length);

                // 데이터 복사
                bitmap.CopyFromBuffer(ibuffer);

                device.ImmediateContext.UnmapSubresource(staging, 0);
                return bitmap;
            }
        }
        public static Bitmap ExtractBitmapFromTexture(Texture2D texture)
        {
            var device = texture.Device;
            var desc = texture.Description;

            var stagingDesc = new Texture2DDescription
            {
                Width = desc.Width,
                Height = desc.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = desc.Format,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CpuAccessFlags = CpuAccessFlags.Read,
                OptionFlags = ResourceOptionFlags.None
            };

            using (var staging = new Texture2D(device, stagingDesc))
            {
                device.ImmediateContext.CopyResource(texture, staging);

                var dataBox = device.ImmediateContext.MapSubresource(
                    staging, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                int width = desc.Width;
                int height = desc.Height;
                int stride = dataBox.RowPitch; // 중요! 실제 stride 사용

                // 새 Bitmap 생성
                var bitmap = new Bitmap(width, height, stride, PixelFormat.Format32bppArgb, dataBox.DataPointer);

                device.ImmediateContext.UnmapSubresource(staging, 0);

                // Bitmap 복사본 생성 (원본 포인터는 GPU 메모리이므로 해제 전 안전하게 복사)
                return new Bitmap(bitmap);
            }
        }
    }
}
