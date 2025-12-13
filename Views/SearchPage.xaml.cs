using Microsoft.VisualBasic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SLSKDONET.Models;

namespace SLSKDONET.Views
{
    public partial class SearchPage : Page
    {
        private MainViewModel? _viewModel;

        public SearchPage()
        {
            InitializeComponent();
            DataContextChanged += (s, e) => _viewModel = DataContext as MainViewModel;
        }

        private void ImportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import CSV File",
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                DefaultExt = ".csv"
            };

            if (openFileDialog.ShowDialog() == true && _viewModel?.ImportCsvCommand.CanExecute(openFileDialog.FileName) == true)
            {
                _viewModel.ImportCsvCommand.Execute(openFileDialog.FileName);
            }
        }

        private void ImportSpotifyButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.ImportFromSpotifyCommand.Execute(null);
        }

        private void ResultsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.SelectedTrackCount = ResultsGrid.SelectedItems.Count;
            }
        }

        private void PreviewTrackCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // This handler is no longer needed as preview is now a dedicated page
            // Track selection is handled in ImportPreviewPage
        }

        private void PreviewTrackSelectionChanged(object sender, RoutedEventArgs e)
        {
            // This handler is no longer needed as preview is now a dedicated page
            // Track selection is handled in ImportPreviewPage
        }
    }
}