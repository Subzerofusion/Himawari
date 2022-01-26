using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Himawari {
    public class HimawariConfig : Config {
        public string[] Screens { get; set; }           // screen info to help with other params
        public Point[] ScreenOffsets { get; set; }      // offset of each screen
        public ImageParams DefaultImage { get; set; }   // default imaging params
        = new ImageParams() {
            Operations = new Operation[] {
                new Operation("B", OperationType.Get, WaveLength.Blue047.ToString(), 0),
                new Operation("G", OperationType.Get, WaveLength.Green051.ToString(), 0),
                new Operation("R", OperationType.Get, WaveLength.Red064.ToString(), 0),
                new Operation("R", OperationType.Get, WaveLength.FarIR133.ToString(), 0),
                                   
                new Operation("R", OperationType.And, "#FFFF0000", 1),
                new Operation("R", OperationType.And, "#FFFF0000", 1),
                new Operation("R", OperationType.And, "#FFFF0000", 1),
                new Operation("I", OperationType.Multiply, "#FF222222", 1),

                new Operation(OperationType.NewLayer, "C", 2),
                new Operation("C", OperationType.Fill, "#FF000000", 3),

                new Operation("C", OperationType.Add, "B", 4),
                new Operation("C", OperationType.Add, "G", 5),
                new Operation("C", OperationType.Add, "R", 6),

                new Operation("C", OperationType.ClampedMultiply, "1.5", 7),

                new Operation(OperationType.NewLayer, "D", 8),
                new Operation("D", OperationType.Fill, "#FF000000", 9),
                new Operation("D", OperationType.Add, "I", 10),

                new Operation("C", OperationType.Lighter, "D", 11),

                new Operation("C", OperationType.Return, 12)
            }
        };
        public Dictionary<string, Dictionary<string, ImageParams[]>> EarthOffsets { get; set; } = new Dictionary<string, Dictionary<string, ImageParams[]>>() {
            { "21 Mar", new Dictionary<string, ImageParams[]>(){
            } },
            { "21 Jun", new Dictionary<string, ImageParams[]>(){
            } },
            { "22 Sep", new Dictionary<string, ImageParams[]>(){
            } },
            { "22 Dec", new Dictionary<string, ImageParams[]>(){
            } }
        };

        public HimawariConfig() {
            Screens = Wallpaper.GetScreens().Select((x) => {
                Rectangle bounds = x.Bounds;
                string str = $"{x.DeviceName}: {bounds.Width}x{bounds.Height} ({bounds.X}, { bounds.Y})";
                return str;
            }).ToArray();

            var rect = Wallpaper.CalculateRectangle(Wallpaper.GetScreens());

            var zoomLvl = 1;
            foreach(int size in Scraper.ACCEPTABLE_WIDEBAND_ZOOMS) { // gets the largest raw image that'll fit on the screen
                if(size * Scraper.TILE_WIDTH < rect.Width && size * Scraper.TILE_HEIGHT < rect.Height) {
                    zoomLvl = size;
                } else {
                    break;
                }
            }

            DefaultImage.EarthDims = new Point(zoomLvl * Scraper.TILE_WIDTH, zoomLvl * Scraper.TILE_HEIGHT);
            DefaultImage.EarthCentre = new Point(rect.Width/ 2, rect.Height / 2);
            ScreenOffsets = new Point[Screens.Length];
        }

        public override void OnChanged() {

        }
    }
}
