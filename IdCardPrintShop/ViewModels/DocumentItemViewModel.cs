using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdCardPrintShop.Models;
using IdCardPrintShop.Services;

namespace IdCardPrintShop.ViewModels
{
    public partial class DocumentItemViewModel : ObservableObject
    {
        public DocumentItem Item { get; }

        public event Action? SettingsChanged;

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private DocumentFileType _fileType;

        [ObservableProperty]
        private int _pageCount;

        [ObservableProperty]
        private string _paperSizeName = "A4";

        [ObservableProperty]
        private PrintColorMode _printMode = PrintColorMode.BlackAndWhite;

        [ObservableProperty]
        private int _copies = 1;

        [ObservableProperty]
        private DocumentOrientationMode _orientation = DocumentOrientationMode.Auto;

        [ObservableProperty]
        private DocumentScalingMode _scaling = DocumentScalingMode.FitToPage;

        [ObservableProperty]
        private string _pageRange = "All";

        [ObservableProperty]
        private DocumentItemStatus _status = DocumentItemStatus.Ready;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private string _outputPagesBadge = string.Empty;

        public bool IsBw => PrintMode == PrintColorMode.BlackAndWhite;
        public bool IsColor => PrintMode == PrintColorMode.Color;

        public DocumentItemViewModel(DocumentItem item)
        {
            Item = item;
            _fileName = item.FileName;
            _fileType = item.FileType;
            _pageCount = item.PageCount;
            _paperSizeName = item.PaperSizeName;
            _printMode = item.PrintMode;
            _copies = item.Copies;
            _orientation = item.Orientation;
            _scaling = item.Scaling;
            _pageRange = item.PageRange;
            _status = item.Status;
            _statusMessage = item.StatusMessage;

            UpdateOutputBadge();
        }

        partial void OnPaperSizeNameChanged(string value)
        {
            Item.PaperSizeName = value;
            SettingsChanged?.Invoke();
        }

        partial void OnPrintModeChanged(PrintColorMode value)
        {
            Item.PrintMode = value;
            OnPropertyChanged(nameof(IsBw));
            OnPropertyChanged(nameof(IsColor));
            SettingsChanged?.Invoke();
        }

        partial void OnCopiesChanged(int value)
        {
            int safeCopies = Math.Max(1, value);
            if (_copies != safeCopies)
            {
                _copies = safeCopies;
            }
            Item.Copies = safeCopies;
            UpdateOutputBadge();
            SettingsChanged?.Invoke();
        }

        partial void OnOrientationChanged(DocumentOrientationMode value)
        {
            Item.Orientation = value;
            SettingsChanged?.Invoke();
        }

        partial void OnScalingChanged(DocumentScalingMode value)
        {
            Item.Scaling = value;
            SettingsChanged?.Invoke();
        }

        partial void OnPageRangeChanged(string value)
        {
            Item.PageRange = value;
            Item.ResolvedPageIndices = PageRangeParser.Parse(value, Item.PageCount);
            UpdateOutputBadge();
            SettingsChanged?.Invoke();
        }

        [RelayCommand]
        public void SetPaperSize(string paper)
        {
            PaperSizeName = paper;
        }

        [RelayCommand]
        public void SetPrintMode(string modeStr)
        {
            if (modeStr.Equals("color", StringComparison.OrdinalIgnoreCase))
            {
                PrintMode = PrintColorMode.Color;
            }
            else
            {
                PrintMode = PrintColorMode.BlackAndWhite;
            }
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
        public void SetCopiesPreset(int count)
        {
            Copies = count;
        }

        [RelayCommand]
        public void SetOrientation(string orientStr)
        {
            Orientation = orientStr.ToLowerInvariant() switch
            {
                "landscape" or "أفقي" => DocumentOrientationMode.Landscape,
                "portrait" or "رأسي" => DocumentOrientationMode.Portrait,
                _ => DocumentOrientationMode.Auto
            };
        }

        public void UpdateOutputBadge()
        {
            int pages = Item.ResolvedPageIndices.Count > 0 ? Item.ResolvedPageIndices.Count : Item.PageCount;
            int total = pages * Copies;
            OutputPagesBadge = $"{pages} ص × {Copies} نسخ = {total} صفحة مطبوعة";
            OnPropertyChanged(nameof(OutputPagesBadge));
        }

        public void RefreshStatus(DocumentItemStatus status, string message)
        {
            Status = status;
            StatusMessage = message;
            Item.Status = status;
            Item.StatusMessage = message;
        }
    }
}
