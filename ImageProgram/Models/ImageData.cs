using System;
using System.Drawing;

namespace WpfImageProcessing.Models
{
    /// <summary>
    /// 메모리에 로드된 이미지를 나타내는 클래스
    /// 
    /// 설계 의도:
    /// - 원본 이미지와 처리된 이미지 데이터를 분리 관리
    /// - IDisposable로 메모리 누수 방지 (Bitmap은 리소스 필요)
    /// - 메타데이터 추적 (언제 로드되었는지, 크기 등)
    /// </summary>
    public class ImageData : IDisposable
    {
        #region Properties

        /// <summary>
        /// 이미지의 고유 ID
        /// 여러 이미지를 추적할 때 사용
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// 원본 파일 경로
        /// </summary>
        public string SourceFilePath { get; set; }

        /// <summary>
        /// 로드된 이미지의 Bitmap 객체
        /// </summary>
        public Bitmap PixelData { get; set; }

        /// <summary>
        /// 이미지 너비 (픽셀)
        /// </summary>
        public int Width => PixelData?.Width ?? 0;

        /// <summary>
        /// 이미지 높이 (픽셀)
        /// </summary>
        public int Height => PixelData?.Height ?? 0;

        /// <summary>
        /// 메모리에 로드된 시간
        /// </summary>
        public DateTime LoadedTime { get; set; }

        /// <summary>
        /// 로드 여부
        /// </summary>
        public bool IsLoaded => PixelData != null;

        /// <summary>
        /// 메모리에 로드된 데이터 크기 (바이트)
        /// </summary>
        public long MemorySize
        {
            get
            {
                if (PixelData == null) return 0;

                // Bitmap 메모리 크기 = Width × Height × (BitDepth / 8)
                // 예: 1000×1000 32bit = 4MB
                int bytesPerPixel = PixelData.PixelFormat switch
                {
                    System.Drawing.Imaging.PixelFormat.Format8bppIndexed => 1,
                    System.Drawing.Imaging.PixelFormat.Format24bppRgb => 3,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb => 4,
                    _ => 4
                };

                return (long)Width * Height * bytesPerPixel;
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// 생성자: 새로운 이미지 데이터 생성
        /// </summary>
        public ImageData()
        {
            Id = Guid.NewGuid();
            LoadedTime = DateTime.Now;
        }

        #endregion

        #region Methods

        /// <summary>
        /// 이미지가 유효한지 검증
        /// </summary>
        public bool IsValid()
        {
            return PixelData != null && Width > 0 && Height > 0;
        }

        /// <summary>
        /// 이미지 정보를 문자열로 반환 (UI 표시용)
        /// </summary>
        public override string ToString()
        {
            if (!IsLoaded)
                return "이미지 미로드";

            return $"{Width}×{Height} " +
                   $"({FormatBytes(MemorySize)}) " +
                   $"로드시간: {LoadedTime:yyyy-MM-dd HH:mm:ss}";
        }

        /// <summary>
        /// Bitmap 메모리 해제
        /// 이 메서드는 반드시 호출되어야 메모리 누수 방지
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Dispose 패턴 구현
        /// 관리되는 리소스와 비관리 리소스를 명확히 구분
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 관리되는 리소스 해제
                PixelData?.Dispose();
                PixelData = null;
            }

            // 비관리 리소스는 이 프로젝트에서 없음
            // (C#이 자동으로 처리)
        }

        /// <summary>
        /// 소멸자 (Dispose 호출 확인용)
        /// 반드시 Dispose를 호출하지 않으면 여기서 정리됨 (성능 저하)
        /// </summary>
        ~ImageData()
        {
            Dispose(false);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 바이트를 읽기 좋은 형식으로 변환
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        #endregion
    }

    /// <summary>
    /// ROI (Region of Interest) 데이터
    /// WEEK 2에서 사용, WEEK 1에서는 placeholder
    /// </summary>
    public class RoiData
    {
        /// <summary>
        /// ROI 시작 X 좌표
        /// </summary>
        public int StartX { get; set; }

        /// <summary>
        /// ROI 시작 Y 좌표
        /// </summary>
        public int StartY { get; set; }

        /// <summary>
        /// ROI 너비
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// ROI 높이
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// ROI가 유효한지 검증
        /// </summary>
        public bool IsValid()
        {
            return StartX >= 0 && StartY >= 0 && Width > 0 && Height > 0;
        }

        /// <summary>
        /// ROI 정보 문자열 변환
        /// </summary>
        public override string ToString()
        {
            return $"ROI: ({StartX}, {StartY}) {Width}×{Height}";
        }
    }
}