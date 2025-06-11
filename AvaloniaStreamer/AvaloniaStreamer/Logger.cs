using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AvaloniaStreamer
{
    public class Logger
    {
        public static void Log(TextBox LogBox, string message)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (LogBox != null)
                {
                    LogBox.Text = $"{DateTime.Now:HH:mm:ss} - {message}\n";
                }
            }, DispatcherPriority.Background);
        }
    }
}
