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
        private const double MaxZoom = 64.0;

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

        // ── Callbacks (wired in code-behind) ──────────────────────────────────

        /// <summary>Fired on mouse move with image-space x, y and channel values (null when outside).</summary>
        public Action<int, int, string[]> CursorChanged;

        /// <summary>Fired when the zoom level changes (in x multiples, e.g. 2.0 = 2x).</summary>
        public Action<double> ZoomChanged;

        // ── State ─────────────────────────────────────────────────────────────

        private Matrix _transform = Matrix.Identity;
        private Point  _lastPan;
        private bool   _isPanning;

        private static readonly Typeface _typeface = new Typeface("Segoe UI");

        private static readonly DashStyle _dashStyle;
        private static readonly Pen       _gridPen;
        private static readonly Brush     _valueBgBrush;

        static ZoomableImageCanvas()
        {
            _dashStyle = new DashStyle(new double[] { 4, 4 }, 0);
            _dashStyle.Freeze();

            var gridBrush = new SolidColorBrush(Color.FromArgb(140, 180, 180, 180));
            gridBrush.Freeze();
            _gridPen = new Pen(gridBrush, 1.0) { DashStyle = _dashStyle };
            _gridPen.Freeze();

            _valueBgBrush = new SolidColorBrush(Color.FromArgb(160, 105, 105, 105));
            _valueBgBrush.Freeze();
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
            double scale  = Math.Min(Math.Min(scaleX, scaleY), MaxZoom);

            _transform = Matrix.Identity;
            _transform.Scale(scale, scale);
            _transform.Translate(
                (ActualWidth  - img.Width  * scale) / 2.0,
                (ActualHeight - img.Height * scale) / 2.0);

            InvalidateVisual();
            ZoomChanged?.Invoke(_transform.M11);
        }

        // ── Rendering ─────────────────────────────────────────────────────────

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(100, 100, 100)), null,
                new Rect(RenderSize));

            var img = ImageSource;
            if (img == null) return;

            dc.PushTransform(new MatrixTransform(_transform));
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

            var inv = _transform;
            inv.Invert();
            var tl = inv.Transform(new Point(0, 0));
            var br = inv.Transform(new Point(ActualWidth, ActualHeight));

            int c0 = Math.Max(0, (int)Math.Floor(tl.X));
            int r0 = Math.Max(0, (int)Math.Floor(tl.Y));
            int c1 = Math.Min(info.Cols - 1, (int)Math.Ceiling(br.X));
            int r1 = Math.Min(info.Rows - 1, (int)Math.Ceiling(br.Y));

            // For multi-channel images require more zoom so text has room to breathe
            int    channels  = info?.Channels ?? 1;
            double textThreshold = 16.0 + (channels - 1) * 8.0; // 1ch→16, 3ch→32
            bool   showText  = scale >= textThreshold;

            // Font sized so all lines fit within ~70% of cell height, capped at 9px
            double maxFontByCell = scale * 0.68 / Math.Max(1, channels * 1.35);
            double fontSize = Math.Max(6.0, Math.Min(maxFontByCell, 9.0));

            const double padX = 2, padY = 1;

            for (int row = r0; row <= r1; row++)
            {
                for (int col = c0; col <= c1; col++)
                {
                    var cellTL   = _transform.Transform(new Point(col,     row));
                    var cellBR   = _transform.Transform(new Point(col + 1, row + 1));
                    var cellRect = new Rect(cellTL, cellBR);

                    // Dashed pixel border
                    dc.DrawRectangle(null, _gridPen, cellRect);

                    if (!showText) continue;

                    string[] vals = PixelValueFormatter.GetPixelValues(rawData, info, row, col);
                    if (vals == null) continue;

                    string text = string.Join("\n", vals);
                    byte   lum  = PixelValueFormatter.GetLuminance(rawData, info, row, col);
                    var textBrush = lum > 120 ? Brushes.Black : Brushes.White;

                    var ft = new FormattedText(
                        text, CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        _typeface, fontSize, textBrush, 1.0);

                    // Badge centered within the cell, clipped so text never bleeds into adjacent cells
                    double bgW = ft.Width  + padX * 2;
                    double bgH = ft.Height + padY * 2;
                    double bgX = cellTL.X + (cellRect.Width  - bgW) / 2.0;
                    double bgY = cellTL.Y + (cellRect.Height - bgH) / 2.0;

                    dc.PushClip(new RectangleGeometry(cellRect));
                    dc.DrawRoundedRectangle(_valueBgBrush, null,
                        new Rect(bgX, bgY, bgW, bgH), 2.0, 2.0);
                    dc.DrawText(ft, new Point(bgX + padX, bgY + padY));
                    dc.Pop();
                }
            }
        }

        // ── Mouse interaction ─────────────────────────────────────────────────

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) { FitImageToView(); e.Handled = true; return; }
            _isPanning = true;
            _lastPan   = e.GetPosition(this);
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var mouse = e.GetPosition(this);

            // Update cursor position info
            var inv   = _transform;
            inv.Invert();
            var imgPt = inv.Transform(mouse);
            int ix = (int)Math.Floor(imgPt.X);
            int iy = (int)Math.Floor(imgPt.Y);
            var info = MatInfo;
            if (info != null && ix >= 0 && ix < info.Cols && iy >= 0 && iy < info.Rows)
                CursorChanged?.Invoke(ix, iy, PixelValueFormatter.GetPixelValues(RawData, info, iy, ix));
            else
                CursorChanged?.Invoke(-1, -1, null);

            // Pan
            if (_isPanning)
            {
                var delta = mouse - _lastPan;
                _transform.Translate(delta.X, delta.Y);
                _lastPan = mouse;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            _isPanning = false;
            ReleaseMouseCapture();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            CursorChanged?.Invoke(-1, -1, null);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            double currentScale = _transform.M11;
            double factor = e.Delta > 0 ? 1.2 : 1.0 / 1.2;

            // Clamp to [fit, 64x]
            double newScale = currentScale * factor;
            if (newScale > MaxZoom) factor = MaxZoom / currentScale;
            if (newScale < 0.001) factor = 0.001 / currentScale;

            var mouse = e.GetPosition(this);

            // Convert mouse to image space, scale, then correct translation
            var inv   = _transform;
            inv.Invert();
            var imgPt = inv.Transform(mouse);

            _transform.Scale(factor, factor);

            var newScreen = _transform.Transform(imgPt);
            _transform.Translate(mouse.X - newScreen.X, mouse.Y - newScreen.Y);

            InvalidateVisual();
            ZoomChanged?.Invoke(_transform.M11);
            e.Handled = true;
        }

        // ── Hit testing ───────────────────────────────────────────────────────

        protected override HitTestResult HitTestCore(PointHitTestParameters p) =>
            new PointHitTestResult(this, p.HitPoint);
    }
}
