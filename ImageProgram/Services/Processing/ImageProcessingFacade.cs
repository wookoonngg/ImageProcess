using WpfImageProcessing.Models;
using WpfImageProcessing.Native;

namespace WpfImageProcessing.Services.Processing
{
    // 퍼사드 !! -> main 단에서 여기에 의존해서 사용 여기 코드만 ui 에선 호출함 
   
    /// 연산 종류가 늘어도 MainWindow는 이 Facade만 보면 됨.
    
    public interface IImageProcessingFacade
    {
        /// <summary>
        /// 1. 형태학 연산 하는 애 : dilation/ erosion 연산 구현 
        /// 2. 필터 3개 모아둔 애 : 가우시안 , 소벨, 라플라시안
        /// 3. 스무싱 연산 
        /// 4. threshold
        /// 5. template : 메소드 두개로 구현 하나는 roi ㄹㅇ 템플릿만 담는거 / 하나는 비교 하는 로직
        /// </summary>

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
            Bridge = bridge; // 생성자 세팅 '


        }




        public ProcessingResult RunMorphology(MorphologyOperation op, PixelBuffer src, ProcessingParams? parameters = null)
        {


            var p = parameters ?? ProcessingParams.Default;

            return op switch

            {

                // morpho 연산 에 두개 호출하면 브릿지에 선언도니 함수 호출 
                MorphologyOperation.Dilation => Bridge.Dilation(src, p),
                MorphologyOperation.Erosion => Bridge.Erosion(src, p),
                _ => ProcessingResult.Fail(op.ToString(), "unknown morphology op") // 디폴트 예외값



            };
        }


        // 필터에 연산자 3갠데 가우시안은 밖으로 따로 빼는게?

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


