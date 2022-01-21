using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Drawing.Imaging;

namespace Himawari {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        static string TEMP = "temp";
        static string CURR = "current";

        public MainWindow() {
            InitializeComponent();

            BitmapImage bmp = new BitmapImage();

            if (!Directory.Exists(TEMP)) Directory.CreateDirectory(TEMP);
            if (!Directory.Exists(CURR)) Directory.CreateDirectory(CURR);

            /*for(int i = 0; i < 10; i++) {
                int val = i;
                new Task(() => {
                    Scraper.GetTile(WaveLength.Blue047, 1, DateTime.Now.AddMinutes(-val * 10), 0, 0).ContinueWith((bytes) => {
                        File.WriteAllBytes(Path.Combine(TEMP, $"img{val}.png"), bytes.Result);
                    });
                }).Start();
            }*/

            DateTime start = DateTime.Now;
            new Task(() => {
                DateTime requested = DateTime.Now.AddMinutes(-60);
                Scraper.GetRegion(20, requested).ContinueWith((task) => {
                    //task.Result.Save(Path.Combine(TEMP, $"{requested:yyMMdd_HHmmss}.png"), ImageFormat.Png);
                    task.Result.Save(Path.Combine(TEMP, $"{(DateTime.Now - start).TotalMilliseconds}ms.png"), ImageFormat.Png);
                    task.Result.Dispose();
                });
            }).Start();
        }
    }
}
