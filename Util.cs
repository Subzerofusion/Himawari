using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using static Himawari.ColorExtensions;

namespace Himawari {

    public static class NumericalExtensions {

        public static float Loop(this float f, float min, float max) {
            float diff = max - min; // overall range
            f -= min; //sets it to distance from min;
            f %= diff; //loops
            f = f < 0 ? f + diff : f; // covers when it's negative
            f += min; // return to original value range
            return f;
        }

        public static int Clamp(this int n, int min, int max) {
            if (n < min) return min;
            if (n > max) return max;
            return n;
        }

        public static float Clamp(this float n, float min, float max) {
            if (n < min) return min;
            if (n > max) return max;
            return n;
        }
    }

    public static class ColorExtensions {
        public struct HSL { public float h; public float s; public float l; }
        // the Color Converter

        public static Color ToColor(this HSL hsl) {
            if (hsl.s == 0) { int L = (int)hsl.l; return Color.FromArgb(255, L, L, L); }

            double min, max, h;
            h = hsl.h / 360d;

            max = hsl.l < 0.5d ? hsl.l * (1 + hsl.s) : (hsl.l + hsl.s) - (hsl.l * hsl.s);
            min = (hsl.l * 2d) - max;

            Color c = Color.FromArgb(byte.MaxValue, (int)(byte.MaxValue * RGBChannelFromHue(min, max, h + 1 / 3d)),
                                                    (int)(byte.MaxValue * RGBChannelFromHue(min, max, h)),
                                                    (int)(byte.MaxValue * RGBChannelFromHue(min, max, h - 1 / 3d)));
            return c;
        }

        public static double RGBChannelFromHue(double min, double max, double h) {
            h = (h + 1d) % 1d;
            if (h < 0) h += 1;
            if (h * 6 < 1) return min + (max - min) * 6 * h;
            else if (h * 2 < 1) return max;
            else if (h * 3 < 2) return min + (max - min) * 6 * (2d / 3d - h);
            else return min;
        }

        public static float GetHSLBrightness(this Color c) {
            return (c.R * 0.299f + c.G * 0.587f + c.B * 0.114f) / 256f;
        }

        public static HSL ToHSL(this Color c) {
            return new HSL() { h = c.GetHue(), s = c.GetSaturation(), l = c.GetHSLBrightness() };
        }
    }

    public static class BitmapExtensions {
        public delegate Color Filter<T>(Color a, T b);

        public class Filters {
            public static Color AddHSL(Color a, HSL b) {
                HSL hsl = a.ToHSL();
                hsl.h = (hsl.h + b.h).Loop(0, 360);
                hsl.s = (hsl.s + b.s).Clamp(0, 1);
                hsl.l = (hsl.l + b.l).Clamp(0, 1);
                return hsl.ToColor();
            }
            public static Color MultiplyHSL(Color a, HSL b) {
                HSL hsl = a.ToHSL();
                hsl.h = (hsl.h * b.h).Loop(0, 360);
                hsl.s = (hsl.s * b.s).Clamp(0, 1);
                hsl.l = (hsl.l * b.l).Clamp(0, 1);
                return hsl.ToColor();
            }
            public static Color And(Color a, Color b) {
                return Color.FromArgb(a.ToArgb() & b.ToArgb());
            }
            public static Color Add(Color a, Color b) {
                float t = b.A / (float)byte.MaxValue;
                var c = Color.FromArgb(
                        (a.A + b.A).Clamp(0, byte.MaxValue),
                        (a.R + (int)(b.R * t)).Clamp(0, byte.MaxValue),
                        (a.G + (int)(b.G * t)).Clamp(0, byte.MaxValue),
                        (a.B + (int)(b.B * t)).Clamp(0, byte.MaxValue)
                    );
                return c;
            }
            public static Color ClampedMultiply(Color a, float b) {
                var c = Color.FromArgb(
                        (int)(a.A * b).Clamp(0, byte.MaxValue),
                        (int)(a.R * b).Clamp(0, byte.MaxValue),
                        (int)(a.G * b).Clamp(0, byte.MaxValue),
                        (int)(a.B * b).Clamp(0, byte.MaxValue)
                    );
                return c;
            }
            public static Color Multiply(Color a, Color b) {
                var c = Color.FromArgb(
                        (int)((float)a.A * (float)b.A / byte.MaxValue),
                        (int)((float)a.R * (float)b.R / byte.MaxValue),
                        (int)((float)a.G * (float)b.G / byte.MaxValue),
                        (int)((float)a.B * (float)b.B / byte.MaxValue)
                    );
                return c;
            }
            public static Color Lighter(Color a, Color b) {
                return Color.FromArgb(
                    Math.Max(a.A, b.A),
                    Math.Max(a.R, b.R),
                    Math.Max(a.G, b.G),
                    Math.Max(a.B, b.B)
                    );
            }
        };
        public static Bitmap Fill(this Bitmap bitmap, Color color) {
            using (Graphics gfx = Graphics.FromImage(bitmap))
            using (SolidBrush brush = new SolidBrush(color))
                gfx.FillRectangle(brush, 0, 0, bitmap.Width, bitmap.Height);
            return bitmap;
        }

