using Avalonia.Controls;
using GitPeach.Services;

namespace GitPeach.Views;

/// <summary>
/// View locator for secondary windows: maps a view-model type to the window that renders it,
/// so view models can open windows without referencing any view.
/// </summary>
public sealed class WindowService : IWindowService
{
    private readonly Dictionary<Type, Func<Window>> _views = new();

    public WindowService Register<TViewModel>(Func<Window> create)
    {
        _views[typeof(TViewModel)] = create;
        return this;
    }

    public void Show(object viewModel)
    {
        if (!_views.TryGetValue(viewModel.GetType(), out var create))
            throw new InvalidOperationException($"No window registered for {viewModel.GetType().Name}");
        var w = create();
        w.DataContext = viewModel;
        if (viewModel is IDisposable d) w.Closed += (_, _) => d.Dispose();
        if (DialogService.Owner is { } owner)
        {
            w.Width = Math.Min(w.Width, owner.Bounds.Width * 0.95);
            w.Height = Math.Min(w.Height, owner.Bounds.Height * 0.95);
            w.Show(owner);
        }
        else w.Show();
    }
}

public sealed class ClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        if (DialogService.Owner?.Clipboard is { } cb) await cb.SetTextAsync(text);
    }
}
