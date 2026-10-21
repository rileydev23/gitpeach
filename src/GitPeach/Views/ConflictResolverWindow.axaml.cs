using Avalonia.Controls;
using Avalonia.Input;
using GitPeach.ViewModels;

namespace GitPeach.Views;

public partial class ConflictResolverWindow : Window
{
    private ConflictResolverViewModel? _vm;
    private bool _closeConfirmed;

    public ConflictResolverWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) => { if (e.Key == Key.Escape && e.Source is not TextBox) Close(); };
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.CloseRequested -= OnCloseRequested;
            _vm = DataContext as ConflictResolverViewModel;
            if (_vm is not null) _vm.CloseRequested += OnCloseRequested;
        };
        Closing += async (_, e) =>
        {
            if (_closeConfirmed || _vm is not { HasUnsavedChoices: true }) return;
            e.Cancel = true;
            if (await _vm.ConfirmCloseAsync()) { _closeConfirmed = true; Close(); }
        };
    }

    private void OnCloseRequested()
    {
        _closeConfirmed = true;
        Close();
    }
}
