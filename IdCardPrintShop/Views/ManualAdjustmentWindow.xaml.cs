using System.Windows;
using IdCardPrintShop.ViewModels;

namespace IdCardPrintShop.Views
{
    public partial class ManualAdjustmentWindow : Window
    {
        public ManualAdjustmentViewModel ViewModel { get; }

        public ManualAdjustmentWindow(ManualAdjustmentViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;

            CornerCanvas.ViewModel = viewModel;

            viewModel.RequestClose += () =>
            {
                DialogResult = true;
                Close();
            };
        }
    }
}
