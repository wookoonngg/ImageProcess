using System;
using System.Runtime.InteropServices;

public struct IpRoi { public int X, Y, Width, Height; }

public static class Native {
  [DllImport("ImageProcessingNative", CallingConvention = CallingConvention.Cdecl)]
  public static extern int IpThreshold(byte[] src, byte[] dst, int width, int height, int thresholdValue, IntPtr roi);
}

public static class Program {
  public static void Main() {
    int w=10, h=10;
    var src = new byte[w*h];
    for (int i=0;i<src.Length;i++) src[i]=200;
    var dst = new byte[w*h];
    var arr = new[]{ new IpRoi{ X=2, Y=2, Width=3, Height=3 } };
    var handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
    try {
      int st = Native.IpThreshold(src, dst, w, h, 128, handle.AddrOfPinnedObject());
      int changed=0, outsideWrong=0;
      for (int y=0;y<h;y++) for (int x=0;x<w;x++) {
        int i=y*w+x;
        bool inRoi = x>=2&&x<5&&y>=2&&y<5;
        if (inRoi) { if (dst[i]==255) changed++; }
        else { if (dst[i]!=200) outsideWrong++; }
      }
      var dst2 = new byte[w*h];
      int st2 = Native.IpThreshold(src, dst2, w, h, 128, IntPtr.Zero);
      int fullBin=0; for (int i=0;i<dst2.Length;i++) if (dst2[i]==255) fullBin++;

      // invalid ROI outside image -> should fallback to full?
      var dst3 = new byte[w*h];
      var bad = new[]{ new IpRoi{ X=100, Y=100, Width=5, Height=5 } };
      var h2 = GCHandle.Alloc(bad, GCHandleType.Pinned);
      int st3 = Native.IpThreshold(src, dst3, w, h, 128, h2.AddrOfPinnedObject());
      h2.Free();
      int badFull=0; for (int i=0;i<dst3.Length;i++) if (dst3[i]==255) badFull++;

      Console.WriteLine("roiOK status="+st+" changed="+changed+"/9 outsideWrong="+outsideWrong);
      Console.WriteLine("nullRoi status="+st2+" fullBin="+fullBin+"/100");
      Console.WriteLine("badRoi status="+st3+" fullBin="+badFull+"/100 (fallback?)");
    } finally { handle.Free(); }
  }
}
