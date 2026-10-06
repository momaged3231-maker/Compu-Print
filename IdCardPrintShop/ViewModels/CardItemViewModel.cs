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

        public CardItemViewModel(CardRegion region, IImageProcessingService imageService)
        {
            Region = region;
            _imageService = imageService;

            DisplayLabel = region.Label;
            Role = region.Role;
            StatusMessage = region.StatusMessage;
            IsManualAdjusted = region.IsManualAdjusted;
            RotationQuarterTurns = region.RotationQuarterTurns;
            Confidence = region.Confidence;

            RefreshRectifiedPreview();
        }

        public void RefreshRectifiedPreview()
        {
            try
            {
                if (string.IsNullOrEmpty(Region.SourceImagePath) || !System.IO.File.Exists(Region.SourceImagePath))
                {
                    return;
                }

                using var mat = _imageService.WarpAndCorrectCard(Region.SourceImagePath, Region);
                RectifiedImage = _imageService.MatToBitmapSource(mat);
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
    }
}
