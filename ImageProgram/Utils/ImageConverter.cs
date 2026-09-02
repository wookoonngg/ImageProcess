using System;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.IO;

namespace WpfImageProcessing.Utils
{
    /// <summary>
    /// Bitmap과 BitmapImage 간의 형식 변환
    /// 
    /// 설계 의도:
    /// - .NET Framework Bitmap (System.Drawing) ↔ WPF BitmapImage 변환
    /// - 변환 로직을 한 곳에 중앙집중화
    /// - 다양한 상황(메모리/파일)에 대응
    /// 
    /// 왜 필요한가?
    /// - WPF UI는 BitmapImage 사용
    /// - 이미지 처리는 Bitmap 사용
    /// - 둘을 효율적으로 변환해야 함
    /// 
    /// 성능 고려사항:
    /// - 변환 중 메모리 사용량 증가
    /// - 큰 이미지 변환 시 시간 소요
    /// - WEEK 4에서 최적화 예정
    /// </summary>
    public static class ImageConverter
    {
        #region Bitmap to BitmapImage

        /// <summary>
        /// Bitmap을 BitmapImage로 변환 (메모리 방식)
        /// 
        /// 방식:
        /// 1. Bitmap을 메모리 스트림으로 저장 (BMP 형식)
        /// 2. BitmapImage로 로드
        /// 3. 원본 Bitmap은 그대로 (복사 아님)
        /// 
        /// 주의:
        /// - 메모리 사용량 증가 (Bitmap + 메모리 스트림 + BitmapImage)
        /// - 변환 후 원본 Bitmap은 계속 유지되어야 함
        /// - 큰 이미지는 시간이 오래 걸림
        /// </summary>
        public static BitmapImage BitmapToBitmapImage(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            try
            {
                // 1단계: 메모리 스트림 생성
                MemoryStream memoryStream = new MemoryStream();

                // 2단계: Bitmap을 BMP 형식으로 메모리에 저장
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
                memoryStream.Seek(0, SeekOrigin.Begin);

                // 3단계: BitmapImage 생성
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;  // 메모리 스트림에서 로드
                bitmapImage.EndInit();
                bitmapImage.Freeze();  // UI 스레드 안전성

                return bitmapImage;
            }
            catch (OutOfMemoryException)
            {
                throw new InvalidOperationException(
                    $"메모리 부족: Bitmap을 BitmapImage로 변환할 수 없습니다 " +
                    $"({bitmap.Width}×{bitmap.Height})");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Bitmap을 BitmapImage로 변환하는 중 오류 발생: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Bitmap을 BitmapImage로 변환 (파일 방식)
        /// 
        /// 방식:
        /// 1. Bitmap을 임시 파일로 저장
        /// 2. BitmapImage로 로드
        /// 3. 임시 파일 삭제
        /// 
        /// 장점:
        /// - 메모리 사용량 적음
        /// - 큰 이미지에 유리
        /// 
        /// 단점:
        /// - 디스크 I/O 필요
        /// - 임시 파일 관리 필요
        /// 
        /// WEEK 4에서 적용 고려
        /// </summary>
        public static BitmapImage BitmapToBitmapImageViaFile(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            string tempFile = Path.GetTempFileName();

            try
            {
                // 1단계: 임시 파일에 저장
                bitmap.Save(tempFile, System.Drawing.Imaging.ImageFormat.Bmp);

                // 2단계: 파일에서 BitmapImage로 로드
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(tempFile, UriKind.Absolute);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
            finally
            {
                // 3단계: 임시 파일 삭제
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // 무시 (부팅 시 정리될 것)
                }
            }
        }

        #endregion

        #region BitmapImage to Bitmap

        /// <summary>
        /// BitmapImage를 Bitmap으로 변환
        /// 
        /// 방식:
        /// 1. BitmapImage의 픽셀 데이터를 바이트 배열로 추출
        /// 2. 새로운 Bitmap 생성
        /// 3. 바이트 데이터 복사
        /// 
        /// 주의:
        /// - 메모리 복사가 발생 (성능 영향)
        /// - 원본 BitmapImage는 영향 없음
        /// </summary>
        public static Bitmap BitmapImageToBitmap(BitmapImage bitmapImage)
        {
            if (bitmapImage == null)
                throw new ArgumentNullException(nameof(bitmapImage));

            try
            {
                // 1단계: 메모리 스트림에 인코딩
                MemoryStream memoryStream = new MemoryStream();
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                encoder.Save(memoryStream);

                // 2단계: 메모리 스트림에서 Bitmap 생성
                memoryStream.Seek(0, SeekOrigin.Begin);
                Bitmap bitmap = new Bitmap(memoryStream);

                return bitmap;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"BitmapImage를 Bitmap으로 변환하는 중 오류 발생: {ex.Message}", ex);
            }
        }

        #endregion

        #region File Path Conversion

        /// <summary>
        /// 파일 경로에서 BitmapImage로 직접 로드
        /// 
        /// 가장 효율적인 방식:
        /// - 중간 변환 없음
        /// - 직접 파일에서 로드
        /// </summary>
        public static BitmapImage LoadBitmapImageFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"파일을 찾을 수 없습니다: {filePath}");

            try
            {
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"파일에서 BitmapImage를 로드하는 중 오류 발생: {filePath}", ex);
            }
        }

        #endregion

        #region Validation

        /// <summary>
        /// BitmapImage가 유효한지 확인
        /// </summary>
        public static bool IsValidBitmapImage(BitmapImage bitmapImage)
        {
            return bitmapImage != null &&
                   bitmapImage.Width > 0 &&
                   bitmapImage.Height > 0 &&
                   bitmapImage.Format != null;
        }

        /// <summary>
        /// Bitmap이 유효한지 확인
        /// </summary>
        public static bool IsValidBitmap(Bitmap bitmap)
        {
            return bitmap != null &&
                   bitmap.Width > 0 &&
                   bitmap.Height > 0;
        }

        #endregion
    }
}