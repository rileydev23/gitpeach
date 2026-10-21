using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using GitPeach.Controls;
using GitPeach.ViewModels;

namespace GitPeach.Views;

public partial class FileViewerWindow : Window
{
    private bool _closeConfirmed;

    public FileViewerWindow()
    {
        InitializeComponent();
        var o = Editor.Options;
        o.HighlightCurrentLine = true;
        o.EnableHyperlinks = false;
        o.EnableEmailHyperlinks = false;
        o.ConvertTabsToSpaces = false;
        var area = Editor.TextArea;
        area.TextView.CurrentLineBackground = new SolidColorBrush(Color.Parse("#232428"));
        area.TextView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
        area.SelectionBrush = new SolidColorBrush(Color.Parse("#33507a"));
        area.SelectionBorder = null;
        area.SelectionCornerRadius = 0;
        area.SelectionForeground = null;
        area.Caret.CaretBrush = Palette.Accent;

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Opened += (_, _) => Editor.TextArea.Focus();
        Closing += async (_, e) =>
        {
            if (_closeConfirmed || DataContext is not FileViewerViewModel { IsDirty: true } vm) return;
            e.Cancel = true;
            if (await vm.ConfirmCloseAsync()) { _closeConfirmed = true; Close(); }
        };
    }
}
