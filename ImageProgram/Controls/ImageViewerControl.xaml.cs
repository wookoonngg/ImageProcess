using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfImageProcessing.Models;

namespace WpfImageProcessing.Controls
{
    public partial class ImageViewerControl : UserControl
    {
        private Point _panStart;
        private Point _scrollStart;
        private Point _roiStartPx;
        private bool _isPanning;
        private bool _isSelectingRoi;
        private bool _roiSelectMode;
        private bool _suppressViewportEvent;

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ImageViewerControl),
                new PropertyMetadata("Viewer"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public bool RoiSelectMode
        {
            get => _roiSelectMode;
            set
            {
                _roiSelectMode = value;
                Cursor = value ? Cursors.Cross : Cursors.Arrow;
            }
        }

        public RoiData? CurrentRoi { get; private set; }

        public double CurrentScale =>
            (ImageContainer.LayoutTransform as ScaleTransform)?.ScaleX ?? 1.0;

        public event EventHandler<ViewportChangedEventArgs>? ViewportChanged;
        public event EventHandler<RoiData>? RoiSelected;

        public ImageViewerControl()
        {
            InitializeComponent();
            DisplayImage.SizeChanged += (_, _) => SyncRoiCanvasSize();
        }

        public void SetImage(BitmapSource? image, bool preserveView = false)
        {
            double scale = CurrentScale;
            double ox = ScrollHost.HorizontalOffset;
            double oy = ScrollHost.VerticalOffset;
            var roi = CurrentRoi;

            DisplayImage.Source = image;
            SyncRoiCanvasSize();

            if (!preserveView || image == null)
            {
                ClearRoi();
                ResetView();
                return;
            }

            ApplyViewState(scale, ox, oy, raiseEvent: false);
            if (roi != null && roi.IsValid())
                ShowRoi(roi);
            else
                ClearRoi();
        }

        public void ResetView()
        {
            ApplyViewState(1.0, 0, 0, raiseEvent: true);
        }

        public void ClearRoi()
        {
            CurrentRoi = null;
            RoiRectangle.Visibility = Visibility.Collapsed;
        }

        public void ShowRoi(RoiData roi)
        {
            CurrentRoi = roi;
            UpdateRoiVisual(roi.StartX, roi.StartY, roi.Width, roi.Height);
        }

        /// <summary>다른 Viewer / Navigator와 줌·스크롤 상태를 맞춘다.</summary>
        public void SyncViewport(ViewportChangedEventArgs viewport)
        {
            if (DisplayImage.Source == null)
                return;

            ApplyViewState(viewport.Scale, viewport.ScrollOffsetX, viewport.ScrollOffsetY, raiseEvent: false);
        }

        /// <summary>이미지 픽셀 좌표를 Viewer 중앙에 오도록 스크롤 이동.</summary>
        public void NavigateToImagePoint(double imageX, double imageY, bool raiseEvent = true)
        {
            if (DisplayImage.Source == null)
                return;

            ScrollHost.UpdateLayout();
            double scale = CurrentScale;
            double viewW = Math.Max(1, ScrollHost.ViewportWidth);
            double viewH = Math.Max(1, ScrollHost.ViewportHeight);
            double targetX = imageX * scale - viewW / 2.0;
            double targetY = imageY * scale - viewH / 2.0;
            ApplyViewState(scale, Math.Max(0, targetX), Math.Max(0, targetY), raiseEvent);
        }

        public ViewportChangedEventArgs GetViewportState()
        {
            double imageWidth = DisplayImage.Source is BitmapSource bs ? bs.PixelWidth : 0;
            double imageHeight = DisplayImage.Source is BitmapSource bs2 ? bs2.PixelHeight : 0;
            return new ViewportChangedEventArgs
            {
                ImageWidth = imageWidth,
                ImageHeight = imageHeight,
                ViewportWidth = ScrollHost.ViewportWidth,
                ViewportHeight = ScrollHost.ViewportHeight,
                ScrollOffsetX = ScrollHost.HorizontalOffset,
                ScrollOffsetY = ScrollHost.VerticalOffset,
                Scale = CurrentScale
            };
        }

        private void ApplyViewState(double scale, double offsetX, double offsetY, bool raiseEvent)
        {
            _suppressViewportEvent = true;
            try
            {
                scale = Math.Clamp(scale, 0.1, 20.0);
                ImageContainer.LayoutTransform = new ScaleTransform(scale, scale);
                ScrollHost.UpdateLayout();
                ScrollHost.ScrollToHorizontalOffset(offsetX);
                ScrollHost.ScrollToVerticalOffset(offsetY);
                UpdateZoomLabel();
            }
            finally
            {
                _suppressViewportEvent = false;
            }

            if (raiseEvent)
                RaiseViewportChanged();
        }

        private void SyncRoiCanvasSize()
        {
            if (DisplayImage.Source is BitmapSource bmp)
            {
                RoiCanvas.Width = bmp.PixelWidth;
                RoiCanvas.Height = bmp.PixelHeight;
            }
            else
            {
                RoiCanvas.Width = DisplayImage.ActualWidth;
                RoiCanvas.Height = DisplayImage.ActualHeight;
            }
        }

        /// <summary>
        /// 마우스 → 이미지 픽셀 좌표.
        /// LayoutTransform 줌 + ScrollViewer 오프셋을 직접 반영해 커서와 ROI가 일치하도록 한다.
        /// </summary>
        private Point GetImagePixelPoint(MouseEventArgs e)
        {
            double scale = Math.Max(CurrentScale, 1e-6);
            Point p = e.GetPosition(ScrollHost);
            double x = (ScrollHost.HorizontalOffset + p.X) / scale;
            double y = (ScrollHost.VerticalOffset + p.Y) / scale;
            return new Point(x, y);
        }

        private void ClampToImage(ref int x, ref int y, ref int w, ref int h)
        {
            if (DisplayImage.Source is not BitmapSource bmp)
                return;

            int imgW = bmp.PixelWidth;
            int imgH = bmp.PixelHeight;
            int x2 = Math.Clamp(x + w, 0, imgW);
            int y2 = Math.Clamp(y + h, 0, imgH);
            x = Math.Clamp(x, 0, Math.Max(0, imgW - 1));
            y = Math.Clamp(y, 0, Math.Max(0, imgH - 1));
            w = Math.Max(0, x2 - x);
            h = Math.Max(0, y2 - y);
        }

        private void ScrollHost_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DisplayImage.Source == null)
                return;

