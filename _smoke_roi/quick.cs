using System; using System.Runtime.InteropServices;
public struct IpRoi { public int X,Y,Width,Height; }
public class T {
 [DllImport("ImageProcessingNative", CallingConvention=CallingConvention.Cdecl)]
 static extern int IpThreshold(byte[] s, byte[] d, int w, int h, int t, IntPtr r);
 static void Run(string name, IpRoi? roi) {
  var src=new byte[100]; for(int i=0;i<100;i++) src[i]=200; var dst=new byte[100];
  IntPtr p=IntPtr.Zero; GCHandle g=default; bool pinned=false;
  if (roi.HasValue) { var a=new[]{roi.Value}; g=GCHandle.Alloc(a,GCHandleType.Pinned); p=g.AddrOfPinnedObject(); pinned=true; }
  int st=IpThreshold(src,dst,10,10,128,p);
  if (pinned) g.Free();
  Console.WriteLine(name+" status="+st+" marker="+dst[0]+","+dst[1]+","+dst[2]+","+dst[3]+","+dst[4]+","+dst[5]+","+dst[6]+","+dst[7]);
 }
 public static void Main() {
  Run("valid", new IpRoi{X=2,Y=2,Width=3,Height=3});
  Run("bad", new IpRoi{X=100,Y=100,Width=5,Height=5});
  Run("null", null);
 }
}
