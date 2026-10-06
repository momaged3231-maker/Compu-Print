using System;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdCardPrintShop.Models;
using IdCardPrintShop.Services;
using OpenCvSharp;

namespace IdCardPrintShop.ViewModels
{
    public partial class ManualPersonalPhotoAdjustmentViewModel : ObservableObject
    {
        private readonly PersonalPhotoItemViewModel _itemVm;
        private readonly IPersonalPhotoProcessor _processor;
        private readonly IImageProcessingService _imgService;
        private readonly PersonalPhotoParameters _originalParams;

        [ObservableProperty]
        private BitmapSource? _sourceImage;

        [ObservableProperty]
        private BitmapSource? _livePreview;

        [ObservableProperty]
        private double _imageWidth;

        [ObservableProperty]
        private double _imageHeight;

        // Crop box in pixel coordinates
        [ObservableProperty]
        private double _cropX;
        [ObservableProperty]
        private double _cropY;
        [ObservableProperty]
        private double _cropWidth;
        [ObservableProperty]
        private double _cropHeight;

        // Color & Image Adjustment Sliders
        [ObservableProperty]
        private double _brightness;

        [ObservableProperty]
        private double _contrast;

        [ObservableProperty]
        private double _saturation;

        [ObservableProperty]
        private double _temperature;

        [ObservableProperty]
        private double _edgeFeatherRadius;

        [ObservableProperty]
        private bool _removeBackground;

        [ObservableProperty]
        private PhotoBackgroundType _backgroundType;

        [ObservableProperty]
        private int _rotationQuarterTurns;

        [ObservableProperty]
        private string _statusMessage = "اسحب الإطار لتعديل موضع وحجم الرأس والقص.";

        public event Action? RequestClose;

        public ManualPersonalPhotoAdjustmentViewModel(
            PersonalPhotoItemViewModel itemVm,
            IPersonalPhotoProcessor processor,
            IImageProcessingService imgService)
        {
            _itemVm = itemVm;
            _processor = processor;
            _imgService = imgService;

            var p = itemVm.Item.Parameters;
            _originalParams = p.Clone();

            CropX = p.CropBox.X;
            CropY = p.CropBox.Y;
            CropWidth = p.CropBox.Width;
            CropHeight = p.CropBox.Height;

            Brightness = p.Brightness;
            Contrast = p.Contrast;
            Saturation = p.Saturation;
            Temperature = p.Temperature;
            EdgeFeatherRadius = p.EdgeFeatherRadius;
            RemoveBackground = p.RemoveBackground;
            BackgroundType = p.BackgroundType;
            RotationQuarterTurns = p.RotationQuarterTurns;

            LoadSource();
            UpdateLivePreview();
        }

        private void LoadSource()
        {
            string path = _itemVm.Item.SourceImagePath;
            if (!File.Exists(path)) return;

            using var mat = _imgService.LoadMat(path);
            ImageWidth = mat.Width;
            ImageHeight = mat.Height;
            SourceImage = _imgService.MatToBitmapSource(mat);

            if (CropWidth <= 0 || CropHeight <= 0)
            {
                CropWidth = ImageWidth * 0.6;
                CropHeight = CropWidth / _itemVm.Profile.AspectRatio;
                CropX = (ImageWidth - CropWidth) / 2.0;
                CropY = (ImageHeight - CropHeight) / 4.0;
            }
        }

        public void SetCropRect(double x, double y, double w, double h)
        {
            // Maintain aspect ratio of profile
            double targetAspect = _itemVm.Profile.AspectRatio;
            w = Math.Clamp(w, 40, ImageWidth);
            h = w / targetAspect;

            if (h > ImageHeight)
            {
                h = ImageHeight;
                w = h * targetAspect;
            }

            x = Math.Clamp(x, 0, ImageWidth - w);
            y = Math.Clamp(y, 0, ImageHeight - h);

            CropX = x;
            CropY = y;
            CropWidth = w;
            CropHeight = h;

            UpdateLivePreview();
        }

