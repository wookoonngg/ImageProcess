using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfImageProcessing.Models;

namespace WpfImageProcessing.Utils
{
    /// <summary>
    /// 화면 표시용 BMP 로드.
    /// WPF BitmapImage는 2GB 초과 / 초대형 BMP에서 실패하므로,
    /// 큰 파일은 헤더 기준으로 픽셀을 직접 샘플링한다.
    /// </summary>
    public static class BmpDisplayLoader
    {
        public static BitmapSource Load(string filePath, BitmapFileInfo info)
        {
            bool useDirectSample =
                info.FileSize > Constants.PREVIEW_MODE_BYTES ||
                info.GetActualPixelDataSize() > Constants.PREVIEW_MODE_BYTES ||
                info.Width > Constants.PREVIEW_MAX_PIXEL ||
                info.Height > Constants.PREVIEW_MAX_PIXEL;

            if (!useDirectSample)
            {
                try
                {
                    return LoadWithWpf(filePath);
                }
                catch
                {
                    // fall through to manual decode
                }
            }

            var (pixels, previewW, previewH) = LoadSubsampledPixels(filePath, info);
            return CreateBgraBitmap(pixels, previewW, previewH);
        }

        /// <summary>
        /// UI 스레드 블로킹 없이 샘플링만 백그라운드에서 수행.
        /// BitmapSource 생성은 UI 스레드에서 CreateBgraBitmap을 호출한다.
        /// </summary>
        public static (byte[] Pixels, int Width, int Height) LoadSubsampledPixels(string filePath, BitmapFileInfo info)
        {
            int stride = GetRowStride(info.Width, info.BitDepth);
            var fileInfo = new FileInfo(filePath);
            int readableHeight = GetReadableHeight(fileInfo.Length, info.PixelDataOffset, stride);
            if (readableHeight <= 0)
                throw new InvalidOperationException("BMP 픽셀 데이터 영역이 비어 있거나 파일이 손상되었습니다.");

            // 헤더 Height보다 파일이 짧은 경우(부분 저장/잘림) — 읽을 수 있는 행만 사용
            int sourceHeight = Math.Min(info.Height, readableHeight);

            int maxPixel = GetPreviewMaxPixel(info);
            int previewW = info.Width;
            int previewH = sourceHeight;
            if (previewW > maxPixel || previewH > maxPixel)
            {
                double scale = Math.Min(maxPixel / (double)info.Width, maxPixel / (double)sourceHeight);
                previewW = Math.Max(1, (int)(info.Width * scale));
                previewH = Math.Max(1, (int)(sourceHeight * scale));
            }

            byte[] pixels = new byte[checked(previewW * previewH * 4)];

            using var stream = new FileStream(
                filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                bufferSize: 1024 * 1024, options: FileOptions.RandomAccess);

            byte[]? palette = info.BitDepth == 8 ? ReadPalette(stream, info) : null;
            byte[] row = new byte[stride];

            for (int py = 0; py < previewH; py++)
            {
                int srcY = (int)((long)py * sourceHeight / previewH);
                srcY = Math.Clamp(srcY, 0, sourceHeight - 1);
                int fileY = info.IsBottomUp ? sourceHeight - 1 - srcY : srcY;

                long rowPos = (long)info.PixelDataOffset + (long)fileY * stride;
                stream.Seek(rowPos, SeekOrigin.Begin);
                ReadFully(stream, row);

                int destRow = py * previewW * 4;
                for (int px = 0; px < previewW; px++)
                {
                    int srcX = (int)((long)px * info.Width / previewW);
                    srcX = Math.Clamp(srcX, 0, info.Width - 1);
                    WriteBgra(pixels, destRow + px * 4, row, srcX, info.BitDepth, palette);
                }
            }

            return (pixels, previewW, previewH);
        }

        public static BitmapSource CreateBgraBitmap(byte[] pixels, int width, int height)
        {
            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            bitmap.Freeze();
            return bitmap;
        }

        private static int GetReadableHeight(long fileLength, int pixelDataOffset, int stride)
        {
            if (stride <= 0 || fileLength <= pixelDataOffset)
                return 0;
            return (int)((fileLength - pixelDataOffset) / stride);
        }

        private static int GetPreviewMaxPixel(BitmapFileInfo info)
        {
            if (info.FileSize > 2L * 1024 * 1024 * 1024 ||
                info.Width > 20000 || info.Height > 20000)
                return Constants.PREVIEW_MAX_PIXEL_HUGE;

            return Constants.PREVIEW_MAX_PIXEL;
        }

        private static int GetRowStride(int width, short bitDepth) =>
            ((width * bitDepth + 31) / 32) * 4;

        private static BitmapSource LoadWithWpf(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.PreservePixelFormat;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static byte[] ReadPalette(FileStream stream, BitmapFileInfo info)
        {
            int paletteStart = 14 + Math.Max(info.HeaderSize, 40);
            int paletteBytes = Math.Max(0, info.PixelDataOffset - paletteStart);
            int count = Math.Min(256, paletteBytes / 4);
            var palette = new byte[256 * 4];

            if (count <= 0)
            {
                for (int i = 0; i < 256; i++)
                {
                    palette[i * 4] = (byte)i;
                    palette[i * 4 + 1] = (byte)i;
                    palette[i * 4 + 2] = (byte)i;
                    palette[i * 4 + 3] = 255;
                }
                return palette;
            }

            stream.Seek(paletteStart, SeekOrigin.Begin);
            var raw = new byte[count * 4];
            ReadFully(stream, raw);
            Array.Copy(raw, palette, raw.Length);
            for (int i = count; i < 256; i++)
            {
                palette[i * 4] = (byte)i;
                palette[i * 4 + 1] = (byte)i;
                palette[i * 4 + 2] = (byte)i;
                palette[i * 4 + 3] = 255;
            }
            return palette;
        }

        private static void WriteBgra(byte[] dest, int destIndex, byte[] row, int srcX, short bitDepth, byte[]? palette)
        {
            byte b, g, r, a = 255;
            switch (bitDepth)
            {
                case 8:
                {
                    int index = row[srcX];
                    b = palette![index * 4];
                    g = palette[index * 4 + 1];
                    r = palette[index * 4 + 2];
                    break;
                }
                case 24:
                {
                    int i24 = srcX * 3;
                    b = row[i24];
                    g = row[i24 + 1];
                    r = row[i24 + 2];
                    break;
                }
                case 32:
                {
                    int i32 = srcX * 4;
                    b = row[i32];
                    g = row[i32 + 1];
                    r = row[i32 + 2];
                    a = row[i32 + 3] == 0 ? (byte)255 : row[i32 + 3];
                    break;
                }
                default:
                    b = g = r = 0;
                    break;
            }

            dest[destIndex] = b;
            dest[destIndex + 1] = g;
            dest[destIndex + 2] = r;
            dest[destIndex + 3] = a;
        }

        private static void ReadFully(Stream stream, byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = stream.Read(buffer, offset, buffer.Length - offset);
                if (read <= 0)
                    throw new EndOfStreamException("BMP 픽셀 데이터를 끝까지 읽지 못했습니다.");
                offset += read;
            }
        }
    }
}
