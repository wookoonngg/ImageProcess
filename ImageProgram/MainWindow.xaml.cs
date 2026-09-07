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

        public MainWindow()
        {
            InitializeComponent();

            _logger = new ConsoleLogger();
            _imageFileService = new ImageFileService(_logger);
            _processing = new ImageProcessingFacade(new NativeImageProcessingBridge(_logger));

            SourceViewer.ViewportChanged += (_, e) => Navigator.UpdateViewport(e);
            SourceViewer.RoiSelected += OnRoiSelected;

            CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (_, _) => OpenImage()));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (_, _) => SaveImage()));
            InputBindings.Add(new KeyBinding(ApplicationCommands.Open, Key.O, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(ApplicationCommands.Save, Key.S, ModifierKeys.Control));
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
                _templateData = null;
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
                    }

                    try
                    {
                        Bitmap bitmap = await Task.Run(() => _imageFileService.LoadImage(filePath));
                        _sourceImage = new ImageData { SourceFilePath = filePath, PixelData = bitmap };
                        _resultImage = new ImageData { SourceFilePath = filePath, PixelData = (Bitmap)bitmap.Clone() };
                        _sourceBuffer = await Task.Run(() => PixelBuffer.FromBitmap(bitmap));
                    }
                    catch
                    {
                        _isPreviewMode = true;
                        _sourceBuffer = null;
                    }
                }

                SourceViewer.SetImage(displayImage);
                ResultViewer.SetImage(displayImage);
                Navigator.SetPreviewImage(displayImage);
                Histogram.Clear();
                RoiInfoText.Text = "ROI: -";
                MatchScoreText.Text = "Score: -";
                MatchPositionText.Text = "Position: -";

                StatusText.Text = _isPreviewMode
                    ? $"Preview 모드 ({displayImage.PixelWidth}×{displayImage.PixelHeight} 표시) — 원본: {headerInfo.Width}×{headerInfo.Height}"
                    : Constants.SUCCESS_FILE_OPENED;

                ImageInfoText.Text =
                    $"{headerInfo.Width}×{headerInfo.Height} | {headerInfo.BitDepth}bit | " +
                    $"{FormatBytes(headerInfo.FileSize)} | {Path.GetFileName(filePath)}";
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
                MessageBox.Show("먼저 ROI를 선택하세요.", "Template", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryGetSourceBuffer(out PixelBuffer buffer))
                return;

            RunWithTimer("Template 등록", () =>
            {
                _templateData = _processing.RegisterTemplate(buffer, _currentRoi);
                if (_templateData == null)
                {
                    StatusText.Text =
                        $"Template ROI 확보: {_currentRoi.Width}×{_currentRoi.Height} — C++ IpExtractRoi 스텁(미구현) 또는 DLL 없음";
                    // C++ 미구현 시에도 Matching 경로 테스트용으로 ROI 크기만 보관
                    _templateData = new TemplateData
                    {
                        SourceRoi = _currentRoi,
                        Pixels = new byte[checked(_currentRoi.Width * _currentRoi.Height)],
                        Width = _currentRoi.Width,
                        Height = _currentRoi.Height
                    };
                }
                else
                {
                    StatusText.Text = $"Template 등록됨: {_templateData.Width}×{_templateData.Height}";
                }
            });
        }

        private void UpdateHistogramPlaceholder(RoiData roi)
        {
            if (_sourceBuffer != null)
            {
                int[]? bins = _processing.GetHistogram(_sourceBuffer, roi);
                if (bins != null)
                {
                    Histogram.SetHistogram(bins);
                    return;
                }
            }

            // C++ 스텁/미로드 시 자리표시용
            var demo = new int[256];
            int peak = Math.Clamp(roi.Width + roi.Height, 1, 255);
            for (int i = 0; i < 256; i++)
                demo[i] = Math.Max(0, peak - Math.Abs(i - peak / 2));
            Histogram.SetHistogram(demo);
        }

        #endregion

        #region Processing (C# Facade → C++ DLL)

        private void Process_Dilation(object sender, RoutedEventArgs e) =>
            RunMorphology(MorphologyOperation.Dilation);
        private void Process_Erosion(object sender, RoutedEventArgs e) =>
            RunMorphology(MorphologyOperation.Erosion);
        private void Process_Smoothing(object sender, RoutedEventArgs e)
        {
            if (!TryGetSourceBuffer(out PixelBuffer buffer))
                return;
            ApplyProcessingResult(_processing.RunSmoothing(buffer, BuildParams()));
        }

        private void Process_Threshold(object sender, RoutedEventArgs e)
        {
            if (!TryGetSourceBuffer(out PixelBuffer buffer))
                return;
            ApplyProcessingResult(_processing.RunThreshold(buffer, BuildParams()));
        }
        private void Process_Gaussian(object sender, RoutedEventArgs e) =>
            RunFilter(FilterOperation.Gaussian);
        private void Process_Laplacian(object sender, RoutedEventArgs e) =>
            RunFilter(FilterOperation.Laplacian);
        private void Process_Sobel(object sender, RoutedEventArgs e) =>
            RunFilter(FilterOperation.Sobel);

        private void Match_DIFF(object sender, RoutedEventArgs e) =>
            RunMatching(MatchingMethod.Diff);
        private void Match_CORR(object sender, RoutedEventArgs e) =>
            RunMatching(MatchingMethod.Corr);
        private void Match_COEFF(object sender, RoutedEventArgs e) =>
            RunMatching(MatchingMethod.Coeff);

        private void RunMorphology(MorphologyOperation op)
        {
            if (!TryGetSourceBuffer(out PixelBuffer buffer))
                return;
            ApplyProcessingResult(_processing.RunMorphology(op, buffer, BuildParams()));
        }

        private void RunFilter(FilterOperation op)
        {
            if (!TryGetSourceBuffer(out PixelBuffer buffer))
                return;
            ApplyProcessingResult(_processing.RunFilter(op, buffer, BuildParams()));
        }

        private void RunMatching(MatchingMethod method)
        {
            if (_templateData == null)
            {
                MessageBox.Show("먼저 Template을 등록하세요.", method.ToString(),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryGetSourceBuffer(out PixelBuffer buffer))
                return;

            MatchingResult result = _processing.RunMatching(method, buffer, _templateData, _currentRoi);
            ProcessingTimeText.Text = $"Processing Time: - ms (Template {method})";
            if (result.Success)
            {
                MatchScoreText.Text = $"Score: {result.Score:F4}";
                MatchPositionText.Text = $"Position: ({result.BestX}, {result.BestY})";
                StatusText.Text = $"Template Matching {method} OK";
            }
            else
            {
                MatchScoreText.Text = "Score: -";
                MatchPositionText.Text = "Position: -";
                StatusText.Text = $"Template Matching {method}: {result.Message}";
            }
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
            using Bitmap bmp = outBuffer.ToBitmap();
            BitmapSource display = Utils.ImageConverter.BitmapToBitmapImage(bmp);
            ResultViewer.SetImage(display);

            _resultImage?.Dispose();
            _resultImage = new ImageData
            {
                SourceFilePath = _currentFilePath ?? "",
                PixelData = (Bitmap)bmp.Clone()
            };
            StatusText.Text = $"{result.OperationName} 완료";
        }

        private ProcessingParams BuildParams() => new()
        {
            KernelSize = 3,
            ThresholdValue = 128,
            GaussianSigma = 1.0,
            Roi = _currentRoi
        };

        private bool TryGetSourceBuffer(out PixelBuffer buffer)
        {
            if (_sourceBuffer != null)
            {
                buffer = _sourceBuffer;
                return true;
            }

            MessageBox.Show(
                "처리용 픽셀 버퍼가 없습니다.\n" +
                "작은 BMP를 열거나(전체 로드), Preview 모드에서는 C++ 연동 후 ROI 타일 처리를 사용하세요.",
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
            _templateData = null;
            base.OnClosed(e);
        }

        #endregion
    }
}

