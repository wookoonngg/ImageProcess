using System.Windows;
using System.Windows.Controls;
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

        public NavigatorControl()
        {
            InitializeComponent();
        }

        public void SetPreviewImage(BitmapSource? image)
        {
            PreviewImage.Source = image;
            _imageWidth = image?.PixelWidth ?? 0;
            _imageHeight = image?.PixelHeight ?? 0;
            UpdatePreviewLayout();
        }

        public void UpdateViewport(ViewportChangedEventArgs viewport)
        {
            if (_imageWidth <= 0 || _imageHeight <= 0 || PreviewCanvas.ActualWidth <= 0)
            {
                ViewportRect.Visibility = Visibility.Collapsed;
                return;
            }

            UpdatePreviewLayout();

            double x = _previewOffsetX + viewport.ScrollOffsetX / viewport.Scale * _previewScale;
            double y = _previewOffsetY + viewport.ScrollOffsetY / viewport.Scale * _previewScale;
            double w = viewport.ViewportWidth / viewport.Scale * _previewScale;
            double h = viewport.ViewportHeight / viewport.Scale * _previewScale;

            Canvas.SetLeft(ViewportRect, x);
            Canvas.SetTop(ViewportRect, y);
            ViewportRect.Width = Math.Max(w, 2);
            ViewportRect.Height = Math.Max(h, 2);
            ViewportRect.Visibility = Visibility.Visible;
        }

        private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePreviewLayout();
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
            _previewOffsetX = (PreviewCanvas.ActualWidth - displayW) / 2;
            _previewOffsetY = (PreviewCanvas.ActualHeight - displayH) / 2;
            Canvas.SetLeft(PreviewImage, _previewOffsetX);
            Canvas.SetTop(PreviewImage, _previewOffsetY);
        }
    }
}
