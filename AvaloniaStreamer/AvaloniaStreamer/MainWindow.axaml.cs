using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using NAudio.Wave;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaStreamer
{
    public partial class MainWindow : Window
    {
        private WriteableBitmap _bitmap;
        private ConcurrentQueue<WriteableBitmap> _frameQueue = new ConcurrentQueue<WriteableBitmap>();
        private WasapiLoopbackCapture _capture;
        private WaveFileWriter _writer;
        private WaveOutEvent _waveOut;
        public MainWindow()
        {
            InitializeComponent(); 
            StartCaptureLoop();
            // ������ ������� �����
            Task.Run(StartAudioCapture);
        }

        private void StartAudioCapture()
        {
            try
            {
                _capture = new WasapiLoopbackCapture();
                _writer = new WaveFileWriter("output.wav", _capture.WaveFormat);

                _capture.DataAvailable += (s, e) =>
                {
                    _writer.Write(e.Buffer, 0, e.BytesRecorded);

                    // ����� �������� ��������� ������ (��������, �������� �� ����)
                };

                _capture.RecordingStopped += (s, e) =>
                {
                    _writer.Dispose();
                    Logger.Log(LogBox, "����������� �����������");
                };

                _capture.StartRecording();
                Logger.Log(LogBox, "������ ����� �������");
            }
            catch (Exception ex)
            {
                Logger.Log(LogBox, $"������ ������� �����: {ex.Message}");
            }
        }
        private void StartCaptureLoop()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        var skImage = CaptureFullScreen();
                        if (skImage != null)
                        {
                            // ��������� ��������� ����
                            _frameQueue.Enqueue(skImage);
                        }

                        await Task.Delay(33); // ~30 FPS
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogBox,$"������ ������� ������: {ex.Message}");
                    }
                }
            });
            Thread.Sleep(1000);
            // ���������� UI �� �������
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        if (_frameQueue.TryDequeue(out var frame))
                        {
                            UpdateUI(frame);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogBox, $"������ ���������� UI: {ex.Message}");
                    }

                    await Task.Delay(33); // ~30 FPS
                }
            });
        }
        private unsafe WriteableBitmap CaptureFullScreen()
        {
            Win32.GetWindowRect(Win32.GetDesktopWindow(), out var rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            Logger.Log(LogBox, $"����������� �����: {width}x{height}");

            // ������ ����� ��� �������� ��������
            int bytesPerPixel = 4;
            int rowBytes = width * bytesPerPixel;
            byte[] pixels = new byte[width * height * bytesPerPixel];

            // ���������� GDI ��� ������� ������
            IntPtr hdcSrc = Win32.CreateDC("DISPLAY", null, null, IntPtr.Zero);
            IntPtr hdcMem = Win32.CreateCompatibleDC(hdcSrc);
            IntPtr hBitmap = Win32.CreateCompatibleBitmap(hdcSrc, width, height);
            IntPtr oldBitmap = Win32.SelectObject(hdcMem, hBitmap);

            Win32.BitBlt(hdcMem, 0, 0, width, height, hdcSrc, 0, 0, Win32.SRCCOPY);

            // �������� ������ ��������
            Win32.BITMAPINFO bmi = new Win32.BITMAPINFO();
            bmi.bmiHeader.biSize = Marshal.SizeOf<Win32.BITMAPINFOHEADER>();
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height; // ���� �����
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = (int)Win32.BI_RGB;

            fixed (byte* ptr = pixels)
            {
                Win32.GetDIBits(hdcMem, hBitmap, 0, (uint)height, (IntPtr)ptr, ref bmi, Win32.DIB_RGB_COLORS);
            }

            // ����������� ������� GDI
            Win32.SelectObject(hdcMem, oldBitmap);
            Win32.DeleteObject(hBitmap);
            Win32.DeleteDC(hdcMem);
            Win32.DeleteDC(hdcSrc);

            // ������ WriteableBitmap
            var writeableBitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormats.Bgra8888,
                AlphaFormat.Opaque);

            using (var locked = writeableBitmap.Lock())
            {
                fixed (byte* p = pixels)
                {
                    Buffer.MemoryCopy(p, locked.Address.ToPointer(), pixels.Length, pixels.Length);
                }
            }

            return writeableBitmap;
        }
        private void UpdateUI(WriteableBitmap writeableBitmap)
        {
            if (writeableBitmap == null)
            {
                Logger.Log(LogBox, "WriteableBitmap ������");
                return;
            }

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ImageView != null)
                {
                    ImageView.Source = writeableBitmap;
                }
                else
                {
                    Logger.Log(LogBox, "ImageView �� ������ � XAML!");
                }
            }, DispatcherPriority.Background);
        }
    }

}