using Himawari.Properties;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CSharpImageLibrary;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Media;
using System.IO;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Color = System.Drawing.Color;
using Screen = System.Windows.Forms.Screen;
using static Himawari.BitmapExtensions;

namespace Himawari {
    public enum WaveLength {
        RGB = 0,
        Blue047 = 1,
        Green051 = 2,
        Red064 = 3,
        NearIR086 = 4,
        NearIR16 = 5,
        NearIR23 = 6,
        ShortIR39 = 7,
        MidIR62 = 8,
        MidIR69 = 9,
        MidIR73 = 10,
        FarIR86 = 11,
        FarIR96 = 12,
        FarIR104 = 13,
        FarIR112 = 14,
        FarIR124 = 15,
        FarIR133 = 16,
    }

    public class Scraper {
        public static int[] ACCEPTABLE_WIDEBAND_ZOOMS { get; private set; } = new int[] { 1, 2, 4, 8, 10 } ;
        public static int[] ACCEPTABLE_COLOUR_ZOOMS { get; private set; } = new int[] { 8, 16, 20 };

        public static int TILE_WIDTH { get; private set; } = 550;
        public static int TILE_HEIGHT { get; private set; } = 550;
        private static string WIDEBAND_URL_BASE = "https://himawari8.nict.go.jp/img/FULL_24h";
        private static string COLOUR_URL_BASE = "https://himawari8.nict.go.jp/img/D531106";
        private static HttpClient CLIENT = new HttpClient();
        private static int NO_IMAGE_SIZE = Resource.no_image_png.Length;
        private static byte[] NO_IMAGE_HASH = new SHA1Managed().ComputeHash(Resource.no_image_png);

        public delegate void ProgressCallback(string info);

        public static async Task<Bitmap> ComposeDisk(DateTime time, int zoom, int xStart = -1, int xEnd = -1, int yStart = -1, int yEnd = -1, ProgressCallback progress = null) {
            List<Task<Bitmap>> tasks = new List<Task<Bitmap>>();
            List<Task> tasks2 = new List<Task>();

            // get layers
            tasks.Add(GetRegion(WaveLength.Blue047, zoom, time, xStart, xEnd, yStart, yEnd, progress: progress));
            tasks.Add(GetRegion(WaveLength.Green051, zoom, time, xStart, xEnd, yStart, yEnd, progress: progress));
            tasks.Add(GetRegion(WaveLength.Red064, zoom, time, xStart, xEnd, yStart, yEnd, progress: progress));
            tasks.Add(GetRegion(WaveLength.FarIR133, zoom, time, xStart, xEnd, yStart, yEnd, progress: progress));

            tasks2.Add(tasks[0].ContinueWith((_) => { progress?.Invoke($"{WaveLength.Blue047} {tasks[0].Status}"); }));
            tasks2.Add(tasks[1].ContinueWith((_) => { progress?.Invoke($"{WaveLength.Green051} {tasks[1].Status}"); }));
            tasks2.Add(tasks[2].ContinueWith((_) => { progress?.Invoke($"{WaveLength.Red064} {tasks[2].Status}"); }));
            tasks2.Add(tasks[3].ContinueWith((_) => { progress?.Invoke($"{WaveLength.FarIR133} {tasks[3].Status}"); }));

            // wait
            await Task.WhenAll(tasks);

            progress?.Invoke("all layers acquired");

            // extract bitmaps
            List<Bitmap> images = tasks.Select(x => x.Result).ToList();
            tasks.Clear();

            // filter bitmaps to what they should be
            tasks.Add(images[0].Blend(Filters.And, Color.FromArgb(0xff, 0x00, 0x00, 0xff)));
            tasks.Add(images[1].Blend(Filters.And, Color.FromArgb(0xff, 0x00, 0xff, 0x00)));
            tasks.Add(images[2].Blend(Filters.And, Color.FromArgb(0xff, 0xff, 0x00, 0x00)));
            tasks.Add(images[3].Blend(Filters.Multiply, Color.FromArgb(0xff, 0x22, 0x22, 0x22)));

            // wait
            await Task.WhenAll(tasks);
            tasks.Clear();

            progress?.Invoke("colours filtered");

            Bitmap canvas = new Bitmap(images[0].Width, images[0].Height, PixelFormat.Format32bppArgb);
            canvas.Fill(Color.Black);

            // colour image
            await canvas.Blend(images[0], Filters.Add);
            await canvas.Blend(images[1], Filters.Add);
            await canvas.Blend(images[2], Filters.Add);

            progress?.Invoke("image blended");

            // increase saturation
            await canvas.Blend(Filters.ClampedMultiply, 1.5f);

            // create clouds
            Bitmap clouds = new Bitmap(images[0].Width, images[0].Height, PixelFormat.Format32bppArgb);
            clouds.Fill(Color.Black);
            await clouds.Blend(images[3], Filters.Add);

            // add clouds
            await canvas.Blend(clouds, Filters.Lighter);

            progress?.Invoke("clouds applied");

            // cleanup
            clouds.Dispose();
            foreach (var image in images) image.Dispose();

            return canvas;
        }

