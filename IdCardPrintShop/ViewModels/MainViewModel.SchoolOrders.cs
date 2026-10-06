using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdCardPrintShop.Models;
using IdCardPrintShop.Services;
using Microsoft.Win32;

namespace IdCardPrintShop.ViewModels
{
    public partial class MainViewModel
    {
        private ISchoolOrderEngine _schoolOrderEngine = new SchoolOrderEngine();

        // School Orders Collections & Properties
        public ObservableCollection<StudentBatchItem> Students { get; } = new();
        public ObservableCollection<StudentBatchItem> FilteredStudents { get; } = new();
        public ObservableCollection<SchoolPackageTemplate> SchoolTemplates { get; } = new(SchoolPackageTemplate.DefaultTemplates);

        [ObservableProperty]
        private SchoolPackageTemplate? _selectedSchoolTemplate;

        [ObservableProperty]
        private string _schoolSourceFolder = string.Empty;

        [ObservableProperty]
        private string _schoolName = "مدرسة / حضانة جديدة";

        [ObservableProperty]
        private int _schoolTotalFiles;

        [ObservableProperty]
        private int _schoolValidPhotos;

        [ObservableProperty]
        private int _schoolUnsupportedCount;

        [ObservableProperty]
        private int _schoolInvalidCount;

        public ObservableCollection<string> SchoolUnsupportedFiles { get; } = new();
        public ObservableCollection<string> SchoolInvalidFiles { get; } = new();

        [ObservableProperty]
        private bool _schoolHasScannedFolder;

        [ObservableProperty]
        private bool _schoolGenerateIndividualPdfs = false;

        [ObservableProperty]
        private bool _schoolIncludeStudentName = true;

        [ObservableProperty]
        private string _studentSearchQuery = string.Empty;

        [ObservableProperty]
        private string _studentStatusFilter = "All"; // All, Ready, Warnings, Errors, Selected

        [ObservableProperty]
        private string _studentSortOrder = "NameAZ"; // NameAZ, NameZA, Original

        [ObservableProperty]
        private bool _isBatchProcessing;

        [ObservableProperty]
        private double _batchProgressPercentage;

        [ObservableProperty]
        private string _batchProgressText = string.Empty;

        [ObservableProperty]
        private int _batchProcessedCount;

        [ObservableProperty]
        private int _batchTotalCount;

        [ObservableProperty]
        private int _batchSuccessCount;

        [ObservableProperty]
        private int _batchWarningCount;

        [ObservableProperty]
        private int _batchFailedCount;

        [ObservableProperty]
        private bool _isBatchComplete;

        [ObservableProperty]
        private BatchProcessingResult? _lastBatchResult;

        [ObservableProperty]
        private SchoolOrder? _currentSchoolOrder;

        private CancellationTokenSource? _batchCts;

        // Custom template dimensions
        [ObservableProperty]
        private decimal _customSchoolPhotoWidthMm = 40.0m;

        [ObservableProperty]
        private decimal _customSchoolPhotoHeightMm = 60.0m;

        [ObservableProperty]
        private int _customSchoolPhotoQuantity = 8;

        public bool IsSchoolOrdersMode => AppMode == ApplicationMode.SchoolOrders;

        public void InitializeSchoolOrders()
        {
            SelectedSchoolTemplate = SchoolTemplates.FirstOrDefault();
        }

        partial void OnSelectedSchoolTemplateChanged(SchoolPackageTemplate? value)
        {
            if (value != null)
            {
                value.IncludeStudentName = SchoolIncludeStudentName;
            }
            UpdateLayoutPlan();
        }

        partial void OnSchoolIncludeStudentNameChanged(bool value)
        {
            if (SelectedSchoolTemplate != null)
            {
                SelectedSchoolTemplate.IncludeStudentName = value;
            }
            UpdateLayoutPlan();
        }

