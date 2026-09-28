using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;

namespace ArcLightBrand
{
    // One embedded original is shared by the application, installer and ICO builder.
    internal static class LogoArtwork
    {
        internal static Bitmap Render(int size)
        {
            if (size < 1 || size > 2048) throw new ArgumentOutOfRangeException("size");
            Bitmap result = new Bitmap(size, size);
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ArcLight.Logo.png"))
                {
                    if (stream == null) throw new InvalidDataException("Embedded logo is missing.");
                    using (Image source = Image.FromStream(stream))
                    using (Graphics graphics = Graphics.FromImage(result))
                    {
                        // Match the original background instead of adding black letterbox bars.
                        graphics.Clear(((Bitmap)source).GetPixel(0, 0));
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        float scale = Math.Min((float)size / source.Width, (float)size / source.Height);
                        float width = source.Width * scale, height = source.Height * scale;
                        graphics.DrawImage(source, new RectangleF((size - width) / 2, (size - height) / 2, width, height));
                    }
                }
                return result;
            }
            catch { result.Dispose(); throw; }
        }
    }
}


