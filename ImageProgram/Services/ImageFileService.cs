using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using WpfImageProcessing.Models;
using WpfImageProcessing.Utils;

namespace WpfImageProcessing.Services
{
    /// <summary>
    /// BMP 파일 읽기/쓰기 (외부 라이브러리 없이 헤더 파싱 + System.Drawing 로드)
    /// </summary>
    public sealed class ImageFileService : IImageFileService
    {
        private const ushort BmpMagic = 0x4D42; // 'BM'
        private readonly ILogger _logger;

        public ImageFileService(ILogger logger)
        {
            _logger = logger;
        }

        public BitmapFileInfo ReadHeader(string filePath)
        {
            ValidationHelper.ValidateFileReadable(filePath);

            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);

            ushort signature = reader.ReadUInt16();
            if (signature != BmpMagic)
                throw new InvalidOperationException(Constants.ERROR_INVALID_BMP);

            reader.ReadUInt32(); // file size in header (32bit, 4GB 이상에서 부정확)
            reader.ReadUInt16(); // reserved
            reader.ReadUInt16(); // reserved
            int pixelDataOffset = reader.ReadInt32();

            int headerSize = reader.ReadInt32();
            if (headerSize < 40)
                throw new InvalidOperationException("지원하지 않는 BMP 헤더 크기입니다.");

            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            reader.ReadUInt16(); // planes
            short bitDepth = reader.ReadInt16();
            int compression = reader.ReadInt32();

            ValidationHelper.ValidateHeaderDimensions(Math.Abs(width), Math.Abs(height));

            var info = new BitmapFileInfo
            {
                Width = Math.Abs(width),
                Height = Math.Abs(height),
                BitDepth = bitDepth,
                FileSize = new FileInfo(filePath).Length,
                PixelDataOffset = pixelDataOffset,
                HeaderSize = headerSize,
                IsBottomUp = height > 0,
                CompressionType = compression,
                LoadedTime = DateTime.Now
            };

            if (!info.IsValid())
                throw new InvalidOperationException(Constants.ERROR_UNSUPPORTED_FORMAT);

            _logger.Debug($"BMP 헤더: {info}");
            return info;
        }

        public Bitmap LoadImage(string filePath)
        {
            ValidationHelper.ValidateFileReadable(filePath);
            ReadHeader(filePath);

            try
            {
                var bitmap = new Bitmap(filePath);
                _logger.Info($"이미지 로드: {filePath} ({bitmap.Width}×{bitmap.Height})");
                return bitmap;
            }
            catch (OutOfMemoryException)
            {
                throw new InvalidOperationException(Constants.ERROR_OUT_OF_MEMORY);
            }
        }

        public void SaveImage(Bitmap bitmap, string filePath, bool overwrite = true)
        {
            ValidationHelper.ValidateBitmap(bitmap);
            ValidationHelper.ValidateFilePath(filePath);

            if (!filePath.EndsWith(Constants.BMP_EXTENSION, StringComparison.OrdinalIgnoreCase))
                filePath += Constants.BMP_EXTENSION;

            ValidationHelper.ValidateFileWritable(filePath);

            if (File.Exists(filePath) && !overwrite)
                throw new IOException($"파일이 이미 존재합니다: {filePath}");

            try
            {
                bitmap.Save(filePath, ImageFormat.Bmp);
                _logger.Info($"이미지 저장: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.Error(Constants.ERROR_SAVE_FAILED, ex);
                throw new InvalidOperationException(Constants.ERROR_SAVE_FAILED, ex);
            }
        }









    }
}
