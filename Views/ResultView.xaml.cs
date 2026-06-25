using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Client.Views
{
    public partial class ResultView : UserControl
    {
        private Point _start;
        private Point _origin;

        public ResultView()
        {
            InitializeComponent();
            this.DataContextChanged += ResultView_DataContextChanged;
        }

        private void ResultView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ResetZoom();
        }

        private void ImageContainer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ResultImage == null || ImageScale == null || ImageTranslate == null) return;

            double zoomFactor = e.Delta > 0 ? 1.15 : (1.0 / 1.15);
            double nextScaleX = ImageScale.ScaleX * zoomFactor;
            double nextScaleY = ImageScale.ScaleY * zoomFactor;

            // Prevent zooming out below 100% (1.0)
            if (nextScaleX < 1.0)
            {
                ResetZoom();
                return;
            }
            if (nextScaleX > 15.0) return;

            Point relative = e.GetPosition(ResultImage);
            
            // Adjust translation so zooming focuses on the cursor position
            ImageTranslate.X = ImageTranslate.X - relative.X * (nextScaleX - ImageScale.ScaleX);
            ImageTranslate.Y = ImageTranslate.Y - relative.Y * (nextScaleY - ImageScale.ScaleY);

            ImageScale.ScaleX = nextScaleX;
            ImageScale.ScaleY = nextScaleY;
        }

        private void ImageContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ResultImage == null) return;

            _start = e.GetPosition(ImageContainer);
            _origin = new Point(ImageTranslate.X, ImageTranslate.Y);
            ImageContainer.Cursor = Cursors.Hand;
            ImageContainer.CaptureMouse();
        }

        private void ImageContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ImageContainer.Cursor = Cursors.Arrow;
            ImageContainer.ReleaseMouseCapture();
        }

        private void ImageContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!ImageContainer.IsMouseCaptured || ImageTranslate == null || ImageScale == null) return;

            // Only allow dragging/panning when zoomed in (> 100%)
            if (ImageScale.ScaleX <= 1.0) return;

            Vector v = e.GetPosition(ImageContainer) - _start;
            ImageTranslate.X = _origin.X + v.X;
            ImageTranslate.Y = _origin.Y + v.Y;
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            Zoom(1.3);
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            Zoom(1.0 / 1.3);
        }

        private void BtnZoomReset_Click(object sender, RoutedEventArgs e)
        {
            ResetZoom();
        }

        private void Zoom(double factor)
        {
            if (ResultImage == null || ImageScale == null || ImageTranslate == null) return;

            double nextScaleX = ImageScale.ScaleX * factor;
            double nextScaleY = ImageScale.ScaleY * factor;

            // Prevent zooming out below 100% (1.0)
            if (nextScaleX < 1.0)
            {
                ResetZoom();
                return;
            }
            if (nextScaleX > 15.0) return;

            // Zoom center is the middle of the container
            Point center = new Point(ImageContainer.ActualWidth / 2, ImageContainer.ActualHeight / 2);
            Point relative = ImageContainer.TranslatePoint(center, ResultImage);

            ImageTranslate.X = ImageTranslate.X - relative.X * (nextScaleX - ImageScale.ScaleX);
            ImageTranslate.Y = ImageTranslate.Y - relative.Y * (nextScaleY - ImageScale.ScaleY);

            ImageScale.ScaleX = nextScaleX;
            ImageScale.ScaleY = nextScaleY;
        }

        private void ResetZoom()
        {
            if (ImageScale == null || ImageTranslate == null) return;
            ImageScale.ScaleX = 1.0;
            ImageScale.ScaleY = 1.0;
            ImageTranslate.X = 0.0;
            ImageTranslate.Y = 0.0;
        }
    }
}

