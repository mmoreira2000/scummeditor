using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace ScummEditor.Encoders
{
    public class ImageDepthConversor
    {
        /// <summary>
        /// Copies a bitmap into a 1bpp/8bpp bitmap of the same dimensions, fast
        /// </summary>
        /// <param name="b">original bitmap</param>
        /// <param name="bpp">1 or 8, target bpp</param>
        /// <returns>a 1bpp copy of the bitmap</returns>
        public System.Drawing.Bitmap CopyToBpp(System.Drawing.Bitmap b, int bpp, Color[] pallete = null)
        {
            if (bpp != 1 && bpp != 8) throw new System.ArgumentException("1 or 8", "bpp");

            // Plan: built into Windows GDI is the ability to convert
            // bitmaps from one format to another. Most of the time, this
            // job is actually done by the graphics hardware accelerator card
            // and so is extremely fast. The rest of the time, the job is done by
            // very fast native code.
            // We will call into this GDI functionality from C#. Our plan:
            // (1) Convert our Bitmap into a GDI hbitmap (ie. copy unmanaged->managed)
            // (2) Create a GDI monochrome hbitmap
            // (3) Use GDI "BitBlt" function to copy from hbitmap into monochrome (as above)
            // (4) Convert the monochrone hbitmap into a Bitmap (ie. copy unmanaged->managed)

            int w = b.Width, h = b.Height;
            IntPtr hbm = b.GetHbitmap(); // this is step (1)
            //
            // Step (2): create the monochrome bitmap.
            // "BITMAPINFO" is an interop-struct which we define below.
            // In GDI terms, it's a BITMAPHEADERINFO followed by an array of two RGBQUADs
            BITMAPINFO bmi = new BITMAPINFO();
            bmi.biSize = 40;  // the size of the BITMAPHEADERINFO struct
            bmi.biWidth = w;
            bmi.biHeight = h;
            bmi.biPlanes = 1; // "planes" are confusing. We always use just 1. Read MSDN for more info.
            bmi.biBitCount = (short)bpp; // ie. 1bpp or 8bpp
            bmi.biCompression = BI_RGB; // ie. the pixels in our RGBQUAD table are stored as RGBs, not palette indexes
            bmi.biSizeImage = (uint)(((w + 7) & 0xFFFFFFF8) * h / 8);
            bmi.biXPelsPerMeter = 1000000; // not really important
            bmi.biYPelsPerMeter = 1000000; // not really important
            // Now for the colour table.
            uint ncols = (uint)1 << bpp; // 2 colours for 1bpp; 256 colours for 8bpp
            bmi.biClrUsed = ncols;
            bmi.biClrImportant = ncols;
            bmi.cols = new uint[256]; // The structure always has fixed size 256, even if we end up using fewer colours
            if (bpp == 1)
            {
                if (pallete == null)
                {
                    bmi.cols[0] = MAKERGB(0, 0, 0);
                    bmi.cols[1] = MAKERGB(255, 255, 255);
                }
                else
                {
                    bmi.cols[0] = MAKERGB(pallete[0].R, pallete[0].G, pallete[0].B);
                    bmi.cols[1] = MAKERGB(pallete[1].R, pallete[1].G, pallete[1].B);
                }
            }
            else
            {
                if (pallete == null)
                {
                    for (int i = 0; i < ncols; i++)
                    {
                        bmi.cols[i] = MAKERGB(i, i, i);
                    }
                }
                else
                {
                    for (int i = 0; i < ncols; i++)
                    {
                        bmi.cols[i] = MAKERGB(pallete[i].R, pallete[i].G, pallete[i].B);
                    }
                }
            }
            // For 8bpp we've created an palette with just greyscale colours.
            // You can set up any palette you want here. Here are some possibilities:
            // greyscale: for (int i=0; i<256; i++) bmi.cols[i]=MAKERGB(i,i,i);
            // rainbow: bmi.biClrUsed=216; bmi.biClrImportant=216; int[] colv=new int[6]{0,51,102,153,204,255};
            //          for (int i=0; i<216; i++) bmi.cols[i]=MAKERGB(colv[i/36],colv[(i/6)%6],colv[i%6]);
            // optimal: a difficult topic: http://en.wikipedia.org/wiki/Color_quantization
            // 
            // Now create the indexed bitmap "hbm0"
            IntPtr bits0; // not used for our purposes. It returns a pointer to the raw bits that make up the bitmap.
            IntPtr hbm0 = CreateDIBSection(IntPtr.Zero, ref bmi, DIB_RGB_COLORS, out bits0, IntPtr.Zero, 0);
            //
            // Step (3): use GDI's BitBlt function to copy from original hbitmap into monocrhome bitmap
            // GDI programming is kind of confusing... nb. The GDI equivalent of "Graphics" is called a "DC".
            IntPtr sdc = GetDC(IntPtr.Zero);       // First we obtain the DC for the screen
            // Next, create a DC for the original hbitmap
            IntPtr hdc = CreateCompatibleDC(sdc); SelectObject(hdc, hbm);
            // and create a DC for the monochrome hbitmap
            IntPtr hdc0 = CreateCompatibleDC(sdc); SelectObject(hdc0, hbm0);
            // Now we can do the BitBlt:
            BitBlt(hdc0, 0, 0, w, h, hdc, 0, 0, SRCCOPY);
            // Step (4): convert this monochrome hbitmap back into a Bitmap:
            System.Drawing.Bitmap b0 = System.Drawing.Bitmap.FromHbitmap(hbm0);
            //
            // Finally some cleanup.
            DeleteDC(hdc);
            DeleteDC(hdc0);
            ReleaseDC(IntPtr.Zero, sdc);
            DeleteObject(hbm);
            DeleteObject(hbm0);
            //
            return b0;
        }

        /// <summary>
        /// Copies a bitmap into a 1bpp bitmap of the same dimensions, slowly, using code from Bob Powell's GDI+ faq http://www.bobpowell.net/onebit.htm
        /// </summary>
        /// <param name="b">original bitmap</param>
        /// <returns>a 1bpp copy of the bitmap</returns>
        static System.Drawing.Bitmap FaqCopyTo1bpp(System.Drawing.Bitmap b)
        {
            int w = b.Width, h = b.Height; System.Drawing.Rectangle r = new System.Drawing.Rectangle(0, 0, w, h);
            if (b.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppPArgb)
            {
                System.Drawing.Bitmap temp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(temp);
                g.DrawImage(b, r, 0, 0, w, h, System.Drawing.GraphicsUnit.Pixel);
                g.Dispose(); b = temp;
            }
            System.Drawing.Imaging.BitmapData bdat = b.LockBits(r, System.Drawing.Imaging.ImageLockMode.ReadOnly, b.PixelFormat);
            System.Drawing.Bitmap b0 = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
            System.Drawing.Imaging.BitmapData b0dat = b0.LockBits(r, System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format1bppIndexed);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int index = y * bdat.Stride + (x * 4);
                    if (System.Drawing.Color.FromArgb(System.Runtime.InteropServices.Marshal.ReadByte(bdat.Scan0, index + 2), System.Runtime.InteropServices.Marshal.ReadByte(bdat.Scan0, index + 1), System.Runtime.InteropServices.Marshal.ReadByte(bdat.Scan0, index)).GetBrightness() > 0.5f)
                    {
                        int index0 = y * b0dat.Stride + (x >> 3);
                        byte p = System.Runtime.InteropServices.Marshal.ReadByte(b0dat.Scan0, index0);
                        byte mask = (byte)(0x80 >> (x & 0x7));
                        System.Runtime.InteropServices.Marshal.WriteByte(b0dat.Scan0, index0, (byte)(p | mask));
                    }
                }
            }
            b0.UnlockBits(b0dat);
            b.UnlockBits(bdat);
            return b0;
        }

        /// <summary>
        /// Draws a bitmap onto the screen. Note: this will be overpainted
        /// by other windows when they come to draw themselves. Only use it
        /// if you want to draw something quickly and can't be bothered with forms.
        /// </summary>
        /// <param name="b">the bitmap to draw on the screen</param>
        /// <param name="x">x screen coordinate</param>
        /// <param name="y">y screen coordinate</param>
        static void SplashImage(System.Drawing.Bitmap b, int x, int y)
        { // Drawing onto the screen is supported by GDI, but not by the Bitmap/Graphics class.
            // So we use interop:
            // (1) Copy the Bitmap into a GDI hbitmap
            IntPtr hbm = b.GetHbitmap();
            // (2) obtain the GDI equivalent of a "Graphics" for the screen
            IntPtr sdc = GetDC(IntPtr.Zero);
            // (3) obtain the GDI equivalent of a "Graphics" for the hbitmap
            IntPtr hdc = CreateCompatibleDC(sdc);
            SelectObject(hdc, hbm);
            // (4) Draw from the hbitmap's "Graphics" onto the screen's "Graphics"
            BitBlt(sdc, x, y, b.Width, b.Height, hdc, 0, 0, SRCCOPY);
            // and do boring GDI cleanup:
            DeleteDC(hdc);
            ReleaseDC(IntPtr.Zero, sdc);
            DeleteObject(hbm);
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int InvalidateRect(IntPtr hwnd, IntPtr rect, int bErase);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern int DeleteDC(IntPtr hdc);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern int BitBlt(IntPtr hdcDst, int xDst, int yDst, int w, int h, IntPtr hdcSrc, int xSrc, int ySrc, int rop);
        static int SRCCOPY = 0x00CC0020;

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi, uint Usage, out IntPtr bits, IntPtr hSection, uint dwOffset);
        static uint BI_RGB = 0;
        static uint DIB_RGB_COLORS = 0;
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public uint biSize;
            public int biWidth, biHeight;
            public short biPlanes, biBitCount;
            public uint biCompression, biSizeImage;
            public int biXPelsPerMeter, biYPelsPerMeter;
            public uint biClrUsed, biClrImportant;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 256)]
            public uint[] cols;
        }

        static uint MAKERGB(int r, int g, int b)
        {
            return ((uint)(b & 255)) | ((uint)((g & 255) << 8)) | ((uint)((r & 255) << 16));
        }


    }
}
