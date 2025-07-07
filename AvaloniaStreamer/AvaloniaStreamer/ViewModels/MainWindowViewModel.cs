using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ReactiveUI;
using ScreenCaptureLibrary.Platforms.Windows;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaStreamer.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private Bitmap? _capturedImage;
        private string _status = "Ready";
        private bool _isCapturing;
        private CancellationTokenSource? _cts;

        public Bitmap? CapturedImage
        {
            get => _capturedImage;
            private set => this.RaiseAndSetIfChanged(ref _capturedImage, value);
        }

        public string Status
        {
            get => _status;
            private set => this.RaiseAndSetIfChanged(ref _status, value);
        }
        private float _volume = 0.5f;
        public float Volume
        {
            get => _volume;
            set
            {
                this.RaiseAndSetIfChanged(ref _volume, value);
                _audioCapturer?.SetVolume(value);
            }
        }
        // Свойства
        public bool CaptureSystemAudio { get; set; } = true;
        public bool CaptureMicrophone { get; set; } = true;

        // Команды
        public ReactiveCommand<Unit, Unit> StartAudioCaptureCommand { get; }
        public ReactiveCommand<Unit, Unit> StopAudioCaptureCommand { get; }
        public ReactiveCommand<Unit, Unit> CaptureCommand { get; }
        public ReactiveCommand<Unit, Unit> StartContinuousCommand { get; }
        public ReactiveCommand<Unit, Unit> StopContinuousCommand { get; }
        public ReactiveCommand<Unit, Unit> PlaySystemAudioCommand { get; }
        public ReactiveCommand<Unit, Unit> PlayMicrophoneAudioCommand { get; }
        public ReactiveCommand<Unit, Unit> StopAudioPlaybackCommand { get; }

        private WindowsAudioCapturer _audioCapturer;

        private byte[] _lastSystemAudio;
        private byte[] _lastMicrophoneAudio;
        public MainWindowViewModel()
        {
            // Обработка ошибок в командах
            CaptureCommand = ReactiveCommand.CreateFromTask(CaptureScreenAsync, outputScheduler: RxApp.MainThreadScheduler);
            StartContinuousCommand = ReactiveCommand.CreateFromTask(StartContinuousCaptureAsync, outputScheduler: RxApp.MainThreadScheduler);
            StopContinuousCommand = ReactiveCommand.CreateFromTask(StopContinuousCaptureAsync, outputScheduler: RxApp.MainThreadScheduler);

            // Подписка на ошибки команд
            CaptureCommand.ThrownExceptions.Subscribe(ex =>
                Dispatcher.UIThread.Post(() => Status = $"Capture error: {ex.Message}"));

            StartContinuousCommand.ThrownExceptions.Subscribe(ex =>
                Dispatcher.UIThread.Post(() => Status = $"Start error: {ex.Message}"));

            StopContinuousCommand.ThrownExceptions.Subscribe(ex =>
                Dispatcher.UIThread.Post(() => Status = $"Stop error: {ex.Message}"));
            StartAudioCaptureCommand = ReactiveCommand.Create(() =>
            {
                _audioCapturer = new WindowsAudioCapturer();
                _audioCapturer.AudioDataAvailable += OnAudioData;
                _audioCapturer.StartCapture(CaptureSystemAudio, CaptureMicrophone);
                Status = "Audio: Recording...";
            });

            StopAudioCaptureCommand = ReactiveCommand.Create(() =>
            {
                _audioCapturer?.StopCapture();
                Status = "Audio: Stopped";
            });
            PlaySystemAudioCommand = ReactiveCommand.Create(() =>
            {
                if (_lastSystemAudio?.Length > 0)
                    _audioCapturer?.PlayAudio(_lastSystemAudio);
            });

            PlayMicrophoneAudioCommand = ReactiveCommand.Create(() =>
            {
                if (_lastMicrophoneAudio?.Length > 0)
                    _audioCapturer?.PlayAudio(_lastMicrophoneAudio);
            });

            StopAudioPlaybackCommand = ReactiveCommand.Create(() =>
            {
                _audioCapturer?.StopPlayback();
            });
        }
        private void OnAudioData(object sender, (byte[], byte[]) data)
        {
            (_lastSystemAudio, _lastMicrophoneAudio) = data;
            var (systemAudio, micAudio) = data;
            Dispatcher.UIThread.Post(() =>
            {
                if (systemAudio.Length > 0)
                    File.WriteAllBytes($"system_{DateTime.Now:HHmmss}.wav", systemAudio);

                if (micAudio.Length > 0)
                    File.WriteAllBytes($"mic_{DateTime.Now:HHmmss}.wav", micAudio);
            });
        }
        private async Task CaptureScreenAsync(CancellationToken ct)
        {
            await UpdateStatusAsync("Capturing...");
            var imageData = await GenerateSampleImageAsync(ct);
            await LoadImageAsync(imageData);
            await UpdateStatusAsync("Capture completed");
        }

        private async Task StartContinuousCaptureAsync(CancellationToken ct)
        {
            try
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                _isCapturing = true;

                await UpdateStatusAsync("Continuous capture started (5 мс)");

                while (_isCapturing && !_cts.IsCancellationRequested)
                {
                    ct.ThrowIfCancellationRequested();

                    var imageData = await CaptureRealScreenAsync(_cts.Token);
                    await LoadImageAsync(imageData);
                    await Task.Delay(5, _cts.Token); // 5мс
                }
            }
            finally
            {
                if (!_isCapturing)
                {
                    _cts?.Dispose();
                    _cts = null;
                }
            }
        }
        private async Task<byte[]> CaptureRealScreenAsync(CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                using var bitmap = WindowsScreenCapturer.CaptureScreen();
                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }, ct);
        }
        private async Task StopContinuousCaptureAsync(CancellationToken ct)
        {
            _isCapturing = false;
            _cts?.Cancel();
            await UpdateStatusAsync("Continuous capture stopped");
        }

        private async Task LoadImageAsync(byte[] imageData)
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    using var stream = new MemoryStream(imageData);
                    CapturedImage = new Bitmap(stream);
                });
            }
            catch (Exception ex)
            {
                await UpdateStatusAsync($"Image load error: {ex.Message}");
                throw;
            }
        }

        private async Task UpdateStatusAsync(string message)
        {
            await Dispatcher.UIThread.InvokeAsync(() => Status = message);
        }

        private async Task<byte[]> GenerateSampleImageAsync(CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                // Генерация тестового изображения (синий градиент)
                var width = 600;
                var height = 400;
                var pixelFormat = Avalonia.Platform.PixelFormat.Bgra8888;
                var stride = width * 4;
                var pixels = new byte[stride * height];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var index = y * stride + x * 4;
                        pixels[index] = (byte)(x * 255 / width);     // B
                        pixels[index + 1] = (byte)(y * 255 / height);  // G
                        pixels[index + 2] = 128;                        // R
                        pixels[index + 3] = 255;                        // A
                    }
                }

                // Создание Bitmap в Avalonia
                using var ms = new MemoryStream();
                var bitmap = new WriteableBitmap(
                    new PixelSize(width, height),
                    new Vector(96, 96),
                    pixelFormat,
                    AlphaFormat.Opaque);

                using (var fb = bitmap.Lock())
                {
                    System.Runtime.InteropServices.Marshal.Copy(
                        pixels, 0, fb.Address, pixels.Length);
                }

                // Сохранение в PNG
                bitmap.Save(ms);
                return ms.ToArray();
            });
        }
    }
}