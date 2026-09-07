namespace WpfImageProcessing.Models
{
    public enum MorphologyOperation
    {
        Dilation,
        Erosion
    }

    public enum FilterOperation
    {
        Gaussian,
        Laplacian,
        Sobel
    }

    public enum MatchingMethod
    {
        Diff = 0,
        Corr = 1,
        Coeff = 2
    }

    /// <summary>영상처리 공통 파라미터 (UI → Service → Native).</summary>
    public sealed class ProcessingParams
    {
        public int KernelSize { get; set; } = 3;
        public int ThresholdValue { get; set; } = 128;
        public double GaussianSigma { get; set; } = 1.0;
        public RoiData? Roi { get; set; }

        public static ProcessingParams Default => new();
    }

    public sealed class TemplateData
    {
        public required RoiData SourceRoi { get; init; }
        public required byte[] Pixels { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public DateTime RegisteredAt { get; init; } = DateTime.Now;
    }

    public sealed class MatchingResult
    {
        public int BestX { get; init; }
        public int BestY { get; init; }
        public double Score { get; init; }
        public MatchingMethod Method { get; init; }
        public bool Success { get; init; }
        public string? Message { get; init; }
    }

    public sealed class ProcessingResult
    {
        public bool Success { get; init; }
        public string OperationName { get; init; } = "";
        public byte[]? OutputPixels { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public long ElapsedMs { get; init; }
        public string? Message { get; init; }
        public int NativeStatus { get; init; }

        public static ProcessingResult Fail(string name, string message, int status = -1, long ms = 0) => new()
        {
            Success = false,
            OperationName = name,
            Message = message,
            NativeStatus = status,
            ElapsedMs = ms
        };
    }
}
