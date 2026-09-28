using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

class IconBuilder
{
    static Bitmap RenderLogo(int size) { return ArcLightBrand.LogoArtwork.Render(size); }

    static void Main()
    {
        int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
        byte[][] imgData = new byte[sizes.Length][];
        bool[] isPng = new bool[sizes.Length];

        for (int i = 0; i < sizes.Length; i++)
        {
            int sz = sizes[i];
            using (Bitmap bmp = RenderLogo(sz))
            {
                if (sz == 256)
                {
                    // 256x256 is stored as PNG in modern Vista+ ICO
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        imgData[i] = ms.ToArray();
                        isPng[i] = true;
                    }
                }
                else
                {
                    // Sizes <= 128 stored as 32-bit uncompressed DIB (compatible with csc.exe Win32 compiler)
                    imgData[i] = CreateDibData(bmp);
                    isPng[i] = false;
                }
            }
        }

        using (FileStream fs = new FileStream("app.ico", FileMode.Create))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            // ICONDIR
            bw.Write((ushort)0); // Reserved
            bw.Write((ushort)1); // Type 1 = ICO
            bw.Write((ushort)sizes.Length);

            int offset = 6 + (sizes.Length * 16);

            for (int i = 0; i < sizes.Length; i++)
            {
                int sz = sizes[i];
                byte w = (byte)(sz >= 256 ? 0 : sz);
                byte h = (byte)(sz >= 256 ? 0 : sz);

                bw.Write(w);
                bw.Write(h);
                bw.Write((byte)0); // Color count
                bw.Write((byte)0); // Reserved
                bw.Write((ushort)1); // Planes
                bw.Write((ushort)32); // Bit count
                bw.Write((uint)imgData[i].Length); // Bytes in res
                bw.Write((uint)offset); // Offset

                offset += imgData[i].Length;
            }

            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write(imgData[i]);
            }
        }

        Console.WriteLine("app.ico generated successfully!");
    }

    static byte[] CreateDibData(Bitmap bmp)
    {
        int w = bmp.Width;
        int h = bmp.Height;
        int andRowBytes = ((w + 31) / 32) * 4;
        int andMaskSize = andRowBytes * h;
        int xorSize = w * h * 4;
        int totalSize = 40 + xorSize + andMaskSize;

        byte[] dib = new byte[totalSize];
        using (MemoryStream ms = new MemoryStream(dib))
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            // BITMAPINFOHEADER (40 bytes)
            bw.Write((uint)40);        // biSize
            bw.Write((int)w);           // biWidth
            bw.Write((int)(h * 2));     // biHeight (doubled for XOR + AND)
            bw.Write((ushort)1);        // biPlanes
            bw.Write((ushort)32);       // biBitCount
            bw.Write((uint)0);         // biCompression (BI_RGB)
            bw.Write((uint)(xorSize + andMaskSize)); // biSizeImage
            bw.Write((int)0);           // biXPelsPerMeter
            bw.Write((int)0);           // biYPelsPerMeter
            bw.Write((uint)0);         // biClrUsed
            bw.Write((uint)0);         // biClrImportant

            // XOR 32-bit pixel data (bottom-to-top)
            for (int y = h - 1; y >= 0; y--)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    bw.Write(c.B);
                    bw.Write(c.G);
                    bw.Write(c.R);
                    bw.Write(c.A);
                }
            }

            // AND mask (all zeros for 32-bit ARGB icons)
            byte[] andMask = new byte[andMaskSize];
            bw.Write(andMask);
        }
        return dib;
    }
}

