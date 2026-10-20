using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GitPeach.Controls;
using GitPeach.Services;

namespace GitPeach.Views;

/// <summary>Small code-built modal dialogs, so we avoid a XAML file per prompt.</summary>
public sealed class DialogService : IDialogService
{
    /// <summary>The active window (dialogs opened from a secondary window stay on top of it), else the main one.</summary>
    public static Window? Owner
    {
        get
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime l) return null;
            return l.Windows.FirstOrDefault(w => w.IsActive) ?? l.MainWindow;
        }
    }

    private static Window Make(string title, double width)
    {
        return new Window
        {
            Title = title,
            Width = width,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Palette.Panel,
            ShowInTaskbar = false,
        };
    }

    private static Button Btn(string text, bool primary = false) => new()
    {
        Content = text,
        MinWidth = 84,
        Padding = new Thickness(12, 5),
        Background = primary ? Palette.Accent : Palette.Panel2,
        Foreground = Brushes.White,
        HorizontalContentAlignment = HorizontalAlignment.Center,
    };

    private static StackPanel Buttons(params Button[] buttons)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0),
        };
        panel.Children.AddRange(buttons);
        return panel;
    }

    public async Task<bool> Confirm(string title, string message, string ok = "Aceptar")
    {
        var owner = Owner;
        if (owner is null) return false;
        var w = Make(title, 440);
        bool result = false;
        var okBtn = Btn(ok, true);
        var cancel = Btn("Cancelar");
        okBtn.Click += (_, _) => { result = true; w.Close(); };
        cancel.Click += (_, _) => w.Close();
        w.Content = new StackPanel
        {
            Margin = new Thickness(18), Spacing = 6,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Palette.Text },
                Buttons(okBtn, cancel),
            },
        };
        w.KeyDown += (_, e) => { if (e.Key == Key.Escape) w.Close(); };
        await w.ShowDialog(owner);
        return result;
    }

    public async Task Message(string title, string message)
    {
        var owner = Owner;
        if (owner is null) return;
        var w = Make(title, 520);
        var ok = Btn("Cerrar", true);
        ok.Click += (_, _) => w.Close();
        w.Content = new StackPanel
        {
            Margin = new Thickness(18), Spacing = 6,
            Children =
            {
                new TextBox { Text = message, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 420,
                    Background = Palette.Bg, BorderThickness = new Thickness(0), FontFamily = new FontFamily("Consolas") },
                Buttons(ok),
            },
        };
        w.KeyDown += (_, e) => { if (e.Key is Key.Escape or Key.Enter) w.Close(); };
        await w.ShowDialog(owner);
    }

    public async Task<string?> Prompt(string title, string label, string initial = "")
    {
        var r = await Form(title, new[] { (label, initial) });
        return r?[0];
    }

    public async Task<string[]?> Form(string title, (string Label, string Initial)[] fields, string ok = "Aceptar")
    {
        var owner = Owner;
        if (owner is null) return null;
        var w = Make(title, 480);
        string[]? result = null;
        var boxes = new List<TextBox>();
        var panel = new StackPanel { Margin = new Thickness(18), Spacing = 8 };
        foreach (var (label, initial) in fields)
        {
            panel.Children.Add(new TextBlock { Text = label, Foreground = Palette.Dim });
            var tb = new TextBox { Text = initial };
            boxes.Add(tb);
            panel.Children.Add(tb);
        }
        var okBtn = Btn(ok, true);
        var cancel = Btn("Cancelar");
        void Accept() { result = boxes.Select(b => b.Text ?? "").ToArray(); w.Close(); }
        okBtn.Click += (_, _) => Accept();
        cancel.Click += (_, _) => w.Close();
        panel.Children.Add(Buttons(okBtn, cancel));
        w.Content = panel;
        w.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) w.Close();
            else if (e.Key == Key.Enter) Accept();
        };
        w.Opened += (_, _) => { boxes[0].Focus(); boxes[0].SelectAll(); };
        await w.ShowDialog(owner);
        return result;
    }

    public async Task<string?> Choose(string title, string label, IReadOnlyList<string> options, string ok = "Aceptar")
    {
        var owner = Owner;
        if (owner is null || options.Count == 0) return null;
        var w = Make(title, 420);
        string? result = null;
        var combo = new ComboBox { ItemsSource = options, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var okBtn = Btn(ok, true);
        var cancel = Btn("Cancelar");
        void Accept() { result = combo.SelectedItem as string; w.Close(); }
        okBtn.Click += (_, _) => Accept();
        cancel.Click += (_, _) => w.Close();
        w.Content = new StackPanel
        {
            Margin = new Thickness(18), Spacing = 8,
            Children = { new TextBlock { Text = label, Foreground = Palette.Dim }, combo, Buttons(okBtn, cancel) },
        };
        w.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) w.Close();
            else if (e.Key == Key.Enter && !combo.IsDropDownOpen) Accept();
        };
        w.Opened += (_, _) => combo.Focus();
        await w.ShowDialog(owner);
        return result;
    }

    public async Task<string?> PickFolder(string title)
    {
        var owner = Owner;
        if (owner is null) return null;
        var res = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return res.Count > 0 ? res[0].TryGetLocalPath() : null;
    }

    /// <summary>Ctrl+P: fuzzy-pick a saved repository.</summary>
    public async Task<RepoEntry?> QuickOpen(IReadOnlyList<RepoEntry> repos)
    {
        var owner = Owner;
        if (owner is null) return null;
        var w = Make("Abrir repositorio", 560);
        w.SizeToContent = SizeToContent.Manual;
        w.Height = 380;
        RepoEntry? result = null;
        var search = new TextBox { Watermark = "Buscar repositorio..." };
        var list = new ListBox { Background = Palette.Bg, ItemsSource = repos, SelectedIndex = repos.Count > 0 ? 0 : -1 };
        list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<RepoEntry>((r, _) =>
            new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = r?.Name ?? "", FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = r?.Path ?? "", Foreground = Palette.Dim, FontSize = 11 },
                },
            });
        void Filter()
        {
            var q = search.Text ?? "";
            var filtered = repos.Where(r => FuzzyMatcher.Matches(r.Name + " " + r.Path, q)).ToList();
            list.ItemsSource = filtered;
            list.SelectedIndex = filtered.Count > 0 ? 0 : -1;
        }
        search.TextChanged += (_, _) => Filter();
        void Accept() { result = list.SelectedItem as RepoEntry; if (result is not null) w.Close(); }
        list.DoubleTapped += (_, _) => Accept();
        w.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) w.Close();
            else if (e.Key == Key.Enter) Accept();
            else if (e.Key == Key.Down && list.ItemCount > 0) { list.SelectedIndex = Math.Min(list.SelectedIndex + 1, list.ItemCount - 1); e.Handled = true; }
            else if (e.Key == Key.Up && list.ItemCount > 0) { list.SelectedIndex = Math.Max(list.SelectedIndex - 1, 0); e.Handled = true; }
        };
        var grid = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(search, Dock.Top);
        search.Margin = new Thickness(0, 0, 0, 8);
        grid.Children.Add(search);
        grid.Children.Add(list);
        w.Content = grid;
        w.Opened += (_, _) => search.Focus();
        await w.ShowDialog(owner);
        return result;
    }
}
