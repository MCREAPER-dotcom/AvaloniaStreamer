using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenCaptureLibrary
{
    public interface IScreenCapturer : IDisposable
    {
        Task<byte[]> CaptureScreenAsync();
        Task<byte[]> CaptureWindowAsync(IntPtr windowHandle);
        Task<byte[]> CaptureRegionAsync(Rectangle region);

        event EventHandler<byte[]> FrameCaptured;
        event EventHandler<Exception> CaptureError;

        bool IsCapturing { get; }
        Task StartContinuousCaptureAsync(int fps);
        Task StopContinuousCaptureAsync();
    }
}