            double scale = e.Delta > 0 ? 1.15 : 1 / 1.15;
            ApplyZoom(scale, e.GetPosition(ScrollHost));
            e.Handled = true;
        }

        private void ScrollHost_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DisplayImage.Source == null)
                return;

            if (RoiSelectMode)
            {
                _isSelectingRoi = true;
                _roiStartPx = GetImagePixelPoint(e);
                RoiRectangle.Visibility = Visibility.Visible;
                UpdateRoiVisual(_roiStartPx.X, _roiStartPx.Y, 0, 0);
                ScrollHost.CaptureMouse();
                e.Handled = true;
                return;
            }

            _isPanning = true;
            _panStart = e.GetPosition(ScrollHost);
            _scrollStart = new Point(ScrollHost.HorizontalOffset, ScrollHost.VerticalOffset);
            ScrollHost.CaptureMouse();
        }

        private void ScrollHost_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSelectingRoi)
            {
                _isSelectingRoi = false;
                ScrollHost.ReleaseMouseCapture();

                Point end = GetImagePixelPoint(e);
                int x = (int)Math.Floor(Math.Min(_roiStartPx.X, end.X));
                int y = (int)Math.Floor(Math.Min(_roiStartPx.Y, end.Y));
                int w = (int)Math.Ceiling(Math.Abs(end.X - _roiStartPx.X));
                int h = (int)Math.Ceiling(Math.Abs(end.Y - _roiStartPx.Y));
                ClampToImage(ref x, ref y, ref w, ref h);

                if (w > 2 && h > 2)
                {
                    CurrentRoi = new RoiData { StartX = x, StartY = y, Width = w, Height = h };
                    UpdateRoiVisual(x, y, w, h);
                    RoiSelected?.Invoke(this, CurrentRoi);
                }
                else
                {
                    ClearRoi();
                }

                e.Handled = true;
                return;
            }

            if (!_isPanning)
                return;

            _isPanning = false;
            ScrollHost.ReleaseMouseCapture();
        }

        private void ScrollHost_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isSelectingRoi)
            {
                Point current = GetImagePixelPoint(e);
                double x = Math.Min(_roiStartPx.X, current.X);
                double y = Math.Min(_roiStartPx.Y, current.Y);
                double w = Math.Abs(current.X - _roiStartPx.X);
                double h = Math.Abs(current.Y - _roiStartPx.Y);
                UpdateRoiVisual(x, y, w, h);
                return;
            }

            if (!_isPanning)
                return;

            Point currentPan = e.GetPosition(ScrollHost);
            ScrollHost.ScrollToHorizontalOffset(_scrollStart.X + (_panStart.X - currentPan.X));
            ScrollHost.ScrollToVerticalOffset(_scrollStart.Y + (_panStart.Y - currentPan.Y));
        }

        private void UpdateRoiVisual(double x, double y, double w, double h)
        {
            SyncRoiCanvasSize();
            Canvas.SetLeft(RoiRectangle, x);
            Canvas.SetTop(RoiRectangle, y);
            RoiRectangle.Width = Math.Max(w, 1);
            RoiRectangle.Height = Math.Max(h, 1);
            RoiRectangle.Visibility = Visibility.Visible;
        }

        private void ScrollHost_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_suppressViewportEvent)
                return;
            if (e.HorizontalChange == 0 && e.VerticalChange == 0 && e.ExtentWidthChange == 0 && e.ExtentHeightChange == 0)
                return;
            RaiseViewportChanged();
        }

        private void ApplyZoom(double factor, Point anchorInScrollHost)
        {
            double oldScale = CurrentScale;
            double newScale = Math.Clamp(oldScale * factor, 0.1, 20.0);
            double ratio = newScale / oldScale;

            ScrollHost.UpdateLayout();
            double offsetX = (ScrollHost.HorizontalOffset + anchorInScrollHost.X) * ratio - anchorInScrollHost.X;
            double offsetY = (ScrollHost.VerticalOffset + anchorInScrollHost.Y) * ratio - anchorInScrollHost.Y;

            ApplyViewState(newScale, offsetX, offsetY, raiseEvent: true);
        }

        private void UpdateZoomLabel()
        {
            ZoomLabel.Text = $"{CurrentScale * 100:F0}%";
        }

        private void RaiseViewportChanged()
        {
            if (_suppressViewportEvent || DisplayImage.Source == null)
                return;

            double imageWidth = DisplayImage.Source is BitmapSource bs ? bs.PixelWidth : DisplayImage.Source.Width;
            double imageHeight = DisplayImage.Source is BitmapSource bs2 ? bs2.PixelHeight : DisplayImage.Source.Height;

            ViewportChanged?.Invoke(this, new ViewportChangedEventArgs
            {
                ImageWidth = imageWidth,
                ImageHeight = imageHeight,
                ViewportWidth = ScrollHost.ViewportWidth,
                ViewportHeight = ScrollHost.ViewportHeight,
                ScrollOffsetX = ScrollHost.HorizontalOffset,
                ScrollOffsetY = ScrollHost.VerticalOffset,
                Scale = CurrentScale
            });
        }
    }

    public class ViewportChangedEventArgs : EventArgs
    {
        public double ImageWidth { get; init; }
        public double ImageHeight { get; init; }
        public double ViewportWidth { get; init; }
        public double ViewportHeight { get; init; }
        public double ScrollOffsetX { get; init; }
        public double ScrollOffsetY { get; init; }
        public double Scale { get; init; }
    }
}
