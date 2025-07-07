using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenCaptureLibrary
{
    public interface ICaptureController
    {
        IScreenCapturer ScreenCapturer { get; }
        IAudioCapturer AudioCapturer { get; }

        Task<CaptureResult> CaptureWithAudioAsync(int durationMs);
    }

    public record CaptureResult(byte[] VideoData, byte[] AudioData);

    public record DisplayInfo(int Index, string Name, Rectangle Bounds);
}
