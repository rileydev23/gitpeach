using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitPeach.ViewModels;

namespace GitPeach.Views;

public partial class RepoView : UserControl
{
    private RepoViewModel? _vm;
    private double _wipTop = 360, _commitTop = 230;

    public RepoView()
    {
        InitializeComponent();

        CommitList.ContainerPrepared += (_, e) =>
        {
            if (_vm is not null && e.Index >= _vm.Commits.Count - 25) _ = _vm.LoadMoreAsync();
        };
        CommitList.ContextRequested += (_, e) =>
        {
            if (_vm is null || e.Source is not Control c) return;
            var item = c.FindAncestorOfType<ListBoxItem>();
            if (item?.DataContext is not CommitRowVM row || row.IsWip) { e.Handled = true; return; }
            CommitList.SelectedItem = row;
            ShowMenu(CommitList, _vm.ActionsFor(row), e);
        };
        SidebarList.ContextRequested += (_, e) =>
        {
            if (_vm is null || e.Source is not Control c) return;
            var item = c.FindAncestorOfType<ListBoxItem>();
            if (item?.DataContext is not SidebarItem si || si.IsHeader) { e.Handled = true; return; }
            ShowMenu(SidebarList, _vm.ActionsFor(si), e);
        };
        SidebarList.DoubleTapped += async (_, e) =>
        {
            if (_vm is null || e.Source is not Control c) return;
            if (c.FindAncestorOfType<ListBoxItem>()?.DataContext is SidebarItem { IsHeader: false } si)
                await _vm.ActivateAsync(si);
        };

        DataContextChanged += (_, _) => Attach();
        DetachedFromVisualTree += (_, _) => { };
    }

    private void Attach()
    {
        if (_vm is not null)
        {
            _vm.ScrollToRowRequested -= OnScrollTo;
            _vm.CommitMessageFocusRequested -= OnFocusMessage;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }
        _vm = DataContext as RepoViewModel;
        if (_vm is not null)
        {
            _vm.ScrollToRowRequested += OnScrollTo;
            _vm.CommitMessageFocusRequested += OnFocusMessage;
            _vm.PropertyChanged += OnVmPropertyChanged;
            SetTopHeight(_vm.IsWipMode);
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RepoViewModel.IsWipMode) && _vm is not null)
        {
            // Remember the height the user chose for the mode we are leaving.
            var h = RightGrid.RowDefinitions[0].Height.Value;
            if (_vm.IsWipMode) _commitTop = h; else _wipTop = h;
            SetTopHeight(_vm.IsWipMode);
        }
        else if (e.PropertyName == nameof(RepoViewModel.CommandLog))
            Dispatcher.UIThread.Post(() => LogBox.CaretIndex = int.MaxValue, DispatcherPriority.Background);
    }

    private void OnScrollTo(DiffRowVM row)
    {
        DiffView.ScrollToRow(row);
    }

    private void SetTopHeight(bool wip)
    {
        var rows = RightGrid.RowDefinitions;
        rows[0].MinHeight = 140;
        rows[0].Height = new GridLength(wip ? _wipTop : _commitTop);
    }

    private void OnFocusMessage() => MessageBox.Focus();

    private static void ShowMenu(Control target, IReadOnlyList<(string Header, Func<Task> Action)> actions, ContextRequestedEventArgs e)
    {
        if (actions.Count == 0) { e.Handled = true; return; }
        var menu = new ContextMenu();
        var items = new List<MenuItem>();
        foreach (var (header, action) in actions)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += async (_, _) => await action();
            items.Add(mi);
        }
        menu.ItemsSource = items;
        menu.Open(target);
        e.Handled = true;
    }
}
