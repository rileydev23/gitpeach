using Avalonia.Controls;
using Avalonia.Input;

namespace GitPeach.Views;

public partial class BlameWindow : Window
{
    public BlameWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
}
