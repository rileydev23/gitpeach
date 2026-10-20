using Avalonia.Controls;
using Avalonia.Input;

namespace GitPeach.Views;

public partial class FileHistoryWindow : Window
{
    public FileHistoryWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
}
