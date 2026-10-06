using System;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdCardPrintShop.Models;

namespace IdCardPrintShop.ViewModels
{
    public partial class CustomLayoutItemViewModel : ObservableObject
    {
        public CustomLayoutItem Item { get; }

        public event Action? RequestDelete;
        public event Action? RequestEdit;
        public event Action? SettingsChanged;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private decimal _widthMm;

        [ObservableProperty]
        private decimal _heightMm;

        [ObservableProperty]
        private int _copies;

        [ObservableProperty]
        private bool _rotationAllowed;

        [ObservableProperty]
        private string _sourceFile;

        [ObservableProperty]
        private string _status;

        [ObservableProperty]
        private BitmapSource? _thumbnail;

        public bool IsMissingSource => !string.IsNullOrEmpty(SourceFile) && !File.Exists(SourceFile);

        public string SizeDisplay => $"{WidthMm:G29} × {HeightMm:G29} mm";

        public CustomLayoutItemViewModel(CustomLayoutItem item)
        {
            Item = item;
            _name = item.Name;
            _widthMm = item.WidthMm;
            _heightMm = item.HeightMm;
            _copies = item.Copies;
            _rotationAllowed = item.RotationAllowed;
            _sourceFile = item.SourceFile;
            _status = item.Status;

            LoadThumbnail();
            CheckMissingStatus();
        }

        private void CheckMissingStatus()
        {
            if (IsMissingSource)
            {
                Status = "Needs Review (الصورة غير موجودة)";
                Item.Status = Status;
            }
            else if (string.IsNullOrEmpty(SourceFile))
            {
                Status = "بدون صورة";
                Item.Status = Status;
            }
            else
            {
                Status = "جاهز";
                Item.Status = Status;
            }
            OnPropertyChanged(nameof(IsMissingSource));
        }

        public void LoadThumbnail()
        {
            if (string.IsNullOrEmpty(SourceFile) || !File.Exists(SourceFile))
            {
                Thumbnail = null;
                CheckMissingStatus();
                return;
            }

            try
            {
                var uri = new Uri(SourceFile);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.DecodePixelWidth = 140;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                Thumbnail = bmp;
            }
            catch
            {
                Thumbnail = null;
            }

            CheckMissingStatus();
        }

        partial void OnNameChanged(string value)
        {
            Item.Name = value;
            SettingsChanged?.Invoke();
        }

        partial void OnWidthMmChanged(decimal value)
        {
            Item.WidthMm = value;
            OnPropertyChanged(nameof(SizeDisplay));
            SettingsChanged?.Invoke();
        }

        partial void OnHeightMmChanged(decimal value)
        {
            Item.HeightMm = value;
            OnPropertyChanged(nameof(SizeDisplay));
            SettingsChanged?.Invoke();
        }

        partial void OnCopiesChanged(int value)
        {
            if (value < 1) value = 1;
            Item.Copies = value;
            SettingsChanged?.Invoke();
        }

        partial void OnRotationAllowedChanged(bool value)
        {
            Item.RotationAllowed = value;
            SettingsChanged?.Invoke();
        }

        partial void OnSourceFileChanged(string value)
        {
            Item.SourceFile = value;
            LoadThumbnail();
            SettingsChanged?.Invoke();
        }

        [RelayCommand]
        public void IncrementCopies()
        {
            Copies++;
        }

        [RelayCommand]
        public void DecrementCopies()
        {
            if (Copies > 1) Copies--;
        }

        [RelayCommand]
        public void Delete()
        {
            RequestDelete?.Invoke();
        }

        [RelayCommand]
        public void Edit()
        {
            RequestEdit?.Invoke();
        }

        [RelayCommand]
        public void ApplyPreset(CustomSizePreset preset)
        {
            if (preset == null) return;
            WidthMm = preset.WidthMm;
            HeightMm = preset.HeightMm;
        }

        [RelayCommand]
        public void SwapOrientation()
        {
            var temp = WidthMm;
            WidthMm = HeightMm;
            HeightMm = temp;
        }
    }
}