        partial void OnStudentSearchQueryChanged(string value) => ApplyStudentFilters();
        partial void OnStudentStatusFilterChanged(string value) => ApplyStudentFilters();
        partial void OnStudentSortOrderChanged(string value) => ApplyStudentFilters();

        public void ApplyStudentFilters()
        {
            var query = Students.AsEnumerable();

            // Status Filter
            query = StudentStatusFilter switch
            {
                "Ready" => query.Where(s => s.Status == StudentItemStatus.Ready),
                "Warnings" => query.Where(s => s.Status == StudentItemStatus.Warning),
                "Errors" => query.Where(s => s.Status == StudentItemStatus.Failed),
                "Selected" => query.Where(s => s.IsSelected),
                _ => query // All
            };

            // Search query
            if (!string.IsNullOrWhiteSpace(StudentSearchQuery))
            {
                var q = StudentSearchQuery.Trim();
                query = query.Where(s =>
                    s.StudentName.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
                    s.OriginalFileName.Contains(q, StringComparison.CurrentCultureIgnoreCase));
            }

            // Sorting
            query = StudentSortOrder switch
            {
                "NameZA" => query.OrderByDescending(s => s.StudentName, StringComparer.CurrentCultureIgnoreCase),
                "Original" => query.OrderBy(s => s.OriginalFileName, StringComparer.CurrentCultureIgnoreCase),
                _ => query.OrderBy(s => s.StudentName, StringComparer.CurrentCultureIgnoreCase) // NameAZ
            };

            FilteredStudents.Clear();
            foreach (var s in query)
            {
                FilteredStudents.Add(s);
            }
        }

        [RelayCommand]
        public async Task ImportSchoolFolderAsync(string? folderPath = null)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                var dlg = new OpenFolderDialog
                {
                    Title = "اختر مجلد صور الطلاب",
                    Multiselect = false
                };

