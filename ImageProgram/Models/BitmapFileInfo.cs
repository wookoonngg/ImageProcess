using System;
using System.Diagnostics;

namespace WpfImageProcessing.Models
{

    /// BMP 파일의 헤더 정보를 나타내는 데이터 클래스
    /// 
    /// 설계 
    /// - BMP 파일 형식의 핵심 정보만 추출
    /// - DTO (Data Transfer Object) 패턴 적용
    /// - 검증 로직은 최소화 (Service에서 담당)
    /// 




    public class BitmapFileInfo
    {
        #region Properties

        /// <summary>
        /// BMP 이미지의 가로 픽셀 수
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// BMP 이미지의 세로 픽셀 수
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 비트 깊이 (8=그레이스케일, 24=RGB, 32=RGBA)
        /// </summary>
        public short BitDepth { get; set; }

        /// <summary>
        /// BMP 파일의 총 크기 (바이트)
        /// 주의: 4GB 이상 파일은 32비트 오버플로우 발생 가능
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 픽셀 데이터가 시작하는 파일 내 위치 (바이트)
        /// </summary>
        public int PixelDataOffset { get; set; }

        /// <summary>
        /// DIB 헤더 크기 (40=BITMAPINFOHEADER, 108=V4, 124=V5)
        /// </summary>
        public int HeaderSize { get; set; }

        /// <summary>
        /// true면 아래→위 저장 (일반적인 BMP)
        /// </summary>
        public bool IsBottomUp { get; set; }

        /// <summary>
        /// 압축 방식 (0=무압축, 3=BI_BITFIELDS)
        /// </summary>
        public int CompressionType { get; set; }

        /// <summary>
        /// 파일이 로드된 시간
        /// 성능 분석용
        /// </summary>
        public DateTime LoadedTime { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// BMP 파일이 유효한 형식인지 검증
        /// 
        /// 검증 항목:
        /// - 이미지 크기가 0보다 큼
        /// - 비트 깊이가 지원하는 값
        /// - 압축 방식이 지원하는 형식
        /// </summary>
        
        public bool IsValid()
        {
            // 크기 검증
            if (Width <= 0 || Height <= 0)
            {
                Debug.WriteLine($"❌ 이미지 크기 오류: {Width}×{Height}");
                return false;
            }

            // 비트 깊이 검증 (WEEK 1: 8bit, 24bit만 지원)
            if (BitDepth != 8 && BitDepth != 24 && BitDepth != 32)
            {
                Debug.WriteLine($"❌ 지원하지 않는 비트 깊이: {BitDepth}bit");
                return false;
            }

            // 0=RGB 무압축, 3=BI_BITFIELDS (32bit BMP에서 흔함)
            if (CompressionType != 0 && CompressionType != 3)
            {
                Debug.WriteLine($"❌ 지원하지 않는 압축 방식: {CompressionType}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 이미지 데이터의 실제 크기를 계산
        /// 
        /// 계산식:
        /// - RowStride = ((Width * BitDepth + 31) / 32) * 4
        ///   (각 행은 4바이트 경계에 정렬)
        /// - 실제 크기 = RowStride * Height
        /// 
        /// 왜 필요한가?
        /// - BMP 헤더의 파일 크기 필드는 32비트 정수 (4GB 오버플로우 가능)
        /// - 실제 데이터 크기는 이 공식으로 계산해야 함
        /// </summary>
        


        public long GetActualPixelDataSize()
        {
            // 한 행의 바이트 수 (4바이트 경계에 정렬)
            int rowStride = ((Width * BitDepth + 31) / 32) * 4;

            // 전체 픽셀 데이터 크기
            long pixelDataSize = (long)rowStride * Height;

            return pixelDataSize;
        }

        /// <summary>
        /// 정보를 문자열로 변환 (디버깅/로깅용)
        /// </summary>
        public override string ToString()
        {
            return $"BMP: {Width}×{Height} {BitDepth}bit " +
                   $"(압축: {CompressionType}, 파일크기: {FormatBytes(FileSize)})";
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 바이트를 읽기 좋은 형식으로 변환 (예: 1KB, 1MB)
        /// </summary>
        private static string FormatBytes(long bytes)
        {
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
    /// BMP 파일 로드 결과를 나타내는 클래스
    /// Result 패턴: 성공/실패를 명확히 하기 위함
    /// </summary>
    public class BitmapLoadResult
    {
        /// <summary>
        /// 로드 성공 여부
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 로드된 BMP 정보
        /// </summary>
        public BitmapFileInfo FileInfo { get; set; }

        /// <summary>
        /// 실패 사유 (성공 시 null)
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 로드에 소요된 시간 (성능 분석용)
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// 성공 결과 생성
        /// </summary>
        public static BitmapLoadResult Success(BitmapFileInfo fileInfo, TimeSpan elapsedTime)
        {
            return new BitmapLoadResult
            {
                IsSuccess = true,
                FileInfo = fileInfo,
                ErrorMessage = null,
                ElapsedTime = elapsedTime
            };
        }

        /// <summary>
        /// 실패 결과 생성
        /// </summary>
        public static BitmapLoadResult Failure(string errorMessage, TimeSpan elapsedTime)
        {
            return new BitmapLoadResult
            {
                IsSuccess = false,
                FileInfo = null,
                ErrorMessage = errorMessage,
                ElapsedTime = elapsedTime




            };

        }

    }



}

