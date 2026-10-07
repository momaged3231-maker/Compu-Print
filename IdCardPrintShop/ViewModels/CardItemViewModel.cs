using System;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdCardPrintShop.Models;
using IdCardPrintShop.Services;
using OpenCvSharp;

namespace IdCardPrintShop.ViewModels
{
    public partial class CardItemViewModel : ObservableObject
    {
        private readonly IImageProcessingService _imageService;

        public CardRegion Region { get; }

        [ObservableProperty]
        private string _displayLabel = string.Empty;

        [ObservableProperty]
        private CardRole _role;

        [ObservableProperty]
        private BitmapSource? _rectifiedImage;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isManualAdjusted;

        [ObservableProperty]
        private int _rotationQuarterTurns;

        [ObservableProperty]
        private double _confidence;

        [ObservableProperty]
        private CardDocumentType _documentType;

        public bool IsPassport => DocumentType == CardDocumentType.Passport;
        public bool IsNationalId => DocumentType == CardDocumentType.NationalId;

        public CardItemViewModel(CardRegion region, IImageProcessingService imageService)
        {
            Region = region;
            _imageService = imageService;

            DisplayLabel = region.Label;
            Role = region.Role;
            DocumentType = region.DocumentType;
            StatusMessage = region.StatusMessage;
            IsManualAdjusted = region.IsManualAdjusted;
            RotationQuarterTurns = region.RotationQuarterTurns;
            Confidence = region.Confidence;

            RefreshRectifiedPreview();
        }

        public void RefreshRectifiedPreview()
        {
            // Fire-and-forget async: keeps UI responsive and prevents crash on batch import
            _ = RefreshRectifiedPreviewAsync();
        }

        private async System.Threading.Tasks.Task RefreshRectifiedPreviewAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(Region.SourceImagePath) ||
                    !System.IO.File.Exists(Region.SourceImagePath))
                {
                    return;
                }

                // Run heavy OpenCV work on background thread
                var regionSnapshot = Region;
                var bitmapSource = await System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        using var mat = _imageService.WarpAndCorrectCard(
                            regionSnapshot.SourceImagePath, regionSnapshot);
                        if (mat == null || mat.Empty()) return null;
                        // MatToBitmapSource creates a frozen BitmapSource (cross-thread safe)
                        return _imageService.MatToBitmapSource(mat);
                    }
                    catch
                    {
                        return null;
                    }
                });

                // Update the observable property on the UI thread
                if (bitmapSource != null)
                {
                    RectifiedImage = bitmapSource;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في المعالجة: {ex.Message}";
            }
        }

        [RelayCommand]
        public void RotateCW()
        {
            Region.RotationQuarterTurns = (Region.RotationQuarterTurns + 1) % 4;
            RotationQuarterTurns = Region.RotationQuarterTurns;
            RefreshRectifiedPreview();
        }

        [RelayCommand]
        public void RotateCCW()
        {
            Region.RotationQuarterTurns = (Region.RotationQuarterTurns + 3) % 4;
            RotationQuarterTurns = Region.RotationQuarterTurns;
            RefreshRectifiedPreview();
        }

        [RelayCommand]
        public void SetRole(CardRole newRole)
        {
            Role = newRole;
            Region.Role = newRole;
            DisplayLabel = newRole switch
            {
                CardRole.Front => "الوجه (Front)",
                CardRole.Back => "الظهر (Back)",
                CardRole.Single => "بطاقة مستقلة",
                _ => Region.Label
            };
            Region.Label = DisplayLabel;
        }

        [RelayCommand]
        public void SetDocumentType(CardDocumentType newType)
        {
            DocumentType = newType;
            Region.DocumentType = newType;
            OnPropertyChanged(nameof(IsPassport));
            OnPropertyChanged(nameof(IsNationalId));
            if (newType == CardDocumentType.Passport && !DisplayLabel.Contains("جواز"))
            {
                DisplayLabel = "جواز سفر";
                Region.Label = DisplayLabel;
            }
            else if (newType == CardDocumentType.NationalId && DisplayLabel.Contains("جواز"))
            {
                DisplayLabel = "بطاقة هوية";
                Region.Label = DisplayLabel;
            }
            RefreshRectifiedPreview();
        }

        [RelayCommand]
        public void ToggleDocumentType()
        {
            var nextType = DocumentType == CardDocumentType.NationalId
                ? CardDocumentType.Passport
                : CardDocumentType.NationalId;
            SetDocumentType(nextType);
        }
    }
}
