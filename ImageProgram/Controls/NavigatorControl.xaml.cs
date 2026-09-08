using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace WpfImageProcessing.Controls
{
    public partial class NavigatorControl : UserControl
    {
        private double _previewScale = 1.0;
        private double _imageWidth;
        private double _imageHeight;
        private double _previewOffsetX;
        private double _previewOffsetY;
        private ViewportChangedEventArgs? _lastViewport;

        /// <summary>미리보기 이미지 영역(여백 제외)을 클릭하면 해당 위치로 이동 요청.</summary>
        public event EventHandler<NavigatorNavigateEventArgs>? NavigateRequested;

        public NavigatorControl()
        {
            InitializeComponent();
            PreviewCanvas.MouseLeftButtonDown += PreviewCanvas_MouseLeftButtonDown;
        }

        public void SetPreviewImage(BitmapSource? image)
        {
            PreviewImage.Source = image;
            _imageWidth = image?.PixelWidth ?? 0;
            _imageHeight = image?.PixelHeight ?? 0;
            UpdatePreviewLayout();
            ViewportRect.Visibility = Visibility.Collapsed;
        }

        public void UpdateViewport(ViewportChangedEventArgs viewport)
        {
            _lastViewport = viewport;
            if (_imageWidth <= 0 || _imageHeight <= 0 || PreviewCanvas.ActualWidth <= 0)
            {
                ViewportRect.Visibility = Visibility.Collapsed;
                return;
            }

            UpdatePreviewLayout();

            // 이미지 좌표계 → 프리뷰 좌표 (letterbox 오프셋 포함)
            double x = _previewOffsetX + viewport.ScrollOffsetX / viewport.Scale * _previewScale;
            double y = _previewOffsetY + viewport.ScrollOffsetY / viewport.Scale * _previewScale;
            double w = viewport.ViewportWidth / viewport.Scale * _previewScale;
            double h = viewport.ViewportHeight / viewport.Scale * _previewScale;

            // 여백(letterbox) 밖으로 나가지 않도록 이미지 표시 영역으로 clamp
            double imgLeft = _previewOffsetX;
            double imgTop = _previewOffsetY;
            double imgRight = _previewOffsetX + _imageWidth * _previewScale;
            double imgBottom = _previewOffsetY + _imageHeight * _previewScale;

            double left = Math.Max(x, imgLeft);
            double top = Math.Max(y, imgTop);
            double right = Math.Min(x + w, imgRight);
            double bottom = Math.Min(y + h, imgBottom);

            if (right <= left || bottom <= top)
            {
                ViewportRect.Visibility = Visibility.Collapsed;
                return;
            }

            Canvas.SetLeft(ViewportRect, left);
            Canvas.SetTop(ViewportRect, top);
            ViewportRect.Width = Math.Max(right - left, 2);
            ViewportRect.Height = Math.Max(bottom - top, 2);
            ViewportRect.Visibility = Visibility.Visible;
        }

        private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_imageWidth <= 0 || _imageHeight <= 0 || _previewScale <= 0)
                return;

            Point p = e.GetPosition(PreviewCanvas);
            double imgLeft = _previewOffsetX;
            double imgTop = _previewOffsetY;
            double imgRight = _previewOffsetX + _imageWidth * _previewScale;
            double imgBottom = _previewOffsetY + _imageHeight * _previewScale;

            // 여백 클릭 → 무시 (이미지 영역만 처리)
            if (p.X < imgLeft || p.X > imgRight || p.Y < imgTop || p.Y > imgBottom)
            {
                e.Handled = true;
                return;
            }

            double imageX = (p.X - _previewOffsetX) / _previewScale;
            double imageY = (p.Y - _previewOffsetY) / _previewScale;
            imageX = Math.Clamp(imageX, 0, _imageWidth - 1);
            imageY = Math.Clamp(imageY, 0, _imageHeight - 1);

            double scale = _lastViewport?.Scale ?? 1.0;
            double viewW = _lastViewport?.ViewportWidth ?? PreviewCanvas.ActualWidth;
            double viewH = _lastViewport?.ViewportHeight ?? PreviewCanvas.ActualHeight;

            NavigateRequested?.Invoke(this, new NavigatorNavigateEventArgs
            {
                ImageX = imageX,
                ImageY = imageY,
                Scale = scale,
                ViewportWidth = viewW,
                ViewportHeight = viewH
            });
            e.Handled = true;
        }

        private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePreviewLayout();
            if (_lastViewport != null)
                UpdateViewport(_lastViewport);
        }

        private void UpdatePreviewLayout()
        {
            if (_imageWidth <= 0 || _imageHeight <= 0 || PreviewCanvas.ActualWidth <= 0 || PreviewCanvas.ActualHeight <= 0)
                return;

            double scaleX = PreviewCanvas.ActualWidth / _imageWidth;
            double scaleY = PreviewCanvas.ActualHeight / _imageHeight;
            _previewScale = Math.Min(scaleX, scaleY);

            double displayW = _imageWidth * _previewScale;
            double displayH = _imageHeight * _previewScale;

            PreviewImage.Width = displayW;
            PreviewImage.Height = displayH;
            _previewOffsetX = (PreviewCanvas.ActualWidth - displayW) / 2.0;
            _previewOffsetY = (PreviewCanvas.ActualHeight - displayH) / 2.0;
            Canvas.SetLeft(PreviewImage, _previewOffsetX);
            Canvas.SetTop(PreviewImage, _previewOffsetY);
        }
    }

    public sealed class NavigatorNavigateEventArgs : EventArgs
    {
        public double ImageX { get; init; }
        public double ImageY { get; init; }
        public double Scale { get; init; }
        public double ViewportWidth { get; init; }
        public double ViewportHeight { get; init; }
    }
}
