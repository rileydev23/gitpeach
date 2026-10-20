using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitPeach.ViewModels;

namespace GitPeach.Views;

public partial class DiffListView : UserControl
{
    public DiffListView() => InitializeComponent();

    public bool ShowPopOut
    {
        get => PopOutButton.IsVisible;
        set => PopOutButton.IsVisible = value;
    }

    public void ScrollToRow(DiffRowVM row)
    {
        // Scroll to the very end first so the target ends up at the top of the viewport.
        if (DiffList.ItemCount > 0) DiffList.ScrollIntoView(DiffList.ItemCount - 1);
        Dispatcher.UIThread.Post(() => DiffList.ScrollIntoView(row), DispatcherPriority.Background);
    }

    private void OnPopOut(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not RepoViewModel vm) return;
        var w = new DiffWindow { DataContext = vm };
        if (Dialogs.Owner is { } owner)
        {
            w.Width = Math.Min(w.Width, owner.Bounds.Width * 0.95);
            w.Height = Math.Min(w.Height, owner.Bounds.Height * 0.95);
            w.Show(owner);
        }
        else w.Show();
    }
}
