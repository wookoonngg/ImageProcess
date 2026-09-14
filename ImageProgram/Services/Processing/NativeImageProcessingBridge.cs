using System.Diagnostics;
using WpfImageProcessing.Models;
using WpfImageProcessing.Native;

namespace WpfImageProcessing.Services.Processing
{
    /// <summary>
    /// 인보크 브리지 
    /// DLL이 없으면 IsNativeAvailable=false 이고 호출 시 안내 메시지 반환.
    /// </summary>
    /// 



    public sealed class NativeImageProcessingBridge : IImageProcessingBridge
    {
        private readonly ILogger _logger;
        private bool? _available;

        public NativeImageProcessingBridge(ILogger logger)
        {
            _logger = logger;
        }

        public bool IsNativeAvailable
        {
            get
            {
                _available ??= ProbeNative();
                return _available.Value;
            }
        }

        public ProcessingResult Dilation(PixelBuffer src, ProcessingParams p) =>
            RunUnary("Dilation", src, p, (s, d, roi) =>
                NativeMethods.IpDilation(s, d, src.Width, src.Height, p.KernelSize, roi));

        public ProcessingResult Erosion(PixelBuffer src, ProcessingParams p) =>
            RunUnary("Erosion", src, p, (s, d, roi) =>
                NativeMethods.IpErosion(s, d, src.Width, src.Height, p.KernelSize, roi));

        public ProcessingResult Smoothing(PixelBuffer src, ProcessingParams p) =>
            RunUnary("Smoothing", src, p, (s, d, roi) =>
                NativeMethods.IpSmoothing(s, d, src.Width, src.Height, p.KernelSize, roi));

        public ProcessingResult Threshold(PixelBuffer src, ProcessingParams p) =>
            RunUnary("Threshold", src, p, (s, d, roi) =>
                NativeMethods.IpThreshold(s, d, src.Width, src.Height, p.ThresholdValue, roi));

        public ProcessingResult Gaussian(PixelBuffer src, ProcessingParams p) =>
            RunUnary("Gaussian", src, p, (s, d, roi) =>
                NativeMethods.IpGaussian(s, d, src.Width, src.Height, p.KernelSize, p.GaussianSigma, roi));

        public ProcessingResult Laplacian(PixelBuffer src, ProcessingParams p) =>
            RunUnary("Laplacian", src, p, (s, d, roi) =>
                NativeMethods.IpLaplacian(s, d, src.Width, src.Height, roi));

        public ProcessingResult Sobel(PixelBuffer src, ProcessingParams p) =>
            RunUnary("Sobel", src, p, (s, d, roi) =>
                NativeMethods.IpSobel(s, d, src.Width, src.Height, roi));

