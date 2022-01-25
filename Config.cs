using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft;
using Newtonsoft.Json;

namespace Himawari {
    public abstract class Config {
        // this is called when a config changes
        public abstract void OnChanged();
    }
    public class ConfigController<T> where T : Config, new() {
        public T Config { get; private set; }
        private object configLock = new object();
        private string path;
        private FileSystemWatcher watcher;

        private static object lock1 = new object();
        private static object lock2 = new object();
        private static ConfigController<T> Instance;
        public ConfigController(string path, T defaultConfig = null, bool isLocal = true, bool createIfMissing = true) {
            this.path = path;

            Config = defaultConfig ?? new T();

            if (isLocal) path = Path.Combine(Directory.GetCurrentDirectory(), path);

            if (!File.Exists(path)) {
                if (createIfMissing) File.Create(path).Close();
                else return;
                WriteConfig();
            }

            UpdateConfig();
            RegisterWatcher();
            Config.OnChanged();

            Instance = this;
        }

        public void WriteConfig() {
            lock (configLock) File.WriteAllText(path, JsonConvert.SerializeObject(Config, Formatting.Indented));
        }

        void UpdateConfig() {
            lock (configLock) Config = JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
            
        }

        void OnChanged(object source, FileSystemEventArgs e) {
            UpdateConfig();
            Config.OnChanged();
        }

        void RegisterWatcher() {
            if (watcher == null) {
                var fi = new FileInfo(path);
                watcher = new FileSystemWatcher(fi.DirectoryName, fi.Name);
                watcher.NotifyFilter = NotifyFilters.LastWrite;
                watcher.Changed += new FileSystemEventHandler(OnChanged);
                watcher.EnableRaisingEvents = true;
            }
        }
    }
}