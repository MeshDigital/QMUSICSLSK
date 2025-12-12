using System.Windows.Controls;
using System.Windows.Input;
using SLSKDONET.ViewModels;
using SLSKDONET.Models;

namespace SLSKDONET.Views;

public partial class ImportPreviewPage : Page
{
    private ImportPreviewViewModel? _viewModel;

    public ImportPreviewPage()
    {
        InitializeComponent();
        DataContextChanged += (s, e) => 
        {
            _viewModel = DataContext as ImportPreviewViewModel;
        };
    }

    private void TrackCard_MouseDown(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border border && border.DataContext is Track track)
        {
            // Toggle selection on click
            track.IsSelected = !track.IsSelected;
            _viewModel?.UpdateSelectedCount();
            e.Handled = true;
        }
    }
}
