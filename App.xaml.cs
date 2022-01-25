using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Himawari {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        public TaskbarIcon TrayIcon { get; private set; }
        public ConfigController<HimawariConfig> Config { get; private set; }
        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);

            //create the notifyicon (it's a resource declared in NotifyIconResources.xaml
            TrayIcon = (TaskbarIcon)FindResource("TrayIcon");
            Config = new ConfigController<HimawariConfig>("himawari.config");
        }

        protected override void OnExit(ExitEventArgs e) {
            //notifyIcon.Dispose(); //the icon would clean up automatically, but this is cleaner
            base.OnExit(e);
        }
    }
}
