using System;
using System.Diagnostics;
using System.IO;
using WpfImageProcessing.Services;
using WpfImageProcessing.Utils;

class Program {
  [STAThread]
  static int Main() {
    var logger = new ConsoleLogger();
    var svc = new ImageFileService(logger);
    string sample = @"c:\Users\woobin\Desktop\ImageProgram\ImageProgram\Resource\TestImages\sample_64x64.bmp";
    string foundry = @"c:\Users\woobin\Desktop\ImageProgram\ImageProgram\Resource\TestImages\Foundry_4umDF 1.bmp";

    var h1 = svc.ReadHeader(sample);
    var img1 = BmpDisplayLoader.Load(sample, h1);
    Console.WriteLine($"sample OK {img1.PixelWidth}x{img1.PixelHeight}");

    var h2 = svc.ReadHeader(foundry);
    var sw = Stopwatch.StartNew();
    var (px,w,h) = BmpDisplayLoader.LoadSubsampledPixels(foundry, h2);
    var img2 = BmpDisplayLoader.CreateBgraBitmap(px,w,h);
    sw.Stop();
    Console.WriteLine($"foundry OK {img2.PixelWidth}x{img2.PixelHeight} in {sw.ElapsedMilliseconds}ms pixels={px.Length}");
    return 0;
  }
}
