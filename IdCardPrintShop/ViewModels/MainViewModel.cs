using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdCardPrintShop.Models;
using IdCardPrintShop.Services;
using Microsoft.Win32;
using OpenCvSharp;
namespace IdCardPrintShop.ViewModels
{
    public enum ApplicationMode
    {
        IdCards = 0,
        PersonalPhotos = 1,
        GenericDocuments = 2,
        CustomSize = 3,
        SchoolOrders = 4
    }

    public partial class MainViewModel : ObservableObject
    {
        private readonly IImageProcessingService _imageService;
        private readonly ILayoutEngine _layoutEngine;
        private readonly IPdfExportService _pdfService;
        private readonly IJobPersistenceService _jobService;
        private readonly IPersonalPhotoProcessor _photoProcessor;
        private readonly IDocumentInspectionService _docInspectionService;
        private readonly IGenericDocumentExportService _genericExportService;

        // Current Application Mode
        [ObservableProperty]
        private ApplicationMode _appMode = ApplicationMode.IdCards;

        public bool IsIdCardMode => AppMode == ApplicationMode.IdCards;
        public bool IsPersonalPhotoMode => AppMode == ApplicationMode.PersonalPhotos;
        public bool IsGenericDocumentsMode => AppMode == ApplicationMode.GenericDocuments;
        public bool IsCustomSizeMode => AppMode == ApplicationMode.CustomSize;

        private void NotifyModeProperties()
        {
            OnPropertyChanged(nameof(IsIdCardMode));
            OnPropertyChanged(nameof(IsPersonalPhotoMode));
            OnPropertyChanged(nameof(IsGenericDocumentsMode));
            OnPropertyChanged(nameof(IsCustomSizeMode));
            OnPropertyChanged(nameof(IsSchoolOrdersMode));
        }

        [RelayCommand]
        public void SwitchToIdCards()
        {
            AppMode = ApplicationMode.IdCards;
            NotifyModeProperties();
            UpdateLayoutPlan();
        }

        [RelayCommand]
        public void SwitchToPersonalPhotos()
        {
            AppMode = ApplicationMode.PersonalPhotos;
            NotifyModeProperties();
            UpdateLayoutPlan();
        }

        [RelayCommand]
        public void SwitchToGenericDocuments()
        {
            AppMode = ApplicationMode.GenericDocuments;
            NotifyModeProperties();
            UpdateLayoutPlan();
        }

        [RelayCommand]
        public void SwitchToCustomSize()
        {
            AppMode = ApplicationMode.CustomSize;
            NotifyModeProperties();
            UpdateLayoutPlan();
        }

        [RelayCommand]
        public void SwitchToSchoolOrders()
        {
            AppMode = ApplicationMode.SchoolOrders;
            NotifyModeProperties();
            UpdateLayoutPlan();
        }

        // Order Information
        [ObservableProperty]
        private string _orderNumber = $"#{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}";

        [ObservableProperty]
        private string _customerName = "عميل جديد";

        [ObservableProperty]
        private string _notes = string.Empty;

        // Processing / State
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _busyMessage = string.Empty;

        [ObservableProperty]
        private string _detectionNotice = string.Empty;

        [ObservableProperty]
        private bool _isNoticeWarning;

        // Cards Collection (ID Cards)
        public ObservableCollection<CardItemViewModel> Cards { get; } = new();

        [ObservableProperty]
        private CardItemViewModel? _selectedCard;

        [ObservableProperty]
        private bool _hasCards;

        // Personal Photos Collection (Studio)
        public ObservableCollection<PersonalPhotoItemViewModel> PersonalPhotos { get; } = new();

        [ObservableProperty]
        private PersonalPhotoItemViewModel? _selectedPersonalPhoto;

        [ObservableProperty]
        private bool _hasPersonalPhotos;

        public ObservableCollection<PhotoProfile> PhotoProfiles { get; } = new();

        [ObservableProperty]
        private PhotoProfile? _selectedPhotoProfile;

        [ObservableProperty]
        private int _personalPhotoCopies = 8;

        // Generic Documents Collection
        public ObservableCollection<DocumentItemViewModel> Documents { get; } = new();

        [ObservableProperty]
        private DocumentItemViewModel? _selectedDocument;

        [ObservableProperty]
        private bool _hasDocuments;

        [ObservableProperty]
        private PrintOrderSummary _orderSummary = new();

        // Layout & Print Settings
        public ObservableCollection<LayoutTemplate> Templates { get; } = new();

        [ObservableProperty]
        private LayoutTemplate? _selectedTemplate;

        [ObservableProperty]
        private int _copies = 1;

        [ObservableProperty]
        private bool _showCutMarks = true;

        [ObservableProperty]
        private bool _drawBorderBox = true;

        // Sheet Preview State
        [ObservableProperty]
        private LayoutPlan? _currentPlan;

        [ObservableProperty]
        private int _currentPageIndex = 0;

        [ObservableProperty]
        private int _totalPages = 1;

        [ObservableProperty]
        private string _pageIndicatorText = "صفحة 1 من 1";

        [ObservableProperty]
        private bool _canGoNextPage;

        [ObservableProperty]
        private bool _canGoPrevPage;

        public ObservableCollection<LayoutItem> CurrentPageItems { get; } = new();

        // Custom Size Workflow Collections & Properties
        public ObservableCollection<CustomLayoutItemViewModel> CustomItems { get; } = new();

        [ObservableProperty]
        private CustomLayoutItemViewModel? _selectedCustomItem;

        [ObservableProperty]
        private LayoutItem? _selectedPlanItem;

        public ObservableCollection<CustomSizePreset> CustomSizePresets { get; } = new(CustomSizePreset.DefaultPresets);

        public ObservableCollection<PaperSize> CustomPaperSizes { get; } = new(new[] { PaperSize.A4, PaperSize.A5, PaperSize.A3 });

        [ObservableProperty]
        private PaperSize _customPaperSize = PaperSize.A4;

        [ObservableProperty]
        private CustomLayoutOrientationMode _customOrientationMode = CustomLayoutOrientationMode.Auto;

        [ObservableProperty]
        private decimal _customSafetyMarginMm = 3.0m;

        [ObservableProperty]
        private decimal _customSpacingMm = 2.0m;

        [ObservableProperty]
        private bool _customShowCutMarks = true;

        [ObservableProperty]
        private bool _customDrawBorderBox = true;

        [ObservableProperty]
        private int _customTotalItemsCount;

        [ObservableProperty]
        private int _customTotalCopiesCount;

        partial void OnCustomPaperSizeChanged(PaperSize value) => UpdateCustomSizeLayoutPlan();
        partial void OnCustomOrientationModeChanged(CustomLayoutOrientationMode value) => UpdateCustomSizeLayoutPlan();
        partial void OnCustomSafetyMarginMmChanged(decimal value) => UpdateCustomSizeLayoutPlan();
        partial void OnCustomSpacingMmChanged(decimal value) => UpdateCustomSizeLayoutPlan();
        partial void OnCustomShowCutMarksChanged(bool value) => UpdateCustomSizeLayoutPlan();
        partial void OnCustomDrawBorderBoxChanged(bool value) => UpdateCustomSizeLayoutPlan();

        // PDF Export
        [ObservableProperty]
        private string _lastExportedPdfPath = string.Empty;

        [ObservableProperty]
        private bool _hasExportedPdf;

