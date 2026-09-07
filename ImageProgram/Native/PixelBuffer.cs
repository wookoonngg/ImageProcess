using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WpfImageProcessing.Native
{
    /// <summary>
    /// C++ DLL과 주고받는 8bit grayscale 버퍼.
    /// stride == Width (패딩 없음) — 네이티브 API 계약과 동일.
    /// </summary>
    public sealed class PixelBuffer
    {
        public byte[] Data { get; }
        public int Width { get; }
        public int Height { get; }

        public PixelBuffer(byte[] data, int width, int height)
        {
            if (data.Length < checked(width * height))
                throw new ArgumentException("버퍼 크기가 Width×Height보다 작습니다.");
            Data = data;
            Width = width;
            Height = height;
        }

        public PixelBuffer CloneEmpty() => new(new byte[Width * Height], Width, Height);

        public PixelBuffer Clone()
        {
            var copy = new byte[Data.Length];
            Buffer.BlockCopy(Data, 0, copy, 0, Data.Length);
            return new PixelBuffer(copy, Width, Height);
        }

        /// <summary>System.Drawing Bitmap → Gray8.</summary>
        public static PixelBuffer FromBitmap(Bitmap bitmap)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            var gray = new byte[checked(w * h)];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = bitmap.GetPixel(x, y);
                    gray[y * w + x] = (byte)((c.R * 30 + c.G * 59 + c.B * 11) / 100);
                }
            }

            return new PixelBuffer(gray, w, h);
        }

        public Bitmap ToBitmap()
        {
            var bmp = new Bitmap(Width, Height, PixelFormat.Format24bppRgb);
            var rect = new Rectangle(0, 0, Width, Height);
            BitmapData bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                int stride = bd.Stride;
                byte[] row = new byte[stride];
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        byte g = Data[y * Width + x];
                        int i = x * 3;
                        row[i] = g;
                        row[i + 1] = g;
                        row[i + 2] = g;
                    }
                    Marshal.Copy(row, 0, bd.Scan0 + y * stride, stride);
                }
            }
            finally
            {
                bmp.UnlockBits(bd);
            }
            return bmp;
        }
    }
}
