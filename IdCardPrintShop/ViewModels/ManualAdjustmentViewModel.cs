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
    public partial class ManualAdjustmentViewModel : ObservableObject
    {
        private readonly IImageProcessingService _imageService;
        private readonly CardItemViewModel _cardItemVm;
        private readonly Point2D[] _initialCorners;

        [ObservableProperty]
        private BitmapSource? _sourceImage;

        [ObservableProperty]
        private BitmapSource? _liveRectifiedPreview;

        [ObservableProperty]
        private double _imagePixelWidth;

        [ObservableProperty]
        private double _imagePixelHeight;

        // 4 Corner coordinates (in image pixel space)
        [ObservableProperty]
        private double _corner0X;
        [ObservableProperty]
        private double _corner0Y;

        [ObservableProperty]
        private double _corner1X;
        [ObservableProperty]
        private double _corner1Y;

        [ObservableProperty]
        private double _corner2X;
        [ObservableProperty]
        private double _corner2Y;

        [ObservableProperty]
        private double _corner3X;
        [ObservableProperty]
        private double _corner3Y;

        [ObservableProperty]
        private int _rotationQuarterTurns;

        [ObservableProperty]
        private double _fineRotationDegrees;

        [ObservableProperty]
        private double _safetyMarginPercent;

        [ObservableProperty]
        private CardRole _cardRole;

        [ObservableProperty]
        private CardDocumentType _documentType;

        public bool IsPassport => DocumentType == CardDocumentType.Passport;
        public bool IsNationalId => DocumentType == CardDocumentType.NationalId;

        [ObservableProperty]
        private string _statusMessage = "اسحب الدوائر الأربعة لتحديد أركان البطاقة بدقة.";

        public event Action? RequestClose;

        public ManualAdjustmentViewModel(CardItemViewModel cardItemVm, IImageProcessingService imageService)
        {
            _cardItemVm = cardItemVm;
            _imageService = imageService;

            var r = cardItemVm.Region;
            _initialCorners = (Point2D[])r.Corners.Clone();

            RotationQuarterTurns = r.RotationQuarterTurns;
            FineRotationDegrees = r.FineRotationDegrees;
            SafetyMarginPercent = r.SafetyMarginPercent;
            CardRole = r.Role;
            DocumentType = r.DocumentType;

            LoadSourceImage(r.SourceImagePath);

            Corner0X = r.Corners[0].X;
            Corner0Y = r.Corners[0].Y;
            Corner1X = r.Corners[1].X;
            Corner1Y = r.Corners[1].Y;
            Corner2X = r.Corners[2].X;
            Corner2Y = r.Corners[2].Y;
            Corner3X = r.Corners[3].X;
            Corner3Y = r.Corners[3].Y;

            UpdateLivePreview();
        }

        private void LoadSourceImage(string path)
        {
            if (!File.Exists(path)) return;

            using var mat = _imageService.LoadMat(path);
            ImagePixelWidth = mat.Width;
            ImagePixelHeight = mat.Height;
            SourceImage = _imageService.MatToBitmapSource(mat);
        }

        public void SetCorner(int index, double x, double y)
        {
            x = Math.Clamp(x, 0, ImagePixelWidth - 1);
            y = Math.Clamp(y, 0, ImagePixelHeight - 1);

            switch (index)
            {
                case 0:
                    Corner0X = x;
                    Corner0Y = y;
                    break;
                case 1:
                    Corner1X = x;
                    Corner1Y = y;
                    break;
                case 2:
                    Corner2X = x;
                    Corner2Y = y;
                    break;
                case 3:
                    Corner3X = x;
                    Corner3Y = y;
                    break;
            }

            UpdateLivePreview();
        }

        public void UpdateLivePreview()
        {
            try
            {
                var tempRegion = new CardRegion
                {
                    SourceImagePath = _cardItemVm.Region.SourceImagePath,
                    DocumentType = DocumentType,
                    Corners = new[]
                    {
                        new Point2D(Corner0X, Corner0Y),
                        new Point2D(Corner1X, Corner1Y),
                        new Point2D(Corner2X, Corner2Y),
                        new Point2D(Corner3X, Corner3Y)
                    },
                    RotationQuarterTurns = RotationQuarterTurns,
                    FineRotationDegrees = FineRotationDegrees,
                    SafetyMarginPercent = SafetyMarginPercent
                };

                using var mat = _imageService.WarpAndCorrectCard(tempRegion.SourceImagePath, tempRegion);
                LiveRectifiedPreview = _imageService.MatToBitmapSource(mat);
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
        public void Flip180()
        {
            RotationQuarterTurns = (RotationQuarterTurns + 2) % 4;
            UpdateLivePreview();
        }

        [RelayCommand]
        public void ResetCorners()
        {
            Corner0X = _initialCorners[0].X;
            Corner0Y = _initialCorners[0].Y;
            Corner1X = _initialCorners[1].X;
            Corner1Y = _initialCorners[1].Y;
            Corner2X = _initialCorners[2].X;
            Corner2Y = _initialCorners[2].Y;
            Corner3X = _initialCorners[3].X;
            Corner3Y = _initialCorners[3].Y;
            RotationQuarterTurns = 0;
            FineRotationDegrees = 0;
            UpdateLivePreview();
            StatusMessage = "تمت استعادة الحدود السابقة.";
        }

        [RelayCommand]
        public void AutoDetectAgain()
        {
            try
            {
                var result = _imageService.DetectCards(_cardItemVm.Region.SourceImagePath);
                if (result.DetectedCards.Count > 0)
                {
                    var first = result.DetectedCards[0];
                    Corner0X = first.Corners[0].X;
                    Corner0Y = first.Corners[0].Y;
                    Corner1X = first.Corners[1].X;
                    Corner1Y = first.Corners[1].Y;
                    Corner2X = first.Corners[2].X;
                    Corner2Y = first.Corners[2].Y;
                    Corner3X = first.Corners[3].X;
                    Corner3Y = first.Corners[3].Y;
                    UpdateLivePreview();
                    StatusMessage = result.Message;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"تعذر الاكتشاف: {ex.Message}";
            }
        }

        [RelayCommand]
        public void SetAsFront()
        {
            CardRole = CardRole.Front;
        }

        [RelayCommand]
        public void SetAsBack()
        {
            CardRole = CardRole.Back;
        }

        [RelayCommand]
        public void SetDocumentType(CardDocumentType newType)
        {
            DocumentType = newType;
            OnPropertyChanged(nameof(IsPassport));
            OnPropertyChanged(nameof(IsNationalId));
            UpdateLivePreview();
        }

        [RelayCommand]
        public void ApplyNationalIdPreset()
        {
            SetDocumentType(CardDocumentType.NationalId);
            ApplyPresetWithAspectRatio(85.60 / 54.00);
        }

        [RelayCommand]
        public void ApplyPassportPreset()
        {
            SetDocumentType(CardDocumentType.Passport);
            ApplyPresetWithAspectRatio(125.00 / 88.00);
        }

        private void ApplyPresetWithAspectRatio(double targetAspect)
        {
            double cardW = ImagePixelWidth * 0.70;
            double cardH = cardW / targetAspect;
            if (cardH > ImagePixelHeight * 0.75)
            {
                cardH = ImagePixelHeight * 0.70;
                cardW = cardH * targetAspect;
            }

            double startX = (ImagePixelWidth - cardW) / 2.0;
            double startY = (ImagePixelHeight - cardH) / 2.0;

            Corner0X = startX;
            Corner0Y = startY;
            Corner1X = startX + cardW;
            Corner1Y = startY;
            Corner2X = startX + cardW;
            Corner2Y = startY + cardH;
            Corner3X = startX;
            Corner3Y = startY + cardH;

            UpdateLivePreview();
        }

        [RelayCommand]
        public void Apply()
        {
            var r = _cardItemVm.Region;
            r.Corners[0] = new Point2D(Corner0X, Corner0Y);
            r.Corners[1] = new Point2D(Corner1X, Corner1Y);
            r.Corners[2] = new Point2D(Corner2X, Corner2Y);
            r.Corners[3] = new Point2D(Corner3X, Corner3Y);
            r.RotationQuarterTurns = RotationQuarterTurns;
            r.FineRotationDegrees = FineRotationDegrees;
            r.SafetyMarginPercent = SafetyMarginPercent;
            r.Role = CardRole;
            r.DocumentType = DocumentType;
            r.IsManualAdjusted = true;
            r.StatusMessage = "تم التعديل اليدوي";

            _cardItemVm.SetRole(CardRole);
            _cardItemVm.SetDocumentType(DocumentType);
            _cardItemVm.RotationQuarterTurns = RotationQuarterTurns;
            _cardItemVm.IsManualAdjusted = true;
            _cardItemVm.RefreshRectifiedPreview();

            RequestClose?.Invoke();
        }

        [RelayCommand]
        public void Cancel()
        {
            RequestClose?.Invoke();
        }
    }
}