        // Manual Adjustment modal triggers
        public event Action<ManualAdjustmentViewModel>? OpenManualAdjustmentRequested;
        public event Action<ManualPersonalPhotoAdjustmentViewModel>? OpenPersonalPhotoAdjustmentRequested;

        public MainViewModel(
            IImageProcessingService imageService,
            ILayoutEngine layoutEngine,
            IPdfExportService pdfService,
            IJobPersistenceService jobService,
            IPersonalPhotoProcessor? photoProcessor = null,
            IDocumentInspectionService? docInspectionService = null,
            IGenericDocumentExportService? genericExportService = null,
            ISchoolOrderEngine? schoolOrderEngine = null)
        {
            _imageService = imageService;
            _layoutEngine = layoutEngine;
            _pdfService = pdfService;
            _jobService = jobService;
            _docInspectionService = docInspectionService ?? new DocumentInspectionService();
            _genericExportService = genericExportService ?? new GenericDocumentExportService();
            _schoolOrderEngine = schoolOrderEngine ?? new SchoolOrderEngine();

            var faceService = new OpenCvFaceDetectionService();
            var bgService = new SmartBackgroundRemovalService();
            var colorService = new PhotoCorrectionService();
            _photoProcessor = photoProcessor ?? new PersonalPhotoProcessor(faceService, bgService, colorService, imageService);

            // Load templates
            foreach (var t in LayoutTemplate.GetDefaultTemplates())
            {
                Templates.Add(t);
            }
            SelectedTemplate = Templates.FirstOrDefault();

            // Load Photo Profiles
            foreach (var p in PhotoProfile.DefaultProfiles)
            {
                PhotoProfiles.Add(p);
            }
            SelectedPhotoProfile = PhotoProfiles.FirstOrDefault();

            InitializeSchoolOrders();
        }

        partial void OnAppModeChanged(ApplicationMode value)
        {
            UpdateLayoutPlan();
        }

        partial void OnSelectedPhotoProfileChanged(PhotoProfile? value)
        {
            if (value != null && SelectedPersonalPhoto != null)
            {
                SelectedPersonalPhoto.Profile = value;
                SelectedPersonalPhoto.RefreshRenderedPreview();
            }
            UpdateLayoutPlan();
        }

        partial void OnSelectedPersonalPhotoChanged(PersonalPhotoItemViewModel? value)
        {
            foreach (var p in PersonalPhotos)
            {
                p.IsSelected = (p == value);
            }
            if (value != null)
            {
                PersonalPhotoCopies = value.Copies;
                if (value.Profile != null)
                {
                    SelectedPhotoProfile = value.Profile;
                }
            }
            UpdateLayoutPlan();
        }

        partial void OnSelectedDocumentChanged(DocumentItemViewModel? value)
        {
            foreach (var doc in Documents)
            {
                doc.IsSelected = (doc == value);
            }
            UpdateLayoutPlan();
        }

        partial void OnPersonalPhotoCopiesChanged(int value)
        {
            if (SelectedPersonalPhoto != null)
            {
                SelectedPersonalPhoto.Copies = value;
                SelectedPersonalPhoto.Item.Copies = value;
            }
            UpdateLayoutPlan();
        }

        partial void OnSelectedTemplateChanged(LayoutTemplate? value)
        {
            UpdateLayoutPlan();
        }

        partial void OnCopiesChanged(int value)
        {
            UpdateLayoutPlan();
        }

        partial void OnShowCutMarksChanged(bool value)
        {
            if (SelectedTemplate != null)
            {
                SelectedTemplate.ShowCutMarks = value;
            }
            UpdateLayoutPlan();
        }

        partial void OnDrawBorderBoxChanged(bool value)
        {
            if (SelectedTemplate != null)
            {
                SelectedTemplate.DrawBorderBox = value;
            }
            UpdateLayoutPlan();
        }

        partial void OnCurrentPageIndexChanged(int value)
        {
            RefreshCurrentPageItems();
        }

