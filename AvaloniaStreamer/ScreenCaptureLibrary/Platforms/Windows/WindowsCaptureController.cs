using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenCaptureLibrary.Platforms.Windows
{
    public class WindowsCaptureController : ICaptureController
    {
        public IScreenCapturer ScreenCapturer { get; }
        public IAudioCapturer AudioCapturer { get; }

        public WindowsCaptureController()
        {
            ScreenCapturer = new WindowsScreenCapturer();
            AudioCapturer = new WindowsAudioCapturer();
        }

        public async Task<CaptureResult> CaptureWithAudioAsync(int durationMs)
        {
            var screenTask = ScreenCapturer.CaptureScreenAsync();
            var audioTask = AudioCapturer.CaptureAudioAsync(durationMs);

            await Task.WhenAll(screenTask, audioTask);

            return new CaptureResult(screenTask.Result, audioTask.Result);
        }
    }
}
