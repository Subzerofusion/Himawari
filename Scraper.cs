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

namespace Himawari {
    public enum WaveLength {
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
        private static int[] ACCEPTABLE_WIDEBAND_ZOOMS = new int[] { 1, 2, 4, 8, 10 };
        private static int[] ACCEPTABLE_COLOUR_ZOOMS = new int[] { 8, 16, 20 };
        private static string WIDEBAND_URL_BASE = "https://himawari8.nict.go.jp/img/FULL_24h";
        private static string COLOUR_URL_BASE = "https://himawari8.nict.go.jp/img/D531106";
        private static HttpClient CLIENT = new HttpClient();
        private static int NO_IMAGE_SIZE = Resource.no_image_png.Length;
        private static byte[] NO_IMAGE_HASH = new SHA1Managed().ComputeHash(Resource.no_image_png);
        private static int TILE_WIDTH = 550;
        private static int TILE_HEIGHT = 550;
        public static async Task<Bitmap> GetRegion(WaveLength waveLength, int zoom, DateTime time, int xStart = -1, int xEnd = -1, int yStart = -1, int yEnd = -1) {
            return await GetRegion(zoom, time, waveLength, xStart, xEnd, yStart, yEnd);
        }

        public static async Task<Bitmap> GetRegion(int zoom, DateTime time, int xStart = -1, int xEnd = -1, int yStart = -1, int yEnd = -1) {
            return await GetRegion(zoom, time, null, xStart, xEnd, yStart, yEnd);
        }

        private static async Task<Bitmap> GetRegion(int zoom, DateTime time, WaveLength? waveLength, int xStart = -1, int xEnd = -1, int yStart = -1, int yEnd = -1) {
            if (!(waveLength == null ? ACCEPTABLE_COLOUR_ZOOMS : ACCEPTABLE_WIDEBAND_ZOOMS).Contains(zoom))
                throw new Exception();

            xStart = xStart == -1 ? 0 : xStart;
            yStart = yStart == -1 ? 0 : yStart;
            xEnd = xEnd == -1 ? zoom : xEnd;
            yEnd = yEnd == -1 ? zoom : yEnd;

            CheckParameters(zoom, time, xStart, xEnd, yStart, yEnd);

            Bitmap bitmap = new Bitmap((xEnd - xStart) * TILE_WIDTH, (yEnd - yStart) * TILE_HEIGHT, PixelFormat.Format32bppArgb);
            Graphics graphics = Graphics.FromImage(bitmap);

            graphics.CompositingMode = CompositingMode.SourceCopy;
            object graphicsLock = new object();

            List<Task> tasks = new List<Task>();
            bool first = true;
            for (int i = 0; i < xEnd - xStart; i++) {
                for (int j = 0; j < yEnd - yStart; j++) {
                    var x = i;
                    var y = j;

                    Task<byte[]> getTile = waveLength == null ?
                            GetTile(zoom, time, x + xStart, y + yStart) :
                            GetTile((WaveLength)waveLength, zoom, time, x + xStart, y + yStart);

                    if (first) {
                        first = false;
                        byte[] firstPng = await getTile; // wait for the first image
                        if (!HasImage(firstPng)) return null; // the image is bad
                        Task task = new Task(() => { // continue as usual
                            DrawToGraphics(graphics, graphicsLock, firstPng, x, y);
                        });
                        tasks.Add(task);
                        task.Start();
                    } else {
                        tasks.Add(getTile.ContinueWith((task) => {
                            DrawToGraphics(graphics, graphicsLock, task.Result, x, y);
                        }));
                    }
                    tasks.Add(getTile);
                }
            }

            await Task.WhenAll(tasks);
            graphics.Dispose();
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

        public async static Task<DateTime> GetMostRecent() {
            DateTime now = DateTime.Now;
            while (await GetTile(WaveLength.Blue047, 1, now, 0, 0) == null) now = DecrementTime(now);
            return now;
        }

        public async static Task<byte[]> GetTile(WaveLength waveLength, int zoom, DateTime time, int x, int y) {
            string str = FormatWideBandURL(waveLength, zoom, time, x, y);
            var bytes = await CLIENT.GetByteArrayAsync(str);
            return bytes;
        }

        public async static Task<byte[]> GetTile(int zoom, DateTime time, int x, int y) {
            string str = FormatColourURL(zoom, time, x, y);
            var bytes = await CLIENT.GetByteArrayAsync(str);
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
