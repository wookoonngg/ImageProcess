using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private bool _syncingViewers;
        private bool _uiReady;
        private string? _lastLogMessage;
        private readonly List<string> _pipelineSteps = new();
        private readonly Stack<(PixelBuffer Buffer, string Step)> _undoStack = new();
        private const int MaxUndoDepth = 8;
        private byte[]? _originalBgra;
        private int _displayWidth;
        private int _displayHeight;
        private BitmapSource? _originalDisplay;

        public MainWindow()
        {
            InitializeComponent();

            _logger = new ConsoleLogger();
            _imageFileService = new ImageFileService(_logger);
            _processing = new ImageProcessingFacade(new NativeImageProcessingBridge(_logger));

            SourceViewer.ViewportChanged += OnSourceViewportChanged;
            ResultViewer.ViewportChanged += OnResultViewportChanged;
            SourceViewer.RoiSelected += OnRoiSelected;
            // Viewer 2는 ROI 선택 불가 (표시만)
            ResultViewer.RoiSelectMode = false;
            Navigator.NavigateRequested += OnNavigatorNavigate;
            ResetMatchUi();
            SelectMethod(MatchingMethod.Coeff, log: false);
            ShowPreprocessTab();
            _uiReady = true;

            CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (_, _) => OpenImage()));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (_, _) => SaveImage()));
            InputBindings.Add(new KeyBinding(ApplicationCommands.Open, Key.O, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(ApplicationCommands.Save, Key.S, ModifierKeys.Control));
        }

        private void TabPreprocess_Click(object sender, RoutedEventArgs e) => ShowPreprocessTab();

        private void TabMatching_Click(object sender, RoutedEventArgs e) => ShowMatchingTab();

        private void ShowPreprocessTab()
        {
            if (PanelPreprocess == null || PanelMatching == null)
                return;

            PanelPreprocess.Visibility = Visibility.Visible;
            PanelMatching.Visibility = Visibility.Collapsed;
            TabPreprocessBtn.IsChecked = true;
            TabMatchingBtn.IsChecked = false;
            StatusText.Text = "전처리 탭 — ROI 선택 후 필터/형태학/이진화 적용";
        }

        private void ShowMatchingTab()
        {
            if (PanelPreprocess == null || PanelMatching == null)
                return;

            PanelPreprocess.Visibility = Visibility.Collapsed;
            PanelMatching.Visibility = Visibility.Visible;
            TabPreprocessBtn.IsChecked = false;
            TabMatchingBtn.IsChecked = true;
       
        }

        private void OnNavigatorNavigate(object? sender, NavigatorNavigateEventArgs e)
        {
            _syncingViewers = true;
            try
            {
                SourceViewer.NavigateToImagePoint(e.ImageX, e.ImageY, raiseEvent: false);
                ResultViewer.NavigateToImagePoint(e.ImageX, e.ImageY, raiseEvent: false);
                Navigator.UpdateViewport(SourceViewer.GetViewportState());
            }
            finally
            {
                _syncingViewers = false;
            }

            StatusText.Text = $"Navigator 이동 → 이미지 좌표 ({e.ImageX:F0}, {e.ImageY:F0})";
        }

        private void OnSourceViewportChanged(object? sender, ViewportChangedEventArgs e)
        {
            if (_syncingViewers)
                return;

            _syncingViewers = true;
            try
            {
                ResultViewer.SyncViewport(e);
                Navigator.UpdateViewport(e);
            }
            finally
            {
                _syncingViewers = false;
            }
        }

        private void OnResultViewportChanged(object? sender, ViewportChangedEventArgs e)
        {
            if (_syncingViewers)
                return;

            _syncingViewers = true;
            try
            {
                SourceViewer.SyncViewport(e);
                Navigator.UpdateViewport(e);
            }
            finally
            {
                _syncingViewers = false;
            }
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
                _originalBgra = null;
                _originalDisplay = null;
                // Template은 골든에서 따 두고 검사 이미지로 넘기기 위해 유지
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
                _originalBgra = null;
                if (_isPreviewMode)
                {
                    var sw = Stopwatch.StartNew();
                    var (pixels, w, h) = await Task.Run(() => BmpDisplayLoader.LoadSubsampledPixels(filePath, headerInfo));
                    displayImage = BmpDisplayLoader.CreateBgraBitmap(pixels, w, h);
                    _originalBgra = pixels;
                    _displayWidth = w;
                    _displayHeight = h;
                    _sourceBuffer = await Task.Run(() => PixelBuffer.FromBgra32(pixels, w, h));
                    _workBuffer = _sourceBuffer.Clone();
                    sw.Stop();
                    ProcessingTimeText.Text = $"Processing Time: {sw.ElapsedMilliseconds} ms (Preview Load)";
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
                        _originalBgra = pixels;
                        _displayWidth = w;
                        _displayHeight = h;
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
                            _displayWidth = _sourceBuffer.Width;
                            _displayHeight = _sourceBuffer.Height;
                            _originalBgra = BgraFromGrayBuffer(_sourceBuffer);
                        }
                        catch
                        {
                            _isPreviewMode = true;
                            if (_sourceBuffer == null)
                            {
                                _sourceBuffer = PixelBuffer.FromBitmapSource(displayImage);
                                _workBuffer = _sourceBuffer.Clone();
                            }
                            _displayWidth = displayImage.PixelWidth;
                            _displayHeight = displayImage.PixelHeight;
                            _originalBgra ??= CopyBitmapSourceToBgra(displayImage);
                        }
                    }
                }

                if (_originalBgra == null ||
                    _displayWidth != displayImage.PixelWidth ||
                    _displayHeight != displayImage.PixelHeight)
                {
                    _displayWidth = displayImage.PixelWidth;
                    _displayHeight = displayImage.PixelHeight;
                    _originalBgra = CopyBitmapSourceToBgra(displayImage);
                }

                _originalDisplay = displayImage;
                SourceViewer.SetImage(displayImage);
                ResultViewer.SetImage(displayImage);
                Navigator.SetPreviewImage(displayImage);
                Histogram.Clear();
                RoiInfoText.Text = "ROI: -";
                ClearMatchResultUi();
                ClearPipeline();
                RefreshTemplateStatusAfterImageOpen();

                StatusText.Text = _isPreviewMode
                    ? $"Preview 모드 ({displayImage.PixelWidth}×{displayImage.PixelHeight} 처리 버퍼) — 원본: {headerInfo.Width}×{headerInfo.Height}"
                    : Constants.SUCCESS_FILE_OPENED;

                ImageInfoText.Text =
                    $"{headerInfo.Width}×{headerInfo.Height} | {headerInfo.BitDepth}bit | " +
                    $"{FormatBytes(headerInfo.FileSize)} | {Path.GetFileName(filePath)}";
                if (_isPreviewMode)
                    ImageInfoText.Text += " | Preview 연산";

                ClearAnalysisLog();
                AppendAnalysisLog(
                    $"이미지 열기: {Path.GetFileName(filePath)} | 원본 {headerInfo.Width}×{headerInfo.Height} " +
                    $"({headerInfo.BitDepth}bit, {FormatBytes(headerInfo.FileSize)})" +
                    (_isPreviewMode
                        ? $" | Preview 버퍼 {displayImage.PixelWidth}×{displayImage.PixelHeight}"
                        : ""));
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
            // BmpDisplayLoader.Load와 동일 조건 — 표시 크기와 처리 버퍼가 어긋나면
            // ROI 좌표가 버퍼 밖으로 나가 전체 이미지 연산으로 이어질 수 있음
            return info.FileSize > Constants.PREVIEW_MODE_BYTES ||
                   info.GetActualPixelDataSize() > Constants.PREVIEW_MODE_BYTES ||
                   info.Width > Constants.PREVIEW_MAX_PIXEL ||
                   info.Height > Constants.PREVIEW_MAX_PIXEL;
        }

        private void SaveImage()
        {
            PixelBuffer? saveBuf = _workBuffer ?? _sourceBuffer;
            if (saveBuf == null && _resultImage?.PixelData == null && !_isPreviewMode)
            {
                MessageBox.Show("저장할 이미지가 없습니다.", "저장", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = Constants.BMP_FILTER,
                Title = "BMP 저장 — Viewer2 전체 처리결과 (ROI만 저장하지 않음)",
                FileName = _currentFilePath != null
                    ? Path.GetFileNameWithoutExtension(_currentFilePath) + "_result.bmp"
                    : "result.bmp"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                if (saveBuf != null)
                {
                    using Bitmap bmp = saveBuf.ToBitmap();
                    _imageFileService.SaveImage(bmp, dialog.FileName);
                }
                else if (_isPreviewMode)
                {
                    MessageBox.Show(
                        "Preview 모드: Viewer 2에 표시 중인 전체 이미지를 저장합니다.\nROI만 잘라 저장하지 않습니다.",
                        "Save BMP", MessageBoxButton.OK, MessageBoxImage.Information);
                    SavePreviewImage(dialog.FileName);
                }
                else
                {
                    _imageFileService.SaveImage(_resultImage!.PixelData, dialog.FileName);
                }

                StatusText.Text = "저장 완료 — 전체 처리결과 BMP";
                AppendAnalysisLog($"Save BMP (전체결과): {Path.GetFileName(dialog.FileName)}");
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
            bool on = RoiSelectToggle.IsChecked == true;
            SourceViewer.RoiSelectMode = on;
            ResultViewer.RoiSelectMode = false;
            StatusText.Text = on
                ? "ROI Select 모드 — Viewer 1에서만 드래그하세요. 연산은 ROI에만 적용됩니다."
                : "Pan 모드 — 드래그로 이미지 이동.";
        }

        private void RoiCancel_Click(object sender, RoutedEventArgs e)
        {
            SourceViewer.ClearRoi();
            ResultViewer.ClearRoi();
            _currentRoi = null;
            RoiInfoText.Text = "ROI: -";
            Histogram.Clear();
            RoiSelectToggle.IsChecked = false;
            SourceViewer.RoiSelectMode = false;
            ResultViewer.RoiSelectMode = false;
            StatusText.Text = "ROI가 취소되었습니다.";
            AppendAnalysisLog("ROI 취소");
        }

        private void OnRoiSelected(object? sender, RoiData roi)
        {
            // Viewer 1에서만 ROI 선택 허용
            if (!ReferenceEquals(sender, SourceViewer))
                return;

            _currentRoi = roi;
            SourceViewer.ShowRoi(roi);
            ResultViewer.ShowRoi(roi);
            RoiInfoText.Text = roi.ToString();
            UpdateHistogramPlaceholder(roi);
            StatusText.Text = $"ROI 선택됨: {roi} — 이후 연산은 이 영역에만 적용됩니다.";
            AppendAnalysisLog($"ROI 선택: ({roi.StartX},{roi.StartY}) {roi.Width}×{roi.Height}");
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
                    UpdateTemplatePreview(null);
                    StatusText.Text = "Template 등록 실패";
                    AppendAnalysisLog("Template 등록 실패");
                    return;
                }

                // 골든 파일 경로 기록 (이후 다른 이미지에서 매칭해야 검사)
                _templateData = new TemplateData
                {
                    SourceRoi = _templateData.SourceRoi,
                    Pixels = _templateData.Pixels,
                    Width = _templateData.Width,
                    Height = _templateData.Height,
                    SourceFilePath = _currentFilePath,
                    RegisteredAt = _templateData.RegisteredAt
                };

                string goldenName = Path.GetFileName(_currentFilePath) ?? "(unknown)";
                TemplateStatusText.Text =
                    $"Template: {_templateData.Width}×{_templateData.Height}\n골든: {goldenName}\n→ 검사 이미지를 새로 Open 하세요";
                UpdateTemplatePreview(_templateData);
                SelectMethod(MatchingMethod.Coeff);
                StatusText.Text =
                    "Template 등록 완료 — 검사할 다른 이미지를 연 뒤 Matching 실행";
                AppendAnalysisLog(
                    $"Template 등록: {_templateData.Width}×{_templateData.Height} " +
                    $"(골든 ROI {_templateData.SourceRoi}, 파일 {goldenName})");
            });
        }

        private void TemplateLoadFromFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = Constants.BMP_FILTER,
                Title = "Template BMP 불러오기"
            };
            string? initial = FindDefaultImageFolder();
            if (initial != null)
                dialog.InitialDirectory = initial;

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var sw = Stopwatch.StartNew();

                BitmapFileInfo header = _imageFileService.ReadHeader(dialog.FileName);
                if (header.Width <= 0 || header.Height <= 0)
                    throw new InvalidOperationException("잘못된 Template BMP 헤더입니다.");

                // 큰 파일은 Preview 서브샘플을 Template로 쓰지 않음 — 작은 패턴 파일 전제
                if (header.Width > 2048 || header.Height > 2048 || header.FileSize > 50L * 1024 * 1024)
                {
                    MessageBox.Show(
                        "Template 파일은 작은 패턴 BMP를 권장합니다 (예: ≤2048px).\n" +
                        "큰 이미지는 원본에서 ROI로 등록하세요.",
                        "Template", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Bitmap bitmap = _imageFileService.LoadImage(dialog.FileName);
                PixelBuffer gray = PixelBuffer.FromBitmap(bitmap);
                bitmap.Dispose();

                var pixels = new byte[gray.Data.Length];
                Buffer.BlockCopy(gray.Data, 0, pixels, 0, gray.Data.Length);

                _templateData = new TemplateData
                {
                    SourceRoi = new RoiData { StartX = 0, StartY = 0, Width = gray.Width, Height = gray.Height },
                    Pixels = pixels,
                    Width = gray.Width,
                    Height = gray.Height,
                    SourceFilePath = dialog.FileName
                };

                sw.Stop();
                ProcessingTimeText.Text = $"Processing Time: {sw.ElapsedMilliseconds} ms (Template Load)";
                TemplateStatusText.Text =
                    $"Template: {_templateData.Width}×{_templateData.Height}\n파일: {Path.GetFileName(dialog.FileName)}";
                UpdateTemplatePreview(_templateData);
                SelectMethod(MatchingMethod.Coeff);
                StatusText.Text = "Template 파일 로드 완료 — 검사 이미지에서 Matching 실행";
                AppendAnalysisLog(
                    $"Template 파일 로드: {Path.GetFileName(dialog.FileName)} " +
                    $"{_templateData.Width}×{_templateData.Height}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Template 로드 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void UpdateTemplatePreview(TemplateData? template)
        {
            if (TemplatePreviewImage == null || TemplatePreviewPlaceholder == null)
                return;

            if (template == null || template.Pixels.Length == 0)
            {
                TemplatePreviewImage.Source = null;
                TemplatePreviewPlaceholder.Visibility = Visibility.Visible;
                return;
            }

            var wb = new WriteableBitmap(template.Width, template.Height, 96, 96,
                System.Windows.Media.PixelFormats.Gray8, null);
            wb.WritePixels(new Int32Rect(0, 0, template.Width, template.Height),
                template.Pixels, template.Width, 0);
            wb.Freeze();
            TemplatePreviewImage.Source = wb;
            TemplatePreviewPlaceholder.Visibility = Visibility.Collapsed;
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
                Height = h,
                SourceFilePath = null
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
            if (!TryBeginRoiProcessing(out PixelBuffer buffer))
                return;
            var p = BuildParams();
            ApplyProcessingResult(_processing.RunSmoothing(buffer, p), $"Smoothing(k={p.KernelSize})");
        }

        private void Process_Threshold(object sender, RoutedEventArgs e)
        {
            if (!TryBeginRoiProcessing(out PixelBuffer buffer))
                return;

            var p = BuildParams();
            if (ThresholdOtsuRadio.IsChecked == true)
            {
                p.ThresholdValue = ComputeOtsuThreshold(buffer, _currentRoi);
                StatusText.Text = $"Otsu 자동 임계값 = {p.ThresholdValue}";
            }

            ApplyProcessingResult(
                _processing.RunThreshold(buffer, p),
                stepLabel: ThresholdOtsuRadio.IsChecked == true
                    ? $"Threshold(Otsu={p.ThresholdValue})"
                    : $"Threshold({p.ThresholdValue})");
        }
        private void Process_Gaussian(object sender, RoutedEventArgs e) =>
            RunFilter(FilterOperation.Gaussian);
        private void Process_Laplacian(object sender, RoutedEventArgs e) =>
            RunFilter(FilterOperation.Laplacian);
        private void Process_Sobel(object sender, RoutedEventArgs e) =>
            RunFilter(FilterOperation.Sobel);

        private void MatchMethodCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // XAML 로드 중 SelectedIndex 설정 시 AnalysisLogText 등 아직 null일 수 있음
            if (!_uiReady)
                return;
            if (MatchMethodCombo?.SelectedItem is not System.Windows.Controls.ComboBoxItem item || item.Tag is null)
                return;

            var method = Enum.Parse<MatchingMethod>(item.Tag.ToString()!);
            SelectMethod(method);
        }

        private void SelectMethod(MatchingMethod method, bool log = true)
        {
            if (_selectedMethod == method && log)
            {
                // 동일 방식 재선택은 로그 중복 방지
                return;
            }

            _selectedMethod = method;
            if (MatchMethodCombo != null)
            {
                for (int i = 0; i < MatchMethodCombo.Items.Count; i++)
                {
                    if (MatchMethodCombo.Items[i] is System.Windows.Controls.ComboBoxItem citem &&
                        string.Equals(citem.Tag?.ToString(), method.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        if (MatchMethodCombo.SelectedIndex != i)
                            MatchMethodCombo.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (!log)
                return;

            string hint = method switch
            {
                MatchingMethod.Diff => "DIFF(SAD): 픽셀 차이가 작을수록 높은 Score",
                MatchingMethod.Corr => "CORR: 상관값이 클수록 유사",
                _ => "COEFF(NCC): 정규화 상관 (0~1 권장)"
            };
            StatusText.Text = $"비교 방식: {method} — {hint}";
            AppendAnalysisLog($"비교 방식 선택: {method} ({hint})");
        }

        private void CompareAndJudge_Click(object sender, RoutedEventArgs e)
        {
            if (_templateData == null)
            {
                MessageBox.Show("먼저 Template을 등록하거나 파일로 불러오세요.", "Template Matching",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryGetWorkBuffer(out PixelBuffer buffer))
                return;

            if (!double.TryParse(PassThresholdBox.Text, out double threshold))
            {
                MessageBox.Show("합격 기준 Score를 숫자로 입력하세요. (예: 0.80)", "Template Matching",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RoiData? searchRoi = null;
            if (SearchRoiRadio.IsChecked == true)
            {
                if (_currentRoi == null || !_currentRoi.IsValid())
                {
                    MessageBox.Show("ROI 영역 내 검색을 선택했습니다. Viewer에서 ROI를 먼저 지정하세요.",
                        "검색 범위", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                searchRoi = _currentRoi;
            }

            bool sameAsGolden =
                !string.IsNullOrEmpty(_templateData.SourceFilePath) &&
                string.Equals(_templateData.SourceFilePath, _currentFilePath, StringComparison.OrdinalIgnoreCase);

            if (sameAsGolden)
            {
                var answer = MessageBox.Show(
                    "지금 열린 이미지가 Template 출처와 같습니다.\n" +
                    "같은 자리에서 거의 최고 Score가 나옵니다.\n\n그래도 실행할까요?",
                    "자기 자신 비교",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes)
                    return;
            }

            var sw = Stopwatch.StartNew();
            MatchingResult result = _processing.RunMatching(_selectedMethod, buffer, _templateData, searchRoi);
            if (!result.Success)
            {
                // Native DLL 실패 시 C# fallback matching
                result = RunMatchingFallback(_selectedMethod, buffer, _templateData, searchRoi);
            }
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
                AppendAnalysisLog(
                    $"Matching 실패 [{_selectedMethod}] {sw.ElapsedMilliseconds}ms — {result.Message}");
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

            string scope = searchRoi == null ? "전체" : $"ROI {searchRoi}";
            AppendAnalysisLog(
                $"Template Matching [{_selectedMethod}] Template {_templateData.Width}×{_templateData.Height} " +
                $"검색={scope} | 최고점 ({result.BestX},{result.BestY}) Score={result.Score:F4} | " +
                $"기준≥{threshold:F2} → {(pass ? "PASS" : "FAIL")} | {sw.ElapsedMilliseconds}ms");

            // Viewer 2: 현재 결과 이미지 유지 + 매칭 위치 Box
            var matchRoi = new RoiData
            {
                StartX = result.BestX,
                StartY = result.BestY,
                Width = _templateData.Width,
                Height = _templateData.Height
            };
            ResultViewer.ShowRoi(matchRoi);
            SourceViewer.ShowRoi(matchRoi);
        }

        /// <summary>Native 실패 시 순수 C# 슬라이딩 윈도우 matching.</summary>
        private static MatchingResult RunMatchingFallback(
            MatchingMethod method, PixelBuffer image, TemplateData template, RoiData? searchRoi)
        {
            int tw = template.Width;
            int th = template.Height;
            if (tw <= 0 || th <= 0 || tw > image.Width || th > image.Height)
            {
                return new MatchingResult
                {
                    Success = false,
                    Method = method,
                    Message = "Template 크기가 이미지보다 큽니다."
                };
            }

            int x0 = 0, y0 = 0, x1 = image.Width - tw, y1 = image.Height - th;
            if (searchRoi != null && searchRoi.IsValid())
            {
                x0 = Math.Clamp(searchRoi.StartX, 0, image.Width - tw);
                y0 = Math.Clamp(searchRoi.StartY, 0, image.Height - th);
                x1 = Math.Clamp(searchRoi.StartX + searchRoi.Width - tw, x0, image.Width - tw);
                y1 = Math.Clamp(searchRoi.StartY + searchRoi.Height - th, y0, image.Height - th);
            }

            int bestX = x0, bestY = y0;
            double bestScore = method == MatchingMethod.Diff ? double.MinValue : double.MinValue;

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    double score = method switch
                    {
                        MatchingMethod.Diff => ScoreDiff(image, template, x, y),
                        MatchingMethod.Corr => ScoreCorr(image, template, x, y),
                        _ => ScoreCoeff(image, template, x, y)
                    };

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestX = x;
                        bestY = y;
                    }
                }
            }

            return new MatchingResult
            {
                Success = true,
                Method = method,
                BestX = bestX,
                BestY = bestY,
                Score = bestScore,
                Message = "C# fallback OK"
            };
        }

        private static double ScoreDiff(PixelBuffer image, TemplateData t, int ox, int oy)
        {
            long sum = 0;
            int n = t.Width * t.Height;
            for (int ty = 0; ty < t.Height; ty++)
            {
                int iRow = (oy + ty) * image.Width + ox;
                int tRow = ty * t.Width;
                for (int tx = 0; tx < t.Width; tx++)
                    sum += Math.Abs(image.Data[iRow + tx] - t.Pixels[tRow + tx]);
            }
            // SAD → 유사도(높을수록 좋음): 1 - 평균차이/255
            return 1.0 - (sum / (double)n) / 255.0;
        }

        private static double ScoreCorr(PixelBuffer image, TemplateData t, int ox, int oy)
        {
            double sumIT = 0, sumI2 = 0, sumT2 = 0;
            for (int ty = 0; ty < t.Height; ty++)
            {
                int iRow = (oy + ty) * image.Width + ox;
                int tRow = ty * t.Width;
                for (int tx = 0; tx < t.Width; tx++)
                {
                    double iv = image.Data[iRow + tx];
                    double tv = t.Pixels[tRow + tx];
                    sumIT += iv * tv;
                    sumI2 += iv * iv;
                    sumT2 += tv * tv;
                }
            }
            double denom = Math.Sqrt(sumI2 * sumT2);
            return denom < 1e-9 ? 0 : sumIT / denom;
        }

        private static double ScoreCoeff(PixelBuffer image, TemplateData t, int ox, int oy)
        {
            int n = t.Width * t.Height;
            double sumI = 0, sumT = 0;
            for (int ty = 0; ty < t.Height; ty++)
            {
                int iRow = (oy + ty) * image.Width + ox;
                int tRow = ty * t.Width;
                for (int tx = 0; tx < t.Width; tx++)
                {
                    sumI += image.Data[iRow + tx];
                    sumT += t.Pixels[tRow + tx];
                }
            }
            double meanI = sumI / n;
            double meanT = sumT / n;

            double num = 0, denI = 0, denT = 0;
            for (int ty = 0; ty < t.Height; ty++)
            {
                int iRow = (oy + ty) * image.Width + ox;
                int tRow = ty * t.Width;
                for (int tx = 0; tx < t.Width; tx++)
                {
                    double di = image.Data[iRow + tx] - meanI;
                    double dt = t.Pixels[tRow + tx] - meanT;
                    num += di * dt;
                    denI += di * di;
                    denT += dt * dt;
                }
            }
            double denom = Math.Sqrt(denI * denT);
            return denom < 1e-9 ? 0 : num / denom;
        }

        private void ResetMatchUi()
        {
            _templateData = null;
            ClearMatchResultUi();
            if (TemplateStatusText != null)
                TemplateStatusText.Text = "Template: 미등록";
            UpdateTemplatePreview(null);
        }

        /// <summary>새 이미지 로드 시 Score/판정만 초기화. Template은 유지.</summary>
        private void ClearMatchResultUi()
        {
            _lastMatchScore = null;
            if (MatchScoreText != null) MatchScoreText.Text = "Score: -";
            if (MatchPositionText != null) MatchPositionText.Text = "Position: -";
            if (JudgeResultText != null)
            {
                JudgeResultText.Text = "판정: -";
                JudgeResultText.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        private void RefreshTemplateStatusAfterImageOpen()
        {
            if (_templateData == null)
            {
                TemplateStatusText.Text = "Template: 미등록";
                UpdateTemplatePreview(null);
                return;
            }

            string goldenName = Path.GetFileName(_templateData.SourceFilePath) ?? "(unknown)";
            string currentName = Path.GetFileName(_currentFilePath) ?? "(unknown)";
            bool same = string.Equals(_templateData.SourceFilePath, _currentFilePath, StringComparison.OrdinalIgnoreCase);

            TemplateStatusText.Text = same
                ? $"Template: {_templateData.Width}×{_templateData.Height} 유지\n골든=현재 ({goldenName})\n→ 검사 이미지를 새로 Open"
                : $"Template: {_templateData.Width}×{_templateData.Height} 유지\n골든: {goldenName}\n검사: {currentName}";
            UpdateTemplatePreview(_templateData);
            if (!same)
                StatusText.Text = $"검사 이미지 로드 — Template({goldenName})로 Matching 가능";
        }

        private void RunMorphology(MorphologyOperation op)
        {
            if (!TryBeginRoiProcessing(out PixelBuffer buffer))
                return;
            var p = BuildParams();
            ApplyProcessingResult(
                _processing.RunMorphology(op, buffer, p),
                $"{op}(k={p.KernelSize})");
        }

        private void RunFilter(FilterOperation op)
        {
            if (!TryBeginRoiProcessing(out PixelBuffer buffer))
                return;
            var p = BuildParams();
            string label = op == FilterOperation.Gaussian
                ? $"Gaussian(k={p.KernelSize},σ={p.GaussianSigma:0.##})"
                : op.ToString();
            ApplyProcessingResult(_processing.RunFilter(op, buffer, p), label);
        }

        private bool TryBeginRoiProcessing(out PixelBuffer buffer)
        {
            buffer = null!;
            if (_currentRoi == null || !_currentRoi.IsValid())
            {
                MessageBox.Show(
                    "먼저 원본 Viewer에서 ROI를 선택하세요.\n연산은 선택된 ROI 영역에만 적용됩니다.",
                    "ROI 필요", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            return TryGetWorkBuffer(out buffer);
        }

        private void ApplyProcessingResult(ProcessingResult result, string? stepLabel = null)
        {
            ProcessingTimeText.Text = $"Processing Time: {result.ElapsedMs} ms ({result.OperationName})";
            if (!result.Success || result.OutputPixels == null)
            {
                StatusText.Text = $"{result.OperationName} 실패 ({result.ElapsedMs} ms): {result.Message}";
                AppendAnalysisLog($"{result.OperationName} 실패 ({result.ElapsedMs}ms): {result.Message}");
                return;
            }

            // Undo용 스냅샷 (최대 MaxUndoDepth)
            if (_workBuffer != null)
            {
                _undoStack.Push((_workBuffer.Clone(), stepLabel ?? result.OperationName));
                if (_undoStack.Count > MaxUndoDepth)
                {
                    var keep = _undoStack.Take(MaxUndoDepth).Reverse().ToList();
                    _undoStack.Clear();
                    foreach (var item in keep)
                        _undoStack.Push(item);
                }
            }

            var outBuffer = new PixelBuffer(result.OutputPixels, result.Width, result.Height);
            _workBuffer = outBuffer;

            BitmapSource display;
            if (_originalBgra != null && _currentRoi != null &&
                outBuffer.Width == _displayWidth && outBuffer.Height == _displayHeight)
            {
                byte[] composed = ComposeRoiResultBgra(_originalBgra, outBuffer, _currentRoi);
                display = BmpDisplayLoader.CreateBgraBitmap(composed, _displayWidth, _displayHeight);
            }
            else
            {
                using Bitmap bmp = outBuffer.ToBitmap();
                display = Utils.ImageConverter.BitmapToBitmapImage(bmp);
            }

            ResultViewer.SetImage(display, preserveView: true);
            if (_currentRoi != null)
            {
                SourceViewer.ShowRoi(_currentRoi);
                ResultViewer.ShowRoi(_currentRoi);
            }

            string step = stepLabel ?? result.OperationName;
            _pipelineSteps.Add($"{_pipelineSteps.Count + 1}. {step} ({result.ElapsedMs}ms)");
            RefreshPipelineUi();

            StatusText.Text = $"{step} 완료 — {result.ElapsedMs} ms";
            AppendAnalysisLog($"{step} 완료 ({result.ElapsedMs}ms) — {result.Message}");
        }

        private void UndoProcessing_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count == 0 || _sourceBuffer == null)
            {
                MessageBox.Show("취소할 처리 단계가 없습니다.", "Undo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var (prev, _) = _undoStack.Pop();
            _workBuffer = prev;

            if (_pipelineSteps.Count > 0)
                _pipelineSteps.RemoveAt(_pipelineSteps.Count - 1);
            RefreshPipelineUi();

            BitmapSource display;
            if (_pipelineSteps.Count == 0 && _originalDisplay != null)
            {
                display = _originalDisplay;
            }
            else if (_originalBgra != null && _currentRoi != null &&
                     prev.Width == _displayWidth && prev.Height == _displayHeight)
            {
                byte[] composed = ComposeRoiResultBgra(_originalBgra, prev, _currentRoi);
                display = BmpDisplayLoader.CreateBgraBitmap(composed, _displayWidth, _displayHeight);
            }
            else
            {
                using Bitmap bmp = prev.ToBitmap();
                display = Utils.ImageConverter.BitmapToBitmapImage(bmp);
            }

            ResultViewer.SetImage(display, preserveView: true);
            if (_currentRoi != null)
            {
                SourceViewer.ShowRoi(_currentRoi);
                ResultViewer.ShowRoi(_currentRoi);
            }

            StatusText.Text = "Undo — 이전 처리 단계로 복구";
            AppendAnalysisLog("Undo — 한 단계 취소");
            ProcessingTimeText.Text = "Processing Time: 0 ms (Undo)";
        }

        private void RefreshPipelineUi()
        {
            if (PipelineList != null)
            {
                PipelineList.Items.Clear();
                foreach (var s in _pipelineSteps)
                    PipelineList.Items.Add(s);
                if (PipelineList.Items.Count > 0)
                    PipelineList.ScrollIntoView(PipelineList.Items[^1]!);
            }

            if (PipelineHeaderText != null)
            {
                if (_pipelineSteps.Count == 0)
                {
                    PipelineHeaderText.Text = "파이프라인: (없음)";
                }
                else
                {
                    var names = _pipelineSteps.Select(s =>
                    {
                        int a = s.IndexOf(". ", StringComparison.Ordinal);
                        int b = s.LastIndexOf(" (", StringComparison.Ordinal);
                        if (a >= 0 && b > a + 2)
                            return s[(a + 2)..b];
                        return s;
                    });
                    PipelineHeaderText.Text = "파이프라인: " + string.Join(" → ", names);
                }
            }
        }

        private void ClearPipeline()
        {
            _pipelineSteps.Clear();
            _undoStack.Clear();
            RefreshPipelineUi();
        }

        private void ResetProcessing_Click(object sender, RoutedEventArgs e)
        {
            if (_sourceBuffer == null || _originalDisplay == null)
            {
                MessageBox.Show("복구할 원본 이미지가 없습니다.", "Reset", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _workBuffer = _sourceBuffer.Clone();
            ClearPipeline();
            ResultViewer.SetImage(_originalDisplay, preserveView: true);
            if (_currentRoi != null)
            {
                SourceViewer.ShowRoi(_currentRoi);
                ResultViewer.ShowRoi(_currentRoi);
            }

            ProcessingTimeText.Text = "Processing Time: 0 ms (Reset)";
            StatusText.Text = "연산 결과 Reset — Viewer 2를 원본으로 복구했습니다.";
            AppendAnalysisLog("연산 Reset — Viewer 2 / 작업 버퍼를 원본으로 복구");
        }

        private void ClearAnalysisLog_Click(object sender, RoutedEventArgs e) => ClearAnalysisLog();

        private void ClearAnalysisLog()
        {
            if (AnalysisLogText == null)
                return;
            AnalysisLogText.Text = "";
            _lastLogMessage = null;
        }

        private void AppendAnalysisLog(string message)
        {
            if (AnalysisLogText == null)
                return;

            // 연속 동일 메시지 중복 방지
            if (string.Equals(_lastLogMessage, message, StringComparison.Ordinal))
                return;
            _lastLogMessage = message;

            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            if (string.IsNullOrWhiteSpace(AnalysisLogText.Text))
                AnalysisLogText.Text = line;
            else
                AnalysisLogText.AppendText(Environment.NewLine + line);

            AnalysisLogText.CaretIndex = AnalysisLogText.Text.Length;
            AnalysisLogText.ScrollToEnd();
        }

        /// <summary>원본 BGRA 위에 ROI 영역만 처리된 Gray 결과를 덮어쓴다.</summary>
        private static byte[] ComposeRoiResultBgra(byte[] originalBgra, PixelBuffer processedGray, RoiData roi)
        {
            var output = (byte[])originalBgra.Clone();
            int w = processedGray.Width;
            int h = processedGray.Height;
            int x0 = Math.Clamp(roi.StartX, 0, w);
            int y0 = Math.Clamp(roi.StartY, 0, h);
            int x1 = Math.Clamp(roi.StartX + roi.Width, 0, w);
            int y1 = Math.Clamp(roi.StartY + roi.Height, 0, h);

            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    byte g = processedGray.Data[y * w + x];
                    int i = (y * w + x) * 4;
                    output[i] = g;
                    output[i + 1] = g;
                    output[i + 2] = g;
                    output[i + 3] = 255;
                }
            }
            return output;
        }

        private static byte[] BgraFromGrayBuffer(PixelBuffer gray)
        {
            var bgra = new byte[checked(gray.Width * gray.Height * 4)];
            for (int i = 0, p = 0; i < gray.Data.Length; i++, p += 4)
            {
                byte g = gray.Data[i];
                bgra[p] = g;
                bgra[p + 1] = g;
                bgra[p + 2] = g;
                bgra[p + 3] = 255;
            }
            return bgra;
        }

        private static byte[] CopyBitmapSourceToBgra(BitmapSource source)
        {
            var converted = new FormatConvertedBitmap(source, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            int w = converted.PixelWidth;
            int h = converted.PixelHeight;
            int stride = w * 4;
            var bgra = new byte[checked(h * stride)];
            converted.CopyPixels(bgra, stride, 0);
            return bgra;
        }

        private static int ComputeOtsuThreshold(PixelBuffer buffer, RoiData? roi)
        {
            var hist = new int[256];
            int x0 = 0, y0 = 0, x1 = buffer.Width, y1 = buffer.Height;
            if (roi != null && roi.IsValid())
            {
                x0 = Math.Clamp(roi.StartX, 0, buffer.Width);
                y0 = Math.Clamp(roi.StartY, 0, buffer.Height);
                x1 = Math.Clamp(roi.StartX + roi.Width, 0, buffer.Width);
                y1 = Math.Clamp(roi.StartY + roi.Height, 0, buffer.Height);
            }

            long total = 0;
            for (int y = y0; y < y1; y++)
            {
                int row = y * buffer.Width;
                for (int x = x0; x < x1; x++)
                {
                    hist[buffer.Data[row + x]]++;
                    total++;
                }
            }
            if (total <= 0) return 128;

            double sumAll = 0;
            for (int i = 0; i < 256; i++)
                sumAll += i * hist[i];

            double sumB = 0;
            long wB = 0;
            double maxVar = -1;
            int best = 128;
            for (int t = 0; t < 256; t++)
            {
                wB += hist[t];
                if (wB == 0) continue;
                long wF = total - wB;
                if (wF == 0) break;
                sumB += t * hist[t];
                double mB = sumB / wB;
                double mF = (sumAll - sumB) / wF;
                double between = wB * (double)wF * (mB - mF) * (mB - mF);
                if (between > maxVar)
                {
                    maxVar = between;
                    best = t;
                }
            }
            return best;
        }

        private ProcessingParams BuildParams()
        {
            int kernel = 3;
            if (KernelSizeCombo?.SelectedItem is System.Windows.Controls.ComboBoxItem kItem &&
                int.TryParse(kItem.Tag?.ToString(), out int kParsed))
                kernel = kParsed;

            int thresh = 128;
            if (!int.TryParse(ThresholdValueBox?.Text, out thresh))
                thresh = 128;
            thresh = Math.Clamp(thresh, 0, 255);

            double sigma = 1.0;
            if (!double.TryParse(GaussianSigmaBox?.Text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out sigma) &&
                !double.TryParse(GaussianSigmaBox?.Text, out sigma))
                sigma = 1.0;
            if (sigma <= 0) sigma = 1.0;

            return new ProcessingParams
            {
                KernelSize = kernel,
                ThresholdValue = thresh,
                GaussianSigma = sigma,
                Roi = _currentRoi
            };
        }

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
