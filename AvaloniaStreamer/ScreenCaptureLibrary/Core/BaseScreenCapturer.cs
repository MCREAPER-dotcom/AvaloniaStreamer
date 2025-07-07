using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenCaptureLibrary.Core
{
    public abstract class BaseScreenCapturer : IScreenCapturer
    {
        public event EventHandler<byte[]> FrameCaptured;
        public event EventHandler<Exception> CaptureError;

        public abstract bool IsCapturing { get; protected set; }

        public abstract Task<byte[]> CaptureScreenAsync();
        public abstract Task<byte[]> CaptureWindowAsync(IntPtr windowHandle);
        public abstract Task<byte[]> CaptureRegionAsync(Rectangle region);

        public abstract Task StartContinuousCaptureAsync(int fps);
        public abstract Task StopContinuousCaptureAsync();

        protected virtual void OnFrameCaptured(byte[] frame) => FrameCaptured?.Invoke(this, frame);
        protected virtual void OnCaptureError(Exception ex) => CaptureError?.Invoke(this, ex);

        public virtual void Dispose() { }
    }
}
