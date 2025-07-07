using NAudio.CoreAudioApi;
using NAudio.Wave;
using ScreenCaptureLibrary;

namespace ScreenCaptureLibrary.Platforms.Windows
{
    public class WindowsAudioCapturer : IAudioCapturer
    {
        private WasapiLoopbackCapture _systemCapture;
        private WasapiCapture _microphoneCapture;
        private WaveFileWriter _systemWriter;
        private WaveFileWriter _micWriter;
        private MemoryStream _systemStream;
        private MemoryStream _micStream;
        private WaveOutEvent _player;
        public void SetVolume(float volume)
        {
            if (_player != null)
            {
                _player.Volume = Math.Clamp(volume, 0, 1);
            }
        }
        public event EventHandler<(byte[] SystemAudio, byte[] MicrophoneAudio)> AudioDataAvailable;
        public event EventHandler<Exception> AudioError;

        event EventHandler<byte[]> IAudioCapturer.AudioDataAvailable
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }
        public void PlayAudio(byte[] audioData)
        {
            StopPlayback(); // Остановить текущее воспроизведение

            _player = new WaveOutEvent();
            using var stream = new MemoryStream(audioData);
            using var reader = new WaveFileReader(stream);

            _player.Init(reader);
            _player.Play();

            _player.PlaybackStopped += (s, e) =>
            {
                _player.Dispose();
                _player = null;
            };
        }

        public void StopPlayback()
        {
            _player?.Stop();
            _player?.Dispose();
            _player = null;
        }

        public void StartCapture(bool captureSystem = true, bool captureMic = true)
        {
            if (captureSystem)
            {
                _systemStream = new MemoryStream();
                _systemCapture = new WasapiLoopbackCapture();
                _systemWriter = new WaveFileWriter(_systemStream, _systemCapture.WaveFormat);
                _systemCapture.DataAvailable += (s, e) => _systemWriter.Write(e.Buffer, 0, e.BytesRecorded);
                _systemCapture.StartRecording();
            }

            if (captureMic)
            {
                _micStream = new MemoryStream();
                var micDevice = new MMDeviceEnumerator()
                    .GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                _microphoneCapture = new WasapiCapture(micDevice);
                _micWriter = new WaveFileWriter(_micStream, _microphoneCapture.WaveFormat);
                _microphoneCapture.DataAvailable += (s, e) => _micWriter.Write(e.Buffer, 0, e.BytesRecorded);
                _microphoneCapture.StartRecording();
            }
        }

        public void StopCapture()
        {
            _systemCapture?.StopRecording();
            _microphoneCapture?.StopRecording();

            _systemWriter?.Dispose();
            _micWriter?.Dispose();

            AudioDataAvailable?.Invoke(this, (
                _systemStream?.ToArray() ?? Array.Empty<byte>(),
                _micStream?.ToArray() ?? Array.Empty<byte>()
            ));

            DisposeStreams();
        }

        private void DisposeStreams()
        {
            _systemStream?.Dispose();
            _micStream?.Dispose();
        }

        public void Dispose()
        {
            _systemCapture?.Dispose();
            _microphoneCapture?.Dispose();
            DisposeStreams();
        }

        public Task<byte[]> CaptureAudioAsync(int durationMs)
        {
            throw new NotImplementedException();
        }

        public Task StartAudioCaptureAsync()
        {
            throw new NotImplementedException();
        }

        public Task StopAudioCaptureAsync()
        {
            throw new NotImplementedException();
        }
    }
}