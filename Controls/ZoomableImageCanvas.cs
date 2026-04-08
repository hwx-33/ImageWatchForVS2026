using ImageWatch.Imaging;
using ImageWatch.Models;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageWatch.Controls
{
    public class ZoomableImageCanvas : FrameworkElement
    {
        // ── Dependency properties ──────────────────────────────────────────────

        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(nameof(ImageSource), typeof(BitmapSource),
                typeof(ZoomableImageCanvas),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender,
                    OnImageSourceChanged));

        public static readonly DependencyProperty RawDataProperty =
            DependencyProperty.Register(nameof(RawData), typeof(byte[]),
                typeof(ZoomableImageCanvas),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MatInfoProperty =
            DependencyProperty.Register(nameof(MatInfo), typeof(MatInfo),
                typeof(ZoomableImageCanvas),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public BitmapSource ImageSource
        {
            get => (BitmapSource)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        public byte[] RawData
        {
            get => (byte[])GetValue(RawDataProperty);
            set => SetValue(RawDataProperty, value);
        }

        public MatInfo MatInfo
        {
            get => (MatInfo)GetValue(MatInfoProperty);
            set => SetValue(MatInfoProperty, value);
        }

        // ── State ─────────────────────────────────────────────────────────────

        private Matrix _transform = Matrix.Identity;
        private Point  _lastPan;
        private bool   _isPanning;

        private static readonly Typeface _typeface = new Typeface("Segoe UI");

        // Pre-built pen for pixel grid (rebuilt only when scale changes significantly)
        private static readonly DashStyle _dashStyle;
        private static readonly Pen _gridPen;

        static ZoomableImageCanvas()
        {
            _dashStyle = new DashStyle(new double[] { 4, 4 }, 0);
            _dashStyle.Freeze();

            var gridBrush = new SolidColorBrush(Color.FromArgb(160, 190, 190, 190));
            gridBrush.Freeze();
            _gridPen = new Pen(gridBrush, 1.0) { DashStyle = _dashStyle };
            _gridPen.Freeze();
        }

        // ── Constructor ───────────────────────────────────────────────────────

        public ZoomableImageCanvas()
        {
            ClipToBounds = true;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            Focusable = true;
        }

        // ── Callbacks ─────────────────────────────────────────────────────────

        private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (ZoomableImageCanvas)d;
            canvas._transform = Matrix.Identity;
            canvas.FitImageToView();
        }

        // ── Layout ────────────────────────────────────────────────────────────

        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            FitImageToView();
        }

        private void FitImageToView()
        {
            var img = ImageSource;
            if (img == null || ActualWidth <= 0 || ActualHeight <= 0) return;

            double scaleX = ActualWidth  / img.Width;
            double scaleY = ActualHeight / img.Height;
            double scale  = Math.Min(scaleX, scaleY);

            double offsetX = (ActualWidth  - img.Width  * scale) / 2.0;
            double offsetY = (ActualHeight - img.Height * scale) / 2.0;

            _transform = Matrix.Identity;
            _transform.Scale(scale, scale);
            _transform.Translate(offsetX, offsetY);
            InvalidateVisual();
        }

        // ── Rendering ─────────────────────────────────────────────────────────

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(45, 45, 48)), null,
                new Rect(RenderSize));

            var img = ImageSource;
            if (img == null) return;

            var mt = new MatrixTransform(_transform);
            dc.PushTransform(mt);
            dc.DrawImage(img, new Rect(0, 0, img.Width, img.Height));
            dc.Pop();

            double scale = _transform.M11;
            if (scale >= 8.0)
                DrawPixelOverlay(dc, scale);
        }

        private void DrawPixelOverlay(DrawingContext dc, double scale)
        {
            var rawData = RawData;
            var info    = MatInfo;
            if (rawData == null || info == null) return;

            // Compute visible pixel range from the inverse transform
            var inv = _transform;
            inv.Invert();
            var tl = inv.Transform(new Point(0, 0));
            var br = inv.Transform(new Point(ActualWidth, ActualHeight));

            int c0 = Math.Max(0, (int)Math.Floor(tl.X));
            int r0 = Math.Max(0, (int)Math.Floor(tl.Y));
            int c1 = Math.Min(info.Cols - 1, (int)Math.Ceiling(br.X));
            int r1 = Math.Min(info.Rows - 1, (int)Math.Ceiling(br.Y));

            bool showText  = scale >= 16.0;
            double fontSize = Math.Max(7.0, Math.Min(scale * 0.28, 13.0));

            for (int row = r0; row <= r1; row++)
            {
                for (int col = c0; col <= c1; col++)
                {
                    // Pixel cell rectangle in screen space
                    var cellTL   = _transform.Transform(new Point(col,     row));
                    var cellBR   = _transform.Transform(new Point(col + 1, row + 1));
                    var cellRect = new Rect(cellTL, cellBR);

                    // Dashed border around each pixel
                    dc.DrawRectangle(null, _gridPen, cellRect);

                    // Pixel value text
                    if (showText)
                    {
                        string[] vals = PixelValueFormatter.GetPixelValues(rawData, info, row, col);
                        if (vals == null) continue;

                        byte lum      = PixelValueFormatter.GetLuminance(rawData, info, row, col);
                        var textBrush = lum > 120 ? Brushes.Black : Brushes.White;

                        var ft = new FormattedText(
                            string.Join("\n", vals),
                            CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            _typeface, fontSize, textBrush, 1.0);

                        dc.DrawText(ft, new Point(
                            cellTL.X + (cellRect.Width  - ft.Width)  / 2.0,
                            cellTL.Y + (cellRect.Height - ft.Height) / 2.0));
                    }
                }
            }
        }

        // ── Mouse interaction ─────────────────────────────────────────────────

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            // Double-click: fit image to window
            if (e.ClickCount == 2)
            {
                FitImageToView();
                e.Handled = true;
                return;
            }

            _isPanning = true;
            _lastPan   = e.GetPosition(this);
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_isPanning) return;
            var pos   = e.GetPosition(this);
            var delta = pos - _lastPan;
            _transform.Translate(delta.X, delta.Y);
            _lastPan = pos;
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            _isPanning = false;
            ReleaseMouseCapture();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 1.2 : 1.0 / 1.2;
            var mouse = e.GetPosition(this);

            // Convert mouse position from screen space to image space
            var inv   = _transform;
            inv.Invert();
            var imgPt = inv.Transform(mouse);

            // Apply scale (uniform, no centering yet)
            _transform.Scale(factor, factor);

            // Correct the translation so imgPt stays under the mouse cursor
            var newScreen = _transform.Transform(imgPt);
            _transform.Translate(mouse.X - newScreen.X, mouse.Y - newScreen.Y);

            InvalidateVisual();
            e.Handled = true;
        }

        // ── Hit testing ───────────────────────────────────────────────────────

        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters) =>
            new PointHitTestResult(this, hitTestParameters.HitPoint);
    }
}
