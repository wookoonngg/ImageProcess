using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

class P {
  static void Main() {
    var bmp = new Bitmap(100, 80, PixelFormat.Format24bppRgb);
    for (int y=0;y<80;y++) for (int x=0;x<100;x++) bmp.SetPixel(x,y,Color.FromArgb(x,y,50));
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Bmp);
    ms.Position = 0;
    var bi = new BitmapImage();
    bi.BeginInit(); bi.StreamSource = ms; bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit(); bi.Freeze();
    Console.WriteLine("bmp="+bmp.Width+"x"+bmp.Height+" dpi="+bmp.HorizontalResolution);
    Console.WriteLine("bi="+bi.PixelWidth+"x"+bi.PixelHeight+" dpi="+bi.DpiX+" layout="+bi.Width+"x"+bi.Height);
  }
}
