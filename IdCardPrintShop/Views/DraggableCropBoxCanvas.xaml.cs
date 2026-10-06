using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IdCardPrintShop.ViewModels;

namespace IdCardPrintShop.Views
{
    public partial class DraggableCropBoxCanvas : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(ManualPersonalPhotoAdjustmentViewModel),
                typeof(DraggableCropBoxCanvas),
                new PropertyMetadata(null, OnViewModelChanged));

        public ManualPersonalPhotoAdjustmentViewModel? ViewModel
        {
            get => (ManualPersonalPhotoAdjustmentViewModel?)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private bool _isDraggingCenter;
        private Point _dragStartPoint;
        private double _initialCropX, _initialCropY;
        private double _imgDispX, _imgDispY, _imgDispW, _imgDispH;

        public DraggableCropBoxCanvas()
        {
            InitializeComponent();
        }

        private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DraggableCropBoxCanvas ctrl)
            {
                if (e.OldValue is ManualPersonalPhotoAdjustmentViewModel oldVm)
                {
                    oldVm.PropertyChanged -= ctrl.OnVmPropertyChanged;
                }
                if (e.NewValue is ManualPersonalPhotoAdjustmentViewModel newVm)
                {
                    newVm.PropertyChanged += ctrl.OnVmPropertyChanged;
                    ctrl.DisplayImage.Source = newVm.SourceImage;
                    ctrl.RecalculateLayout();
                }
            }
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManualPersonalPhotoAdjustmentViewModel.SourceImage))
            {
                DisplayImage.Source = ViewModel?.SourceImage;
                RecalculateLayout();
            }
            else if (e.PropertyName?.StartsWith("Crop") == true)
            {
                UpdateCropVisuals();
            }
        }

        private void OnRootGridSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RecalculateLayout();
        }

        private void RecalculateLayout()
        {
            if (ViewModel == null || ViewModel.ImageWidth <= 0 || ViewModel.ImageHeight <= 0) return;

            double containerW = RootGrid.ActualWidth;
            double containerH = RootGrid.ActualHeight;
            if (containerW < 20 || containerH < 20) return;

            double scaleX = containerW / ViewModel.ImageWidth;
            double scaleY = containerH / ViewModel.ImageHeight;
            double scale = Math.Min(scaleX, scaleY);

            _imgDispW = ViewModel.ImageWidth * scale;
            _imgDispH = ViewModel.ImageHeight * scale;
            _imgDispX = (containerW - _imgDispW) / 2.0;
            _imgDispY = (containerH - _imgDispH) / 2.0;

            Canvas.SetLeft(DisplayImage, _imgDispX);
            Canvas.SetTop(DisplayImage, _imgDispY);
            DisplayImage.Width = _imgDispW;
            DisplayImage.Height = _imgDispH;

            UpdateCropVisuals();
        }

        private void UpdateCropVisuals()
        {
            if (ViewModel == null || ViewModel.ImageWidth <= 0 || _imgDispW <= 0) return;

            double scale = _imgDispW / ViewModel.ImageWidth;

            double x = _imgDispX + (ViewModel.CropX * scale);
            double y = _imgDispY + (ViewModel.CropY * scale);
            double w = ViewModel.CropWidth * scale;
            double h = ViewModel.CropHeight * scale;

            Canvas.SetLeft(CropRectVisual, x);
            Canvas.SetTop(CropRectVisual, y);
            CropRectVisual.Width = w;
            CropRectVisual.Height = h;

            // Lines
            LineH1.X1 = x; LineH1.Y1 = y + (h / 3.0); LineH1.X2 = x + w; LineH1.Y2 = y + (h / 3.0);
            LineH2.X1 = x; LineH2.Y1 = y + (2 * h / 3.0); LineH2.X2 = x + w; LineH2.Y2 = y + (2 * h / 3.0);
            LineV1.X1 = x + (w / 3.0); LineV1.Y1 = y; LineV1.X2 = x + (w / 3.0); LineV1.Y2 = y + h;
            LineV2.X1 = x + (2 * w / 3.0); LineV2.Y1 = y; LineV2.X2 = x + (2 * w / 3.0); LineV2.Y2 = y + h;

            // Handles
            Canvas.SetLeft(ThumbTL, x); Canvas.SetTop(ThumbTL, y);
            Canvas.SetLeft(ThumbTR, x + w); Canvas.SetTop(ThumbTR, y);
            Canvas.SetLeft(ThumbBR, x + w); Canvas.SetTop(ThumbBR, y + h);
            Canvas.SetLeft(ThumbBL, x); Canvas.SetTop(ThumbBL, y + h);

            // Shaded Dim overlay geometry (hole for crop box)
            var geom = new CombinedGeometry(
                GeometryCombineMode.Exclude,
                new RectangleGeometry(new Rect(_imgDispX, _imgDispY, _imgDispW, _imgDispH)),
                new RectangleGeometry(new Rect(x, y, w, h))
            );
            DimPath.Data = geom;
        }

        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || ViewModel == null) return;

            var pos = e.GetPosition(OverlayCanvas);
            double scale = _imgDispW / ViewModel.ImageWidth;
            double x = _imgDispX + (ViewModel.CropX * scale);
            double y = _imgDispY + (ViewModel.CropY * scale);
            double w = ViewModel.CropWidth * scale;
            double h = ViewModel.CropHeight * scale;

            var cropScreenRect = new Rect(x, y, w, h);
            if (cropScreenRect.Contains(pos))
            {
                _isDraggingCenter = true;
                _dragStartPoint = pos;
                _initialCropX = ViewModel.CropX;
                _initialCropY = ViewModel.CropY;
                OverlayCanvas.CaptureMouse();
            }
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingCenter || ViewModel == null) return;

            var pos = e.GetPosition(OverlayCanvas);
            double deltaX = pos.X - _dragStartPoint.X;
            double deltaY = pos.Y - _dragStartPoint.Y;

            double scale = _imgDispW / ViewModel.ImageWidth;
            double pixelDeltaX = deltaX / scale;
            double pixelDeltaY = deltaY / scale;

            ViewModel.SetCropRect(
                _initialCropX + pixelDeltaX,
                _initialCropY + pixelDeltaY,
                ViewModel.CropWidth,
                ViewModel.CropHeight
            );
        }

        private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingCenter)
            {
                _isDraggingCenter = false;
                OverlayCanvas.ReleaseMouseCapture();
            }
        }

        private void OnCanvasMouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDraggingCenter && e.LeftButton != MouseButtonState.Pressed)
            {
                _isDraggingCenter = false;
                OverlayCanvas.ReleaseMouseCapture();
            }
        }
    }
}
