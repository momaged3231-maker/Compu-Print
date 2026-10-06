using System.Windows;
using IdCardPrintShop.ViewModels;

namespace IdCardPrintShop.Views
{
    public partial class ManualPersonalPhotoAdjustmentWindow : Window
    {
        public ManualPersonalPhotoAdjustmentViewModel ViewModel { get; }

        public ManualPersonalPhotoAdjustmentWindow(ManualPersonalPhotoAdjustmentViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;

            CropCanvas.ViewModel = viewModel;

            viewModel.RequestClose += () =>
            {
                DialogResult = true;
                Close();
            };
        }
    }
}
