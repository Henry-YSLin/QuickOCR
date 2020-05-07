using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using Tesseract;

namespace QuickOCR
{
    public partial class Form1 : Form
    {
        Graphics pg;
        Image img;
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(System.Windows.Forms.Keys vkey);

        public Form1()
        {
            Thread.Sleep(500);
            ScreenCapture sc = new ScreenCapture();
            img = sc.CaptureScreen();
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
        }

        private void killListener_Tick(object sender, EventArgs e)
        {
            if (GetAsyncKeyState(Keys.ControlKey) != 0)
            {
                if (GetAsyncKeyState(Keys.ShiftKey) != 0)
                {
                    Application.Exit();
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            pictureBox1.BackgroundImage = img;
            pictureBox1.Image = new Bitmap(Width, Height);
            pg = Graphics.FromImage(pictureBox1.Image);
            pg.Clear(Color.FromArgb(100, 0, 0, 255));
            pictureBox1.Refresh();
        }

        Point sLoc = default(Point);
        Point endLoc = default(Point);
        Rectangle playArea;

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            sLoc = e.Location;
            endLoc = e.Location;
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                endLoc = e.Location;
                pg.Clear(Color.FromArgb(100, 0, 0, 255));
                pg.FillRectangle(new SolidBrush(Color.FromArgb(100, 255, 255, 255)), new Rectangle(Math.Min(sLoc.X, e.Location.X), Math.Min(sLoc.Y, e.Location.Y), Math.Abs(e.Location.X - sLoc.X), Math.Abs(e.Location.Y - sLoc.Y)));
                pictureBox1.Refresh();
            }
        }

        private async void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            Size = new Size(Width / 5, Height / 5);
            WindowState = FormWindowState.Normal;
            playArea = new Rectangle(Math.Min(sLoc.X, endLoc.X), Math.Min(sLoc.Y, endLoc.Y), Math.Abs(endLoc.X - sLoc.X), Math.Abs(endLoc.Y - sLoc.Y));
            if (Screen.GetBounds(this).Height - endLoc.Y > Height)
            {
                Location = new Point(sLoc.X, endLoc.Y);
                Width = playArea.Width;
            }
            else if (Screen.GetBounds(this).Width - endLoc.X > Width)
            {
                Location = new Point(endLoc.X, sLoc.Y);
                Height = playArea.Height;
            }
            else if (sLoc.X > Width)
            {
                Location = new Point(sLoc.X - Width, sLoc.Y);
                Height = playArea.Height;
            }
            else if (sLoc.Y > Height)
            {
                Location = new Point(sLoc.X, sLoc.Y - Height);
                Width = playArea.Width;
            }
            else
            {
                Location = new Point(sLoc.X, endLoc.Y);
                Width = playArea.Width;
            }
            if (playArea.Size.IsEmpty)
            {
                Application.Exit();
                return;
            }
            else
            {
                pictureBox1.BackgroundImage = null;
                pictureBox1.Image = null;
                textBox1.Show();
                textBox1.Text = "Loading...";
                pictureBox1.Hide();
                await Task.Run(() => doOCR());
            }
        }

        void doOCR()
        {
            var engine = new TesseractEngine("tessdata", "eng+chi_tra");
            Pix tessImg;
            using (var ms = new MemoryStream())
            {
                cropAtRect(img, playArea).Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                tessImg = Pix.LoadFromMemory(ms.ToArray());
            }
            using (var page = engine.Process(tessImg))
            {
                textBox1.Text = Regex.Replace(page.GetText(), @"\r\n|\r|\n", "\r\n");
            }
        }

