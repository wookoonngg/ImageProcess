using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WpfImageProcessing.Models;

namespace WpfImageProcessing.Controls
{
    public partial class ImageViewerControl : UserControl
    {
        private Point _panStart;
        private Point _scrollStart;
        private Point _roiStart;
        private bool _isPanning;
        private bool _isSelectingRoi;
        private bool _roiSelectMode;

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

        public event EventHandler<ViewportChangedEventArgs>? ViewportChanged;
        public event EventHandler<RoiData>? RoiSelected;

        public ImageViewerControl()
        {
            InitializeComponent();
        }

        public void SetImage(BitmapImage? image)
        {
            DisplayImage.Source = image;
            ClearRoi();
            ResetView();
        }

        public void ResetView()
        {
            ImageContainer.LayoutTransform = new ScaleTransform(1, 1);
            ScrollHost.ScrollToHorizontalOffset(0);
            ScrollHost.ScrollToVerticalOffset(0);
            UpdateZoomLabel();
            RaiseViewportChanged();
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
                _roiStart = e.GetPosition(DisplayImage);
                RoiRectangle.Visibility = Visibility.Visible;
                UpdateRoiVisual(_roiStart.X, _roiStart.Y, 0, 0);
                ScrollHost.CaptureMouse();
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

                Point end = e.GetPosition(DisplayImage);
                int x = (int)Math.Round(Math.Min(_roiStart.X, end.X));
                int y = (int)Math.Round(Math.Min(_roiStart.Y, end.Y));
                int w = (int)Math.Round(Math.Abs(end.X - _roiStart.X));
                int h = (int)Math.Round(Math.Abs(end.Y - _roiStart.Y));

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
                Point current = e.GetPosition(DisplayImage);
                int x = (int)Math.Round(Math.Min(_roiStart.X, current.X));
                int y = (int)Math.Round(Math.Min(_roiStart.Y, current.Y));
                int w = (int)Math.Round(Math.Abs(current.X - _roiStart.X));
                int h = (int)Math.Round(Math.Abs(current.Y - _roiStart.Y));
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
            Canvas.SetLeft(RoiRectangle, x);
            Canvas.SetTop(RoiRectangle, y);
            RoiRectangle.Width = Math.Max(w, 1);
            RoiRectangle.Height = Math.Max(h, 1);
            RoiRectangle.Visibility = Visibility.Visible;
        }

        private void ScrollHost_ScrollChanged(object sender, ScrollChangedEventArgs e) => RaiseViewportChanged();

        private void ApplyZoom(double factor, Point anchor)
        {
            var transform = ImageContainer.LayoutTransform as ScaleTransform ?? new ScaleTransform(1, 1);
            double newScale = Math.Clamp(transform.ScaleX * factor, 0.1, 20.0);
            double ratio = newScale / transform.ScaleX;

            ScrollHost.UpdateLayout();
            double offsetX = ScrollHost.HorizontalOffset + anchor.X;
            double offsetY = ScrollHost.VerticalOffset + anchor.Y;

            ImageContainer.LayoutTransform = new ScaleTransform(newScale, newScale);
            ScrollHost.UpdateLayout();
            ScrollHost.ScrollToHorizontalOffset(offsetX * ratio - anchor.X);
            ScrollHost.ScrollToVerticalOffset(offsetY * ratio - anchor.Y);

            UpdateZoomLabel();
            RaiseViewportChanged();
        }

        private void UpdateZoomLabel()
        {
            double scale = (ImageContainer.LayoutTransform as ScaleTransform)?.ScaleX ?? 1.0;
            ZoomLabel.Text = $"{scale * 100:F0}%";
        }

        private void RaiseViewportChanged()
        {
            if (DisplayImage.Source == null)
                return;

            double scale = (ImageContainer.LayoutTransform as ScaleTransform)?.ScaleX ?? 1.0;
            ViewportChanged?.Invoke(this, new ViewportChangedEventArgs
            {
                ImageWidth = DisplayImage.Source.Width,
                ImageHeight = DisplayImage.Source.Height,
                ViewportWidth = ScrollHost.ViewportWidth,
                ViewportHeight = ScrollHost.ViewportHeight,
                ScrollOffsetX = ScrollHost.HorizontalOffset,
                ScrollOffsetY = ScrollHost.VerticalOffset,
                Scale = scale
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
