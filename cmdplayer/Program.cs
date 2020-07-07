using Emgu.CV;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
//using System.Drawing;
//using System.Drawing.Drawing2D;
//using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace cmdplayer {

    class Program {

        [StructLayout(LayoutKind.Sequential)]
        public struct COORD {
            public short X;
            public short Y;

            public COORD(short X, short Y) {
                this.X = X;
                this.Y = Y;
            }
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct CONSOLE_FONT_INFO_EX {
            public uint cbSize;
            public uint nFont;
            public COORD dwFontSize;
            public int FontFamily;
            public int FontWeight;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] // Edit sizeconst if the font name is too big
            public string FaceName;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetCurrentConsoleFontEx(IntPtr consoleOutput,
            bool maximumWindow, ref CONSOLE_FONT_INFO_EX consoleCurrentFontEx);

        private const int STD_OUTPUT_HANDLE = -11;
        private const int TMPF_TRUETYPE = 4;
        private const int LF_FACESIZE = 32;
        private static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport("kernel32")]
        private static extern IntPtr GetStdHandle(int dwType);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        static Factory factory;
        static SharpDX.DirectWrite.Factory dwFactory;

        [STAThread]
        static void Main(string[] args) {
            string filename = null;

            using(var dialog = new OpenFileDialog { Multiselect = false, Title = "Select video", Filter = "Video|*.mp4" }) {
                if(dialog.ShowDialog() == DialogResult.OK) {
                    filename = dialog.FileName;
                } else {
                    Environment.Exit(0);
                }
            }

            //CONSOLE_FONT_INFO_EX fontInfo = new CONSOLE_FONT_INFO_EX();
            //fontInfo.FaceName = "Terminal";
            //fontInfo.FontFamily = 0x00;
            //fontInfo.FontWeight = 400;
            //fontInfo.cbSize = 100;
            //fontInfo.dwFontSize.X = fontInfo.dwFontSize.Y = 8;

            //SetCurrentConsoleFontEx(GetStdHandle(STD_OUTPUT_HANDLE), false, ref fontInfo);

            MoveWindow(GetConsoleWindow(), 100, 100, 100, 100, true);
            Console.BufferWidth = 192;
            Console.BufferHeight = 108;
            Console.WindowWidth = 192;
            Console.WindowHeight = 108;

            factory = new Factory(FactoryType.MultiThreaded);
            dwFactory = new SharpDX.DirectWrite.Factory(SharpDX.DirectWrite.FactoryType.Shared);

            f = new SharpDX.DirectWrite.TextFormat(dwFactory, "Terminal", 8f);

            var renderTargetProperties = new RenderTargetProperties() {
                PixelFormat = new SharpDX.Direct2D1.PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Ignore)
            };

            var hwndRenderTargetProperties = new HwndRenderTargetProperties() {
                Hwnd = GetConsoleWindow(),
                PixelSize = new Size2(Console.WindowWidth * 8, Console.WindowHeight * 8),
                PresentOptions = PresentOptions.Immediately,
            };

            var renderTarget = new WindowRenderTarget(factory, renderTargetProperties, hwndRenderTargetProperties);

            AppDomain.CurrentDomain.ProcessExit += (s, e) => { renderTarget.EndDraw(); };

            //Graphics g = Graphics.FromHwnd(GetConsoleWindow());
            renderTarget.BeginDraw();

            PlayVideo(filename, renderTarget);
        }

        static SharpDX.DirectWrite.TextFormat f;
        //static System.Drawing.Point p = new System.Drawing.Point(0, 0);
        static RawRectangleF p = new RawRectangleF(0, 0, 8, 8);

        static void DrawChar(char c, Brush brush, WindowRenderTarget g) {
            //if((p.Y / 8) >= 108) {
            if((p.Top / 8) >= 108) {
                //Debug.WriteLine(p.Y);
                //p.X = 0;
                //p.Y = 0;
                p.Left = 0;
                p.Top = 0;
                g.DrawText(c.ToString(), f, p, brush);
                //g.DrawString(c.ToString(), f, brush, p);
            } else {
                //if ((p.X / 8) > (192 - 1)) {
                if((p.Left / 8) > (192 - 1)) {
                    //p.X = 0;
                    //p.Y += 8;
                    p.Left = 0;
                    p.Top += 8;
                    //g.DrawString(c.ToString(), f, brush, p);
                    g.DrawText(c.ToString(), f, p, brush);
                } else {
                    //g.DrawString(c.ToString(), f, brush, p);
                    g.DrawText(c.ToString(), f, p, brush);
                    //p.X += 8;
                    p.Left += 8;
                }
            }
        }

        static void PlayVideo(string filename, WindowRenderTarget g) {
            using (var video = new VideoCapture(filename)) {
                using (var img = new Mat()) {
                    while (video.Grab()) {
                        video.Retrieve(img);

                        System.Drawing.Bitmap resized = ResizeImage(img.ToBitmap(), 192, 108);
                        DrawAscii(resized, g);
                        //g.DrawImage(img.ToBitmap(), new Point(0, 0));
                    }
                }
            }
        }

        public static System.Drawing.Bitmap ResizeImage(System.Drawing.Bitmap image, int width, int height) {
            var destRect = new System.Drawing.Rectangle(0, 0, width, height);
            var destImage = new System.Drawing.Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = System.Drawing.Graphics.FromImage(destImage)) {
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                using (var wrapMode = new System.Drawing.Imaging.ImageAttributes()) {
                    wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, System.Drawing.GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        private static void DrawAscii(System.Drawing.Bitmap image, WindowRenderTarget g) {

            Boolean toggle = false;

            //StringBuilder sb = new StringBuilder();



            for (int h = 0; h < image.Height; h++) {

                for (int w = 0; w < image.Width; w++) {

                    System.Drawing.Color pixelColor = image.GetPixel(w, h);

                    DrawChar('█', new SolidColorBrush(g, new RawColor4(pixelColor.R, pixelColor.G, pixelColor.B, pixelColor.A)), g);

                    //Average out the RGB components to find the Gray Color

                    //int red = (pixelColor.R + pixelColor.G + pixelColor.B) / 3;

                    //int green = (pixelColor.R + pixelColor.G + pixelColor.B) / 3;

                    //int blue = (pixelColor.R + pixelColor.G + pixelColor.B) / 3;

                    //Color grayColor = Color.FromArgb(red, green, blue);



                    //Use the toggle flag to minimize height-wise stretch

                    //if (!toggle) {

                        //int index = (grayColor.R * 10) / 255;

                        //sb.Append(_AsciiChars[index]);
                        //DrawChar(g, '█', new SolidBrush(Color.FromArgb(pixelColor.A, pixelColor.R, pixelColor.G, pixelColor.B)));
                    //}

                }

                //if (!toggle) {

                //    sb.Append("\r\n");

                //    toggle = true;

                //} else {

                //    toggle = false;

                //}

            }

            //return sb.ToString();

        }
    }
}