        public static Bitmap cropAtRect(Image b, Rectangle r)
        {
            Bitmap nb = new Bitmap(r.Width, r.Height);
            Graphics g = Graphics.FromImage(nb);
            g.DrawImage(b, -r.X, -r.Y);
            return nb;
        }
    }

    /// <summary>
    /// Provides functions to capture the entire screen, or a particular window, and save it to a file.
    /// </summary>
    public class ScreenCapture
    {
        /// <summary>
        /// Creates an Image object containing a screen shot of the entire desktop
        /// </summary>
        /// <returns></returns>
        public Image CaptureScreen()
        {
            return CaptureWindow(User32.GetDesktopWindow());
        }
        /// <summary>
        /// Creates an Image object containing a screen shot of a specific window
        /// </summary>
        /// <param name="handle">The handle to the window. (In windows forms, this is obtained by the Handle property)</param>
        /// <returns></returns>
        public Image CaptureWindow(IntPtr handle)
        {
            // get te hDC of the target window
            IntPtr hdcSrc = User32.GetWindowDC(handle);
            // get the size
            User32.RECT windowRect = new User32.RECT();
            User32.GetWindowRect(handle, ref windowRect);
            int width = windowRect.right - windowRect.left;
            int height = windowRect.bottom - windowRect.top;
            // create a device context we can copy to
            IntPtr hdcDest = GDI32.CreateCompatibleDC(hdcSrc);
            // create a bitmap we can copy it to,
            // using GetDeviceCaps to get the width/height
            IntPtr hBitmap = GDI32.CreateCompatibleBitmap(hdcSrc, width, height);
            // select the bitmap object
            IntPtr hOld = GDI32.SelectObject(hdcDest, hBitmap);
            // bitblt over
            GDI32.BitBlt(hdcDest, 0, 0, width, height, hdcSrc, 0, 0, GDI32.SRCCOPY);
            // restore selection
            GDI32.SelectObject(hdcDest, hOld);
            // clean up 
            GDI32.DeleteDC(hdcDest);
            User32.ReleaseDC(handle, hdcSrc);
            // get a .NET image object for it
            Image img = Image.FromHbitmap(hBitmap);
            // free up the Bitmap object
            GDI32.DeleteObject(hBitmap);
            return img;
        }
        /// <summary>
        /// Captures a screen shot of a specific window, and saves it to a file
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="filename"></param>
        /// <param name="format"></param>
        public void CaptureWindowToFile(IntPtr handle, string filename, System.Drawing.Imaging.ImageFormat format)
        {
            Image img = CaptureWindow(handle);
            img.Save(filename, format);
        }
        /// <summary>
        /// Captures a screen shot of the entire desktop, and saves it to a file
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="format"></param>
        public void CaptureScreenToFile(string filename, System.Drawing.Imaging.ImageFormat format)
        {
            Image img = CaptureScreen();
            img.Save(filename, format);
        }

        /// <summary>
        /// Helper class containing Gdi32 API functions
        /// </summary>
        private class GDI32
        {

            public const int SRCCOPY = 0x00CC0020; // BitBlt dwRop parameter
            [DllImport("gdi32.dll")]
            public static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest,
                int nWidth, int nHeight, IntPtr hObjectSource,
                int nXSrc, int nYSrc, int dwRop);
            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth,
                int nHeight);
            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
            [DllImport("gdi32.dll")]
            public static extern bool DeleteDC(IntPtr hDC);
            [DllImport("gdi32.dll")]
            public static extern bool DeleteObject(IntPtr hObject);
            [DllImport("gdi32.dll")]
            public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        }

        /// <summary>
        /// Helper class containing User32 API functions
        /// </summary>
        private class User32
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int left;
                public int top;
                public int right;
                public int bottom;
            }
            [DllImport("user32.dll")]
            public static extern IntPtr GetDesktopWindow();
            [DllImport("user32.dll")]
            public static extern IntPtr GetWindowDC(IntPtr hWnd);
            [DllImport("user32.dll")]
            public static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);
            [DllImport("user32.dll")]
            public static extern IntPtr GetWindowRect(IntPtr hWnd, ref RECT rect);
        }
    }
}