        public static async Task<Bitmap> Blend<T>(this Bitmap baseImage, Filter<T> filter, T filterParam) {
            Task<Bitmap> task = new Task<Bitmap>(() => {
                BitmapData baseImageData = baseImage.LockBits(new Rectangle(0, 0, baseImage.Width, baseImage.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                byte[] baseImageBuffer = new byte[baseImageData.Stride * baseImageData.Height];

                Marshal.Copy(baseImageData.Scan0, baseImageBuffer, 0, baseImageBuffer.Length);

                for (int k = 0; k < baseImageBuffer.Length; k += 4) {

                    var colour = filter(
                        Color.FromArgb(baseImageBuffer[k + 3], baseImageBuffer[k + 2], baseImageBuffer[k + 1], baseImageBuffer[k]), filterParam
                    );

                    baseImageBuffer[k] = colour.B;
                    baseImageBuffer[k + 1] = colour.G;
                    baseImageBuffer[k + 2] = colour.R;
                    baseImageBuffer[k + 3] = colour.A;
                }
                Marshal.Copy(baseImageBuffer, 0, baseImageData.Scan0, baseImageBuffer.Length);

                baseImage.UnlockBits(baseImageData);

                return baseImage;
            });
            task.Start();
            var result = await task;
            return result;
        }

        public static async Task<Bitmap> Blend(this Bitmap baseImage, Bitmap overlayImage, Filter<Color> filter) {
            Task<Bitmap> task = new Task<Bitmap>(() => {
                BitmapData baseImageData = baseImage.LockBits(new Rectangle(0, 0, baseImage.Width, baseImage.Height),
                  ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                byte[] baseImageBuffer = new byte[baseImageData.Stride * baseImageData.Height];

                Marshal.Copy(baseImageData.Scan0, baseImageBuffer, 0, baseImageBuffer.Length);

                BitmapData overlayImageData = overlayImage.LockBits(new Rectangle(0, 0, overlayImage.Width, overlayImage.Height),
                               ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                byte[] overlayImageBuffer = new byte[overlayImageData.Stride * overlayImageData.Height];


                Marshal.Copy(overlayImageData.Scan0, overlayImageBuffer, 0, overlayImageBuffer.Length);

                for (int k = 0; k < baseImageBuffer.Length && k < overlayImageBuffer.Length; k += 4) {

                    var colour = filter(
                        Color.FromArgb(baseImageBuffer[k + 3], baseImageBuffer[k + 2], baseImageBuffer[k + 1], baseImageBuffer[k]),
                        Color.FromArgb(overlayImageBuffer[k + 3], overlayImageBuffer[k + 2], overlayImageBuffer[k + 1], overlayImageBuffer[k])
                    );

                    baseImageBuffer[k] = colour.B;
                    baseImageBuffer[k + 1] = colour.G;
                    baseImageBuffer[k + 2] = colour.R;
                    baseImageBuffer[k + 3] = colour.A;
                }
                Marshal.Copy(baseImageBuffer, 0, baseImageData.Scan0, baseImageBuffer.Length);

                baseImage.UnlockBits(baseImageData);
                overlayImage.UnlockBits(overlayImageData);
                return baseImage;
            });
            task.Start();
            var result = await task;
            return result;
        }
    }
}