        [RelayCommand]
        public async Task ImportImagesAsync()
        {
            var dlg = new OpenFileDialog
            {
                Title = "اختر صورة أو صورتي البطاقة (Front / Back)",
                Filter = "صور البطاقات والمستندات (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|كل الملفات (*.*)|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
            {
                // Append if cards already exist (additive workflow)
                if (Cards.Count > 0)
                    await AppendImportedFilesAsync(dlg.FileNames);
                else
                    await ProcessImportedFilesAsync(dlg.FileNames);
            }
        }

        [RelayCommand]
        public async Task ImportAdditionalImagesAsync()
        {
            var dlg = new OpenFileDialog
            {
                Title = "إضافة صور بطاقات أو مستندات إضافية",
                Filter = "ملفات الصور|*.jpg;*.jpeg;*.png;*.bmp;*.webp|كل الملفات|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
            {
                await AppendImportedFilesAsync(dlg.FileNames);
            }
        }

        public async Task AppendImportedFilesAsync(string[] filePaths)
        {
            await ProcessImportedFilesInternalAsync(filePaths, append: true);
        }

        public async Task ProcessImportedFilesAsync(string[] filePaths)
        {
            await ProcessImportedFilesInternalAsync(filePaths, append: false);
        }

        private async Task ProcessImportedFilesInternalAsync(string[] filePaths, bool append)
        {
            IsBusy = true;
            BusyMessage = "جاري فحص الصور واكتشاف حدود البطاقة تلقائياً...";

            try
            {
                if (!append)
                {
                    Cards.Clear();
                }

                var detectedRegions = new List<CardRegion>();

                await Task.Run(() =>
                {
                    if (filePaths.Length == 1)
                    {
                        var result = _imageService.DetectCards(filePaths[0]);

                        if (!result.IsConfident)
                        {
                            IsNoticeWarning = true;
                            DetectionNotice = result.Message;
                        }
                        else
                        {
                            IsNoticeWarning = false;
                            DetectionNotice = result.Message;
                        }

                        // If appending to an existing single front card, suggest Back role
                        if (append && Cards.Count == 1 && result.DetectedCards.Count == 1)
                        {
                            var c = result.DetectedCards[0];
                            c.Role = CardRole.Back;
                            c.Label = "الظهر (Back)";
                        }

                        detectedRegions.AddRange(result.DetectedCards);
                    }
                    else if (filePaths.Length == 2)
                    {
                        IsNoticeWarning = false;
                        DetectionNotice = "تم استيراد صورتين (الوجه والظهر).";

                        for (int i = 0; i < filePaths.Length; i++)
                        {
                            var result = _imageService.DetectCards(filePaths[i]);
                            if (result.DetectedCards.Count == 0)
                            {
                                var fallbackCard = new CardRegion
                                {
                                    Id = $"Card_{Cards.Count + detectedRegions.Count + 1}",
                                    SourceImagePath = filePaths[i],
                                    Corners = _imageService.GetFallbackCardCorners(result.ImageWidth, result.ImageHeight),
                                    Role = (i == 0) ? CardRole.Front : CardRole.Back,
                                    Label = (i == 0) ? "الوجه (Front)" : "الظهر (Back)",
                                    StatusMessage = "تحديد يدوي"
                                };
                                detectedRegions.Add(fallbackCard);
                            }
                            else
                            {
                                for (int k = 0; k < result.DetectedCards.Count; k++)
                                {
                                    var card = result.DetectedCards[k];
                                    if (result.DetectedCards.Count == 1)
                                    {
                                        card.Role = (i == 0) ? CardRole.Front : CardRole.Back;
                                        card.Label = (i == 0) ? "الوجه (Front)" : "الظهر (Back)";
                                    }
                                    detectedRegions.Add(card);
                                }
                            }
                        }
                    }
                    else
                    {
                        // Multi-file batch (> 2 files)
                        IsNoticeWarning = false;

                        for (int i = 0; i < filePaths.Length; i++)
                        {
                            var result = _imageService.DetectCards(filePaths[i]);
                            if (result.DetectedCards.Count == 0)
                            {
                                var fallbackCard = new CardRegion
                                {
                                    Id = $"Card_{Cards.Count + detectedRegions.Count + 1}",
                                    SourceImagePath = filePaths[i],
                                    Corners = _imageService.GetFallbackCardCorners(result.ImageWidth, result.ImageHeight),
                                    Label = $"مستند {Cards.Count + detectedRegions.Count + 1}",
                                    StatusMessage = "تحديد يدوي"
                                };
                                detectedRegions.Add(fallbackCard);
                            }
                            else
                            {
                                foreach (var card in result.DetectedCards)
                                {
                                    detectedRegions.Add(card);
                                }
                            }
                        }

                        // If even number of files with 1 card each, pair as Front and Back:
                        if (filePaths.Length % 2 == 0 && detectedRegions.Count == filePaths.Length)
                        {
                            for (int p = 0; p < detectedRegions.Count; p += 2)
                            {
                                int pairNum = (p / 2) + 1;
                                detectedRegions[p].Role = CardRole.Front;
                                detectedRegions[p].Label = $"بطاقة {pairNum} - الوجه";
                                detectedRegions[p + 1].Role = CardRole.Back;
                                detectedRegions[p + 1].Label = $"بطاقة {pairNum} - الظهر";
                            }
                            DetectionNotice = $"تم استيراد {filePaths.Length} ملفات واكتشاف {detectedRegions.Count} بطاقات بنجاح (مرتبة كأزواج وجه وظهر).";
                        }
                        else
                        {
                            DetectionNotice = $"تم استيراد {filePaths.Length} ملفات واكتشاف {detectedRegions.Count} بطاقات/مستندات بنجاح.";
                        }
                    }
                });

                foreach (var r in detectedRegions)
                {
                    var vm = new CardItemViewModel(r, _imageService);
                    Cards.Add(vm);
                }

                HasCards = Cards.Count > 0;
                SelectedCard = Cards.FirstOrDefault();

                // If any detected card is a passport, auto-select a passport template if not already selected
                if (detectedRegions.Any(r => r.DocumentType == CardDocumentType.Passport) &&
                    SelectedTemplate != null && !SelectedTemplate.Id.Contains("passport"))
                {
                    var passTemplate = Templates.FirstOrDefault(t => t.Id == "a4_passport_single");
                    if (passTemplate != null)
                    {
                        SelectedTemplate = passTemplate;
                    }
                }

                UpdateLayoutPlan();
            }
            catch (Exception ex)
            {
                IsNoticeWarning = true;
                DetectionNotice = $"حدث خطأ أثناء فحص الصورة: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void SwapRoles()
        {
            if (Cards.Count < 2) return;

            var role0 = Cards[0].Role;
            var role1 = Cards[1].Role;

            Cards[0].SetRole(role1);
            Cards[1].SetRole(role0);

            UpdateLayoutPlan();
        }

        [RelayCommand]
        public void ClearCards()
        {
            Cards.Clear();
            HasCards = false;
            DetectionNotice = string.Empty;
            CurrentPlan = null!;
        }

        [RelayCommand]
        public void SetCopiesPreset(object? parameter)
        {
            if (parameter is int i)
            {
                Copies = i;
            }
            else if (parameter != null && int.TryParse(parameter.ToString(), out int parsed))
            {
                Copies = parsed;
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
        public void OpenManualAdjustment(CardItemViewModel? cardVm)
        {
            cardVm ??= SelectedCard;
            if (cardVm == null) return;

            var adjustVm = new ManualAdjustmentViewModel(cardVm, _imageService);
            adjustVm.RequestClose += () =>
            {
                UpdateLayoutPlan();
            };

            OpenManualAdjustmentRequested?.Invoke(adjustVm);
        }

        [RelayCommand]
        public async Task ImportPersonalPhotosAsync()
        {
            var dlg = new OpenFileDialog
            {
                Title = "اختر صور شخصية للعميل (صورة واحدة أو دفعة صور)",
                Filter = "صور شخصية ومستندات (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|كل الملفات (*.*)|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
            {
                await ProcessImportedPersonalPhotosAsync(dlg.FileNames);
            }
        }

        public async Task ProcessImportedPersonalPhotosAsync(string[] filePaths)
        {
            IsBusy = true;
            BusyMessage = "جاري فحص الصور الشخصية، اكتشاف الوجوه وتجهيز الخلفية...";

            try
            {
                var profile = SelectedPhotoProfile ?? PhotoProfile.Standard4x6;

                foreach (var path in filePaths)
                {
                    var item = await _photoProcessor.ProcessPhotoAsync(path, profile);
                    var vm = new PersonalPhotoItemViewModel(item, _photoProcessor, _imageService);
                    PersonalPhotos.Add(vm);
                }

                HasPersonalPhotos = PersonalPhotos.Count > 0;
                SelectedPersonalPhoto = PersonalPhotos.FirstOrDefault();
                AppMode = ApplicationMode.PersonalPhotos;
                UpdateLayoutPlan();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء معالجة الصور: {ex.Message}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void OpenManualPersonalPhotoAdjustment(PersonalPhotoItemViewModel? itemVm)
        {
            itemVm ??= SelectedPersonalPhoto;
            if (itemVm == null) return;

            var adjustVm = new ManualPersonalPhotoAdjustmentViewModel(itemVm, _photoProcessor, _imageService);
            adjustVm.RequestClose += () =>
            {
                UpdateLayoutPlan();
            };

            OpenPersonalPhotoAdjustmentRequested?.Invoke(adjustVm);
        }

        [RelayCommand]
        public void SetPersonalPhotoCopiesPreset(object? parameter)
        {
            if (parameter is int i)
            {
                PersonalPhotoCopies = i;
            }
            else if (parameter != null && int.TryParse(parameter.ToString(), out int parsed))
            {
                PersonalPhotoCopies = parsed;
            }
        }

        [RelayCommand]
        public void IncrementPersonalPhotoCopies()
        {
            PersonalPhotoCopies++;
        }

        [RelayCommand]
        public void DecrementPersonalPhotoCopies()
        {
            if (PersonalPhotoCopies > 1) PersonalPhotoCopies--;
        }

        [RelayCommand]
        public void SelectPersonalPhoto(PersonalPhotoItemViewModel? photo)
        {
            if (photo != null)
            {
                SelectedPersonalPhoto = photo;
            }
        }

        [RelayCommand]
        public void SetPersonalPhotoBackground(string bgTypeStr)
        {
            if (SelectedPersonalPhoto == null) return;
            if (Enum.TryParse<PhotoBackgroundType>(bgTypeStr, true, out var bgType))
            {
                SelectedPersonalPhoto.Item.Parameters.BackgroundType = bgType;
                SelectedPersonalPhoto.RefreshRenderedPreview();
                UpdateLayoutPlan();
            }
        }

        // Generic Documents Commands
        [RelayCommand]
        public async Task ImportDocumentsAsync(string[]? filePaths = null)
        {
            if (filePaths == null || filePaths.Length == 0)
            {
                var ofd = new OpenFileDialog
                {
                    Title = "استيراد مستندات للطباعة (PDF, Images, DOCX)",
                    Filter = "ملفات مدعومة (*.pdf;*.jpg;*.jpeg;*.png;*.docx)|*.pdf;*.jpg;*.jpeg;*.png;*.docx|ملفات PDF (*.pdf)|*.pdf|ملفات صور (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|ملفات Word (*.docx)|*.docx|جميع الملفات (*.*)|*.*",
                    Multiselect = true
                };

                if (ofd.ShowDialog() != true) return;
                filePaths = ofd.FileNames;
            }

            if (filePaths == null || filePaths.Length == 0) return;

            IsBusy = true;
            BusyMessage = "جاري فحص المستندات وإعداد خيارات الطباعة...";

            try
            {
                var inspected = await _docInspectionService.InspectFilesAsync(filePaths);

                foreach (var docItem in inspected)
                {
                    var vm = new DocumentItemViewModel(docItem);
                    vm.SettingsChanged += () =>
                    {
                        RefreshOrderSummary();
                        UpdateLayoutPlan();
                    };
                    Documents.Add(vm);
                }

                HasDocuments = Documents.Count > 0;
                SelectedDocument ??= Documents.FirstOrDefault();

                RefreshOrderSummary();
                UpdateLayoutPlan();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء فحص المستندات:\n{ex.Message}", "خطأ في الاستيراد", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void MoveDocumentUp(DocumentItemViewModel? doc)
        {
            var target = doc ?? SelectedDocument;
            if (target == null) return;
            int idx = Documents.IndexOf(target);
            if (idx > 0)
            {
                Documents.Move(idx, idx - 1);
                SelectedDocument = target;
                RefreshOrderSummary();
                UpdateLayoutPlan();
            }
        }

        [RelayCommand]
        public void MoveDocumentDown(DocumentItemViewModel? doc)
        {
            var target = doc ?? SelectedDocument;
            if (target == null) return;
            int idx = Documents.IndexOf(target);
            if (idx >= 0 && idx < Documents.Count - 1)
            {
                Documents.Move(idx, idx + 1);
                SelectedDocument = target;
                RefreshOrderSummary();
                UpdateLayoutPlan();
            }
        }

        [RelayCommand]
        public void RemoveDocument(DocumentItemViewModel? doc)
        {
            var target = doc ?? SelectedDocument;
            if (target == null) return;

            int idx = Documents.IndexOf(target);
            Documents.Remove(target);
            HasDocuments = Documents.Count > 0;

            if (SelectedDocument == target)
            {
                if (Documents.Count > 0)
                {
                    int nextIdx = Math.Min(idx, Documents.Count - 1);
                    SelectedDocument = Documents[nextIdx];
                }
                else
                {
                    SelectedDocument = null;
                }
            }

            RefreshOrderSummary();
            UpdateLayoutPlan();
        }

        [RelayCommand]
        public void DuplicateDocument(DocumentItemViewModel? doc)
        {
            var target = doc ?? SelectedDocument;
            if (target == null) return;

            var cloneItem = new DocumentItem
            {
                FileName = target.FileName,
                OriginalPath = target.Item.OriginalPath,
                FileType = target.FileType,
                PageCount = target.PageCount,
                PaperSizeName = target.PaperSizeName,
                PrintMode = target.PrintMode,
                Copies = target.Copies,
                Orientation = target.Orientation,
                Scaling = target.Scaling,
                PageRange = target.PageRange,
                Status = target.Status,
                StatusMessage = target.StatusMessage,
                ConvertedPdfPath = target.Item.ConvertedPdfPath,
                ResolvedPageIndices = new List<int>(target.Item.ResolvedPageIndices)
            };

            var cloneVm = new DocumentItemViewModel(cloneItem);
            cloneVm.SettingsChanged += () =>
            {
                RefreshOrderSummary();
                UpdateLayoutPlan();
            };

            int idx = Documents.IndexOf(target);
            Documents.Insert(idx + 1, cloneVm);
            SelectedDocument = cloneVm;

            RefreshOrderSummary();
            UpdateLayoutPlan();
        }

        [RelayCommand]
        public void DuplicateSettings(DocumentItemViewModel? source)
        {
            var src = source ?? SelectedDocument;
            if (src == null) return;

            var targets = Documents.Where(d => d.IsSelected).ToList();
            if (targets.Count <= 1)
            {
                targets = Documents.Where(d => d != src).ToList();
            }
            else
            {
                targets = targets.Where(d => d != src).ToList();
            }

            foreach (var doc in targets)
            {
                doc.PaperSizeName = src.PaperSizeName;
                doc.PrintMode = src.PrintMode;
                doc.Copies = src.Copies;
                doc.Orientation = src.Orientation;
                doc.Scaling = src.Scaling;
            }

            RefreshOrderSummary();
            UpdateLayoutPlan();
        }

        [RelayCommand]
        public void BatchApplyPaperSize(string paper)
        {
            var targets = Documents.Where(d => d.IsSelected).ToList();
            if (targets.Count == 0) targets = Documents.ToList();

            foreach (var doc in targets)
            {
                doc.PaperSizeName = paper;
            }

            RefreshOrderSummary();
            UpdateLayoutPlan();
        }

        [RelayCommand]
        public void BatchApplyPrintMode(string modeStr)
        {
            var targets = Documents.Where(d => d.IsSelected).ToList();
            if (targets.Count == 0) targets = Documents.ToList();

            var mode = modeStr.Equals("color", StringComparison.OrdinalIgnoreCase) 
                ? PrintColorMode.Color 
                : PrintColorMode.BlackAndWhite;

            foreach (var doc in targets)
            {
                doc.PrintMode = mode;
            }

            RefreshOrderSummary();
            UpdateLayoutPlan();
        }

        [RelayCommand]
        public void BatchApplyCopies(object? parameter)
        {
            int count = 1;
            if (parameter is int i)
            {
                count = i;
            }
            else if (parameter != null && int.TryParse(parameter.ToString(), out int parsed))
            {
                count = parsed;
            }

            var targets = Documents.Where(d => d.IsSelected).ToList();
            if (targets.Count == 0) targets = Documents.ToList();

            foreach (var doc in targets)
            {
                doc.Copies = count;
            }

            RefreshOrderSummary();
            UpdateLayoutPlan();
        }

        [RelayCommand]
        public void BatchApplyOrientation(string orientStr)
        {
            var targets = Documents.Where(d => d.IsSelected).ToList();
            if (targets.Count == 0) targets = Documents.ToList();

            var orient = orientStr.ToLowerInvariant() switch
            {
                "landscape" or "أفقي" => DocumentOrientationMode.Landscape,
                "portrait" or "رأسي" => DocumentOrientationMode.Portrait,
                _ => DocumentOrientationMode.Auto
            };

            foreach (var doc in targets)
            {
                doc.Orientation = orient;
            }

            RefreshOrderSummary();
            UpdateLayoutPlan();
        }

        [RelayCommand]
        public void SelectAllDocuments()
        {
            foreach (var doc in Documents)
            {
                doc.IsSelected = true;
            }
        }

        [RelayCommand]
        public void DeselectAllDocuments()
        {
            foreach (var doc in Documents)
            {
                doc.IsSelected = false;
            }
        }

        [RelayCommand]
        public void ClearDocuments()
        {
            Documents.Clear();
            HasDocuments = false;
            SelectedDocument = null;
            RefreshOrderSummary();
            UpdateLayoutPlan();
        }

        public void UpdateLayoutPlan()
        {
            if (AppMode == ApplicationMode.PersonalPhotos)
            {
                UpdatePersonalPhotoLayoutPlan();
                return;
            }

            if (AppMode == ApplicationMode.GenericDocuments)
            {
                UpdateGenericDocumentLayoutPlan();
                return;
            }

            if (AppMode == ApplicationMode.CustomSize)
            {
                UpdateCustomSizeLayoutPlan();
                return;
            }

            if (AppMode == ApplicationMode.SchoolOrders)
            {
                UpdateSchoolOrdersLayoutPlan();
                return;
            }

            if (Cards.Count == 0 || SelectedTemplate == null)
            {
                CurrentPlan = null;
                TotalPages = 1;
                CurrentPageIndex = 0;
                CurrentPageItems.Clear();
                UpdatePageNavigationState();
                return;
            }

            var cardRegions = Cards.Select(c => c.Region).ToList();
            CurrentPlan = _layoutEngine.CalculateLayout(cardRegions, SelectedTemplate, Copies);

            TotalPages = Math.Max(1, CurrentPlan.TotalPages);
            if (CurrentPageIndex >= TotalPages)
            {
                CurrentPageIndex = TotalPages - 1;
            }

            RefreshCurrentPageItems();
            UpdatePageNavigationState();
        }

        private void UpdatePersonalPhotoLayoutPlan()
        {
            if (PersonalPhotos.Count == 0 || SelectedPersonalPhoto == null)
            {
                CurrentPlan = null;
                TotalPages = 1;
                CurrentPageIndex = 0;
                CurrentPageItems.Clear();
                UpdatePageNavigationState();
                return;
            }

            var profile = SelectedPersonalPhoto.Profile ?? PhotoProfile.Standard4x6;
            int copies = PersonalPhotoCopies;

            var photoRegion = new CardRegion
            {
                Id = SelectedPersonalPhoto.Item.Id,
                Label = SelectedPersonalPhoto.DisplayName,
                Role = CardRole.Single,
                SourceImagePath = SelectedPersonalPhoto.Item.SourceImagePath
            };

            var photoTemplate = new LayoutTemplate
            {
                Id = "personal_photo_grid",
                Name = $"A4 - {profile.Name}",
                Mode = LayoutMode.GridCopies,
                PaperSize = PaperSize.A4,
                Orientation = PaperOrientation.Portrait,
                CardDimensions = new CardDimensions(profile.Name, profile.WidthMm, profile.HeightMm),
                MarginLeftMm = 10.0,
                MarginRightMm = 10.0,
                MarginTopMm = 10.0,
                MarginBottomMm = 10.0,
                SpacingX_Mm = 5.0,
                SpacingY_Mm = 5.0,
                ShowCutMarks = ShowCutMarks,
                DrawBorderBox = DrawBorderBox
            };

            CurrentPlan = _layoutEngine.CalculateLayout(new[] { photoRegion }, photoTemplate, copies);

            TotalPages = Math.Max(1, CurrentPlan.TotalPages);
            if (CurrentPageIndex >= TotalPages)
            {
                CurrentPageIndex = TotalPages - 1;
            }

            RefreshCurrentPageItems();
            UpdatePageNavigationState();
        }

        private void UpdateGenericDocumentLayoutPlan()
        {
            if (Documents.Count == 0 || SelectedDocument == null)
            {
                CurrentPlan = null;
                TotalPages = 1;
                CurrentPageIndex = 0;
                CurrentPageItems.Clear();
                UpdatePageNavigationState();
                return;
            }

            var doc = SelectedDocument;
            var paper = PaperSize.AllStandardSizes.FirstOrDefault(p => p.Name.Equals(doc.PaperSizeName, StringComparison.OrdinalIgnoreCase)) ?? PaperSize.A4;
            var effPaperOrientation = doc.Item.ResolveEffectiveOrientation();
            var (paperW, paperH) = paper.GetDimensions(effPaperOrientation);

            int pagesInDoc = doc.Item.ResolvedPageIndices.Count > 0 ? doc.Item.ResolvedPageIndices.Count : Math.Max(1, doc.PageCount);
            TotalPages = Math.Max(1, pagesInDoc);
            if (CurrentPageIndex >= TotalPages)
            {
                CurrentPageIndex = 0;
            }

            var plan = new LayoutPlan
            {
                PaperSize = paper,
                Orientation = effPaperOrientation,
                TotalPages = TotalPages,
                DrawBorderBox = true,
                ShowCutMarks = false
            };

            for (int p = 0; p < TotalPages; p++)
            {
                var item = new LayoutItem
                {
                    CardRegionId = doc.Item.Id,
                    PageIndex = p,
                    CopyIndex = doc.Copies,
                    Role = CardRole.Single,
                    X_Mm = 0.0,
                    Y_Mm = 0.0,
                    Width_Mm = paperW,
                    Height_Mm = paperH
                };
                plan.Items.Add(item);
            }

            CurrentPlan = plan;
            RefreshCurrentPageItems();
            UpdatePageNavigationState();
        }

        private void UpdateCustomSizeLayoutPlan()
        {
            RefreshCustomSizeCounts();

            if (CustomItems.Count == 0)
            {
                CurrentPlan = null;
                TotalPages = 1;
                CurrentPageIndex = 0;
                CurrentPageItems.Clear();
                UpdatePageNavigationState();
                return;
            }

            try
            {
                var parameters = new CustomSizeLayoutParameters
                {
                    PaperSize = CustomPaperSize,
                    OrientationMode = CustomOrientationMode,
                    SafetyMarginMm = CustomSafetyMarginMm,
                    SpacingMm = CustomSpacingMm,
                    ShowCutMarks = CustomShowCutMarks,
                    DrawBorderBox = CustomDrawBorderBox
                };

                var itemsList = CustomItems.Select(ci => ci.Item).ToList();
                CurrentPlan = _layoutEngine.CalculateCustomSizeLayout(itemsList, parameters);

                TotalPages = Math.Max(1, CurrentPlan.TotalPages);
                if (CurrentPageIndex >= TotalPages)
                {
                    CurrentPageIndex = TotalPages - 1;
                }

                RefreshCurrentPageItems();
                UpdatePageNavigationState();
            }
            catch (Exception ex)
            {
                DetectionNotice = ex.Message;
                IsNoticeWarning = true;
            }
        }

        public void RefreshCustomSizeCounts()
        {
            CustomTotalItemsCount = CustomItems.Count;
            CustomTotalCopiesCount = CustomItems.Sum(c => c.Copies);
        }

        [RelayCommand]
        public void AddCustomImage(string[]? filePaths = null)
        {
            if (filePaths == null || filePaths.Length == 0)
            {
                var ofd = new OpenFileDialog
                {
                    Title = "إضافة صورة لمقاس مخصص",
                    Filter = "ملفات صور (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|جميع الملفات (*.*)|*.*",
                    Multiselect = true
                };

                if (ofd.ShowDialog() != true) return;
                filePaths = ofd.FileNames;
            }

            if (filePaths == null || filePaths.Length == 0) return;

            foreach (var path in filePaths)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var item = new CustomLayoutItem(name, 30.0m, 40.0m, 1, path, true);
                AddCustomItemViewModel(item);
            }

            UpdateLayoutPlan();
        }

        [RelayCommand]
        public void AddCustomItem()
        {
            int nextNum = CustomItems.Count + 1;
            var item = new CustomLayoutItem($"عنصر {nextNum}", 30.0m, 40.0m, 1, "", true);
            AddCustomItemViewModel(item);
            UpdateLayoutPlan();
        }

        private void AddCustomItemViewModel(CustomLayoutItem item)
        {
            var vm = new CustomLayoutItemViewModel(item);
            vm.RequestDelete += () =>
            {
                CustomItems.Remove(vm);
                if (SelectedCustomItem == vm) SelectedCustomItem = CustomItems.FirstOrDefault();
                UpdateLayoutPlan();
            };
            vm.SettingsChanged += () =>
            {
                UpdateLayoutPlan();
            };
            CustomItems.Add(vm);
            SelectedCustomItem = vm;
        }

        [RelayCommand]
        public void ClearCustomItems()
        {
            CustomItems.Clear();
            SelectedCustomItem = null;
            SelectedPlanItem = null;
            UpdateLayoutPlan();
        }

        [RelayCommand]
        public void AutoLayoutCustomSize()
        {
            UpdateCustomSizeLayoutPlan();
        }

        [RelayCommand]
        public void ResetCustomLayout()
        {
            UpdateCustomSizeLayoutPlan();
        }

        [RelayCommand]
        public void SelectPlanItem(LayoutItem? item)
        {
            SelectedPlanItem = item;
        }

        [RelayCommand]
        public void RotateSelectedPlanItem()
        {
            if (SelectedPlanItem == null || CurrentPlan == null) return;

            double temp = SelectedPlanItem.Width_Mm;
            SelectedPlanItem.Width_Mm = SelectedPlanItem.Height_Mm;
            SelectedPlanItem.Height_Mm = temp;
            SelectedPlanItem.RotationDegrees = SelectedPlanItem.RotationDegrees == 90 ? 0 : 90;

            var (paperW, paperH) = CurrentPlan.SheetDimensions;
            double margin = (double)CustomSafetyMarginMm;
            SelectedPlanItem.X_Mm = Math.Clamp(SelectedPlanItem.X_Mm, margin, paperW - margin - SelectedPlanItem.Width_Mm);
            SelectedPlanItem.Y_Mm = Math.Clamp(SelectedPlanItem.Y_Mm, margin, paperH - margin - SelectedPlanItem.Height_Mm);

            RefreshCurrentPageItems();
        }

        [RelayCommand]
        public void DeleteSelectedPlanItem()
        {
            if (SelectedPlanItem == null || CurrentPlan == null) return;
            CurrentPlan.Items.Remove(SelectedPlanItem);
            SelectedPlanItem = null;
            RefreshCurrentPageItems();
        }

        [RelayCommand]
        public void MoveSelectedPlanItem(string direction)
        {
            if (SelectedPlanItem == null || CurrentPlan == null) return;

            var (paperW, paperH) = CurrentPlan.SheetDimensions;
            double margin = (double)CustomSafetyMarginMm;
            double step = 2.0;

            switch (direction.ToLowerInvariant())
            {
                case "left":
                    SelectedPlanItem.X_Mm = Math.Max(margin, SelectedPlanItem.X_Mm - step);
                    break;
                case "right":
                    SelectedPlanItem.X_Mm = Math.Min(paperW - margin - SelectedPlanItem.Width_Mm, SelectedPlanItem.X_Mm + step);
                    break;
                case "up":
                    SelectedPlanItem.Y_Mm = Math.Max(margin, SelectedPlanItem.Y_Mm - step);
                    break;
                case "down":
                    SelectedPlanItem.Y_Mm = Math.Min(paperH - margin - SelectedPlanItem.Height_Mm, SelectedPlanItem.Y_Mm + step);
                    break;
            }

            RefreshCurrentPageItems();
        }

        public void RefreshOrderSummary()
        {
            OrderSummary = PrintOrderSummary.Compute(Documents.Select(d => d.Item));
        }

        private void RefreshCurrentPageItems()
        {
            CurrentPageItems.Clear();
            if (CurrentPlan == null) return;

            var items = CurrentPlan.GetItemsForPage(CurrentPageIndex);
            foreach (var item in items)
            {
                CurrentPageItems.Add(item);
            }

            PageIndicatorText = $"صفحة {CurrentPageIndex + 1} من {TotalPages}";
            UpdatePageNavigationState();
        }

        private void UpdatePageNavigationState()
        {
            CanGoPrevPage = CurrentPageIndex > 0;
            CanGoNextPage = CurrentPageIndex < TotalPages - 1;
        }

        [RelayCommand]
        public void NextPage()
        {
            if (CanGoNextPage)
            {
                CurrentPageIndex++;
            }
        }

        [RelayCommand]
        public void PreviousPage()
        {
            if (CanGoPrevPage)
            {
                CurrentPageIndex--;
            }
        }

        [RelayCommand]
        public async Task ExportPdfAsync()
        {
            if (AppMode == ApplicationMode.GenericDocuments)
            {
                await ExportGenericDocumentsPdfAsync();
                return;
            }

            if (AppMode == ApplicationMode.CustomSize)
            {
                await ExportCustomSizePdfAsync();
                return;
            }

            if (AppMode == ApplicationMode.SchoolOrders)
            {
                await StartBatchProcessingAsync();
                return;
            }

            if (CurrentPlan == null || CurrentPlan.Items.Count == 0)
            {
                MessageBox.Show("يرجى استيراد البطاقات واختيار قالب التوزيع أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var defaultFileName = $"IDCard_Order_{OrderNumber.Replace("#", "").Replace("-", "_")}.pdf";
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            var sfd = new SaveFileDialog
            {
                Title = "تصدير ملف الطباعة النهائي (PDF)",
                Filter = "ملف PDF (*.pdf)|*.pdf",
                FileName = defaultFileName,
                InitialDirectory = desktopPath
            };

            if (sfd.ShowDialog() != true)
            {
                return;
            }

            IsBusy = true;
            BusyMessage = "جاري إنشاء ملف PDF جاهز للطباعة بدقة 300 DPI...";

            try
            {
                var rectifiedMats = new Dictionary<string, Mat>();

                await Task.Run(() =>
                {
                    if (AppMode == ApplicationMode.PersonalPhotos)
                    {
                        if (SelectedPersonalPhoto != null)
                        {
                            using var rawMat = _imageService.LoadMat(SelectedPersonalPhoto.Item.SourceImagePath);
                            var rendered = _photoProcessor.RenderFinalPhoto(rawMat, SelectedPersonalPhoto.Item.Parameters, SelectedPersonalPhoto.Profile);
                            rectifiedMats[SelectedPersonalPhoto.Item.Id] = rendered;
                        }
                    }
                    else
                    {
                        foreach (var cardVm in Cards)
                        {
                            var mat = _imageService.WarpAndCorrectCard(cardVm.Region.SourceImagePath, cardVm.Region);
                            rectifiedMats[cardVm.Region.Id] = mat;
                        }
                    }
                });

                var order = new JobOrder
                {
                    OrderNumber = OrderNumber,
                    CustomerName = CustomerName,
                    Notes = Notes,
                    JobType = AppMode == ApplicationMode.PersonalPhotos ? "PersonalPhoto" : "IdCard",
                    Copies = AppMode == ApplicationMode.PersonalPhotos ? PersonalPhotoCopies : Copies,
                    SelectedTemplateId = SelectedTemplate?.Id ?? "",
                    InputFiles = AppMode == ApplicationMode.PersonalPhotos
                        ? PersonalPhotos.Select(p => p.Item.SourceImagePath).Distinct().ToList()
                        : Cards.Select(c => c.Region.SourceImagePath).Distinct().ToList(),
                    DetectedCards = Cards.Select(c => c.Region).ToList(),
                    PersonalPhotos = PersonalPhotos.Select(p => p.Item).ToList(),
                    OutputPdfPath = sfd.FileName,
                    ExportTimestamp = DateTime.Now
                };

                await _pdfService.ExportPdfAsync(CurrentPlan, rectifiedMats, sfd.FileName, order);

                // Auto save job info next to pdf for full audit & reproducibility
                var jobPath = Path.ChangeExtension(sfd.FileName, ".idjob");
                await _jobService.SaveJobAsync(order, jobPath);

                // Dispose temporary mats
                foreach (var mat in rectifiedMats.Values)
                {
                    mat.Dispose();
                }

                LastExportedPdfPath = sfd.FileName;
                HasExportedPdf = true;

                var result = MessageBox.Show(
                    $"تم إنشاء وتصدير ملف الـ PDF بنجاح!\nالمسار: {sfd.FileName}\n\nهل ترغب في فتح الملف الآن للطباعة أو المعاينة؟",
                    "نجاح التصدير",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    OpenPdf();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء إنشاء ملف الـ PDF:\n{ex.Message}", "خطأ في التصدير", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExportGenericDocumentsPdfAsync()
        {
            if (Documents.Count == 0)
            {
                MessageBox.Show("لا توجد مستندات في القائمة للتصدير.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var defaultFileName = $"Order_{OrderNumber.Replace("#", "").Replace("-", "_")}_Print.pdf";
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            var sfd = new SaveFileDialog
            {
                Title = "تصدير مستندات الطباعة المجمعة إلى PDF",
                Filter = "ملف PDF جاهز للطباعة (*.pdf)|*.pdf",
                FileName = defaultFileName,
                InitialDirectory = desktopPath
            };

            if (sfd.ShowDialog() != true) return;

            IsBusy = true;
            BusyMessage = "جاري تجميع المستندات وإعداد ملف الـ PDF الجاهز للطباعة...";

            try
            {
                var docItems = Documents.Select(d => d.Item).ToList();
                var exportResult = await _genericExportService.ExportCombinedPdfAsync(docItems, sfd.FileName);

                if (!exportResult.Success)
                {
                    MessageBox.Show($"فشل تصدير ملف الطباعة:\n{exportResult.ErrorMessage}", "خطأ في التصدير", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var order = new JobOrder
                {
                    OrderNumber = OrderNumber,
                    CustomerName = CustomerName,
                    Notes = Notes,
                    Copies = 1,
                    SelectedTemplateId = "GenericDocuments",
                    JobType = "GenericDocuments",
                    InputFiles = Documents.Select(d => d.Item.OriginalPath).Distinct().ToList(),
                    Documents = docItems,
                    OutputPdfPath = sfd.FileName,
                    ExportTimestamp = DateTime.Now
                };

                var jobPath = Path.ChangeExtension(sfd.FileName, ".idjob");
                await _jobService.SaveJobAsync(order, jobPath);

                LastExportedPdfPath = sfd.FileName;
                HasExportedPdf = true;

                var result = MessageBox.Show(
                    $"تم إنشاء وتصدير ملف الـ PDF بنجاح!\nالمسار: {sfd.FileName}\nعدد الصفحات الكلي: {exportResult.TotalPagesProduced}\n\nهل ترغب في فتح الملف الآن للطباعة أو المعاينة؟",
                    "نجاح التصدير",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    OpenPdf();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء تصدير المستندات:\n{ex.Message}", "خطأ في التصدير", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExportCustomSizePdfAsync()
        {
            if (CurrentPlan == null || CurrentPlan.Items.Count == 0)
            {
                MessageBox.Show("يرجى إضافة عناصر وتوليد التوزيع أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var defaultFileName = $"CustomSize_Order_{OrderNumber.Replace("#", "").Replace("-", "_")}.pdf";
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            var sfd = new SaveFileDialog
            {
                Title = "تصدير ملف المقاسات المخصصة للطباعة (PDF)",
                Filter = "ملف PDF (*.pdf)|*.pdf",
                FileName = defaultFileName,
                InitialDirectory = desktopPath
            };

            if (sfd.ShowDialog() != true)
            {
                return;
            }

            IsBusy = true;
            BusyMessage = "جاري إنشاء ملف PDF جاهز للطباعة بدقة 300 DPI...";

            try
            {
                var rectifiedMats = new Dictionary<string, Mat>();

                await Task.Run(() =>
                {
                    foreach (var item in CurrentPlan.Items)
                    {
                        if (!string.IsNullOrEmpty(item.SourceImagePath) && File.Exists(item.SourceImagePath))
                        {
                            if (!rectifiedMats.ContainsKey(item.CardRegionId))
                            {
                                var mat = _imageService.LoadMat(item.SourceImagePath);
                                if (item.RotationDegrees == 90)
                                {
                                    Cv2.Rotate(mat, mat, RotateFlags.Rotate90Clockwise);
                                }
                                rectifiedMats[item.CardRegionId] = mat;
                            }
                        }
                    }
                });

                var order = new JobOrder
                {
                    OrderNumber = OrderNumber,
                    CustomerName = CustomerName,
                    Notes = Notes,
                    JobType = "CustomSize",
                    Copies = CustomTotalCopiesCount,
                    SelectedTemplateId = CustomPaperSize.Name,
                    InputFiles = CustomItems.Where(c => !string.IsNullOrEmpty(c.SourceFile)).Select(c => c.SourceFile).Distinct().ToList(),
                    CustomSizeItems = CustomItems.Select(c => c.Item).ToList(),
                    CustomSizeParameters = new CustomSizeLayoutParameters
                    {
                        PaperSize = CustomPaperSize,
                        OrientationMode = CustomOrientationMode,
                        SafetyMarginMm = CustomSafetyMarginMm,
                        SpacingMm = CustomSpacingMm,
                        ShowCutMarks = CustomShowCutMarks,
                        DrawBorderBox = CustomDrawBorderBox
                    },
                    OutputPdfPath = sfd.FileName,
                    ExportTimestamp = DateTime.Now
                };

                await _pdfService.ExportPdfAsync(CurrentPlan, rectifiedMats, sfd.FileName, order);

                var jobPath = Path.ChangeExtension(sfd.FileName, ".idjob");
                await _jobService.SaveJobAsync(order, jobPath);

                foreach (var mat in rectifiedMats.Values)
                {
                    mat.Dispose();
                }

                LastExportedPdfPath = sfd.FileName;
                HasExportedPdf = true;

                var result = MessageBox.Show(
                    $"تم إنشاء وتصدير ملف الـ PDF بنجاح!\nالمسار: {sfd.FileName}\n\nهل ترغب في فتح الملف الآن للطباعة أو المعاينة؟",
                    "نجاح التصدير",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    OpenPdf();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء تصدير المقاسات المخصصة:\n{ex.Message}", "خطأ في التصدير", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void OpenPdf()
        {
            if (string.IsNullOrEmpty(LastExportedPdfPath) || !File.Exists(LastExportedPdfPath))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = LastExportedPdfPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"تعذر فتح الملف: {ex.Message}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        public async Task SaveJobAsync()
        {
            var sfd = new SaveFileDialog
            {
                Title = "حفظ بيانات الطلب وإعدادات المعالجة",
                Filter = "ملف مشروع طباعة (*.idjob)|*.idjob",
                FileName = $"Order_{OrderNumber.Replace("#", "")}.idjob"
            };

            if (sfd.ShowDialog() == true)
            {
                var order = new JobOrder
                {
                    OrderNumber = OrderNumber,
                    CustomerName = CustomerName,
                    Notes = Notes,
                    JobType = AppMode switch
                    {
                        ApplicationMode.CustomSize => "CustomSize",
                        ApplicationMode.GenericDocuments => "GenericDocuments",
                        ApplicationMode.PersonalPhotos => "PersonalPhoto",
                        _ => "IdCard"
                    },
                    Copies = AppMode == ApplicationMode.CustomSize
                        ? CustomTotalCopiesCount
                        : (AppMode == ApplicationMode.PersonalPhotos ? PersonalPhotoCopies : Copies),
                    SelectedTemplateId = AppMode == ApplicationMode.CustomSize ? CustomPaperSize.Name : (SelectedTemplate?.Id ?? ""),
                    InputFiles = AppMode == ApplicationMode.CustomSize
                        ? CustomItems.Where(c => !string.IsNullOrEmpty(c.SourceFile)).Select(c => c.SourceFile).Distinct().ToList()
                        : (AppMode == ApplicationMode.GenericDocuments
                            ? Documents.Select(d => d.Item.OriginalPath).Distinct().ToList()
                            : (AppMode == ApplicationMode.PersonalPhotos
                                ? PersonalPhotos.Select(p => p.Item.SourceImagePath).Distinct().ToList()
                                : Cards.Select(c => c.Region.SourceImagePath).Distinct().ToList())),
                    DetectedCards = Cards.Select(c => c.Region).ToList(),
                    PersonalPhotos = PersonalPhotos.Select(p => p.Item).ToList(),
                    Documents = Documents.Select(d => d.Item).ToList(),
                    CustomSizeItems = CustomItems.Select(c => c.Item).ToList(),
                    CustomSizeParameters = new CustomSizeLayoutParameters
                    {
                        PaperSize = CustomPaperSize,
                        OrientationMode = CustomOrientationMode,
                        SafetyMarginMm = CustomSafetyMarginMm,
                        SpacingMm = CustomSpacingMm,
                        ShowCutMarks = CustomShowCutMarks,
                        DrawBorderBox = CustomDrawBorderBox
                    },
                    OutputPdfPath = LastExportedPdfPath,
                    ExportTimestamp = DateTime.Now
                };

                await _jobService.SaveJobAsync(order, sfd.FileName);
                MessageBox.Show("تم حفظ بيانات المشروع بنجاح.", "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        public async Task LoadJobAsync()
        {
            var ofd = new OpenFileDialog
            {
                Title = "فتح ملف مشروع طباعة محفوظ",
                Filter = "ملف مشروع طباعة (*.idjob)|*.idjob"
            };

            if (ofd.ShowDialog() == true)
            {
                IsBusy = true;
                BusyMessage = "جاري استعادة بيانات المشروع...";

                try
                {
                    var order = await _jobService.LoadJobAsync(ofd.FileName);
                    OrderNumber = order.OrderNumber;
                    CustomerName = order.CustomerName;
                    Notes = order.Notes;
                    Copies = order.Copies;

                    if (order.JobType == "CustomSize" || (order.CustomSizeItems != null && order.CustomSizeItems.Count > 0 && order.DetectedCards.Count == 0 && order.PersonalPhotos.Count == 0 && order.Documents.Count == 0))
                    {
                        AppMode = ApplicationMode.CustomSize;
                        NotifyModeProperties();

                        CustomItems.Clear();
                        if (order.CustomSizeItems != null)
                        {
                            foreach (var item in order.CustomSizeItems)
                            {
                                AddCustomItemViewModel(item);
                            }
                        }
                        if (order.CustomSizeParameters != null)
                        {
                            CustomPaperSize = PaperSize.AllStandardSizes.FirstOrDefault(p => p.Name == order.CustomSizeParameters.PaperSize?.Name) ?? PaperSize.A4;
                            CustomOrientationMode = order.CustomSizeParameters.OrientationMode;
                            CustomSafetyMarginMm = order.CustomSizeParameters.SafetyMarginMm;
                            CustomSpacingMm = order.CustomSizeParameters.SpacingMm;
                            CustomShowCutMarks = order.CustomSizeParameters.ShowCutMarks;
                            CustomDrawBorderBox = order.CustomSizeParameters.DrawBorderBox;
                        }

                        UpdateCustomSizeLayoutPlan();
                        DetectionNotice = "تم استعادة مشروع المقاسات المخصصة بنجاح.";
                        IsNoticeWarning = false;
                    }
                    else if (order.JobType == "GenericDocuments" || (order.Documents != null && order.Documents.Count > 0 && order.DetectedCards.Count == 0))
                    {
                        AppMode = ApplicationMode.GenericDocuments;
                        NotifyModeProperties();

                        Documents.Clear();
                        if (order.Documents != null)
                        {
                            foreach (var doc in order.Documents)
                            {
                                var vm = new DocumentItemViewModel(doc);
                                vm.SettingsChanged += () =>
                                {
                                    RefreshOrderSummary();
                                    UpdateLayoutPlan();
                                };
                                Documents.Add(vm);
                            }
                        }
                        HasDocuments = Documents.Count > 0;
                        SelectedDocument = Documents.FirstOrDefault();
                        RefreshOrderSummary();
                        UpdateLayoutPlan();

                        DetectionNotice = "تم استعادة مشروع المستندات وإعدادات الطباعة بنجاح.";
                        IsNoticeWarning = false;
                    }
                    else if (order.JobType == "PersonalPhoto" || (order.PersonalPhotos != null && order.PersonalPhotos.Count > 0 && order.DetectedCards.Count == 0))
                    {
                        AppMode = ApplicationMode.PersonalPhotos;
                        NotifyModeProperties();

                        PersonalPhotos.Clear();
                        if (order.PersonalPhotos != null)
                        {
                            foreach (var p in order.PersonalPhotos)
                            {
                                var vm = new PersonalPhotoItemViewModel(p, _photoProcessor, _imageService);
                                PersonalPhotos.Add(vm);
                            }
                        }
                        HasPersonalPhotos = PersonalPhotos.Count > 0;
                        SelectedPersonalPhoto = PersonalPhotos.FirstOrDefault();
                        PersonalPhotoCopies = order.Copies;
                        UpdateLayoutPlan();

                        DetectionNotice = "تم استعادة مشروع صور الاستوديو بنجاح.";
                        IsNoticeWarning = false;
                    }
                    else
                    {
                        AppMode = ApplicationMode.IdCards;
                        NotifyModeProperties();

                        SelectedTemplate = Templates.FirstOrDefault(t => t.Id == order.SelectedTemplateId) ?? Templates.FirstOrDefault();

                        Cards.Clear();
                        foreach (var region in order.DetectedCards)
                        {
                            var vm = new CardItemViewModel(region, _imageService);
                            Cards.Add(vm);
                        }

                        HasCards = Cards.Count > 0;
                        SelectedCard = Cards.FirstOrDefault();
                        UpdateLayoutPlan();

                        DetectionNotice = "تم استعادة المشروع وحساب التوزيع بنجاح.";
                        IsNoticeWarning = false;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"فشل استعادة المشروع: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
    }
}