        public static async Task<Bitmap> GetRegion(WaveLength waveLength, int zoom, DateTime time, int xStart = -1, int xEnd = -1, int yStart = -1, int yEnd = -1, ProgressCallback progress = null) {
            if(waveLength == WaveLength.RGB) return await GetRegion(zoom, time, xStart, xEnd, yStart, yEnd, progress);
            return await GetRegion(zoom, time, waveLength, xStart, xEnd, yStart, yEnd, progress);
        }

        public static async Task<Bitmap> GetRegion(int zoom, DateTime time, int xStart = -1, int xEnd = -1, int yStart = -1, int yEnd = -1, ProgressCallback progress = null) {
            return await GetRegion(zoom, time, null, xStart, xEnd, yStart, yEnd, progress);
        }

        private static async Task<Bitmap> GetRegion(int zoom, DateTime time, WaveLength? waveLength, int xStart = -1, int xEnd = -1, int yStart = -1, int yEnd = -1, ProgressCallback progress = null) {
            if (!(waveLength == null || waveLength == WaveLength.RGB ? ACCEPTABLE_COLOUR_ZOOMS : ACCEPTABLE_WIDEBAND_ZOOMS).Contains(zoom))
                throw new Exception();

            xStart = xStart == -1 ? 0 : xStart;
            yStart = yStart == -1 ? 0 : yStart;
            xEnd = xEnd == -1 ? zoom : xEnd;
            yEnd = yEnd == -1 ? zoom : yEnd;

            CheckParameters(zoom, time, xStart, xEnd, yStart, yEnd);

            Bitmap bitmap = new Bitmap((xEnd - xStart) * TILE_WIDTH, (yEnd - yStart) * TILE_HEIGHT, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap)) {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                object graphicsLock = new object();

                List<Task> tasks = new List<Task>();
                bool first = true;

                int totalTasks = 0;
                int completedTasks = 1;

                Action reportProgress = () => {
                    progress?.Invoke($"{(waveLength == null ? "rgb" : waveLength.ToString())} tile({completedTasks}/{totalTasks}) retrieved");
                };

                for (int i = 0; i < xEnd - xStart; i++) {
                    for (int j = 0; j < yEnd - yStart; j++) {
                        var x = i;
                        var y = j;

                        Task<byte[]> getTile = waveLength == null || waveLength == WaveLength.RGB?
                                GetTile(zoom, time, x + xStart, y + yStart) :
                                GetTile((WaveLength)waveLength, zoom, time, x + xStart, y + yStart);

                        if (first) {
                            first = false;
                            byte[] firstPng = await getTile; // wait for the first image
                            if (!HasImage(firstPng)) return null; // the image is bad
                            Task task = new Task(() => { // continue as usual
                                completedTasks++;
                                reportProgress();
                                DrawToGraphics(graphics, graphicsLock, firstPng, x, y);
                                completedTasks++;
                                reportProgress();
                            });
                            tasks.Add(task);
                            totalTasks++;
                            task.Start();
                        } else {
                            tasks.Add(getTile.ContinueWith((task) => {
                                completedTasks++;
                                reportProgress();
                                DrawToGraphics(graphics, graphicsLock, task.Result, x, y);
                                completedTasks++;
                                reportProgress();
                            }));
                            totalTasks++;
                        }
                        tasks.Add(getTile);
                        totalTasks++;
                    }
                }
                await Task.WhenAll(tasks);
            }
            return bitmap;
        }

