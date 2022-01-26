using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Himawari {

    public enum OperationType {
        NewLayer, Get, Fill, And, Add, Multiply, ClampedMultiply, Lighter, AddHSL, MultiplyHSL, Return
    }

    public class Operation {
        public string Layer;    // layer to apply operation to
        public OperationType Blend; // blend action to take

        public string Param; // parameter: it's either a layer number or a parsable string

        public int AsyncGroup; // operations that can be performed together, must be ascending, if all values are 0, each step will be performed sequentially
        public Operation() { }
        public Operation(string layer, OperationType blend, string param, int asyncGroup) {
            Layer = layer;
            Blend = blend;
            Param = param;
            AsyncGroup = asyncGroup;
        }

        public Operation(OperationType blend, string param, int asyncGroup) {
            Blend = blend;
            Param = param;
            AsyncGroup = asyncGroup;
        }

        public Operation(string layer, OperationType blend, int asyncGroup) {
            Layer = layer;
            Blend = blend;
            AsyncGroup = asyncGroup;
        }
    }

    public class ImageParams {
        public Point EarthDims { get; set; }        // xy dimensions of how big you want earth to be
        public Point EarthCentre { get; set; }      // where to put the earth relative to screens (without offset)
        public Operation[] Operations { get; set; } // image proccessing operations
    }

    class Composer {
        static Bitmap Compose(ImageParams param) { 
            foreach(int zoom in Scraper.ACCEPTABLE_WIDEBAND_ZOOMS) {

            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="earthDiameter">Desired diameter of the Earth image</param>
        /// <param name="earthLocation"></param>
        /// <param name="finalDimensions"></param>
        /// <returns></returns>
        static (Point, Point) CalculateRegion(int earthDiameter, Point earthLocation, Point finalDimensions) {
            // calculate left bound
            var left = earthLocation.X - earthDiameter / 2
            // calculate right bound
            // calculate top bound
            // calculate bot bound
        }
    }
}
