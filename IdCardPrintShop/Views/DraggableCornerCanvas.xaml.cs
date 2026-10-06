using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IdCardPrintShop.ViewModels;

namespace IdCardPrintShop.Views
{
    public partial class DraggableCornerCanvas : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(ManualAdjustmentViewModel),
                typeof(DraggableCornerCanvas),
                new PropertyMetadata(null, OnViewModelChanged));

        public ManualAdjustmentViewModel? ViewModel
        {
            get => (ManualAdjustmentViewModel?)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private int _draggedCornerIndex = -1;
        private double _imageDisplayX;
        private double _imageDisplayY;
        private double _imageDisplayWidth;
        private double _imageDisplayHeight;

        public DraggableCornerCanvas()
        {
            InitializeComponent();
        }

        private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DraggableCornerCanvas control)
            {
                if (e.OldValue is ManualAdjustmentViewModel oldVm)
                {
                    oldVm.PropertyChanged -= control.OnViewModelPropertyChanged;
                }

                if (e.NewValue is ManualAdjustmentViewModel newVm)
                {
                    newVm.PropertyChanged += control.OnViewModelPropertyChanged;
                    control.DisplayImage.Source = newVm.SourceImage;
                    control.RecalculateLayout();
                }
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManualAdjustmentViewModel.SourceImage))
            {
                DisplayImage.Source = ViewModel?.SourceImage;
                RecalculateLayout();
            }
            else if (e.PropertyName?.StartsWith("Corner") == true)
            {
                UpdateHandlePositions();
            }
        }

        private void OnRootGridSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RecalculateLayout();
        }

        private void RecalculateLayout()
        {
            if (ViewModel == null || ViewModel.ImagePixelWidth <= 0 || ViewModel.ImagePixelHeight <= 0)
            {
                return;
            }

            double containerW = RootGrid.ActualWidth;
            double containerH = RootGrid.ActualHeight;

            if (containerW <= 10 || containerH <= 10)
            {
                return;
            }

            double imgW = ViewModel.ImagePixelWidth;
            double imgH = ViewModel.ImagePixelHeight;

            double scaleX = containerW / imgW;
            double scaleY = containerH / imgH;
            double scale = Math.Min(scaleX, scaleY);

            _imageDisplayWidth = imgW * scale;
            _imageDisplayHeight = imgH * scale;

            _imageDisplayX = (containerW - _imageDisplayWidth) / 2.0;
            _imageDisplayY = (containerH - _imageDisplayHeight) / 2.0;

            Canvas.SetLeft(DisplayImage, _imageDisplayX);
            Canvas.SetTop(DisplayImage, _imageDisplayY);
            DisplayImage.Width = _imageDisplayWidth;
            DisplayImage.Height = _imageDisplayHeight;

            UpdateHandlePositions();
        }

        private Point PixelToCanvas(double px, double py)
        {
            if (ViewModel == null || ViewModel.ImagePixelWidth <= 0 || ViewModel.ImagePixelHeight <= 0)
            {
                return new Point(px, py);
            }

            double sx = _imageDisplayX + (px / ViewModel.ImagePixelWidth) * _imageDisplayWidth;
            double sy = _imageDisplayY + (py / ViewModel.ImagePixelHeight) * _imageDisplayHeight;
            return new Point(sx, sy);
        }

        private Point CanvasToPixel(double sx, double sy)
        {
            if (ViewModel == null || _imageDisplayWidth <= 0 || _imageDisplayHeight <= 0)
            {
                return new Point(sx, sy);
            }

            double px = ((sx - _imageDisplayX) / _imageDisplayWidth) * ViewModel.ImagePixelWidth;
            double py = ((sy - _imageDisplayY) / _imageDisplayHeight) * ViewModel.ImagePixelHeight;

            return new Point(
                Math.Clamp(px, 0, ViewModel.ImagePixelWidth - 1),
                Math.Clamp(py, 0, ViewModel.ImagePixelHeight - 1)
            );
        }

        private void UpdateHandlePositions()
        {
            if (ViewModel == null) return;

            var p0 = PixelToCanvas(ViewModel.Corner0X, ViewModel.Corner0Y);
            var p1 = PixelToCanvas(ViewModel.Corner1X, ViewModel.Corner1Y);
            var p2 = PixelToCanvas(ViewModel.Corner2X, ViewModel.Corner2Y);
            var p3 = PixelToCanvas(ViewModel.Corner3X, ViewModel.Corner3Y);

            // Center handles on the corner point (handle size is 28x28)
            Canvas.SetLeft(Handle0, p0.X - 14);
            Canvas.SetTop(Handle0, p0.Y - 14);

            Canvas.SetLeft(Handle1, p1.X - 14);
            Canvas.SetTop(Handle1, p1.Y - 14);

            Canvas.SetLeft(Handle2, p2.X - 14);
            Canvas.SetTop(Handle2, p2.Y - 14);

            Canvas.SetLeft(Handle3, p3.X - 14);
            Canvas.SetTop(Handle3, p3.Y - 14);

            // Update polygon
            QuadPolygon.Points = new PointCollection { p0, p1, p2, p3 };
        }

        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || ViewModel == null) return;

            var mousePos = e.GetPosition(OverlayCanvas);

            var p0 = PixelToCanvas(ViewModel.Corner0X, ViewModel.Corner0Y);
            var p1 = PixelToCanvas(ViewModel.Corner1X, ViewModel.Corner1Y);
            var p2 = PixelToCanvas(ViewModel.Corner2X, ViewModel.Corner2Y);
            var p3 = PixelToCanvas(ViewModel.Corner3X, ViewModel.Corner3Y);

            double d0 = (mousePos - p0).Length;
            double d1 = (mousePos - p1).Length;
            double d2 = (mousePos - p2).Length;
            double d3 = (mousePos - p3).Length;

            double minDist = Math.Min(Math.Min(d0, d1), Math.Min(d2, d3));

            // Grab within 40px radius
            if (minDist < 40)
            {
                if (minDist == d0) _draggedCornerIndex = 0;
                else if (minDist == d1) _draggedCornerIndex = 1;
                else if (minDist == d2) _draggedCornerIndex = 2;
                else _draggedCornerIndex = 3;

                OverlayCanvas.CaptureMouse();
            }
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedCornerIndex < 0 || ViewModel == null) return;

            var mousePos = e.GetPosition(OverlayCanvas);
            var pixelPoint = CanvasToPixel(mousePos.X, mousePos.Y);

            ViewModel.SetCorner(_draggedCornerIndex, pixelPoint.X, pixelPoint.Y);
        }

        private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedCornerIndex >= 0)
            {
                _draggedCornerIndex = -1;
                OverlayCanvas.ReleaseMouseCapture();
            }
        }

        private void OnCanvasMouseLeave(object sender, MouseEventArgs e)
        {
            if (_draggedCornerIndex >= 0 && e.LeftButton != MouseButtonState.Pressed)
            {
                _draggedCornerIndex = -1;
                OverlayCanvas.ReleaseMouseCapture();
            }
        }
    }
}
