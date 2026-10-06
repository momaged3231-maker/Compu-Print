using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using IdCardPrintShop.Models;
using IdCardPrintShop.ViewModels;

namespace IdCardPrintShop.Views
{
    public partial class SheetPreviewControl : UserControl
    {
        public static readonly DependencyProperty PlanProperty =
            DependencyProperty.Register(
                nameof(Plan),
                typeof(LayoutPlan),
                typeof(SheetPreviewControl),
                new PropertyMetadata(null, OnInputChanged));

        public static readonly DependencyProperty PageIndexProperty =
            DependencyProperty.Register(
                nameof(PageIndex),
                typeof(int),
                typeof(SheetPreviewControl),
                new PropertyMetadata(0, OnInputChanged));

        public static readonly DependencyProperty CardsProperty =
            DependencyProperty.Register(
                nameof(Cards),
                typeof(IEnumerable<CardItemViewModel>),
                typeof(SheetPreviewControl),
                new PropertyMetadata(null, OnInputChanged));

        public static readonly DependencyProperty PersonalPhotosProperty =
            DependencyProperty.Register(
                nameof(PersonalPhotos),
                typeof(IEnumerable<PersonalPhotoItemViewModel>),
                typeof(SheetPreviewControl),
                new PropertyMetadata(null, OnInputChanged));

        public static readonly DependencyProperty DocumentsProperty =
            DependencyProperty.Register(
                nameof(Documents),
                typeof(IEnumerable<DocumentItemViewModel>),
                typeof(SheetPreviewControl),
                new PropertyMetadata(null, OnInputChanged));

        public static readonly DependencyProperty SelectedDocumentProperty =
            DependencyProperty.Register(
                nameof(SelectedDocument),
                typeof(DocumentItemViewModel),
                typeof(SheetPreviewControl),
                new PropertyMetadata(null, OnInputChanged));

        public static readonly DependencyProperty CustomItemsProperty =
            DependencyProperty.Register(
                nameof(CustomItems),
                typeof(IEnumerable<CustomLayoutItemViewModel>),
                typeof(SheetPreviewControl),
                new PropertyMetadata(null, OnInputChanged));

        public static readonly DependencyProperty SelectedPlanItemProperty =
            DependencyProperty.Register(
                nameof(SelectedPlanItem),
                typeof(LayoutItem),
                typeof(SheetPreviewControl),
                new PropertyMetadata(null, OnInputChanged));

        public LayoutPlan? Plan
        {
            get => (LayoutPlan?)GetValue(PlanProperty);
            set => SetValue(PlanProperty, value);
        }

        public int PageIndex
        {
            get => (int)GetValue(PageIndexProperty);
            set => SetValue(PageIndexProperty, value);
        }

        public IEnumerable<CardItemViewModel>? Cards
        {
            get => (IEnumerable<CardItemViewModel>?)GetValue(CardsProperty);
            set => SetValue(CardsProperty, value);
        }

        public IEnumerable<PersonalPhotoItemViewModel>? PersonalPhotos
        {
            get => (IEnumerable<PersonalPhotoItemViewModel>?)GetValue(PersonalPhotosProperty);
            set => SetValue(PersonalPhotosProperty, value);
        }

        public IEnumerable<DocumentItemViewModel>? Documents
        {
            get => (IEnumerable<DocumentItemViewModel>?)GetValue(DocumentsProperty);
            set => SetValue(DocumentsProperty, value);
        }

        public DocumentItemViewModel? SelectedDocument
        {
            get => (DocumentItemViewModel?)GetValue(SelectedDocumentProperty);
            set => SetValue(SelectedDocumentProperty, value);
        }

        public IEnumerable<CustomLayoutItemViewModel>? CustomItems
        {
            get => (IEnumerable<CustomLayoutItemViewModel>?)GetValue(CustomItemsProperty);
            set => SetValue(CustomItemsProperty, value);
        }

        public LayoutItem? SelectedPlanItem
        {
            get => (LayoutItem?)GetValue(SelectedPlanItemProperty);
            set => SetValue(SelectedPlanItemProperty, value);
        }

        public event Action<LayoutItem?>? ItemSelected;
        public event Action? LayoutPlanModified;

        // Interactive dragging state
        private bool _isDragging;
        private LayoutItem? _draggedItem;
        private Border? _draggedBorder;
        private Point _dragStartPos;
        private double _itemStartX_mm;
        private double _itemStartY_mm;
        private double _currentScale = 1.0;

