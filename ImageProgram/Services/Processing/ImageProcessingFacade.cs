using WpfImageProcessing.Models;
using WpfImageProcessing.Native;

namespace WpfImageProcessing.Services.Processing
{
    /// <summary>
    /// UI가 사용하는 단일 진입점.
    /// 연산 종류가 늘어도 MainWindow는 이 Facade만 보면 됨.
    /// </summary>
    public interface IImageProcessingFacade
    {
        IImageProcessingBridge Bridge { get; }

        ProcessingResult RunMorphology(MorphologyOperation op, PixelBuffer src, ProcessingParams? parameters = null);
        ProcessingResult RunFilter(FilterOperation op, PixelBuffer src, ProcessingParams? parameters = null);
        ProcessingResult RunSmoothing(PixelBuffer src, ProcessingParams? parameters = null);
        ProcessingResult RunThreshold(PixelBuffer src, ProcessingParams? parameters = null);
        MatchingResult RunMatching(MatchingMethod method, PixelBuffer image, TemplateData template, RoiData? searchRoi = null);
        TemplateData? RegisterTemplate(PixelBuffer image, RoiData roi);
        int[]? GetHistogram(PixelBuffer image, RoiData? roi);
    }

    public sealed class ImageProcessingFacade : IImageProcessingFacade
    {
        public IImageProcessingBridge Bridge { get; }

        public ImageProcessingFacade(IImageProcessingBridge bridge)
        {
            Bridge = bridge;
        }

        public ProcessingResult RunMorphology(MorphologyOperation op, PixelBuffer src, ProcessingParams? parameters = null)
        {
            var p = parameters ?? ProcessingParams.Default;
            return op switch
            {
                MorphologyOperation.Dilation => Bridge.Dilation(src, p),
                MorphologyOperation.Erosion => Bridge.Erosion(src, p),
                _ => ProcessingResult.Fail(op.ToString(), "unknown morphology op")
            };
        }

        public ProcessingResult RunFilter(FilterOperation op, PixelBuffer src, ProcessingParams? parameters = null)
        {
            var p = parameters ?? ProcessingParams.Default;
            return op switch
            {
                FilterOperation.Gaussian => Bridge.Gaussian(src, p),
                FilterOperation.Laplacian => Bridge.Laplacian(src, p),
                FilterOperation.Sobel => Bridge.Sobel(src, p),
                _ => ProcessingResult.Fail(op.ToString(), "unknown filter op")
            };
        }

        public ProcessingResult RunSmoothing(PixelBuffer src, ProcessingParams? parameters = null) =>
            Bridge.Smoothing(src, parameters ?? ProcessingParams.Default);

        public ProcessingResult RunThreshold(PixelBuffer src, ProcessingParams? parameters = null) =>
            Bridge.Threshold(src, parameters ?? ProcessingParams.Default);

        public MatchingResult RunMatching(MatchingMethod method, PixelBuffer image, TemplateData template, RoiData? searchRoi = null) =>
            Bridge.TemplateMatch(image, template, method, searchRoi);

        public TemplateData? RegisterTemplate(PixelBuffer image, RoiData roi) =>
            Bridge.ExtractTemplate(image, roi);

        public int[]? GetHistogram(PixelBuffer image, RoiData? roi) =>
            Bridge.ComputeHistogram(image, roi);
    }
}
