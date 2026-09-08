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

        /// <summary>
        /// BGRA32 표시용 픽셀(대용량 Preview) → Gray8 처리 버퍼.
        /// 원본 전체 대신 화면 Preview로 연산을 수행할 때 사용.
        /// </summary>
        public static PixelBuffer FromBgra32(byte[] bgra, int width, int height)
        {
            int expected = checked(width * height * 4);
            if (bgra.Length < expected)
                throw new ArgumentException("BGRA 버퍼 크기가 부족합니다.");

            var gray = new byte[checked(width * height)];
            for (int i = 0, p = 0; i < gray.Length; i++, p += 4)
            {
                byte b = bgra[p];
                byte g = bgra[p + 1];
                byte r = bgra[p + 2];
                gray[i] = (byte)((r * 30 + g * 59 + b * 11) / 100);
            }
            return new PixelBuffer(gray, width, height);
        }

        /// <summary>WPF BitmapSource → Gray8 (표시 이미지 기준 처리용).</summary>
        public static PixelBuffer FromBitmapSource(System.Windows.Media.Imaging.BitmapSource source)
        {
            var converted = new System.Windows.Media.Imaging.FormatConvertedBitmap(
                source, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            int w = converted.PixelWidth;
            int h = converted.PixelHeight;
            int stride = w * 4;
            var bgra = new byte[checked(h * stride)];
            converted.CopyPixels(bgra, stride, 0);
            return FromBgra32(bgra, w, h);
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
