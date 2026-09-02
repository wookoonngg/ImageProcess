using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WpfImageProcessing.Controls
{
    public partial class HistogramControl : UserControl
    {
        public HistogramControl()
        {
            InitializeComponent();
            SizeChanged += (_, _) => Redraw();
        }

        public void Clear()
        {
            HistogramCanvas.Children.Clear();
        }

        public void SetHistogram(int[] bins)
        {
            _bins = bins;
            Redraw();
        }

        private int[]? _bins;

        private void Redraw()
        {
            HistogramCanvas.Children.Clear();
            if (_bins == null || _bins.Length == 0 || HistogramCanvas.ActualWidth <= 0 || HistogramCanvas.ActualHeight <= 0)
                return;

            int max = 1;
            foreach (int v in _bins)
                if (v > max) max = v;

            double barW = HistogramCanvas.ActualWidth / _bins.Length;
            double h = HistogramCanvas.ActualHeight;

            for (int i = 0; i < _bins.Length; i++)
            {
                double barH = _bins[i] / (double)max * (h - 4);
                var rect = new Rectangle
                {
                    Width = Math.Max(barW - 0.5, 1),
                    Height = Math.Max(barH, 1),
                    Fill = new SolidColorBrush(Color.FromRgb(0x88, 0xC0, 0xD0))
                };
                Canvas.SetLeft(rect, i * barW);
                Canvas.SetBottom(rect, 2);
                HistogramCanvas.Children.Add(rect);
            }
        }
    }
}
