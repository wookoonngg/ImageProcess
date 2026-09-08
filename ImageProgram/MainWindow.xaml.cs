using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WpfImageProcessing.Controls;
using WpfImageProcessing.Models;
using WpfImageProcessing.Native;
using WpfImageProcessing.Services;
using WpfImageProcessing.Services.Processing;
using WpfImageProcessing.Utils;

namespace WpfImageProcessing
{
    public partial class MainWindow : Window
    {
        private readonly IImageFileService _imageFileService;
        private readonly IImageProcessingFacade _processing;
        private readonly ILogger _logger;
        private ImageData? _sourceImage;
        private ImageData? _resultImage;
        private PixelBuffer? _sourceBuffer;
        private string? _currentFilePath;
        private BitmapFileInfo? _headerInfo;
        private bool _isPreviewMode;
        private RoiData? _currentRoi;
        private TemplateData? _templateData;
        private bool _isOpening;
        private PixelBuffer? _workBuffer;
        private double? _lastMatchScore;
        private MatchingMethod _selectedMethod = MatchingMethod.Coeff;

        public MainWindow()
        {
            InitializeComponent();

            _logger = new ConsoleLogger();
            _imageFileService = new ImageFileService(_logger);
            _processing = new ImageProcessingFacade(new NativeImageProcessingBridge(_logger));

            SourceViewer.ViewportChanged += (_, e) => Navigator.UpdateViewport(e);
            SourceViewer.RoiSelected += OnRoiSelected;
            Navigator.NavigateRequested += OnNavigatorNavigate;
            ResetMatchUi();

            CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (_, _) => OpenImage()));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (_, _) => SaveImage()));
            InputBindings.Add(new KeyBinding(ApplicationCommands.Open, Key.O, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(ApplicationCommands.Save, Key.S, ModifierKeys.Control));
        }

        private void OnNavigatorNavigate(object? sender, NavigatorNavigateEventArgs e)
        {
            SourceViewer.NavigateToImagePoint(e.ImageX, e.ImageY, e.ViewportWidth, e.ViewportHeight, e.Scale);
        }

        #region File

        private void OpenMenuItem_Click(object sender, RoutedEventArgs e) => OpenImage();
        private void SaveMenuItem_Click(object sender, RoutedEventArgs e) => SaveImage();
        private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();

        private async void OpenImage()
        {
            if (_isOpening)
                return;

            string? initialDir = FindDefaultImageFolder();
            var dialog = new OpenFileDialog
            {
                Filter = Constants.BMP_FILTER,
                Title = "BMP 이미지 열기",
                DefaultExt = ".bmp",
                CheckFileExists = true,
                Multiselect = false,
                InitialDirectory = initialDir
            };

            if (dialog.ShowDialog(this) != true)
                return;

            await LoadImageFromPathAsync(dialog.FileName);
        }

        private async Task LoadImageFromPathAsync(string filePath)
        {
            _isOpening = true;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                _sourceImage?.Dispose();
                _resultImage?.Dispose();
                _sourceBuffer = null;
                _workBuffer = null;
                _templateData = null;
                _lastMatchScore = null;
                _currentRoi = null;

                StatusText.Text = "헤더 읽는 중...";
                ImageInfoText.Text = Path.GetFileName(filePath);

                BitmapFileInfo headerInfo = await Task.Run(() => _imageFileService.ReadHeader(filePath));
                _headerInfo = headerInfo;
                _currentFilePath = filePath;
                _isPreviewMode = NeedsPreviewMode(headerInfo);

                StatusText.Text = _isPreviewMode
                    ? $"Preview 생성 중... ({headerInfo.Width}×{headerInfo.Height}, {FormatBytes(headerInfo.FileSize)})"
                    : "이미지 로드 중...";

                BitmapSource displayImage;
                if (_isPreviewMode)
                {
                    var sw = Stopwatch.StartNew();
                    var (pixels, w, h) = await Task.Run(() => BmpDisplayLoader.LoadSubsampledPixels(filePath, headerInfo));
                    displayImage = BmpDisplayLoader.CreateBgraBitmap(pixels, w, h);
                    // 대용량: 원본 전체가 아니라 Preview 표시분으로 처리 버퍼 생성
                    _sourceBuffer = await Task.Run(() => PixelBuffer.FromBgra32(pixels, w, h));
                    _workBuffer = _sourceBuffer.Clone();
                    sw.Stop();
                    ProcessingTimeText.Text = $"Processing Time: {sw.ElapsedMilliseconds} ms (Preview)";
                }
                else
                {
                    try
                    {
                        displayImage = await Task.Run(() => BmpDisplayLoader.Load(filePath, headerInfo));
                    }
                    catch
                    {
                        var (pixels, w, h) = await Task.Run(() => BmpDisplayLoader.LoadSubsampledPixels(filePath, headerInfo));
                        displayImage = BmpDisplayLoader.CreateBgraBitmap(pixels, w, h);
                        _sourceBuffer = await Task.Run(() => PixelBuffer.FromBgra32(pixels, w, h));
                        _workBuffer = _sourceBuffer.Clone();
                        _isPreviewMode = true;
                    }

                    if (!_isPreviewMode)
                    {
                        try
                        {
                            Bitmap bitmap = await Task.Run(() => _imageFileService.LoadImage(filePath));
                            _sourceImage = new ImageData { SourceFilePath = filePath, PixelData = bitmap };
                            _resultImage = new ImageData { SourceFilePath = filePath, PixelData = (Bitmap)bitmap.Clone() };
                            _sourceBuffer = await Task.Run(() => PixelBuffer.FromBitmap(bitmap));
                            _workBuffer = _sourceBuffer.Clone();
                        }
                        catch
                        {
                            _isPreviewMode = true;
                            if (_sourceBuffer == null)
                            {
                                _sourceBuffer = PixelBuffer.FromBitmapSource(displayImage);
                                _workBuffer = _sourceBuffer.Clone();
                            }
                        }
                    }
                }

                SourceViewer.SetImage(displayImage);
                ResultViewer.SetImage(displayImage);
                Navigator.SetPreviewImage(displayImage);
                Histogram.Clear();
                RoiInfoText.Text = "ROI: -";
                ResetMatchUi();

                StatusText.Text = _isPreviewMode
                    ? $"Preview 모드 ({displayImage.PixelWidth}×{displayImage.PixelHeight} 처리 버퍼) — 원본: {headerInfo.Width}×{headerInfo.Height}"
                    : Constants.SUCCESS_FILE_OPENED;

                ImageInfoText.Text =
                    $"{headerInfo.Width}×{headerInfo.Height} | {headerInfo.BitDepth}bit | " +
                    $"{FormatBytes(headerInfo.FileSize)} | {Path.GetFileName(filePath)}";
                if (_isPreviewMode)
                    ImageInfoText.Text += " | Preview 연산";
            }
            catch (Exception ex)
            {
                string detail = ex.InnerException == null ? ex.Message : $"{ex.Message}\n\n{ex.InnerException.Message}";
                MessageBox.Show(detail, "열기 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = $"오류: {ex.Message}";
            }
            finally
            {
                Mouse.OverrideCursor = null;
                _isOpening = false;
            }
        }

        private static string? FindDefaultImageFolder()
        {
            string[] candidates =
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resource", "TestImages"),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resource", "TestImages")),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ImageProgram", "ImageProgram", "Resource", "TestImages")
            };

            foreach (string path in candidates)
            {
                if (Directory.Exists(path))
                    return path;
            }

            return null;
        }

        private static bool NeedsPreviewMode(BitmapFileInfo info)
        {
            return info.FileSize > Constants.PREVIEW_MODE_BYTES ||
                   info.GetActualPixelDataSize() > Constants.PREVIEW_MODE_BYTES;
        }

        private void SaveImage()
        {
            if (_resultImage?.PixelData == null && !_isPreviewMode)
            {
                MessageBox.Show("저장할 이미지가 없습니다.", "저장", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = Constants.BMP_FILTER,
                Title = "BMP 이미지 저장",
                FileName = _currentFilePath != null
                    ? Path.GetFileNameWithoutExtension(_currentFilePath) + "_result.bmp"
                    : "result.bmp"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                if (_isPreviewMode)
                {
                    MessageBox.Show("Preview 모드에서는 Viewer 2 표시 이미지만 저장됩니다.\n전체 해상도 저장은 C++ 처리 연동 후 지원됩니다.",
                        "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Viewer 2에 표시된 이미지를 Bitmap으로 변환해 저장
                    if (ResultViewer.CurrentRoi != null) { }
                    SavePreviewImage(dialog.FileName);
                }
                else
                {
                    _imageFileService.SaveImage(_resultImage!.PixelData, dialog.FileName);
                }
                StatusText.Text = Constants.SUCCESS_FILE_SAVED;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "저장 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SavePreviewImage(string filePath)
        {
            if (_currentFilePath == null || _headerInfo == null) return;
            var preview = BmpDisplayLoader.Load(_currentFilePath, _headerInfo);
            var encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(preview));
            using var fs = File.Create(filePath);
            encoder.Save(fs);
        }

        #endregion

        #region ROI

        private void RoiSelectToggle_Click(object sender, RoutedEventArgs e)
        {
            SourceViewer.RoiSelectMode = RoiSelectToggle.IsChecked == true;
            StatusText.Text = SourceViewer.RoiSelectMode
                ? "ROI Select 모드 — Viewer 1에서 드래그하세요."
                : "Pan 모드 — 드래그로 이미지 이동.";
        }

        private void RoiCancel_Click(object sender, RoutedEventArgs e)
        {
            SourceViewer.ClearRoi();
            _currentRoi = null;
            RoiInfoText.Text = "ROI: -";
            Histogram.Clear();
            RoiSelectToggle.IsChecked = false;
            SourceViewer.RoiSelectMode = false;
            StatusText.Text = "ROI가 취소되었습니다.";
        }

        private void OnRoiSelected(object? sender, RoiData roi)
        {
            _currentRoi = roi;
            RoiInfoText.Text = roi.ToString();
            UpdateHistogramPlaceholder(roi);
            StatusText.Text = $"ROI 선택됨: {roi}";
        }

        private void TemplateRegister_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRoi == null || !_currentRoi.IsValid())
            {
                MessageBox.Show("먼저 원본 Viewer에서 ROI를 선택하세요.", "Template", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryGetWorkBuffer(out PixelBuffer buffer))
                return;

            RunWithTimer("Template 등록", () =>
            {
                _templateData = _processing.RegisterTemplate(buffer, _currentRoi);
                if (_templateData == null)
                {
                    // DLL 없거나 실패 시 C# fallback crop
                    _templateData = ExtractTemplateFallback(buffer, _currentRoi);
                }

                if (_templateData == null)
                {
                    TemplateStatusText.Text = "Template: 등록 실패";
                    MatchPanel.Visibility = Visibility.Collapsed;
                    StatusText.Text = "Template 등록 실패";
                    return;
                }

                TemplateStatusText.Text = $"Template: {_templateData.Width}×{_templateData.Height} 등록됨";
                MatchPanel.Visibility = Visibility.Visible;
                SelectMethod(MatchingMethod.Coeff);
                StatusText.Text = "Template 등록 완료 — 비교 방식을 선택한 뒤 [비교 · 판정 실행]";
            });
        }

        private static TemplateData? ExtractTemplateFallback(PixelBuffer buffer, RoiData roi)
        {
            if (!roi.IsValid())
                return null;

            int x0 = Math.Clamp(roi.StartX, 0, buffer.Width - 1);
            int y0 = Math.Clamp(roi.StartY, 0, buffer.Height - 1);
            int w = Math.Min(roi.Width, buffer.Width - x0);
            int h = Math.Min(roi.Height, buffer.Height - y0);
            if (w <= 0 || h <= 0)
                return null;

            var pixels = new byte[w * h];
            for (int y = 0; y < h; y++)
                Buffer.BlockCopy(buffer.Data, (y0 + y) * buffer.Width + x0, pixels, y * w, w);

            return new TemplateData
            {
                SourceRoi = new RoiData { StartX = x0, StartY = y0, Width = w, Height = h },
                Pixels = pixels,
                Width = w,
                Height = h
            };
        }

        private void UpdateHistogramPlaceholder(RoiData roi)
        {
            PixelBuffer? buf = _workBuffer ?? _sourceBuffer;
            if (buf != null)
            {
                int[]? bins = _processing.GetHistogram(buf, roi);
                if (bins != null)
                {
                    Histogram.SetHistogram(bins);
                    return;
                }

                // C# fallback histogram
                var local = new int[256];
                int x0 = Math.Clamp(roi.StartX, 0, buf.Width - 1);
                int y0 = Math.Clamp(roi.StartY, 0, buf.Height - 1);
                int x1 = Math.Min(buf.Width, roi.StartX + roi.Width);
                int y1 = Math.Min(buf.Height, roi.StartY + roi.Height);
                for (int y = y0; y < y1; y++)
                {
                    int row = y * buf.Width;
                    for (int x = x0; x < x1; x++)
                        local[buf.Data[row + x]]++;
                }
                Histogram.SetHistogram(local);
                return;
            }

            Histogram.Clear();
        }

        #endregion

        #region Processing (C# Facade → C++ DLL)

        private void Process_Dilation(object sender, RoutedEventArgs e) =>
            RunMorphology(MorphologyOperation.Dilation);
        private void Process_Erosion(object sender, RoutedEventArgs e) =>
            RunMorphology(MorphologyOperation.Erosion);
        private void Process_Smoothing(object sender, RoutedEventArgs e)
        {
            if (!TryGetWorkBuffer(out PixelBuffer buffer))
                return;
            ApplyProcessingResult(_processing.RunSmoothing(buffer, BuildParams()));
        }

        private void Process_Threshold(object sender, RoutedEventArgs e)
        {
            if (!TryGetWorkBuffer(out PixelBuffer buffer))
                return;
            ApplyProcessingResult(_processing.RunThreshold(buffer, BuildParams()));
        }
        private void Process_Gaussian(object sender, RoutedEventArgs e) =>
            RunFilter(FilterOperation.Gaussian);
        private void Process_Laplacian(object sender, RoutedEventArgs e) =>
            RunFilter(FilterOperation.Laplacian);
        private void Process_Sobel(object sender, RoutedEventArgs e) =>
            RunFilter(FilterOperation.Sobel);

        private void MethodSelect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Primitives.ToggleButton btn || btn.Tag is null)
                return;

            var method = Enum.Parse<MatchingMethod>(btn.Tag.ToString()!);
            SelectMethod(method);
        }

        private void SelectMethod(MatchingMethod method)
        {
            _selectedMethod = method;
            MethodDiffBtn.IsChecked = method == MatchingMethod.Diff;
            MethodCorrBtn.IsChecked = method == MatchingMethod.Corr;
            MethodCoeffBtn.IsChecked = method == MatchingMethod.Coeff;
            StatusText.Text = $"비교 방식: {method} 선택됨 — [비교 · 판정 실행]을 누르세요.";
        }

        private void CompareAndJudge_Click(object sender, RoutedEventArgs e)
        {
            if (_templateData == null)
            {
                MessageBox.Show("먼저 Template을 등록하세요.", "비교·판정", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryGetWorkBuffer(out PixelBuffer buffer))
                return;

            if (!double.TryParse(PassThresholdBox.Text, out double threshold))
            {
                MessageBox.Show("합격 기준 Score를 숫자로 입력하세요. (예: 0.80)", "비교·판정",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sw = Stopwatch.StartNew();
            MatchingResult result = _processing.RunMatching(_selectedMethod, buffer, _templateData, null);
            sw.Stop();
            ProcessingTimeText.Text = $"Processing Time: {sw.ElapsedMilliseconds} ms ({_selectedMethod})";

            if (!result.Success)
            {
                _lastMatchScore = null;
                MatchScoreText.Text = "Score: -";
                MatchPositionText.Text = "Position: -";
                JudgeResultText.Text = $"판정: 비교 실패 — {result.Message}";
                JudgeResultText.Foreground = System.Windows.Media.Brushes.Salmon;
                StatusText.Text = JudgeResultText.Text;
                return;
            }

            _lastMatchScore = result.Score;
            MatchScoreText.Text = $"Score: {result.Score:F4}  ({_selectedMethod})";
            MatchPositionText.Text = $"Position: ({result.BestX}, {result.BestY})";

            bool pass = result.Score >= threshold;
            JudgeResultText.Text = pass
                ? $"판정: 정상 (PASS)  {result.Score:F4} ≥ {threshold:F2}"
                : $"판정: 불량 (FAIL)  {result.Score:F4} < {threshold:F2}";
            JudgeResultText.Foreground = pass
                ? System.Windows.Media.Brushes.LightGreen
                : System.Windows.Media.Brushes.Salmon;
            StatusText.Text = JudgeResultText.Text;

            // 매칭 위치를 결과 Viewer ROI로 표시
            ResultViewer.ShowRoi(new RoiData
            {
                StartX = result.BestX,
                StartY = result.BestY,
                Width = _templateData.Width,
                Height = _templateData.Height
            });
        }

        private void ResetMatchUi()
        {
            _templateData = null;
            _lastMatchScore = null;
            TemplateStatusText.Text = "Template: 미등록";
            MatchPanel.Visibility = Visibility.Collapsed;
            MatchScoreText.Text = "Score: -";
            MatchPositionText.Text = "Position: -";
            JudgeResultText.Text = "판정: -";
            JudgeResultText.Foreground = System.Windows.Media.Brushes.White;
            MethodDiffBtn.IsChecked = false;
            MethodCorrBtn.IsChecked = false;
            MethodCoeffBtn.IsChecked = false;
        }

        private void RunMorphology(MorphologyOperation op)
        {
            if (!TryGetWorkBuffer(out PixelBuffer buffer))
                return;
            ApplyProcessingResult(_processing.RunMorphology(op, buffer, BuildParams()));
        }

        private void RunFilter(FilterOperation op)
        {
            if (!TryGetWorkBuffer(out PixelBuffer buffer))
                return;
            ApplyProcessingResult(_processing.RunFilter(op, buffer, BuildParams()));
        }

        private void ApplyProcessingResult(ProcessingResult result)
        {
            ProcessingTimeText.Text = $"Processing Time: {result.ElapsedMs} ms ({result.OperationName})";
            if (!result.Success || result.OutputPixels == null)
            {
                StatusText.Text = $"{result.OperationName}: {result.Message}";
                return;
            }

            var outBuffer = new PixelBuffer(result.OutputPixels, result.Width, result.Height);
            _workBuffer = outBuffer;

            using Bitmap bmp = outBuffer.ToBitmap();
            BitmapSource display = Utils.ImageConverter.BitmapToBitmapImage(bmp);
            ResultViewer.SetImage(display);

            _resultImage?.Dispose();
            _resultImage = new ImageData
            {
                SourceFilePath = _currentFilePath ?? "",
                PixelData = (Bitmap)bmp.Clone()
            };
            StatusText.Text = $"{result.OperationName} 완료 → 다음 단계로 진행하세요.";
        }

        private ProcessingParams BuildParams() => new()
        {
            KernelSize = 3,
            ThresholdValue = 128,
            GaussianSigma = 1.0,
            Roi = _currentRoi
        };

        private bool TryGetWorkBuffer(out PixelBuffer buffer)
        {
            if (_workBuffer != null)
            {
                buffer = _workBuffer;
                return true;
            }

            if (_sourceBuffer != null)
            {
                _workBuffer = _sourceBuffer.Clone();
                buffer = _workBuffer;
                return true;
            }

            MessageBox.Show(
                "처리용 픽셀 버퍼가 없습니다.\n이미지를 다시 열어주세요.",
                "영상처리", MessageBoxButton.OK, MessageBoxImage.Warning);
            buffer = null!;
            return false;
        }

        private bool TryGetSourceBuffer(out PixelBuffer buffer)
        {
            if (_sourceBuffer != null)
            {
                buffer = _sourceBuffer;
                return true;
            }

            MessageBox.Show(
                "처리용 픽셀 버퍼가 없습니다.\n이미지를 다시 열어주세요.",
                "영상처리", MessageBoxButton.OK, MessageBoxImage.Warning);
            buffer = null!;
            return false;
        }

        private void RunWithTimer(string operation, Action action)
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            ProcessingTimeText.Text = $"Processing Time: {sw.ElapsedMilliseconds} ms ({operation})";
        }

        #endregion

        #region Helpers

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
            return $"{len:0.##} {sizes[order]}";
        }

        protected override void OnClosed(EventArgs e)
        {
            _sourceImage?.Dispose();
            _resultImage?.Dispose();
            _sourceBuffer = null;
            _workBuffer = null;
            _templateData = null;
            base.OnClosed(e);
        }

        #endregion
    }
}





