using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

class IconBuilder
{
    static Bitmap RenderLogo(int size)
    {
        Bitmap bmp = new Bitmap(size, size);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float s = (float)size;

            // 1. Dış İnce Glow / Yumuşak Halka
            using (GraphicsPath pGlow = new GraphicsPath())
            {
                pGlow.AddEllipse(s * 0.02f, s * 0.02f, s * 0.96f, s * 0.96f);
                using (PathGradientBrush pgb = new PathGradientBrush(pGlow))
                {
                    pgb.CenterColor = Color.FromArgb(249, 115, 22);
                    pgb.SurroundColors = new Color[] { Color.FromArgb(0, 249, 115, 22) };
                    g.FillPath(pgb, pGlow);
                }
            }

            // 2. Koyu Modern Zemin
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(22, 22, 28)))
            {
                g.FillEllipse(bgBrush, s * 0.04f, s * 0.04f, s * 0.92f, s * 0.92f);
            }

            // 3. Şık Çerçeve (Vibrant Orange)
            float penW = Math.Max(1.2f, s * 0.045f);
            using (Pen ringPen = new Pen(Color.FromArgb(249, 115, 22), penW))
            {
                g.DrawEllipse(ringPen, s * 0.05f, s * 0.05f, s * 0.90f, s * 0.90f);
            }

            // 4. Geometrik Keskin Büyük 'A' Harfi
            // Font yerine GraphicsPath ile elle çizim: Çok daha keskin, simetrik ve yüksek kaliteli!
            using (GraphicsPath aPath = new GraphicsPath())
            {
                // Dış 'A' Üçgeni
                PointF topOuter = new PointF(s * 0.50f, s * 0.15f);
                PointF btmLeftOuter = new PointF(s * 0.19f, s * 0.83f);
                PointF btmLeftInner = new PointF(s * 0.33f, s * 0.83f);
                PointF barLeft = new PointF(s * 0.38f, s * 0.60f);
                PointF barRight = new PointF(s * 0.62f, s * 0.60f);
                PointF btmRightInner = new PointF(s * 0.67f, s * 0.83f);
                PointF btmRightOuter = new PointF(s * 0.81f, s * 0.83f);

                // Sol bacak dış -> Tepe -> Sağ bacak dış -> Sağ bacak alt -> Sağ bacak iç -> Bar alt sağ -> Bar alt sol -> Sol bacak iç -> Sol bacak alt
                PointF[] aOutline = new PointF[]
                {
                    topOuter,
                    btmRightOuter,
                    btmRightInner,
                    new PointF(s * 0.63f, s * 0.66f),
                    new PointF(s * 0.37f, s * 0.66f),
                    btmLeftInner,
                    btmLeftOuter
                };
                aPath.AddPolygon(aOutline);

                // 'A' Harfinin İç Boşluğu (Üst Üçgen)
                PointF topHole = new PointF(s * 0.50f, s * 0.32f);
                PointF leftHole = new PointF(s * 0.40f, s * 0.54f);
                PointF rightHole = new PointF(s * 0.60f, s * 0.54f);
                GraphicsPath holePath = new GraphicsPath();
                holePath.AddPolygon(new PointF[] { topHole, rightHole, leftHole });

                using (Region reg = new Region(aPath))
                {
                    reg.Exclude(holePath);
                    using (Brush aBrush = new SolidBrush(Color.White))
                    {
                        g.FillRegion(aBrush, reg);
                    }
                }
            }

            // 5. 'A' Harfinin Ortasında Parlayan Altın Yıldız (5 Köşeli)
            PointF starCenter = new PointF(s * 0.50f, s * 0.44f);
            float outerR = s * 0.125f;
            float innerR = outerR * 0.48f;
            PointF[] starPts = new PointF[10];
            double step = Math.PI / 5.0;
            double startAngle = -Math.PI / 2.0;
            for (int i = 0; i < 10; i++)
            {
                float r = (i % 2 == 0) ? outerR : innerR;
                double a = startAngle + i * step;
                starPts[i] = new PointF(starCenter.X + (float)(r * Math.Cos(a)), starCenter.Y + (float)(r * Math.Sin(a)));
            }

            using (Brush starBrush = new SolidBrush(Color.FromArgb(251, 191, 36)))
            {
                g.FillPolygon(starBrush, starPts);
            }
            using (Pen starBorder = new Pen(Color.FromArgb(217, 119, 6), Math.Max(0.6f, s * 0.015f)))
            {
                g.DrawPolygon(starBorder, starPts);
            }
        }
        return bmp;
    }

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
