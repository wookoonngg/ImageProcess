using WpfImageProcessing.Models;
using WpfImageProcessing.Native;

namespace WpfImageProcessing.Services.Processing
{
    /// <summary>
    /// C++ DLL 호출 계약. 구현체만 바꾸면 Mock / Native 전환 가능.
    /// </summary>
    public interface IImageProcessingBridge
    {
        bool IsNativeAvailable { get; }

        ProcessingResult Dilation(PixelBuffer src, ProcessingParams parameters);
        ProcessingResult Erosion(PixelBuffer src, ProcessingParams parameters);
        ProcessingResult Smoothing(PixelBuffer src, ProcessingParams parameters);
        ProcessingResult Threshold(PixelBuffer src, ProcessingParams parameters);
        ProcessingResult Gaussian(PixelBuffer src, ProcessingParams parameters);
        ProcessingResult Laplacian(PixelBuffer src, ProcessingParams parameters);
        ProcessingResult Sobel(PixelBuffer src, ProcessingParams parameters);

        MatchingResult TemplateMatch(PixelBuffer image, TemplateData template, MatchingMethod method, RoiData? searchRoi);
        TemplateData? ExtractTemplate(PixelBuffer image, RoiData roi);
        int[]? ComputeHistogram(PixelBuffer image, RoiData? roi);
    }
}
