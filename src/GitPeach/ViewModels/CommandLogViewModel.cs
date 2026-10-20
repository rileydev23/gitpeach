using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GitPeach.ViewModels;

/// <summary>Scroll-back of the git commands run in a tab (bounded to ~60 KB).</summary>
public sealed partial class CommandLogViewModel : ObservableObject
{
    private const int MaxLength = 60000, KeepLength = 40000;

    [ObservableProperty] private string _text = "";
    [ObservableProperty] private bool _isVisible;

    public void Append(string entry)
    {
        var t = Text + (Text.Length > 0 ? "\n\n" : "") + entry;
        Text = t.Length > MaxLength ? t[^KeepLength..] : t;
    }

    [RelayCommand] private void Toggle() => IsVisible = !IsVisible;
    [RelayCommand] private void Clear() => Text = "";
}
