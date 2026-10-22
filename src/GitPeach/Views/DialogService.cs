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

    public async Task<bool> Confirm(string title, string message, string? ok = null, string? cancel = null)
    {
        var owner = Owner;
        if (owner is null) return false;
        var w = Make(title, 440);
        bool result = false;
        var okBtn = Btn(ok ?? Loc.T("common.ok"), true);
        var cancelBtn = Btn(cancel ?? Loc.T("common.cancel"));
        okBtn.Click += (_, _) => { result = true; w.Close(); };
        cancelBtn.Click += (_, _) => w.Close();
        w.Content = new StackPanel
        {
            Margin = new Thickness(18), Spacing = 6,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Palette.Text },
                Buttons(okBtn, cancelBtn),
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
        var ok = Btn(Loc.T("common.close"), true);
        ok.Click += (_, _) => w.Close();
        w.Content = new StackPanel
        {
            Margin = new Thickness(18), Spacing = 6,
            Children =
            {
                // Display-only text is a TextBlock: no caret, no focus border, nothing that looks editable.
                new ScrollViewer
                {
                    MaxHeight = 420,
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Palette.Text },
                },
                Buttons(ok),
            },
        };
        w.KeyDown += (_, e) => { if (e.Key is Key.Escape or Key.Enter) w.Close(); };
        await w.ShowDialog(owner);
    }

    public async Task About(string description, string dataFolder, IReadOnlyList<(string Title, (string Gesture, string Description)[] Items)> shortcutGroups,
        Action openDataFolder, string? savedLanguage, Action<string> chooseLanguage,
        bool checkUpdates, Action<bool> setCheckUpdates, Func<Task> checkNow)
    {
        var owner = Owner;
        if (owner is null) return;
        var w = Make(Loc.T("dlg.about.title"), 520);
        var version = typeof(DialogService).Assembly.GetName().Version;
        var close = Btn(Loc.T("common.close"), true);
        close.Click += (_, _) => w.Close();
        var openData = Btn(Loc.T("dlg.about.openData"));
        openData.Click += (_, _) => openDataFolder();

        var header = new DockPanel();
        var logo = new Image
        {
            Source = new Avalonia.Media.Imaging.Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri("avares://GitPeach/Assets/icon64.png"))),
            Width = 48, Height = 48, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top,
        };
        DockPanel.SetDock(logo, Dock.Left);
        header.Children.Add(logo);
        header.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock { Text = "GitPeach", FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = Palette.Text },
                new TextBlock { Text = Loc.F("dlg.about.version", version?.ToString(3)), Foreground = Palette.Dim, FontSize = 12 },
                new TextBlock { Text = description, Foreground = Palette.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) },
            },
        });

        // Shortcuts as a plain two-column table (never a TextBox: this is read-only information).
        var shortcuts = new StackPanel { Spacing = 2 };
        foreach (var (title, items) in shortcutGroups)
        {
            shortcuts.Children.Add(new TextBlock
            {
                Text = title, FontWeight = FontWeight.SemiBold, Foreground = Palette.Text,
                Margin = new Thickness(0, shortcuts.Children.Count == 0 ? 0 : 10, 0, 4),
            });
            foreach (var (gesture, desc) in items)
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("215,*") };
                row.Children.Add(new TextBlock { Text = gesture, Foreground = Palette.Dim, FontSize = 12, FontFamily = new FontFamily("Cascadia Mono, Consolas, DejaVu Sans Mono") });
                var d = new TextBlock { Text = desc, Foreground = Palette.Text, FontSize = 12, TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(d, 1);
                row.Children.Add(d);
                shortcuts.Children.Add(row);
            }
        }

        w.Content = new StackPanel
        {
            Margin = new Thickness(18), Spacing = 14,
            Children =
            {
                header,
                new Border { Height = 1, Background = Palette.Border },
                new TextBlock { Text = Loc.T("dlg.about.shortcuts"), FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Palette.Text },
                new ScrollViewer
                {
                    MaxHeight = 380,
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    Content = shortcuts,
                },
                new Border { Height = 1, Background = Palette.Border },
                LanguagePicker(savedLanguage, chooseLanguage),
                UpdatesRow(checkUpdates, setCheckUpdates, () => { w.Close(); _ = checkNow(); }),
                new TextBlock { Text = Loc.F("dlg.about.dataPath", dataFolder), Foreground = Palette.Dim, FontSize = 11, TextWrapping = TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { openData, close } },
            },
        };
        w.KeyDown += (_, e) => { if (e.Key is Key.Escape or Key.Enter) w.Close(); };
        await w.ShowDialog(owner);
    }

    /// <summary>"[x] Check for updates once a day   [Check now]".</summary>
    private static Control UpdatesRow(bool enabled, Action<bool> set, Action checkNow)
    {
        var auto = new CheckBox { Content = Loc.T("update.auto"), IsChecked = enabled, Foreground = Palette.Text, VerticalAlignment = VerticalAlignment.Center };
        auto.IsCheckedChanged += (_, _) => set(auto.IsChecked == true);
        var now = Btn(Loc.T("update.checkNow"));
        now.Click += (_, _) => checkNow();
        var row = new DockPanel();
        DockPanel.SetDock(now, Dock.Right);
        row.Children.Add(now);
        row.Children.Add(auto);
        return row;
    }

    public async Task<UpdateChoice> Update(UpdateInfo update, Version current)
    {
        var owner = Owner;
        if (owner is null) return UpdateChoice.Later;
        var w = Make(Loc.F("update.title", update.Version.ToString(3)), 520);
        var result = UpdateChoice.Later;
        var install = Btn(Loc.T("update.install"), true);
        var later = Btn(Loc.T("update.later"));
        var skip = Btn(Loc.T("update.skip"));
        install.Click += (_, _) => { result = UpdateChoice.Install; w.Close(); };
        later.Click += (_, _) => w.Close();
        skip.Click += (_, _) => { result = UpdateChoice.Skip; w.Close(); };
        var page = new Button { Content = Loc.T("update.page"), Foreground = Palette.Accent, VerticalAlignment = VerticalAlignment.Center };
        page.Classes.Add("flat");
        page.Click += (_, _) => Updater.OpenInBrowser(update.PageUrl);

        var notes = Updater.ReleaseNotesText(update.Notes);
        var buttons = new DockPanel();
        DockPanel.SetDock(page, Dock.Left);
        buttons.Children.Add(page);
        buttons.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right,
            Children = { install, later, skip } });
        w.Content = new StackPanel
        {
            Margin = new Thickness(18), Spacing = 14,
            Children =
            {
                new TextBlock { Text = Loc.F("update.text", current.ToString(3), update.Version.ToString(3)), TextWrapping = TextWrapping.Wrap, Foreground = Palette.Text },
                new Border
                {
                    IsVisible = notes.Length > 0, Background = Palette.Panel2, CornerRadius = new CornerRadius(4), Padding = new Thickness(12, 10),
                    Child = new ScrollViewer
                    {
                        MaxHeight = 260,
                        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                        Content = new TextBlock { Text = notes, TextWrapping = TextWrapping.Wrap, Foreground = Palette.Text, FontSize = 12 },
                    },
                },
                new TextBlock { Text = Loc.T("update.restartNote"), Foreground = Palette.Dim, FontSize = 11, TextWrapping = TextWrapping.Wrap },
                buttons,
            },
        };
        w.KeyDown += (_, e) => { if (e.Key == Key.Escape) w.Close(); };
        await w.ShowDialog(owner);
        return result;
    }

    /// <summary>
    /// "Idioma: [English] [Español]": the joined view switch of the design system. The choice is saved at once and
    /// applies on the next launch, since every screen reads its texts when it is built.
    /// </summary>
    private static Control LanguagePicker(string? saved, Action<string> choose)
    {
        var note = new TextBlock { Text = Loc.T("dlg.about.languageNext"), Foreground = Palette.Amber, FontSize = 11, IsVisible = false, VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(new TextBlock { Text = Loc.T("dlg.about.language"), Foreground = Palette.Text, VerticalAlignment = VerticalAlignment.Center });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var chosen = saved ?? Loc.Current;
        for (int i = 0; i < Loc.Languages.Length; i++)
        {
            var (code, name) = Loc.Languages[i];
            var b = new Button { Content = name, Tag = code };
            b.Classes.Add("mode");
            b.Classes.Add(i == 0 ? "left" : "right");
            if (code == chosen) b.Classes.Add("active");
            b.Click += (_, _) =>
            {
                foreach (var other in buttons.Children.OfType<Button>()) other.Classes.Set("active", ReferenceEquals(other, b));
                choose(code);
                note.IsVisible = code != Loc.Current;
            };
            buttons.Children.Add(b);
        }
        row.Children.Add(buttons);
        row.Children.Add(note);
        return row;
    }

    public async Task<string?> Prompt(string title, string label, string initial = "")
    {
        var r = await Form(title, new[] { (label, initial) });
        return r?[0];
    }

    public async Task<string[]?> Form(string title, (string Label, string Initial)[] fields, string? ok = null)
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
        var okBtn = Btn(ok ?? Loc.T("common.ok"), true);
        var cancel = Btn(Loc.T("common.cancel"));
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

    public async Task<string?> Choose(string title, string label, IReadOnlyList<string> options, string? ok = null)
    {
        var owner = Owner;
        if (owner is null || options.Count == 0) return null;
        var w = Make(title, 420);
        string? result = null;
        var combo = new ComboBox { ItemsSource = options, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var okBtn = Btn(ok ?? Loc.T("common.ok"), true);
        var cancel = Btn(Loc.T("common.cancel"));
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
        var w = Make(Loc.T("dlg.quickOpen.title"), 560);
        w.SizeToContent = SizeToContent.Manual;
        w.Height = 380;
        RepoEntry? result = null;
        var search = new TextBox { Watermark = Loc.T("dlg.quickOpen.search") };
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