        private static void DrawToGraphics(Graphics graphics, object graphicsLock, byte[] png, int x, int y) {
            using (var memoryStream = new MemoryStream(png)) {
                using (var tile = new Bitmap(memoryStream)) {
                    lock (graphicsLock) {
                        graphics.DrawImage(tile, x * TILE_WIDTH, y * TILE_HEIGHT, TILE_WIDTH, TILE_HEIGHT);
                    }
                }
            }
        }

        public static bool CheckParameters(int zoom, DateTime time, int xStart, int xEnd, int yStart, int yEnd) {
            if (xStart == -1 || yStart == -1 || xEnd == -1 || yEnd == -1) throw new Exception();
            if (xStart > zoom || xStart < 0 ||
                yStart > zoom || yStart < 0 ||
                xEnd > zoom || xEnd < 0 || xStart > xEnd ||
                yEnd > zoom || yEnd < 0 || yStart > yEnd) throw new Exception();
            if (time.Ticks > DateTime.Now.Ticks) throw new Exception();
            return true;
        }

        public static bool HasImage(byte[] image) {
            if (image.Length != NO_IMAGE_SIZE) return true;
            if (new SHA1Managed().ComputeHash(image).SequenceEqual(NO_IMAGE_HASH)) return false;
            return true;
        }

        public async static Task<DateTime> GetMostRecentTime() {
            DateTime now = DateTime.Now;
            byte[] data;
            do {
                now = now.AddMinutes(-10);
                data = await GetTile(WaveLength.Blue047, 1, now, 0, 0);
            } while (data == null || !HasImage(data));
            return now;
        }

        public async static Task<byte[]> GetTile(WaveLength waveLength, int zoom, DateTime time, int x, int y, ProgressCallback progress = null) {
            string str = FormatWideBandURL(waveLength, zoom, time, x, y);
            var task = CLIENT.GetByteArrayAsync(str);
            var bytes = await task;
            progress?.Invoke($"{str} {task.Status}");
            return bytes;
        }

        public async static Task<byte[]> GetTile(int zoom, DateTime time, int x, int y, ProgressCallback progress = null) {
            string str = FormatColourURL(zoom, time, x, y);
            var task = CLIENT.GetByteArrayAsync(str);
            var bytes = await task;
            progress?.Invoke($"{str} {task.Status}");
            return bytes;
        }

        public static DateTime DecrementTime(DateTime time) {
            return time.AddMinutes(-10);
        }

        public static string FormatValidTime(DateTime time) {
            StringBuilder sb = new StringBuilder(time.ToUniversalTime().ToString("HHmm00"));
            sb[3] = '0';
            return sb.ToString();
        }

        public static string FormatColourURL(int zoom, DateTime time, int x, int y) {

            if (!ACCEPTABLE_COLOUR_ZOOMS.Contains(zoom)) throw new ArgumentException("Zoom level invalid");
            if (x < 0 || x >= zoom || y < 0 || y >= zoom) throw new IndexOutOfRangeException("Tile index is outside of range of image.");
            if (time.Ticks > DateTime.Now.Ticks) throw new ArgumentOutOfRangeException("Time given occurs in the future");

            time = time.ToUniversalTime();
            string url = null;
            url = $"{COLOUR_URL_BASE}/{zoom}d/550/{time:yyyy}/{time:MM}/{time:dd}/{FormatValidTime(time)}_{x}_{y}.png";
            return url;
        }

        public static string FormatWideBandURL(WaveLength waveLength, int zoom, DateTime time, int x, int y) {

            if (!ACCEPTABLE_WIDEBAND_ZOOMS.Contains(zoom)) throw new ArgumentException("Zoom level invalid");
            if (x < 0 || x >= zoom || y < 0 || y >= zoom) throw new IndexOutOfRangeException("Tile index is outside of range of image.");
            if (time.Ticks > DateTime.Now.Ticks) throw new ArgumentOutOfRangeException("Time given occurs in the future");

            time = time.ToUniversalTime();
            string url = null;
            url = $"{WIDEBAND_URL_BASE}/B{(int)waveLength:D2}/{zoom}d/550/{time:yyyy}/{time:MM}/{time:dd}/{FormatValidTime(time)}_{x}_{y}.png";
            return url;
        }
    }
}
