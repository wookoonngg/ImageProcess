using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WpfImageProcessing.Controls;
using WpfImageProcessing.Models;
using WpfImageProcessing.Services;
using WpfImageProcessing.Utils;

namespace WpfImageProcessing
{
    public partial class MainWindow : Window
    {
        private readonly IImageFileService _imageFileService;
        private ImageData? _sourceImage;
        private ImageData? _resultImage;
        private string? _currentFilePath;
        private BitmapFileInfo? _headerInfo;
        private bool _isPreviewMode;
        private RoiData? _currentRoi;
        private Bitmap? _templateImage;

        public MainWindow()
        {
            InitializeComponent();

            _imageFileService = new ImageFileService(new ConsoleLogger());

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

        private void OpenImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = Constants.BMP_FILTER,
                Title = "BMP 이미지 열기"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                LoadImageFromPath(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "열기 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = $"오류: {ex.Message}";
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void LoadImageFromPath(string filePath)
        {
            _sourceImage?.Dispose();
            _resultImage?.Dispose();
            _templateImage?.Dispose();
            _templateImage = null;
            _currentRoi = null;

            _headerInfo = _imageFileService.ReadHeader(filePath);
            _currentFilePath = filePath;
            _isPreviewMode = NeedsPreviewMode(_headerInfo);

            BitmapImage displayImage;
            if (_isPreviewMode)
            {
                displayImage = Utils.ImageConverter.LoadPreviewFromFile(filePath);
                StatusText.Text = $"Preview 모드 ({displayImage.PixelWidth}×{displayImage.PixelHeight} 표시) — 원본: {_headerInfo.Width}×{_headerInfo.Height}";
            }
            else
            {
                Bitmap bitmap = _imageFileService.LoadImage(filePath);
                _sourceImage = new ImageData { SourceFilePath = filePath, PixelData = bitmap };
                _resultImage = new ImageData { SourceFilePath = filePath, PixelData = (Bitmap)bitmap.Clone() };
                displayImage = Utils.ImageConverter.BitmapToBitmapImage(bitmap);
                StatusText.Text = Constants.SUCCESS_FILE_OPENED;
            }

            SourceViewer.SetImage(displayImage);
            ResultViewer.SetImage(displayImage);
            Navigator.SetPreviewImage(displayImage);
            Histogram.Clear();
            RoiInfoText.Text = "ROI: -";
            MatchScoreText.Text = "Score: -";
            MatchPositionText.Text = "Position: -";

            ImageInfoText.Text =
                $"{_headerInfo.Width}×{_headerInfo.Height} | {_headerInfo.BitDepth}bit | " +
                $"{FormatBytes(_headerInfo.FileSize)} | {Path.GetFileName(filePath)}";
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
            if (_currentFilePath == null) return;
            var preview = Utils.ImageConverter.LoadPreviewFromFile(_currentFilePath);
            using var bmp = Utils.ImageConverter.BitmapImageToBitmap(preview);
            _imageFileService.SaveImage(bmp, filePath);
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

            RunWithTimer("Template 등록", () =>
            {
                _templateImage?.Dispose();
                _templateImage = null;
                StatusText.Text = $"Template 등록됨: {_currentRoi.Width}×{_currentRoi.Height} (C++ 연동 후 실제 추출)";
            });
        }

        private void UpdateHistogramPlaceholder(RoiData roi)
        {
            // WEEK 2: C++ Histogram 연동 전 — ROI 크기 기반 데모 히스토그램
            var bins = new int[256];
            int peak = Math.Clamp(roi.Width + roi.Height, 1, 255);
            for (int i = 0; i < 256; i++)
                bins[i] = Math.Max(0, peak - Math.Abs(i - peak / 2));
            Histogram.SetHistogram(bins);
        }

        #endregion

        #region Processing (스텁 — C++ DLL 연동 예정)

        private void Process_Dilation(object sender, RoutedEventArgs e) =>
            RunProcessing("Dilation (팽창)");
        private void Process_Erosion(object sender, RoutedEventArgs e) =>
            RunProcessing("Erosion (수축)");
        private void Process_Smoothing(object sender, RoutedEventArgs e) =>
            RunProcessing("Smoothing (평활화)");
        private void Process_Threshold(object sender, RoutedEventArgs e) =>
            RunProcessing("Threshold (이진화)");
        private void Process_Gaussian(object sender, RoutedEventArgs e) =>
            RunProcessing("Gaussian Filter");
        private void Process_Laplacian(object sender, RoutedEventArgs e) =>
            RunProcessing("Laplacian Filter");
        private void Process_Sobel(object sender, RoutedEventArgs e) =>
            RunProcessing("Sobel Filter");

        private void Match_DIFF(object sender, RoutedEventArgs e) =>
            RunMatching("DIFF");
        private void Match_CORR(object sender, RoutedEventArgs e) =>
            RunMatching("CORR");
        private void Match_COEFF(object sender, RoutedEventArgs e) =>
            RunMatching("COEFF");

        private void RunProcessing(string name)
        {
            if (_headerInfo == null)
            {
                MessageBox.Show("먼저 BMP 파일을 열어주세요.", name, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RunWithTimer(name, () =>
            {
                // WEEK 2~3: C++ DLL 호출 후 ResultViewer 갱신
                StatusText.Text = $"{name} — C++ 영상처리 DLL 연동 예정";
            });
        }

        private void RunMatching(string method)
        {
            if (_templateImage == null && _currentRoi == null)
            {
                MessageBox.Show("Template 등록 또는 ROI 선택이 필요합니다.", method,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RunWithTimer($"Template Matching ({method})", () =>
            {
                MatchScoreText.Text = "Score: (C++ 연동 예정)";
                MatchPositionText.Text = "Position: (C++ 연동 예정)";
                StatusText.Text = $"Template Matching {method} — C++ DLL 연동 예정";
            });
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
            _templateImage?.Dispose();
            base.OnClosed(e);
        }

        #endregion
    }
}
