using System.Runtime.InteropServices;
using WpfImageProcessing.Models;

namespace WpfImageProcessing.Native
{
    /// <summary>네이티브 DLL 파일명 (exe와 같은 폴더).</summary>
    public static class NativeLibraryNames
    {
        public const string ImageProcessing = "ImageProcessingNative";
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IpRoi
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public static IpRoi From(RoiData roi) => new()
        {
            X = roi.StartX,
            Y = roi.StartY,
            Width = roi.Width,
            Height = roi.Height
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IpMatchResultNative
    {
        public int BestX;
        public int BestY;
        public double Score;
    }

    /// <summary>
    /// C++ <c>ImageProcessingApi.h</c> export와 1:1 P/Invoke.
    /// ROI는 null이면 IntPtr.Zero (전체 이미지).
    /// </summary>
    internal static class NativeMethods
    {
        private const string Dll = NativeLibraryNames.ImageProcessing;

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int IpDilation(byte[] src, byte[] dst, int width, int height, int kernelSize, IntPtr roi);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int IpErosion(byte[] src, byte[] dst, int width, int height, int kernelSize, IntPtr roi);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int IpSmoothing(byte[] src, byte[] dst, int width, int height, int kernelSize, IntPtr roi);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int IpThreshold(byte[] src, byte[] dst, int width, int height, int thresholdValue, IntPtr roi);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int IpGaussian(byte[] src, byte[] dst, int width, int height, int kernelSize, double sigma, IntPtr roi);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int IpLaplacian(byte[] src, byte[] dst, int width, int height, IntPtr roi);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int IpSobel(byte[] src, byte[] dst, int width, int height, IntPtr roi);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int IpTemplateMatch(
            byte[] image, int imageWidth, int imageHeight,
            byte[] templ, int templWidth, int templHeight,
            int method,
            out IpMatchResultNative outResult,
            IntPtr searchRoi);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int IpExtractRoi(
            byte[] src, int srcWidth, int srcHeight,
            ref IpRoi roi,
            byte[] dst, int dstCapacityBytes);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int IpComputeHistogram(
            byte[] src, int width, int height,
            IntPtr roi,
            int[] bins256);
    }
}