        public MatchingResult TemplateMatch(PixelBuffer image, TemplateData template, MatchingMethod method, RoiData? searchRoi)
        {
            if (!EnsureAvailable(out var failMsg))
                return new MatchingResult { Success = false, Method = method, Message = failMsg };

            var sw = Stopwatch.StartNew();
            try
            {
                using var _ = NativeRoiHelper.Pin(searchRoi, out IntPtr roiPtr);
                int status = NativeMethods.IpTemplateMatch(
                    image.Data, image.Width, image.Height,
                    template.Pixels, template.Width, template.Height,
                    (int)method,
                    out IpMatchResultNative native,
                    roiPtr);
                sw.Stop();

                if (status != IpStatus.Ok)
                {
                    return new MatchingResult
                    {
                        Success = false,
                        Method = method,
                        Message = IpStatus.ToMessage(status)
                    };
                }

                return new MatchingResult
                {
                    Success = true,
                    Method = method,
                    BestX = native.BestX,
                    BestY = native.BestY,
                    Score = native.Score,
                    Message = $"OK ({sw.ElapsedMilliseconds} ms)"
                };
            }
            catch (DllNotFoundException ex)
            {
                _available = false;
                return new MatchingResult { Success = false, Method = method, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.Error("TemplateMatch failed", ex);
                return new MatchingResult { Success = false, Method = method, Message = ex.Message };
            }
        }

        public TemplateData? ExtractTemplate(PixelBuffer image, RoiData roi)
        {
            if (!roi.IsValid() || !EnsureAvailable(out _))
                return null;

            try
            {
                var ipRoi = IpRoi.From(roi);
                int needed = checked(roi.Width * roi.Height);
                var dst = new byte[needed];
                int status = NativeMethods.IpExtractRoi(image.Data, image.Width, image.Height, ref ipRoi, dst, dst.Length);
                if (status != IpStatus.Ok)
                    return null;

                return new TemplateData
                {
                    SourceRoi = roi,
                    Pixels = dst,
                    Width = roi.Width,
                    Height = roi.Height
                };
            }
            catch (Exception ex)
            {
                _logger.Error("ExtractTemplate failed", ex);
                return null;
            }
        }

        public int[]? ComputeHistogram(PixelBuffer image, RoiData? roi)
        {
            if (!EnsureAvailable(out _))
                return null;

            try
            {
                var bins = new int[256];
                using var _ = NativeRoiHelper.Pin(roi, out IntPtr roiPtr);
                int status = NativeMethods.IpComputeHistogram(image.Data, image.Width, image.Height, roiPtr, bins);
                return status == IpStatus.Ok ? bins : null;
            }
            catch (Exception ex)
            {
                _logger.Error("ComputeHistogram failed", ex);
                return null;
            }
        }

        private ProcessingResult RunUnary(
            string name,
            PixelBuffer src,
            ProcessingParams parameters,
            Func<byte[], byte[], IntPtr, int> nativeCall)
        {
            if (!EnsureAvailable(out var failMsg))
                return ProcessingResult.Fail(name, failMsg);

            var sw = Stopwatch.StartNew();
            try
            {
                var dst = src.CloneEmpty();
                using var _ = NativeRoiHelper.Pin(parameters.Roi, out IntPtr roiPtr);
                int status = nativeCall(src.Data, dst.Data, roiPtr);
                sw.Stop();

                if (status != IpStatus.Ok)
                {
                    return ProcessingResult.Fail(name, IpStatus.ToMessage(status), status, sw.ElapsedMilliseconds);
                }

                return new ProcessingResult
                {
                    Success = true,
                    OperationName = name,
                    OutputPixels = dst.Data,
                    Width = dst.Width,
                    Height = dst.Height,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    NativeStatus = status,
                    Message = "OK"
                };
            }
            catch (DllNotFoundException ex)
            {
                _available = false;
                return ProcessingResult.Fail(name, $"네이티브 DLL 없음: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.Error($"{name} failed", ex);
                return ProcessingResult.Fail(name, ex.Message);
            }
        }

        private bool EnsureAvailable(out string message)
        {
            if (IsNativeAvailable)
            {
                message = "";
                return true;
            }

            message = "ImageProcessingNative.dll 이 없습니다. C++ 프로젝트를 먼저 빌드하세요.";
            return false;
        }

        private bool ProbeNative()
        {
            try
            {
                // 가벼운 호출로 DLL 로드 시도 (스텁이면 NotImplemented)
                var dummy = new byte[1];
                NativeMethods.IpThreshold(dummy, dummy, 1, 1, 128, IntPtr.Zero);
                return true;
            }
            catch (DllNotFoundException)
            {
                _logger.Warning("ImageProcessingNative.dll not found — bridge in stub/UI-only mode.");
                return false;
            }
            catch (BadImageFormatException ex)
            {
                _logger.Error("Native DLL architecture mismatch (need x64).", ex);
                return false;
            }
            catch
            {
                // EntryPoint / NotImplemented 등 — DLL 자체는 로드됨
                return true;
            }
        }
    }
}
