namespace WpfImageProcessing.Utils
{
    /// <summary>
    /// 애플리케이션 전역 상수 정의
    /// 
    /// 설계 의도:
    /// - 매직 넘버 제거
    /// - 중앙집중식 설정 관리
    /// - 나중에 설정 파일로 확장 가능
    /// </summary>
    public static class Constants
    {
        #region Application Info

        public const string APP_NAME = "WPF 반도체 검사 영상처리";
        public const string APP_VERSION = "1.0 WEEK 1";
        public const string COMPANY_NAME = "반도체 검사 SW팀";

        #endregion

        #region File Extensions

        public const string BMP_EXTENSION = ".bmp";
        public const string BMP_FILTER = "BMP 이미지 (*.bmp)|*.bmp|모든 파일 (*.*)|*.*";

        #endregion

        #region Image Constraints

        /// <summary>
        /// WEEK 1에서 지원하는 최대 이미지 크기
        /// (메모리 부족 방지)
        /// </summary>
        public const int MAX_IMAGE_WIDTH = 100000;
        public const int MAX_IMAGE_HEIGHT = 100000;
        public const long MAX_IMAGE_SIZE_GB = 10;  // 10GB

        /// <summary>
        /// 지원하는 비트 깊이
        /// </summary>
        public const short BIT_DEPTH_8BPP = 8;      // 그레이스케일
        public const short BIT_DEPTH_24BPP = 24;    // RGB
        public const short BIT_DEPTH_32BPP = 32;    // RGBA

        #endregion

        #region UI Settings

        /// <summary>
        /// 윈도우 기본 크기
        /// </summary>
        public const int WINDOW_DEFAULT_WIDTH = 1200;
        public const int WINDOW_DEFAULT_HEIGHT = 800;

        /// <summary>
        /// 이미지 뷰어 패널 비율
        /// </summary>
        public const double VIEWER_SPLIT_RATIO = 0.5;  // 50:50

        /// <summary>
        /// 네비게이터 크기
        /// </summary>
        public const int NAVIGATOR_WIDTH = 200;
        public const int NAVIGATOR_HEIGHT = 150;

        /// <summary>
        /// UI 색상 (Modern Theme)
        /// </summary>
        public static class Colors
        {
            public const string PRIMARY = "#2E3440";      // Dark background
            public const string SECONDARY = "#3B4252";    // Medium background
            public const string ACCENT = "#88C0D0";       // Light blue accent
            public const string TEXT_PRIMARY = "#ECEFF4"; // Light text
            public const string TEXT_SECONDARY = "#D8DEE9"; // Medium text
            public const string BORDER = "#4C566A";       // Border color
        }

        #endregion

        #region Performance

        /// <summary>
        /// 성능 측정 활성화 여부
        /// WEEK 4에서 자세한 분석용
        /// </summary>
        public const bool ENABLE_PERFORMANCE_LOGGING = true;

        /// <summary>
        /// 처리 시간 표시 수준 (밀리초)
        /// 이 이상의 시간이 소요되면 경고
        /// </summary>
        public const int WARNING_THRESHOLD_MS = 1000;

        #endregion

        #region Debug

        /// <summary>
        /// 상세 로깅 활성화
        /// DEBUG 빌드에서만 활성화
        /// </summary>
#if DEBUG
        public const bool DEBUG_MODE = true;
#else
        public const bool DEBUG_MODE = false;
#endif

        /// <summary>
        /// 테스트 이미지 경로
        /// </summary>
        public const string TEST_IMAGE_PATH = "Resources/TestImages/Foundry_4umDF_1.bmp";

        #endregion

        #region Error Messages

        public const string ERROR_FILE_NOT_FOUND = "파일을 찾을 수 없습니다.";
        public const string ERROR_INVALID_BMP = "유효한 BMP 파일이 아닙니다.";
        public const string ERROR_OUT_OF_MEMORY = "메모리가 부족합니다. 이미지가 너무 큽니다.";
        public const string ERROR_UNSUPPORTED_FORMAT = "지원하지 않는 이미지 형식입니다.";
        public const string ERROR_SAVE_FAILED = "파일을 저장할 수 없습니다.";

        #endregion

        #region Success Messages

        public const string SUCCESS_FILE_OPENED = "파일이 정상적으로 열렸습니다.";
        public const string SUCCESS_FILE_SAVED = "파일이 정상적으로 저장되었습니다.";

        #endregion
    }

    /// <summary>
    /// UI 관련 상수
    /// </summary>
    public static class UIConstants
    {
        public const double BORDER_THICKNESS = 1.0;
        public const double CORNER_RADIUS = 4.0;
        public const double PADDING_SMALL = 8.0;
        public const double PADDING_MEDIUM = 16.0;
        public const double PADDING_LARGE = 24.0;
        public const double SPACING_SMALL = 4.0;
        public const double SPACING_MEDIUM = 8.0;
        public const double SPACING_LARGE = 16.0;
    }

    /// <summary>
    /// 형식 관련 상수
    /// </summary>
    public static class FormatConstants
    {
        /// <summary>
        /// 이미지 크기를 표시하는 형식
        /// 예: "1920×1080"
        /// </summary>
        public const string SIZE_FORMAT = "{0}×{1}";

        /// <summary>
        /// 메모리 크기를 표시하는 형식
        /// 예: "1.5 MB"
        /// </summary>
        public const string MEMORY_FORMAT = "{0:F2} {1}";

        /// <summary>
        /// 시간을 표시하는 형식
        /// 예: "2024-01-15 14:30:45"
        /// </summary>
        public const string DATETIME_FORMAT = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// 처리 시간을 표시하는 형식
        /// 예: "1234 ms"
        /// </summary>
        public const string ELAPSED_TIME_FORMAT = "{0} ms";
    }
}