using Avalonia.Controls;
using Avalonia.Input;
using System.Linq;
using SLSKDONET.Views;

namespace SLSKDONET.Views.Avalonia
{
    public partial class SearchPage : UserControl
    {
        public SearchPage()
        {
            InitializeComponent();
            
            // Enable Drag & Drop
            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DragOverEvent, OnDragOver);
            AddHandler(DragDrop.DropEvent, OnDrop);
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Files))
            {
                // Only allow CSV files
                var files = e.Data.GetFiles();
                if (files != null && files.Any(f => f.Name.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase)))
                {
                    e.DragEffects = DragDropEffects.Copy;
                    return;
                }
            }
            e.DragEffects = DragDropEffects.None;
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Files))
            {
                var files = e.Data.GetFiles();
                var csvFile = files?.FirstOrDefault(f => f.Name.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase));

                if (csvFile != null && DataContext is MainViewModel vm)
                {
                    // Auto-switch to CSV mode and populate path
                    vm.CurrentSearchMode = Models.SearchInputMode.CsvFile;
                    vm.SearchQuery = System.Uri.UnescapeDataString(csvFile.Path.AbsolutePath);
                    // Handle file URI if needed (Avalonia returns file:///... on some platforms, usually LocalPath is better if available, but IStorageItem is abstract)
                    // For System.IO compatibility we often need to strip file schema if present.
                    // However, GetFiles returns IStorageItem.
                    // Let's safe cast to try get a local path string if possible or use the Path property.
                    // Note: Avalonia 11 IStorageItem.Path is a Uri.
                    
                    if (csvFile.Path.IsAbsoluteUri && csvFile.Path.Scheme == "file")
                    {
                        vm.SearchQuery = csvFile.Path.LocalPath;
                    }
                }
            }
        }
    }
}
