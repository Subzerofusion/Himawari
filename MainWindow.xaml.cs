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
using System.Drawing;
using Color = System.Drawing.Color;
using static Himawari.BitmapExtensions;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using static Himawari.ColorExtensions;
using System.Windows.Forms;
using Point = System.Drawing.Point;
using System.Collections.ObjectModel;

namespace Himawari {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        static string TEMP = "temp";
        static string CURR = "current";

        public void ScrapeTilesTest() {
            for (int i = 0; i < 10; i++) {
                int val = i;
                new Task(() => {
                    Scraper.GetTile(WaveLength.Blue047, 1, DateTime.Now.AddMinutes(-val * 10), 0, 0).ContinueWith((bytes) => {
                        File.WriteAllBytes(Path.Combine(TEMP, $"img{val}.png"), bytes.Result);
                    });
                }).Start();
            }
        }

        public void ScrapeRegionTest(DateTime time, int zoom, string suffix) {
            new Task(() => {
                Scraper.GetRegion(zoom, time).ContinueWith((task) => {
                    task.Result.Save(Path.Combine(TEMP, $"{time:yyMMdd_HHmmss}{suffix}.png"), ImageFormat.Png);
                    //task.Result.Save(Path.Combine(TEMP, $"{(DateTime.Now - start).TotalMilliseconds}ms.png"), ImageFormat.Png);
                    task.Result.Dispose();
                });
            }).Start();
        }

        public async Task ComposeDiskTest(DateTime time, int zoom, string suffix) {
            List<Task<Bitmap>> tasks = new List<Task<Bitmap>>();

            tasks.Add(Scraper.GetRegion(WaveLength.Blue047, zoom, time, yStart: zoom / 2));
            tasks.Add(Scraper.GetRegion(WaveLength.Green051, zoom, time, yStart: zoom / 2));
            tasks.Add(Scraper.GetRegion(WaveLength.Red064, zoom, time, yStart: zoom / 2));
            tasks.Add(Scraper.GetRegion(WaveLength.FarIR133, zoom, time, yStart: zoom / 2));
            //tasks.Add(Scraper.GetRegion(zoom, time));

            await Task.WhenAll(tasks);

            List<Bitmap> images = tasks.Select(x => x.Result).ToList();
            tasks.Clear();

            tasks.Add(images[0].Blend(Filters.And, Color.FromArgb(0xff, 0x00, 0x00, 0xff)));
            tasks.Add(images[1].Blend(Filters.And, Color.FromArgb(0xff, 0x00, 0xff, 0x00)));
            tasks.Add(images[2].Blend(Filters.And, Color.FromArgb(0xff, 0xff, 0x00, 0x00)));
            tasks.Add(images[3].Blend(Filters.Multiply, Color.FromArgb(0xff, 0x22, 0x22, 0x22)));

            await Task.WhenAll(tasks);
            tasks.Clear();

            Bitmap canvas = new Bitmap(images[0].Width, images[0].Height, PixelFormat.Format32bppArgb);
            canvas.Fill(Color.Black);

            await canvas.Blend(images[0], Filters.Add);
            await canvas.Blend(images[1], Filters.Add);
            await canvas.Blend(images[2], Filters.Add);

            await canvas.Blend(Filters.ClampedMultiply, 1.5f);

            Bitmap clouds = new Bitmap(images[0].Width, images[0].Height, PixelFormat.Format32bppArgb);
            clouds.Fill(Color.Black);
            await clouds.Blend(images[3], Filters.Add);

            await canvas.Blend(clouds, Filters.Lighter);

            canvas.Save(Path.Combine(TEMP, $"{time:yyMMdd_HHmmss}{suffix}.png"));
            canvas.Dispose();
            clouds.Dispose();

            foreach (var image in images) image.Dispose();
        }

        public void ComposeWallPaper(Bitmap earth = null) {
            var screens = Wallpaper.GetScreens();

            // I know there are 4 screens, so I can do this
            // offset points are not cumulative
            // offsets compensate for bezels
            var offsets = new Dictionary<Screen, Point>() {
                { screens[0], new Point(-64, 0) },
                { screens[1], new Point(-18, 0) },
                { screens[2], new Point(64, 0) },
                { screens[3], new Point(18, 0) },
            };

            var space = Wallpaper.CalculateRectangle(screens);
            earth = earth ?? new Bitmap(GetTempPath("test.png"));
            Bitmap wallpaper = new Bitmap(space.Width, space.Height, PixelFormat.Format32bppArgb);
            wallpaper.Fill(Color.Black);
            foreach (Screen screen in screens) {
                // earthPos is the centre of the planet
                // hard coded 1920 means middle of the two middle screens
                // hard coded -1700 places bottom of earth just above middle task bars
                Point earthPos = new Point(-screen.Bounds.Left + 1920 + offsets[screen].X, -screen.Bounds.Top - 850 + offsets[screen].Y);
                Point earthCentre = new Point(earth.Width / 2, 0);
                Point earthSize = new Point(earth.Width, earth.Height);

                using (Bitmap slice = Wallpaper.GetSlice(screen, earth, earthPos, earthCentre, earthSize)) {
                    wallpaper = Wallpaper.AddSlice(wallpaper, space, screen, slice);
                }
            }
            // wallpaper.Save(GetTempPath($"{time:yyMMdd_HHmmss}.png"));

            Wallpaper.Set(wallpaper, Wallpaper.Style.Span);
            wallpaper.Dispose();
        }

        public void FullChain(DateTime time, int zoom) {
            new Task(async () => {
                Bitmap earth = await Scraper.ComposeDisk(time, zoom, yStart: zoom / 2, progress: (str) => { ThreadSafeLog(str); });
                Dispatcher.Invoke(() => { ThreadSafeLog("Scraping Done"); });
                ComposeWallPaper(earth);
                Dispatcher.Invoke(() => { ThreadSafeLog("Done"); });
                earth.Dispose();
            }).Start();
        }

       public ObservableCollection<string> Log { get; set; } = new ObservableCollection<string>();

        public void ThreadSafeLog(string text) {
            Dispatcher.Invoke(() => { Log.Insert(0, text); });
        }

        public MainWindow() {
            InitializeComponent();
            DataContext = this;

            if (!Directory.Exists(TEMP)) Directory.CreateDirectory(TEMP);
            if (!Directory.Exists(CURR)) Directory.CreateDirectory(CURR);

            DateTime time = DateTime.Now.AddMinutes(-30);
            int zoom = 10;

            // ScrapeRegionTest(time, zoom, "_rgb");
            // ComposeDiskTest(time, zoom, "_cmd");

            //ComposeWallPaper();

            new Task(async () => {
                DateTime t = await Scraper.GetMostRecentTime();
                // ThreadSafeLog($"most recent data is from {t:yyMMdd_HHmmss}");
                FullChain(t, zoom);
            }
            ).Start();
        }

        private string GetTempPath(string name) {
            return Path.Combine(TEMP, name);
        }

        private string GetCurrPath(string name) {
            return Path.Combine(CURR, name);
        }

    }
}
