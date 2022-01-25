using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Himawari {
    public enum BlendParamType {
        Layer, String, None
    }
    public enum OperationType {
        NewLayer, Get, Fill, And, Add, Multiply, ClampedMultiply, Lighter, AddHSL, MultiplyHSL, Return
    }

    public class Operation {
        public string Layer;    // layer to apply operation to
        public OperationType Blend; // blend action to take

        public BlendParamType ParamType; // parameters type for the action
        public string Param; // parameter: it's either a layer number or a parsable string

        public int AsyncGroup; // operations that can be performed together, must be ascending, if all values are 0, each step will be performed sequentially
        public Operation() { }
        public Operation(string layer, OperationType blend, BlendParamType paramType, string param, int asyncGroup) {
            Layer = layer;
            Blend = blend;
            ParamType = paramType;
            Param = param;
            AsyncGroup = asyncGroup;
        }

        public Operation(OperationType blend, BlendParamType paramType, string param, int asyncGroup) {
            Blend = blend;
            ParamType = paramType;
            Param = param;
            AsyncGroup = asyncGroup;
        }

        public Operation(string layer, OperationType blend, int asyncGroup) {
            Layer = layer;
            Blend = blend;
            ParamType = BlendParamType.None;
            AsyncGroup = asyncGroup;
        }
    }

    public struct ImageParams {
        public Point EarthDims { get; set; }        // xy dimensions of how big you want earth to be
        public Point EarthCentre { get; set; }      // where to put the earth relative to screens (without offset)
        public Dictionary<string, WaveLength> Get { get; set; }       // layers to get from the internet
        public Operation[] Operations { get; set; } // image proccessing operations
    }

    public class HimawariConfig : Config {
        public string[] Screens { get; set; }           // screen info to help with other params
        public Point[] ScreenOffsets { get; set; }      // offset of each screen
        public ImageParams DefaultImage { get; set; }  // default imaging params
        = new ImageParams() {
            EarthDims = new Point(5500, 5500),
            EarthCentre = new Point(1920, -850),
            Operations = new Operation[] {
                new Operation("B", OperationType.Get, BlendParamType.String, WaveLength.Blue047.ToString(), 0),
                new Operation("G", OperationType.Get, BlendParamType.String, WaveLength.Green051.ToString(), 0),
                new Operation("R", OperationType.Get, BlendParamType.String, WaveLength.Red064.ToString(), 0),
                new Operation("R", OperationType.Get, BlendParamType.String, WaveLength.FarIR133.ToString(), 0),
                                   
                new Operation("R", OperationType.And, BlendParamType.String, "#FFFF0000", 1),
                new Operation("R", OperationType.And, BlendParamType.String, "#FFFF0000", 1),
                new Operation("R", OperationType.And, BlendParamType.String, "#FFFF0000", 1),
                new Operation("I", OperationType.Multiply, BlendParamType.String, "#FF222222", 1),

                new Operation(OperationType.NewLayer, BlendParamType.String, "C", 2),
                new Operation("C", OperationType.Fill, BlendParamType.String, "#FF000000", 3),

                new Operation("C", OperationType.Add, BlendParamType.String, "B", 4),
                new Operation("C", OperationType.Add, BlendParamType.String, "G", 5),
                new Operation("C", OperationType.Add, BlendParamType.String, "R", 6),
                                   
                new Operation("C", OperationType.ClampedMultiply, BlendParamType.String, "1.5", 7),

                new Operation(OperationType.NewLayer, BlendParamType.String, "D", 8),
                new Operation("D", OperationType.Fill, BlendParamType.String, "#FF000000", 9),
                new Operation("D", OperationType.Add, BlendParamType.String, "I", 10),

                new Operation("C", OperationType.Lighter, BlendParamType.String, "D", 11),

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

            ScreenOffsets = new Point[Screens.Length];
        }

        public override void OnChanged() {

        }
    }
}
