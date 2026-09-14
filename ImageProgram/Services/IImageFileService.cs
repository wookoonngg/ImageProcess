using System.Drawing;
using WpfImageProcessing.Models;

namespace WpfImageProcessing.Services
{
    /// <summary>
    /// BMP 이미지 파일 I/O 계약
    /// </summary>
    public interface IImageFileService
    {
        BitmapFileInfo ReadHeader(string filePath);
        Bitmap LoadImage(string filePath);

        void SaveImage(Bitmap bitmap, string filePath, bool overwrite = true);
    }
}
