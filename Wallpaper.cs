using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32;

namespace Himawari {
    class Wallpaper {
        public static Screen[] GetScreens() {
            return Screen.AllScreens;
        }

        public static Rectangle CalculateRectangle(params Screen[] screens) {
            Point topLeft = new Point(int.MaxValue, int.MaxValue);
            Point botRight = new Point(int.MinValue, int.MinValue);
            foreach (Screen screen in screens) {
                Rectangle bounds = screen.Bounds;
                topLeft.X = Math.Min(bounds.Left, topLeft.X);
                topLeft.Y = Math.Min(bounds.Top, topLeft.Y);
                botRight.X = Math.Max(bounds.Right, botRight.X);
                botRight.Y = Math.Max(bounds.Bottom, botRight.Y);
            }
            return new Rectangle(topLeft.X, topLeft.Y, botRight.X - topLeft.X, botRight.Y - topLeft.Y);
        }

        public static Bitmap GetSlice(Screen screen, Bitmap earth, Point earthPos, Point earthCentre, Point earthSize) {
            Bitmap slice = new Bitmap(screen.Bounds.Width, screen.Bounds.Height, earth.PixelFormat);
            using (Graphics g = Graphics.FromImage(slice)) {
                g.DrawImage(earth, earthPos.X - earthCentre.X, earthPos.Y - earthCentre.Y, earthSize.X, earthSize.Y);
                // g.DrawImage(earth, new Rectangle(), new Rectangle(), GraphicsUnit.Pixel);
            }
            return slice;
        }

        public static Bitmap AddSlice(Bitmap wallpaper, Rectangle space, Screen screen, Bitmap slice) {
            using (Graphics g = Graphics.FromImage(wallpaper)) {
                Point location = new Point(screen.Bounds.Location.X - space.Location.X, screen.Bounds.Location.Y - space.Location.Y);
                g.DrawImage(slice, new Rectangle(location, new Size(slice.Width, slice.Height)));
            }
            return wallpaper;
        }

        const int SPI_SETDESKWALLPAPER = 20;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDWININICHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        public enum Style : int {
            Tiled,
            Centered,
            Stretched,
            Fill,
            Fit,
            Span,
            Stretch,
            Tile,
            Center
        }

        private static Dictionary<Style, (string, string)> styleValues = new Dictionary<Style, (string, string)>() {
                { Style.Stretched, (2.ToString(), 0.ToString()) },
                { Style.Centered,  (1.ToString(), 0.ToString()) },
                { Style.Tiled,     (1.ToString(), 1.ToString()) },
                { Style.Fill,      (10.ToString(), 0.ToString()) },
                { Style.Fit,       (6.ToString(), 0.ToString()) },
                { Style.Span,      (22.ToString(), 0.ToString()) },
                { Style.Stretch,   (2.ToString(), 0.ToString()) },
                { Style.Tile,      (0.ToString(), 1.ToString()) },
                { Style.Center,    (0.ToString(), 0.ToString()) }
            };

        public static void Set(Uri uri, Style style) {
            Stream s = new System.Net.WebClient().OpenRead(uri.ToString());

            Image img = Image.FromStream(s);
            string tempPath = Path.Combine(Path.GetTempPath(), "wallpaper.bmp");
            img.Save(tempPath, ImageFormat.Bmp);

            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);

            key.SetValue(@"WallpaperStyle", styleValues[style].Item1);
            key.SetValue(@"TileWallpaper", styleValues[style].Item2);

            SystemParametersInfo(SPI_SETDESKWALLPAPER,
                0,
                tempPath,
                SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }

        public static void Set(Bitmap img, Style style) {
            string tempPath = Path.Combine(Path.GetTempPath(), "wallpaper.bmp");
            img.Save(tempPath, ImageFormat.Bmp);

            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);

            key.SetValue(@"WallpaperStyle", styleValues[style].Item1);
            key.SetValue(@"TileWallpaper", styleValues[style].Item2);

            SystemParametersInfo(SPI_SETDESKWALLPAPER,
                0,
                tempPath,
                SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }
    }
}
