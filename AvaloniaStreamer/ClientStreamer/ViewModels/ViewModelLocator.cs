using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientStreamer.ViewModels
{
    public static class ViewModelLocator
    {
        public static ClientViewModel ClientViewModel =>
            App.ServiceProvider!.GetRequiredService<ClientViewModel>();
    }
}