                if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.FolderName))
                {
                    folderPath = dlg.FolderName;
                }
            }

            if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
            {
                await ScanSchoolFolderAsync(folderPath);
            }
        }

        public async Task ScanSchoolFolderAsync(string folderPath)
        {
            IsBusy = true;
            BusyMessage = "جاري فحص مجلد الطلاب واكتشاف الصور...";
            SchoolSourceFolder = folderPath;

            try
            {
                var dirName = Path.GetFileName(folderPath);
                if (!string.IsNullOrWhiteSpace(dirName))
                {
                    SchoolName = dirName.Replace('_', ' ').Replace('-', ' ');
                }

                var scanResult = await _schoolOrderEngine.ScanFolderAsync(folderPath);

                Students.Clear();
                SchoolUnsupportedFiles.Clear();
                SchoolInvalidFiles.Clear();

                foreach (var s in scanResult.Students)
                {
                    Students.Add(s);
                }

                foreach (var u in scanResult.UnsupportedFiles)
                {
                    SchoolUnsupportedFiles.Add(u);
                }

                foreach (var inv in scanResult.InvalidImages)
                {
                    SchoolInvalidFiles.Add(inv);
                }

                SchoolTotalFiles = scanResult.TotalFiles;
                SchoolValidPhotos = scanResult.ValidCount;
                SchoolUnsupportedCount = scanResult.UnsupportedCount;
                SchoolInvalidCount = scanResult.InvalidCount;
                SchoolHasScannedFolder = Students.Count > 0;

                ApplyStudentFilters();
                UpdateSchoolOrdersLayoutPlan();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء فحص المجلد:\n{ex.Message}", "خطأ فحص", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task RefreshSchoolScanAsync()
        {
            if (!string.IsNullOrWhiteSpace(SchoolSourceFolder) && Directory.Exists(SchoolSourceFolder))
            {
                await ScanSchoolFolderAsync(SchoolSourceFolder);
            }
        }

        [RelayCommand]
        public void SetStudentFilter(string filter)
        {
            StudentStatusFilter = filter;
        }

        [RelayCommand]
        public void SetStudentSort(string sort)
        {
            StudentSortOrder = sort;
        }

        [RelayCommand]
        public void SelectAllStudents()
        {
            foreach (var s in Students)
            {
                s.IsSelected = true;
            }
            UpdateLayoutPlan();
        }

        [RelayCommand]
        public void DeselectAllStudents()
        {
            foreach (var s in Students)
            {
                s.IsSelected = false;
            }
            UpdateLayoutPlan();
        }

        public void UpdateSchoolOrdersLayoutPlan()
        {
            if (Students.Count == 0 || SelectedSchoolTemplate == null)
            {
                CurrentPlan = null;
                TotalPages = 1;
                CurrentPageIndex = 0;
                CurrentPageItems.Clear();
                UpdatePageNavigationState();
                return;
            }

            // If custom template, update item dimensions from UI
            if (SelectedSchoolTemplate.Id == "tpl-custom" && SelectedSchoolTemplate.Items.Count > 0)
            {
                SelectedSchoolTemplate.Items[0].WidthMm = (double)CustomSchoolPhotoWidthMm;
                SelectedSchoolTemplate.Items[0].HeightMm = (double)CustomSchoolPhotoHeightMm;
                SelectedSchoolTemplate.Items[0].Quantity = CustomSchoolPhotoQuantity;
            }

            SelectedSchoolTemplate.IncludeStudentName = SchoolIncludeStudentName;

            var activeStudents = Students.Where(s => s.IsSelected && s.Status != StudentItemStatus.Failed).ToList();
            CurrentPlan = _schoolOrderEngine.CalculateSchoolLayout(SelectedSchoolTemplate, activeStudents);

            TotalPages = Math.Max(1, CurrentPlan.TotalPages);
            if (CurrentPageIndex >= TotalPages)
            {
                CurrentPageIndex = TotalPages - 1;
            }

            RefreshCurrentPageItems();
            UpdatePageNavigationState();
        }

        [RelayCommand]
        public async Task StartBatchProcessingAsync()
        {
            if (Students.Count == 0 || SelectedSchoolTemplate == null)
            {
                MessageBox.Show("يرجى اختيار مجلد الطلاب وتحديد الباقة أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var active = Students.Where(s => s.IsSelected && s.Status != StudentItemStatus.Failed).ToList();
            if (active.Count == 0)
            {
                MessageBox.Show("لا يوجد طلاب جاهزون ومحددون للمعالجة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBatchProcessing = true;
            IsBatchComplete = false;
            BatchProgressPercentage = 0;
            BatchProgressText = "جاري تهيئة مهمة المعالجة...";
            BatchProcessedCount = 0;
            BatchTotalCount = active.Count;
            BatchSuccessCount = 0;
            BatchWarningCount = 0;
            BatchFailedCount = 0;

            _batchCts = new CancellationTokenSource();

            var order = new SchoolOrder
            {
                SchoolName = SchoolName,
                SourceFolder = SchoolSourceFolder,
                TemplateId = SelectedSchoolTemplate.Id,
                TemplateName = SelectedSchoolTemplate.Name,
                StudentCount = active.Count,
                GenerateIndividualPdfs = SchoolGenerateIndividualPdfs,
                Students = active
            };

            CurrentSchoolOrder = order;

            var progress = new Progress<BatchProgressReport>(report =>
            {
                BatchProgressPercentage = report.Percentage;
                BatchProgressText = $"جاري معالجة: {report.CurrentStudentName} ({report.CurrentIndex} / {report.TotalCount})";
                BatchProcessedCount = report.CurrentIndex;
                BatchSuccessCount = report.SuccessCount;
                BatchWarningCount = report.WarningCount;
                BatchFailedCount = report.FailedCount;
            });

            try
            {
                var result = await _schoolOrderEngine.ProcessBatchAsync(order, SelectedSchoolTemplate, progress, _batchCts.Token);
                LastBatchResult = result;
                IsBatchComplete = true;

                // Update layout preview to reflect processed items
                UpdateSchoolOrdersLayoutPlan();

                MessageBox.Show(
                    $"اكتملت معالجة باقة المدارس بنجاح!\n\n" +
                    $"• ناجح: {result.SuccessCount}\n" +
                    $"• أخطاء: {result.FailedCount}\n" +
                    $"• الصفحات المولدة: {result.PagesGenerated}\n" +
                    $"• ملف الطباعة: {Path.GetFileName(result.CombinedPdfPath)}",
                    "اكتمال الباقة", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("تم إلغاء عملية المعالجة بواسطة المستخدم.", "تم الإلغاء", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء المعالجة الجماعية:\n{ex.Message}", "خطأ معالجة", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBatchProcessing = false;
                _batchCts?.Dispose();
                _batchCts = null;
            }
        }

        [RelayCommand]
        public void CancelBatchProcessing()
        {
            if (_batchCts != null && !_batchCts.IsCancellationRequested)
            {
                _batchCts.Cancel();
                BatchProgressText = "جاري إيقاف المعالجة...";
            }
        }

        [RelayCommand]
        public async Task ReprocessFailedStudentsAsync()
        {
            if (CurrentSchoolOrder == null || SelectedSchoolTemplate == null) return;

            var failed = CurrentSchoolOrder.Students.Where(s => s.Status == StudentItemStatus.Failed).ToList();
            if (failed.Count == 0)
            {
                MessageBox.Show("لا توجد أخطاء لإعادة معالجتها.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsBatchProcessing = true;
            IsBatchComplete = false;
            BatchProgressPercentage = 0;
            BatchProgressText = "إعادة معالجة الحالات السابقة...";
            _batchCts = new CancellationTokenSource();

            var progress = new Progress<BatchProgressReport>(report =>
            {
                BatchProgressPercentage = report.Percentage;
                BatchProgressText = $"إعادة معالجة: {report.CurrentStudentName} ({report.CurrentIndex} / {report.TotalCount})";
                BatchProcessedCount = report.CurrentIndex;
                BatchSuccessCount = report.SuccessCount;
                BatchFailedCount = report.FailedCount;
            });

            try
            {
                var result = await _schoolOrderEngine.ReprocessFailedAsync(CurrentSchoolOrder, SelectedSchoolTemplate, progress, _batchCts.Token);
                LastBatchResult = result;
                IsBatchComplete = true;
                UpdateSchoolOrdersLayoutPlan();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء إعادة المعالجة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBatchProcessing = false;
                _batchCts?.Dispose();
                _batchCts = null;
            }
        }

        [RelayCommand]
        public void OpenSchoolOutputFolder()
        {
            string? dir = LastBatchResult?.OutputDirectory ?? CurrentSchoolOrder?.OutputFolder;
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }

        [RelayCommand]
        public void OpenSchoolCombinedPdf()
        {
            string? pdfPath = LastBatchResult?.CombinedPdfPath ?? CurrentSchoolOrder?.CombinedPdfPath;
            if (!string.IsNullOrWhiteSpace(pdfPath) && File.Exists(pdfPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = pdfPath,
                    UseShellExecute = true
                });
            }
        }

        [RelayCommand]
        public void ResetSchoolOrder()
        {
            Students.Clear();
            FilteredStudents.Clear();
            SchoolUnsupportedFiles.Clear();
            SchoolInvalidFiles.Clear();
            SchoolSourceFolder = string.Empty;
            SchoolName = "مدرسة / حضانة جديدة";
            SchoolTotalFiles = 0;
            SchoolValidPhotos = 0;
            SchoolUnsupportedCount = 0;
            SchoolInvalidCount = 0;
            SchoolHasScannedFolder = false;
            IsBatchProcessing = false;
            IsBatchComplete = false;
            LastBatchResult = null;
            CurrentSchoolOrder = null;
            UpdateLayoutPlan();
        }
    }
}
