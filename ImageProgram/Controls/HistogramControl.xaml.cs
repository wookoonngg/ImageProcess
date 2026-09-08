using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WpfImageProcessing.Controls
{
    /// <summary>
    /// 표준 가로 히스토그램: X=Gray(0~255), Y=빈도(막대 높이).
    /// </summary>
    public partial class HistogramControl : UserControl
    {
        private int[]? _bins;

        public HistogramControl()
        {
            InitializeComponent();
            SizeChanged += (_, _) => Redraw();
            HistogramCanvas.SizeChanged += (_, _) => Redraw();
        }

        public void Clear()
        {
            _bins = null;
            HistogramCanvas.Children.Clear();
            PeakLabel.Text = "";
        }

        public void SetHistogram(int[] bins)
        {
            _bins = bins;
            Redraw();
        }

        private void Redraw()
        {
            HistogramCanvas.Children.Clear();
            if (_bins == null || _bins.Length == 0 ||
                HistogramCanvas.ActualWidth <= 1 || HistogramCanvas.ActualHeight <= 1)
                return;

            int max = 1;
            int peakIndex = 0;
            for (int i = 0; i < _bins.Length; i++)
            {
                if (_bins[i] > max)
                {
                    max = _bins[i];
                    peakIndex = i;
                }
            }

            PeakLabel.Text = $"peak Gray={peakIndex}, count={max:N0}";

            double canvasW = HistogramCanvas.ActualWidth;
            double canvasH = HistogramCanvas.ActualHeight;
            double barW = canvasW / _bins.Length;
            var fill = new SolidColorBrush(Color.FromRgb(0x88, 0xC0, 0xD0));

            // 가로축을 따라 막대 (왼쪽=0 … 오른쪽=255)
            for (int i = 0; i < _bins.Length; i++)
            {
                double barH = _bins[i] / (double)max * (canvasH - 8);
                var rect = new Rectangle
                {
                    Width = Math.Max(barW, 1),
                    Height = Math.Max(barH, 0.5),
                    Fill = fill,
                    SnapsToDevicePixels = true
                };
                Canvas.SetLeft(rect, i * barW);
                Canvas.SetBottom(rect, 4);
                HistogramCanvas.Children.Add(rect);
            }

            var baseline = new Line
            {
                X1 = 0,
                X2 = canvasW,
                Y1 = canvasH - 2,
                Y2 = canvasH - 2,
                Stroke = new SolidColorBrush(Color.FromRgb(0x4C, 0x56, 0x6A)),
                StrokeThickness = 1
            };
            HistogramCanvas.Children.Add(baseline);

            // peak 세로 가이드
            double peakX = (peakIndex + 0.5) * barW;
            var peakLine = new Line
            {
                X1 = peakX,
                X2 = peakX,
                Y1 = 2,
                Y2 = canvasH - 2,
                Stroke = new SolidColorBrush(Color.FromRgb(0xEB, 0xCB, 0x8B)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 }
            };
            HistogramCanvas.Children.Add(peakLine);
        }
    }
}
