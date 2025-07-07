using ScreenCaptureLibrary.Core;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScreenCaptureLibrary.Platforms.Windows
{
    public class WindowsScreenCapturer: IScreenCapturer
    {
        public bool IsCapturing => throw new NotImplementedException();

        public event EventHandler<byte[]> FrameCaptured;
        public event EventHandler<Exception> CaptureError;

        public static Bitmap CaptureScreen()
        {
            var bounds = GetScreenBounds();
            var bitmap = new System.Drawing.Bitmap(bounds.Width, bounds.Height);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
            }

            return bitmap;
        }

        public static Bitmap CaptureWindow(IntPtr hWnd)
        {
            if (!GetWindowRect(hWnd, out var rect))
                throw new InvalidOperationException("Failed to get window bounds");

            var bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            var bitmap = new System.Drawing.Bitmap(bounds.Width, bounds.Height);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
            }

            return bitmap;
        }

        private static Rectangle GetScreenBounds()
        {
            return new Rectangle(
                0,
                0,
                GetSystemMetrics(0),  // SM_CXSCREEN
                GetSystemMetrics(1)); // SM_CYSCREEN
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);


        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Task<byte[]> CaptureScreenAsync()
        {
            throw new NotImplementedException();
        }

        public Task<byte[]> CaptureWindowAsync(nint windowHandle)
        {
            throw new NotImplementedException();
        }

        public Task<byte[]> CaptureRegionAsync(Rectangle region)
        {
            throw new NotImplementedException();
        }

        public Task StartContinuousCaptureAsync(int fps)
        {
            throw new NotImplementedException();
        }

        public Task StopContinuousCaptureAsync()
        {
            throw new NotImplementedException();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}

