using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GitPeach.Services;
using GitPeach.ViewModels;

namespace GitPeach.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        RepoList.DoubleTapped += (_, e) =>
        {
            if (Vm is { } vm && RowAt(e.Source) is RepoRow r) vm.OpenCommand.Execute(r.Entry);
        };
        RepoList.KeyDown += (_, e) =>
        {
            if (Vm is not { } vm) return;
            if (e.Key == Key.Enter)
            {
                if (RepoList.SelectedItem is GroupHeaderRow h) h.ToggleCommand.Execute(null);
                else vm.OpenCommand.Execute(null);
            }
            else if (e.Key == Key.Delete && SelectedRepos() is { Count: > 0 } repos) vm.RemoveMany(repos);
        };
        RepoList.ContextRequested += (_, e) =>
        {
            e.Handled = true;
            if (Vm is not { } vm) return;
            switch (RowAt(e.Source))
            {
                case GroupHeaderRow h:
                    MenuPresenter.Show(RepoList, vm.MenuForGroup(h));
                    break;
                case RepoRow r:
                    // Right-click outside the current selection acts on that row only.
                    if (!RepoList.Selection.SelectedItems.Contains(r)) RepoList.SelectedItem = r;
                    MenuPresenter.Show(RepoList, vm.MenuForRepos(SelectedRepos()));
                    break;
            }
        };
        AttachedToVisualTree += (_, _) => FilterBox.Focus();
    }

    private HomeViewModel? Vm => DataContext as HomeViewModel;

    private static HomeRow? RowAt(object? source)
        => (source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as HomeRow;

    private List<RepoEntry> SelectedRepos()
        => RepoList.Selection.SelectedItems.OfType<RepoRow>().Select(r => r.Entry).ToList();
}
