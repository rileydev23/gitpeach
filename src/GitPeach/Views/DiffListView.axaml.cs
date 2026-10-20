using Avalonia.Controls;
using Avalonia.Threading;
using GitPeach.ViewModels;

namespace GitPeach.Views;

public partial class DiffListView : UserControl
{
    private DiffPaneViewModel? _vm;

    public DiffListView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.ScrollToRowRequested -= ScrollToRow;
            _vm = DataContext as DiffPaneViewModel;
            if (_vm is not null) _vm.ScrollToRowRequested += ScrollToRow;
        };
    }

    /// <summary>Hides the pop-out button (used when the diff already is in its own window).</summary>
    public bool ShowPopOut
    {
        get => PopOutButton.IsVisible;
        set => PopOutButton.IsVisible = value;
    }

    private void ScrollToRow(DiffRowVM row)
    {
        // Scroll to the very end first so the target ends up at the top of the viewport.
        if (DiffList.ItemCount > 0) DiffList.ScrollIntoView(DiffList.ItemCount - 1);
        Dispatcher.UIThread.Post(() => DiffList.ScrollIntoView(row), DispatcherPriority.Background);
    }
}
