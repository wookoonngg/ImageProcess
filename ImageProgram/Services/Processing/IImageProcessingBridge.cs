using WpfImageProcessing.Models;
using WpfImageProcessing.Native;

namespace WpfImageProcessing.Services.Processing
{

    // C++ 에 라이브러리 호출하기 위해 중간 인터페이스 무슨 함수가 native에 있는 지 보여주고 c# 에서 이 인터페이스에의존해서 호출
  
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
