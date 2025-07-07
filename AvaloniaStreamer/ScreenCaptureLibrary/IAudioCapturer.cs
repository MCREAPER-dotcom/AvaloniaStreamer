using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenCaptureLibrary
{
    public interface IAudioCapturer : IDisposable
    {
        Task<byte[]> CaptureAudioAsync(int durationMs);
        Task StartAudioCaptureAsync();
        Task StopAudioCaptureAsync();

        event EventHandler<byte[]> AudioDataAvailable;
        event EventHandler<Exception> AudioError;
    }
}
