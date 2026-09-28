using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using ArcLight;
using ArcLightBrand;

class BrandTests
{
    [STAThread]
    static void Main(string[] args)
    {
        string output = args[0];
        Directory.CreateDirectory(output);
        int[] sizes = {16,24,32,48,64,128,256};
        using(Bitmap preview = new Bitmap(760, 350))
        using(Graphics g = Graphics.FromImage(preview))
        using(Font label = new Font("Segoe UI", 10))
        {
            g.Clear(Color.FromArgb(24,24,30));
            int x=20;
            foreach(int size in sizes)
            {
                using(Bitmap image=LogoArtwork.Render(size))
                {
                    if(image.Width!=size || image.Height!=size) throw new Exception("Wrong logo size");
                    bool color=false;
                    for(int y=0;y<size;y++) for(int col=0;col<size;col++) {Color c=image.GetPixel(col,y);if(c.R>80 || c.B>80) color=true;}
                    if(!color) throw new Exception("Logo blank");
                    g.DrawImageUnscaled(image,x,55);
                    g.DrawString(size.ToString(),label,Brushes.White,x,22);
                    x+=size+20;
                }
                Console.WriteLine("PASS logo "+size);
            }
            preview.Save(Path.Combine(output,"logo-preview.png"),ImageFormat.Png);
        }
        using(Button button=new Button())
        {
            button.Font=new Font("Segoe UI",8,FontStyle.Regular);
            Font original=button.Font;
            for(int i=0;i<10000;i++) MainForm.SetButtonFont(button,8,FontStyle.Regular);
            if(!Object.ReferenceEquals(original,button.Font)) throw new Exception("Unchanged state allocated fonts");
            MainForm.SetButtonFont(button,8,FontStyle.Bold);
            if(!button.Font.Bold) throw new Exception("Active font missing");
            Font active=button.Font;
            for(int i=0;i<10000;i++) MainForm.SetButtonFont(button,8,FontStyle.Bold);
            if(!Object.ReferenceEquals(active,button.Font)) throw new Exception("Active state allocated fonts");
            button.Font.Dispose();
        }
        Console.WriteLine("PASS 20000 unchanged button updates reuse their font; active state changes correctly");
    }
}
