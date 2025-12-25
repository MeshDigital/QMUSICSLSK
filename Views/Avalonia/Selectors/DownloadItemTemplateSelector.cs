using Avalonia.Controls;
using Avalonia.Controls.Templates;
using SLSKDONET.ViewModels.Downloads;

namespace SLSKDONET.Views.Avalonia.Selectors;

/// <summary>
/// Selects between Skeleton and Real templates based on IsHydrated state.
/// </summary>
public class DownloadItemTemplateSelector : IDataTemplate
{
    public IDataTemplate? SkeletonTemplate { get; set; }
    public IDataTemplate? HydratedTemplate { get; set; }

    public Control? Build(object? data)
    {
        if (data is DownloadItemViewModel vm)
        {
            var template = vm.IsHydrated ? HydratedTemplate : SkeletonTemplate;
            return template?.Build(data);
        }
        
        return null;
    }

    public bool Match(object? data)
    {
        return data is DownloadItemViewModel;
    }
}
