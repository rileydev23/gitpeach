using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>
/// Minimal built-in viewer for a working-tree file, so nothing external is needed to look at it.
/// Read-only until the user presses "Edit" (as in TextPeach); writes to disk only on "Save".
/// </summary>
public sealed partial class FileViewerViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private TextFile? _file;

    public string FullPath { get; }
    public string RelativePath { get; }
    public TextDocument Document { get; } = new();

    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private string _info = Loc.T("conflict.loading");

    public string Title => (IsDirty ? "● " : IsEditing ? "✎ " : "") + RelativePath;
    public bool CanEdit => IsLoaded && !IsEditing;
    public bool HasMessage => Message is not null;

    public FileViewerViewModel(string fullPath, string relativePath, IDialogService dialogs, IShellService shell, bool edit = false)
    {
        FullPath = fullPath;
        RelativePath = relativePath.Replace('\\', '/');
        _dialogs = dialogs;
        _shell = shell;
        Document.TextChanged += (_, _) =>
        {
            IsDirty = IsEditing && !Document.UndoStack.IsOriginalFile;
            UpdateInfo();
        };
        _ = LoadAsync(edit);
    }

    partial void OnIsLoadedChanged(bool value) => OnPropertyChanged(nameof(CanEdit));
    partial void OnIsEditingChanged(bool value) { OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(Title)); UpdateInfo(); }
    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(Title));
    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));

    private async Task LoadAsync(bool edit)
    {
        try
        {
            _file = await TextFile.ReadAsync(FullPath);
        }
        catch (Exception ex)
        {
            Message = Loc.F("conflict.readError", ex.Message);
            Info = "";
            return;
        }
        if (_file is null)
        {
            Message = Loc.T("viewer.binary");
            Info = "";
            return;
        }
        SetText(_file.Text);
        IsLoaded = true;
        IsEditing = edit;
        UpdateInfo();
    }

    private void SetText(string text)
    {
        Document.Text = text;
        Document.UndoStack.ClearAll();
        Document.UndoStack.MarkAsOriginalFile();
        IsDirty = false;
    }

    private void UpdateInfo()
    {
        if (_file is null) return;
        var enc = _file.Encoding.WebName.ToUpperInvariant() + (_file.Encoding.GetPreamble().Length > 0 ? Loc.T("viewer.bom") : "");
        Info = $"{Loc.Plural("viewer.lines", Document.LineCount)} · {enc} · {Loc.T(IsEditing ? "viewer.editing" : "viewer.readOnly")}";
    }

    [RelayCommand]
    private void Edit()
    {
        if (CanEdit) IsEditing = true;
    }

    [RelayCommand]
    private async Task ToggleEdit()
    {
        if (IsEditing) await FinishEditingAsync();
        else Edit();
    }

    [RelayCommand] private Task Save() => SaveAsync();

    public async Task<bool> SaveAsync()
    {
        if (!IsEditing || _file is null) return true;
        try
        {
            var text = Document.Text;
            await _file.WriteAsync(FullPath, text);
            _file = _file with { Text = text };
            Document.UndoStack.MarkAsOriginalFile();
            IsDirty = false;
            UpdateInfo();
            return true;
        }
        catch (Exception ex)
        {
            await _dialogs.Message(Loc.T("conflict.saveError"), ex.Message);
            return false;
        }
    }

    /// <summary>Back to read-only. Unsaved changes are confirmed and then discarded.</summary>
    [RelayCommand] private Task FinishEditing() => FinishEditingAsync();

    public async Task FinishEditingAsync()
    {
        if (!IsEditing) return;
        if (IsDirty)
        {
            if (!await _dialogs.Confirm(Loc.T("viewer.unsaved.title"), Loc.F("viewer.unsaved.finish", RelativePath), Loc.T("viewer.unsaved.discard")))
                return;
            SetText(_file?.Text ?? "");
        }
        IsEditing = false;
    }

    /// <summary>Asked by the window before closing; false keeps it open.</summary>
    public async Task<bool> ConfirmCloseAsync() => !IsDirty ||
        await _dialogs.Confirm(Loc.T("viewer.unsaved.title"), Loc.F("viewer.unsaved.close", RelativePath),
            Loc.T("conflict.unsaved.close"), Loc.T("conflict.unsaved.back"));

    [RelayCommand] private void OpenExternal() => _shell.OpenFile(FullPath);
}
