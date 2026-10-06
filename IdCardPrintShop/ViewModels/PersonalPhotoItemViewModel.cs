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
    public partial class PersonalPhotoItemViewModel : ObservableObject
    {
        private readonly IPersonalPhotoProcessor _processor;
        private readonly IImageProcessingService _imgService;

        public PersonalPhotoItem Item { get; }

        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        private PersonalPhotoStatus _status;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private BitmapSource? _renderedPreview;

        [ObservableProperty]
        private int _copies;

        [ObservableProperty]
        private PhotoProfile _profile;

        [ObservableProperty]
        private bool _isSelected;

        public PersonalPhotoItemViewModel(
            PersonalPhotoItem item,
            IPersonalPhotoProcessor processor,
            IImageProcessingService imgService)
        {
            Item = item;
            _processor = processor;
            _imgService = imgService;

            DisplayName = item.DisplayName;
            Status = item.Status;
            StatusMessage = item.StatusMessage;
            Copies = item.Copies;
            Profile = item.Profile;

            RefreshRenderedPreview();
        }

        public void RefreshRenderedPreview()
        {
            try
            {
                if (string.IsNullOrEmpty(Item.SourceImagePath) || !File.Exists(Item.SourceImagePath))
                {
                    return;
                }

                using var mat = _imgService.LoadMat(Item.SourceImagePath);
                using var renderedMat = _processor.RenderFinalPhoto(mat, Item.Parameters, Profile);
                RenderedPreview = _imgService.MatToBitmapSource(renderedMat);
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ: {ex.Message}";
                Status = PersonalPhotoStatus.Failed;
            }
        }

        [RelayCommand]
        public void RotateCW()
        {
            Item.Parameters.RotationQuarterTurns = (Item.Parameters.RotationQuarterTurns + 1) % 4;
            RefreshRenderedPreview();
        }

        [RelayCommand]
        public void SetCopies(int count)
        {
            Copies = count;
            Item.Copies = count;
        }
    }
}
