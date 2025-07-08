using Avalonia.Media.Imaging;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ScreenCaptureLibrary.Platforms.Windows;

namespace ClientStreamer.Services
{
    public class ScreenCaptureService:IScreenCaptureService
    {
        public async Task<Bitmap> CaptureScreenAsync(CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                using var screenBmp = WindowsScreenCapturer.CaptureScreen();
                using var ms = new MemoryStream();
                screenBmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;

                return new Bitmap(ms);
            }, ct);
        }
    }
}