using System;
using System.Drawing;
using System.IO;

namespace WpfImageProcessing.Utils
{
    /// <summary>
    /// 입력 검증 유틸리티
    /// 
    /// 설계 의도:
    /// - 입력 검증 로직을 중앙집중화
    /// - 여러 계층에서 동일한 검증 규칙 적용
    /// - 검증 로직 중복 제거
    /// </summary>
    public static class ValidationHelper
    {
        #region File Validation

        /// <summary>
        /// 파일 경로 유효성 검사
        /// </summary>
        public static void ValidateFilePath(string filePath, string paramName = nameof(filePath))
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("파일 경로가 비어있습니다.", paramName);

            if (filePath.Length > 260)  // Windows MAX_PATH
                throw new ArgumentException("파일 경로가 너무 깁니다 (260자 초과).", paramName);

            // 경로에 사용할 수 없는 문자 확인
            char[] invalidPathChars = Path.GetInvalidPathChars();
            foreach (char ch in filePath)
            {
                if (Array.IndexOf(invalidPathChars, ch) >= 0)
                    throw new ArgumentException($"파일 경로에 사용할 수 없는 문자: '{ch}'", paramName);
            }
        }

        /// <summary>
        /// BMP 파일 존재 여부 확인
        /// </summary>
        public static void ValidateBmpFileExists(string filePath)
        {
            ValidateFilePath(filePath);

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"파일을 찾을 수 없습니다: {filePath}");

            string extension = Path.GetExtension(filePath).ToLower();
            if (extension != Constants.BMP_EXTENSION)
                throw new InvalidOperationException($"BMP 파일이 아닙니다: {filePath}");
        }

        /// <summary>
        /// 파일이 읽기 가능한지 확인
        /// </summary>
        public static void ValidateFileReadable(string filePath)
        {
            ValidateBmpFileExists(filePath);

            try
            {
                using (FileStream fs = File.OpenRead(filePath))
                {
                    // 읽기만 가능한지 확인
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"파일을 읽을 권한이 없습니다: {filePath}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"파일 읽기 불가: {filePath}", ex);
            }
        }

        /// <summary>
        /// 파일이 쓰기 가능한지 확인
        /// </summary>
        public static void ValidateFileWritable(string filePath)
        {
            ValidateFilePath(filePath);

            string directory = Path.GetDirectoryName(filePath);

            // 디렉토리가 없으면 상위 경로 확인
            string pathToCheck = string.IsNullOrEmpty(directory) ?
                Environment.CurrentDirectory : directory;

            if (!Directory.Exists(pathToCheck))
                throw new InvalidOperationException($"디렉토리가 존재하지 않습니다: {pathToCheck}");

            try
            {
                // 임시 파일로 쓰기 권한 확인
                string testFile = Path.Combine(pathToCheck, ".test_write");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"파일을 쓸 권한이 없습니다: {pathToCheck}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"파일 쓰기 불가: {pathToCheck}", ex);
            }
        }

        #endregion

        #region Image Validation

        /// <summary>
        /// Bitmap 객체 유효성 검사
        /// </summary>
        public static void ValidateBitmap(Bitmap bitmap, string paramName = nameof(bitmap))
        {
            if (bitmap == null)
                throw new ArgumentNullException(paramName, "Bitmap 객체가 null입니다.");

            if (bitmap.Width <= 0 || bitmap.Height <= 0)
                throw new ArgumentException(
                    $"이미지 크기가 유효하지 않습니다: {bitmap.Width}×{bitmap.Height}",
                    paramName);
        }

        /// <summary>
        /// 이미지 크기 검증
        /// (메모리 부족 방지)
        /// </summary>
        public static void ValidateImageSize(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException($"이미지 크기가 유효하지 않습니다: {width}×{height}");

            if (width > Constants.MAX_IMAGE_WIDTH || height > Constants.MAX_IMAGE_HEIGHT)
                throw new InvalidOperationException(
                    $"이미지가 너무 큽니다: {width}×{height} " +
                    $"(최대: {Constants.MAX_IMAGE_WIDTH}×{Constants.MAX_IMAGE_HEIGHT})");

            // 메모리 크기 추정 (32bit 기준)
            long estimatedSize = (long)width * height * 4;
            long maxSize = Constants.MAX_IMAGE_SIZE_GB * 1024 * 1024 * 1024;

            if (estimatedSize > maxSize)
                throw new InvalidOperationException(
                    $"이미지가 메모리 제한을 초과합니다: " +
                    $"약 {FormatBytes(estimatedSize)} " +
                    $"(최대: {Constants.MAX_IMAGE_SIZE_GB}GB)");
        }

        #endregion

        #region String Validation

        /// <summary>
        /// 문자열이 null이 아니고 비어있지 않은지 확인
        /// </summary>
        public static void ValidateNotNullOrEmpty(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{paramName}이(가) null이거나 비어있습니다.", paramName);
        }

        #endregion

        #region Numeric Validation

        /// <summary>
        /// 숫자가 양수인지 확인
        /// </summary>
        public static void ValidatePositive(int value, string paramName)
        {
            if (value <= 0)
                throw new ArgumentException($"{paramName}은(는) 양수여야 합니다. (값: {value})", paramName);
        }

        /// <summary>
        /// 숫자가 특정 범위에 있는지 확인
        /// </summary>
        public static void ValidateInRange(int value, int min, int max, string paramName)
        {
            if (value < min || value > max)
                throw new ArgumentException(
                    $"{paramName}이(가) 범위를 벗어났습니다. (값: {value}, 범위: {min}~{max})",
                    paramName);
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
}