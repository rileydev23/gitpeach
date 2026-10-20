using System.ComponentModel;
using Avalonia.Controls;
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
            if (_vm is not null && e.Index >= _vm.History.Commits.Count - 25) _ = _vm.History.LoadMoreAsync();
        };
        MenuPresenter.Attach<CommitRowVM>(CommitList, row => _vm?.History.MenuFor(row) ?? Array.Empty<MenuAction>());
        MenuPresenter.Attach<SidebarItem>(SidebarList, si => si.IsHeader || _vm is null ? Array.Empty<MenuAction>() : _vm.Sidebar.MenuFor(si), select: false);
        MenuPresenter.Attach<FileEntryVM>(UnstagedList, f => _vm?.WorkingTree.MenuFor(f) ?? Array.Empty<MenuAction>());
        MenuPresenter.Attach<FileEntryVM>(StagedList, f => _vm?.WorkingTree.MenuFor(f) ?? Array.Empty<MenuAction>());
        MenuPresenter.Attach<CommitFileVM>(CommitFilesList, f =>
            _vm?.Details.CurrentCommit is { } c ? _vm.WorkingTree.Files.MenuFor(f, c) : Array.Empty<MenuAction>());

        SidebarList.DoubleTapped += async (_, e) =>
        {
            if (_vm is null || e.Source is not Control c) return;
            if (c.FindAncestorOfType<ListBoxItem>()?.DataContext is SidebarItem { IsHeader: false } si)
                await _vm.Sidebar.ActivateAsync(si);
        };

        DataContextChanged += (_, _) => Attach();
    }

    private void Attach()
    {
        if (_vm is not null)
        {
            _vm.WorkingTree.CommitMessageFocusRequested -= OnFocusMessage;
            _vm.History.FocusSearchRequested -= OnFocusSearch;
            _vm.Details.PropertyChanged -= OnDetailsChanged;
            _vm.Log.PropertyChanged -= OnLogChanged;
        }
        _vm = DataContext as RepoViewModel;
        if (_vm is not null)
        {
            _vm.WorkingTree.CommitMessageFocusRequested += OnFocusMessage;
            _vm.History.FocusSearchRequested += OnFocusSearch;
            _vm.Details.PropertyChanged += OnDetailsChanged;
            _vm.Log.PropertyChanged += OnLogChanged;
            SetTopHeight(_vm.Details.IsWipMode);
        }
    }

    private void OnDetailsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DetailsViewModel.IsWipMode) || _vm is null) return;
        // Remember the height the user chose for the mode we are leaving.
        var h = RightGrid.RowDefinitions[0].Height.Value;
        if (_vm.Details.IsWipMode) _commitTop = h; else _wipTop = h;
        SetTopHeight(_vm.Details.IsWipMode);
    }

    private void OnLogChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandLogViewModel.Text))
            Dispatcher.UIThread.Post(() => LogBox.CaretIndex = int.MaxValue, DispatcherPriority.Background);
    }

    private void SetTopHeight(bool wip)
    {
        var rows = RightGrid.RowDefinitions;
        rows[0].MinHeight = 140;
        rows[0].Height = new GridLength(wip ? _wipTop : _commitTop);
    }

    private void OnFocusMessage() => MessageBox.Focus();

    private void OnFocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }
}
