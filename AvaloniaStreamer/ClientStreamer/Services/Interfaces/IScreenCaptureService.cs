using System.Threading.Tasks;
using System.Threading;
using Avalonia.Media.Imaging;

public interface IScreenCaptureService
{
    Task<Bitmap> CaptureScreenAsync(CancellationToken ct);
}