        public SheetPreviewControl()
        {
            InitializeComponent();
        }

        private static void OnInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SheetPreviewControl ctrl)
            {
                ctrl.RenderSheet();
            }
        }

        private void OnContainerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderSheet();
        }

        public void RenderSheet()
        {
            SheetCanvas.Children.Clear();

            if (Plan == null)
            {
                PaperSheet.Visibility = Visibility.Collapsed;
                return;
            }

            PaperSheet.Visibility = Visibility.Visible;

            double containerW = ContainerGrid.ActualWidth;
            double containerH = ContainerGrid.ActualHeight;

            if (containerW < 20 || containerH < 20)
            {
                return;
            }

            var (paperW_mm, paperH_mm) = Plan.SheetDimensions;

            // Reserve 40px padding around paper inside container
            double availW = Math.Max(100, containerW - 40);
            double availH = Math.Max(100, containerH - 40);

            double scaleX = availW / paperW_mm;
            double scaleY = availH / paperH_mm;
            double scale = Math.Min(scaleX, scaleY);
            _currentScale = scale;

            double sheetDisplayW = paperW_mm * scale;
            double sheetDisplayH = paperH_mm * scale;

            PaperSheet.Width = sheetDisplayW;
            PaperSheet.Height = sheetDisplayH;
            SheetCanvas.Width = sheetDisplayW;
            SheetCanvas.Height = sheetDisplayH;

            var cardsDict = Cards?.ToDictionary(c => c.Region.Id, c => c)
                            ?? new Dictionary<string, CardItemViewModel>();

            var personalPhotosDict = PersonalPhotos?.ToDictionary(p => p.Item.Id, p => p)
                                     ?? new Dictionary<string, PersonalPhotoItemViewModel>();

            var customItemsDict = CustomItems?.ToDictionary(c => c.Item.Id, c => c)
                                  ?? new Dictionary<string, CustomLayoutItemViewModel>();

            var itemsOnPage = Plan.GetItemsForPage(PageIndex);

            var hairlineBrush = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            var borderBrush = new SolidColorBrush(Color.FromRgb(215, 215, 215));
            var selectedBorderBrush = new SolidColorBrush(Color.FromRgb(0, 229, 255));

            foreach (var item in itemsOnPage)
            {
                double x = item.X_Mm * scale;
                double y = item.Y_Mm * scale;
                double w = item.Width_Mm * scale;
                double h = item.Height_Mm * scale;

                bool isSelected = (SelectedPlanItem == item);

                // Card container
                var cardBorder = new Border
                {
                    Width = w,
                    Height = h,
                    Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                    BorderBrush = isSelected ? selectedBorderBrush : (Plan.DrawBorderBox ? borderBrush : Brushes.Transparent),
                    BorderThickness = new Thickness(isSelected ? 2 : 1),
                    ClipToBounds = true,
                    Cursor = Cursors.SizeAll
                };

                // Image source resolution
                BitmapSource? imageSource = null;
                if (cardsDict.TryGetValue(item.CardRegionId, out var cardVm) && cardVm.RectifiedImage != null)
                {
                    imageSource = cardVm.RectifiedImage;
                }
                else if (personalPhotosDict.TryGetValue(item.CardRegionId, out var photoVm) && photoVm.RenderedPreview != null)
                {
                    imageSource = photoVm.RenderedPreview;
                }
                else if (personalPhotosDict.Values.FirstOrDefault()?.RenderedPreview != null)
                {
                    imageSource = personalPhotosDict.Values.First().RenderedPreview;
                }
                else if (customItemsDict.TryGetValue(item.CardRegionId, out var customVm) && customVm.Thumbnail != null)
                {
                    imageSource = customVm.Thumbnail;
                }
                else if (!string.IsNullOrEmpty(item.SourceImagePath) && File.Exists(item.SourceImagePath))
                {
                    try
                    {
                        var uri = new Uri(item.SourceImagePath);
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = uri;
                        if (item.RotationDegrees == 90)
                        {
                            bmp.Rotation = Rotation.Rotate90;
                        }
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        imageSource = bmp;
                    }
                    catch { }
                }
                else if (SelectedDocument != null && SelectedDocument.FileType == DocumentFileType.Image && File.Exists(SelectedDocument.Item.OriginalPath))
                {
                    try
                    {
                        var uri = new Uri(SelectedDocument.Item.OriginalPath);
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = uri;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        imageSource = bmp;
                    }
                    catch { }
                }

                if (imageSource != null)
                {
                    var img = new Image
                    {
                        Source = imageSource,
                        Stretch = (SelectedDocument != null && SelectedDocument.Scaling == DocumentScalingMode.FitToPage) ? Stretch.Uniform : Stretch.Fill
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);

                    if (item.IncludeNameLabel && !string.IsNullOrWhiteSpace(item.StudentName))
                    {
                        var containerGrid = new Grid();
                        containerGrid.Children.Add(img);

                        var labelBorder = new Border
                        {
                            VerticalAlignment = VerticalAlignment.Bottom,
                            Background = new SolidColorBrush(Color.FromArgb(225, 255, 255, 255)),
                            Padding = new Thickness(2, 1, 2, 1)
                        };
                        labelBorder.Child = new TextBlock
                        {
                            Text = item.StudentName,
                            FontSize = Math.Max(8.5, 10.0 * (scale / 3.0)),
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Color.FromRgb(25, 25, 25)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        };
                        containerGrid.Children.Add(labelBorder);
                        cardBorder.Child = containerGrid;
                    }
                    else
                    {
                        cardBorder.Child = img;
                    }
                }
                else if (!string.IsNullOrEmpty(item.ItemName) || !string.IsNullOrEmpty(item.CustomItemId))
                {
                    // Custom size placeholder panel
                    var customPanel = new StackPanel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4)
                    };

                    bool missingImage = !string.IsNullOrEmpty(item.SourceImagePath) && !File.Exists(item.SourceImagePath);

                    customPanel.Children.Add(new TextBlock
                    {
                        Text = missingImage ? "⚠️" : (item.RotationDegrees == 90 ? "📐 ⟳" : "📐"),
                        FontSize = Math.Max(11, 16 * (scale / 2.0)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 2)
                    });

                    customPanel.Children.Add(new TextBlock
                    {
                        Text = string.IsNullOrEmpty(item.ItemName) ? "عنصر مخصص" : item.ItemName,
                        FontSize = Math.Max(9, 12 * (scale / 2.0)),
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        MaxWidth = Math.Max(40, w - 8)
                    });

                    customPanel.Children.Add(new TextBlock
                    {
                        Text = $"{item.Width_Mm:G29}×{item.Height_Mm:G29} mm",
                        FontSize = Math.Max(8, 10 * (scale / 2.0)),
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 0)
                    });

                    if (missingImage)
                    {
                        customPanel.Children.Add(new TextBlock
                        {
                            Text = "Source image not found.",
                            FontSize = Math.Max(7, 9 * (scale / 2.0)),
                            Foreground = Brushes.Crimson,
                            FontWeight = FontWeights.SemiBold,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 2, 0, 0)
                        });
                    }

                    cardBorder.Child = customPanel;
                }
                else if (SelectedDocument != null)
                {
                    var docPanel = new StackPanel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(10)
                    };

                    string icon = SelectedDocument.FileType switch
                    {
                        DocumentFileType.Pdf => "📄 PDF",
                        DocumentFileType.Docx => "📝 Word DOCX",
                        _ => "📁 مستند"
                    };

                    docPanel.Children.Add(new TextBlock
                    {
                        Text = icon,
                        FontSize = Math.Max(12, 22 * (scale / 2.0)),
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 8)
                    });

                    docPanel.Children.Add(new TextBlock
                    {
                        Text = SelectedDocument.FileName,
                        FontSize = Math.Max(10, 14 * (scale / 2.0)),
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        MaxWidth = Math.Max(50, w - 20)
                    });

                    docPanel.Children.Add(new TextBlock
                    {
                        Text = $"{SelectedDocument.PaperSizeName} • {(SelectedDocument.IsBw ? "أبيض وأسود (B&W)" : "ألوان (Color)")} • {SelectedDocument.Copies} نسخ",
                        FontSize = Math.Max(9, 11 * (scale / 2.0)),
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 6, 0, 0)
                    });

                    cardBorder.Child = docPanel;
                }

                // Interactive drag & select handling
                cardBorder.MouseLeftButtonDown += (s, e) =>
                {
                    _isDragging = true;
                    _draggedItem = item;
                    _draggedBorder = cardBorder;
                    _dragStartPos = e.GetPosition(SheetCanvas);
                    _itemStartX_mm = item.X_Mm;
                    _itemStartY_mm = item.Y_Mm;

                    SelectedPlanItem = item;
                    ItemSelected?.Invoke(item);

                    cardBorder.CaptureMouse();
                    e.Handled = true;
                };

                cardBorder.MouseMove += (s, e) =>
                {
                    if (_isDragging && _draggedItem == item && _draggedBorder != null)
                    {
                        var curPos = e.GetPosition(SheetCanvas);
                        double dx_mm = (curPos.X - _dragStartPos.X) / scale;
                        double dy_mm = (curPos.Y - _dragStartPos.Y) / scale;

                        double newX = _itemStartX_mm + dx_mm;
                        double newY = _itemStartY_mm + dy_mm;

                        // Bounds protection: prevent item from exiting paper
                        double margin = 0.0;
                        newX = Math.Clamp(newX, margin, paperW_mm - item.Width_Mm);
                        newY = Math.Clamp(newY, margin, paperH_mm - item.Height_Mm);

                        item.X_Mm = Math.Round(newX, 2);
                        item.Y_Mm = Math.Round(newY, 2);

                        Canvas.SetLeft(_draggedBorder, item.X_Mm * scale);
                        Canvas.SetTop(_draggedBorder, item.Y_Mm * scale);
                    }
                };

                cardBorder.MouseLeftButtonUp += (s, e) =>
                {
                    if (_isDragging && _draggedItem == item)
                    {
                        _isDragging = false;
                        cardBorder.ReleaseMouseCapture();
                        RenderSheet();
                        LayoutPlanModified?.Invoke();
                        e.Handled = true;
                    }
                };

                Canvas.SetLeft(cardBorder, x);
                Canvas.SetTop(cardBorder, y);
                SheetCanvas.Children.Add(cardBorder);

                // Small badge at bottom of item
                var badge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 2, 4, 2),
                    Margin = new Thickness(0)
                };

                string badgeStr;
                if (!string.IsNullOrEmpty(item.ItemName) || !string.IsNullOrEmpty(item.CustomItemId))
                {
                    badgeStr = $"{item.Width_Mm:G29}×{item.Height_Mm:G29}" + (item.RotationDegrees == 90 ? " ⟳" : "") + $" #{item.CopyIndex}";
                }
                else if (SelectedDocument != null)
                {
                    badgeStr = $"{SelectedDocument.PaperSizeName} • ص {item.PageIndex + 1} • ×{item.CopyIndex}";
                }
                else if (item.Role == CardRole.Front) badgeStr = $"الوجه #{item.CopyIndex}";
                else if (item.Role == CardRole.Back) badgeStr = $"الظهر #{item.CopyIndex}";
                else badgeStr = $"#{item.CopyIndex}";

                var badgeText = new TextBlock
                {
                    Text = badgeStr,
                    Foreground = Brushes.White,
                    FontSize = Math.Max(8, 10 * (scale / 2.0)),
                    FontWeight = FontWeights.SemiBold
                };
                badge.Child = badgeText;
                Canvas.SetLeft(badge, x + 4);
                Canvas.SetTop(badge, Math.Max(0, y + h - 18));
                SheetCanvas.Children.Add(badge);

                // Professional cut marks (علامات القص)
                if (Plan.ShowCutMarks)
                {
                    DrawCutMarks(hairlineBrush, scale, item.X_Mm, item.Y_Mm, item.Width_Mm, item.Height_Mm);
                }
            }
        }

        private void DrawCutMarks(Brush brush, double scale, double xMm, double yMm, double wMm, double hMm)
        {
            double tickLen = 3.0 * scale; // 3mm mark
            double gap = 0.8 * scale;     // 0.8mm gap
            double th = 0.75;

            double x = xMm * scale;
            double y = yMm * scale;
            double w = wMm * scale;
            double h = hMm * scale;

            void AddLine(double x1, double y1, double x2, double y2)
            {
                var line = new Line
                {
                    X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                    Stroke = brush,
                    StrokeThickness = th
                };
                SheetCanvas.Children.Add(line);
            }

            // Top-Left Corner
            AddLine(x - gap - tickLen, y, x - gap, y);
            AddLine(x, y - gap - tickLen, x, y - gap);

            // Top-Right Corner
            AddLine(x + w + gap, y, x + w + gap + tickLen, y);
            AddLine(x + w, y - gap - tickLen, x + w, y - gap);

            // Bottom-Left Corner
            AddLine(x - gap - tickLen, y + h, x - gap, y + h);
            AddLine(x, y + h + gap, x, y + h + gap + tickLen);

            // Bottom-Right Corner
            AddLine(x + w + gap, y + h, x + w + gap + tickLen, y + h);
            AddLine(x + w, y + h + gap, x + w, y + h + gap + tickLen);
        }
    }
}
