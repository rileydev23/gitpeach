using Avalonia.Controls;
using Avalonia.Input;

namespace GitPeach.Views;

public partial class DiffWindow : Window
{
    public DiffWindow()
    {
        InitializeComponent();
        Diff.ShowPopOut = false;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
}
