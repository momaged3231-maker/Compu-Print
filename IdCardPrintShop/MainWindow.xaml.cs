using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IdCardPrintShop.Services;
using IdCardPrintShop.ViewModels;
using IdCardPrintShop.Views;

namespace IdCardPrintShop
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();

            var imageService = new OpenCvImageProcessingService();
            var layoutEngine = new LayoutEngine();
            var pdfService = new PdfSharpExportService();
            var jobService = new JobPersistenceService();

            ViewModel = new MainViewModel(imageService, layoutEngine, pdfService, jobService);
            DataContext = ViewModel;

            ViewModel.OpenManualAdjustmentRequested += OnOpenManualAdjustment;
            ViewModel.OpenPersonalPhotoAdjustmentRequested += OnOpenPersonalPhotoAdjustment;
        }

        private void OnOpenManualAdjustment(ManualAdjustmentViewModel adjustVm)
        {
            var win = new ManualAdjustmentWindow(adjustVm)
            {
                Owner = this
            };
            win.ShowDialog();
        }

        private void OnOpenPersonalPhotoAdjustment(ManualPersonalPhotoAdjustmentViewModel adjustVm)
        {
            var win = new ManualPersonalPhotoAdjustmentWindow(adjustVm)
            {
                Owner = this
            };
            win.ShowDialog();
        }

        private void OnCardAdjustClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CardItemViewModel cardVm)
            {
                ViewModel.OpenManualAdjustment(cardVm);
            }
        }

        private void OnPersonalPhotoAdjustClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PersonalPhotoItemViewModel photoVm)
            {
                ViewModel.OpenManualPersonalPhotoAdjustment(photoVm);
            }
        }

        private void OnDropZoneDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                var target = (sender as Border) ?? DropZone;
                if (target != null)
                {
                    target.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 229, 255));
                    target.Background = new SolidColorBrush(Color.FromRgb(30, 42, 60));
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnDropZoneDragLeave(object sender, DragEventArgs e)
        {
            var target = (sender as Border) ?? DropZone;
            if (target != null)
            {
                target.BorderBrush = new SolidColorBrush(Color.FromRgb(58, 69, 89));
                target.Background = new SolidColorBrush(Color.FromRgb(28, 34, 46));
            }
            e.Handled = true;
        }

        private async void OnDropZoneDrop(object sender, DragEventArgs e)
        {
            var target = (sender as Border) ?? DropZone;
            if (target != null)
            {
                target.BorderBrush = new SolidColorBrush(Color.FromRgb(58, 69, 89));
                target.Background = new SolidColorBrush(Color.FromRgb(28, 34, 46));
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files == null || files.Length == 0) return;

                // If user drops a folder directly
                if (Directory.Exists(files[0]))
                {
                    ViewModel.SwitchToSchoolOrders();
                    await ViewModel.ScanSchoolFolderAsync(files[0]);
                    return;
                }

                if (ViewModel.AppMode == ApplicationMode.SchoolOrders)
                {
                    var parentDir = Path.GetDirectoryName(files[0]);
                    if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                    {
                        await ViewModel.ScanSchoolFolderAsync(parentDir);
                    }
                }
                else if (ViewModel.AppMode == ApplicationMode.CustomSize)
                {
                    var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
                    var imageFiles = files
                        .Where(f => validExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .ToArray();

                    if (imageFiles.Length > 0)
                    {
                        ViewModel.AddCustomImage(imageFiles);
                    }
                }
                else if (ViewModel.AppMode == ApplicationMode.GenericDocuments)
                {
                    var validExtensions = new[] { ".pdf", ".docx", ".doc", ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
                    var docFiles = files
                        .Where(f => validExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .ToArray();

                    if (docFiles.Length > 0)
                    {
                        await ViewModel.ImportDocumentsAsync(docFiles);
                    }
                }
                else if (ViewModel.AppMode == ApplicationMode.PersonalPhotos)
                {
                    var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
                    var imageFiles = files
                        .Where(f => validExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .ToArray();

                    if (imageFiles.Length > 0)
                    {
                        await ViewModel.ProcessImportedPersonalPhotosAsync(imageFiles);
                    }
                }
                else
                {
                    var hasDocOrPdf = files.Any(f =>
                    {
                        var ext = Path.GetExtension(f).ToLowerInvariant();
                        return ext == ".pdf" || ext == ".docx" || ext == ".doc";
                    });

                    if (hasDocOrPdf)
                    {
                        ViewModel.SwitchToGenericDocuments();
                        await ViewModel.ImportDocumentsAsync(files);
                    }
                    else
                    {
                        var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
                        var imageFiles = files
                            .Where(f => validExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                            .ToArray();

                        if (imageFiles.Length > 0)
                        {
                            // Append if cards already exist, otherwise fresh import
                            if (ViewModel.Cards.Count > 0)
                                await ViewModel.AppendImportedFilesAsync(imageFiles);
                            else
                                await ViewModel.ProcessImportedFilesAsync(imageFiles);
                        }
                    }
                }
            }
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (ViewModel.AppMode == ApplicationMode.SchoolOrders)
            {
                if (e.Key == Key.O && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    e.Handled = true;
                    await ViewModel.ImportSchoolFolderAsync();
                }
                else if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    e.Handled = true;
                    StudentSearchBox?.Focus();
                    StudentSearchBox?.SelectAll();
                }
                else if (e.Key == Key.Escape)
                {
                    if (ViewModel.IsBatchProcessing)
                    {
                        e.Handled = true;
                        ViewModel.CancelBatchProcessing();
                    }
                }
                else if (e.Key == Key.F5)
                {
                    e.Handled = true;
                    await ViewModel.RefreshSchoolScanAsync();
                }
            }
        }

        private void OnPreset30x40Click(object sender, RoutedEventArgs e)
        {
            var item = new Models.CustomLayoutItem("عنصر 30×40", 30.0m, 40.0m, 1, "", true);
            ViewModel.AddCustomImage(new string[0]);
            ViewModel.CustomItems.Add(new CustomLayoutItemViewModel(item));
            ViewModel.UpdateLayoutPlan();
        }

        private void OnPreset40x50Click(object sender, RoutedEventArgs e)
        {
            var item = new Models.CustomLayoutItem("عنصر 40×50", 40.0m, 50.0m, 1, "", true);
            ViewModel.CustomItems.Add(new CustomLayoutItemViewModel(item));
            ViewModel.UpdateLayoutPlan();
        }

        private void OnPreset50x60Click(object sender, RoutedEventArgs e)
        {
            var item = new Models.CustomLayoutItem("عنصر 50×60", 50.0m, 60.0m, 1, "", true);
            ViewModel.CustomItems.Add(new CustomLayoutItemViewModel(item));
            ViewModel.UpdateLayoutPlan();
        }

        private void OnPreset60x90Click(object sender, RoutedEventArgs e)
        {
            var item = new Models.CustomLayoutItem("عنصر 60×90", 60.0m, 90.0m, 1, "", true);
            ViewModel.CustomItems.Add(new CustomLayoutItemViewModel(item));
            ViewModel.UpdateLayoutPlan();
        }

        private void OnPreset80x100Click(object sender, RoutedEventArgs e)
        {
            var item = new Models.CustomLayoutItem("عنصر 80×100", 80.0m, 100.0m, 1, "", true);
            ViewModel.CustomItems.Add(new CustomLayoutItemViewModel(item));
            ViewModel.UpdateLayoutPlan();
        }
    }
}