        public void UpdateLivePreview()
        {
            try
            {
                string path = _itemVm.Item.SourceImagePath;
                if (!File.Exists(path)) return;

                var tempParams = new PersonalPhotoParameters
                {
                    CropBox = new RectD(CropX, CropY, CropWidth, CropHeight),
                    Brightness = Brightness,
                    Contrast = Contrast,
                    Saturation = Saturation,
                    Temperature = Temperature,
                    EdgeFeatherRadius = EdgeFeatherRadius,
                    RemoveBackground = RemoveBackground,
                    BackgroundType = BackgroundType,
                    RotationQuarterTurns = RotationQuarterTurns,
                    FaceBox = _itemVm.Item.Parameters.FaceBox,
                    HeadCenterX = _itemVm.Item.Parameters.HeadCenterX,
                    HeadCenterY = _itemVm.Item.Parameters.HeadCenterY,
                    HeadHeight = _itemVm.Item.Parameters.HeadHeight
                };

                using var mat = _imgService.LoadMat(path);
                using var rendered = _processor.RenderFinalPhoto(mat, tempParams, _itemVm.Profile);
                LivePreview = _imgService.MatToBitmapSource(rendered);
            }
            catch (Exception ex)
            {
                StatusMessage = $"تنبيه: {ex.Message}";
            }
        }

        [RelayCommand]
        public void Rotate90CW()
        {
            RotationQuarterTurns = (RotationQuarterTurns + 1) % 4;
            UpdateLivePreview();
        }

        [RelayCommand]
        public void Rotate90CCW()
        {
            RotationQuarterTurns = (RotationQuarterTurns + 3) % 4;
            UpdateLivePreview();
        }

        [RelayCommand]
        public void SetBackgroundType(object? parameter)
        {
            if (parameter is PhotoBackgroundType type)
            {
                BackgroundType = type;
            }
            else if (parameter is string str && Enum.TryParse<PhotoBackgroundType>(str, true, out var parsed))
            {
                BackgroundType = parsed;
            }
            RemoveBackground = BackgroundType != PhotoBackgroundType.Original;
            UpdateLivePreview();
        }

        [RelayCommand]
        public void ResetSettings()
        {
            CropX = _originalParams.CropBox.X;
            CropY = _originalParams.CropBox.Y;
            CropWidth = _originalParams.CropBox.Width;
            CropHeight = _originalParams.CropBox.Height;
            Brightness = _originalParams.Brightness;
            Contrast = _originalParams.Contrast;
            Saturation = _originalParams.Saturation;
            Temperature = _originalParams.Temperature;
            EdgeFeatherRadius = _originalParams.EdgeFeatherRadius;
            RemoveBackground = _originalParams.RemoveBackground;
            BackgroundType = _originalParams.BackgroundType;
            RotationQuarterTurns = _originalParams.RotationQuarterTurns;

            UpdateLivePreview();
            StatusMessage = "تمت استعادة الإعدادات الأصلية.";
        }

        [RelayCommand]
        public void Apply()
        {
            var p = _itemVm.Item.Parameters;
            p.CropBox = new RectD(CropX, CropY, CropWidth, CropHeight);
            p.Brightness = Brightness;
            p.Contrast = Contrast;
            p.Saturation = Saturation;
            p.Temperature = Temperature;
            p.EdgeFeatherRadius = EdgeFeatherRadius;
            p.RemoveBackground = RemoveBackground;
            p.BackgroundType = BackgroundType;
            p.RotationQuarterTurns = RotationQuarterTurns;
            p.IsManualAdjusted = true;

            _itemVm.Item.Status = PersonalPhotoStatus.Ready;
            _itemVm.Item.StatusMessage = "تم التعديل اليدوي";
            _itemVm.RefreshRenderedPreview();

            RequestClose?.Invoke();
        }

        [RelayCommand]
        public void Cancel()
        {
            RequestClose?.Invoke();
        }
    }
}
