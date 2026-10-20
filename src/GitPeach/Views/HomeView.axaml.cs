using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GitPeach.ViewModels;

namespace GitPeach.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        RepoList.DoubleTapped += (_, e) =>
        {
            if (e.Source is Control c && c.FindAncestorOfType<ListBoxItem>() is not null)
                (DataContext as HomeViewModel)?.OpenCommand.Execute(null);
        };
        RepoList.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) (DataContext as HomeViewModel)?.OpenCommand.Execute(null);
            else if (e.Key == Key.Delete) (DataContext as HomeViewModel)?.RemoveCommand.Execute(null);
        };
        AttachedToVisualTree += (_, _) => FilterBox.Focus();
    }
}
