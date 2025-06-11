using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaStreamer
{
    public partial class MainWindow : Window
    {
        private WriteableBitmap _bitmap;
        private ConcurrentQueue<WriteableBitmap> _frameQueue = new ConcurrentQueue<WriteableBitmap>();
        private SKImage _latestFrame;
        public MainWindow()
        {
            InitializeComponent(); 
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (LogBox != null)
                {
                    LogBox.Text = "Тестовое сообщение\n";
                }
            });
            StartCaptureLoop();
        }

        private void Log(string message)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (LogBox != null)
                {
                    LogBox.Text = $"{DateTime.Now:HH:mm:ss} - {message}\n";
                }
            }, DispatcherPriority.Background);
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
                            // Сохраняем последний кадр
                            _frameQueue.Enqueue(skImage);
                        }

                        await Task.Delay(33); // ~30 FPS
                    }
                    catch (Exception ex)
                    {
                        Log($"Ошибка захвата экрана: {ex.Message}");
                    }
                }
            });
            Thread.Sleep(1000);
            // Обновление UI из очереди
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
                        Log($"Ошибка обновления UI: {ex.Message}");
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

            Log($"Захватываем экран: {width}x{height}");

            // Создаём буфер для хранения пикселей
            int bytesPerPixel = 4;
            int rowBytes = width * bytesPerPixel;
            byte[] pixels = new byte[width * height * bytesPerPixel];

            // Используем GDI для захвата экрана
            IntPtr hdcSrc = Win32.CreateDC("DISPLAY", null, null, IntPtr.Zero);
            IntPtr hdcMem = Win32.CreateCompatibleDC(hdcSrc);
            IntPtr hBitmap = Win32.CreateCompatibleBitmap(hdcSrc, width, height);
            IntPtr oldBitmap = Win32.SelectObject(hdcMem, hBitmap);

            Win32.BitBlt(hdcMem, 0, 0, width, height, hdcSrc, 0, 0, Win32.SRCCOPY);

            // Получаем данные пикселей
            Win32.BITMAPINFO bmi = new Win32.BITMAPINFO();
            bmi.bmiHeader.biSize = Marshal.SizeOf<Win32.BITMAPINFOHEADER>();
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height; // Верх внизу
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = (int)Win32.BI_RGB;

            fixed (byte* ptr = pixels)
            {
                Win32.GetDIBits(hdcMem, hBitmap, 0, (uint)height, (IntPtr)ptr, ref bmi, Win32.DIB_RGB_COLORS);
            }

            // Освобождаем ресурсы GDI
            Win32.SelectObject(hdcMem, oldBitmap);
            Win32.DeleteObject(hBitmap);
            Win32.DeleteDC(hdcMem);
            Win32.DeleteDC(hdcSrc);

            // Создаём WriteableBitmap
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
                Log("WriteableBitmap пустой");
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
                    Log("ImageView не найден в XAML!");
                }
            }, DispatcherPriority.Background);
        }
    }

    internal static class Win32
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, IntPtr lpBits, ref BITMAPINFO bmi, uint uUsage);

        public const uint SRCCOPY = 0x00CC0020;
        public const uint BI_RGB = 0;
        public const uint DIB_RGB_COLORS = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public int bmiColors;
        }
    }
}