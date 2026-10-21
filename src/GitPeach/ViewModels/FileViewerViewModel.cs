using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>
/// Minimal built-in viewer for a working-tree file, so nothing external is needed to look at it.
/// Read-only until the user presses "Editar" (as in TextPeach); writes to disk only on "Guardar".
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
    [ObservableProperty] private string _info = "Cargando…";

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
            Message = "No se pudo leer el archivo: " + ex.Message;
            Info = "";
            return;
        }
        if (_file is null)
        {
            Message = "Es un archivo binario o demasiado grande para el visor de GitPeach.";
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
        var enc = _file.Encoding.WebName.ToUpperInvariant() + (_file.Encoding.GetPreamble().Length > 0 ? " con BOM" : "");
        Info = $"{Document.LineCount} líneas · {enc} · {(IsEditing ? "Editando" : "Solo lectura")}";
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
            await _dialogs.Message("No se pudo guardar", ex.Message);
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
            if (!await _dialogs.Confirm("Cambios sin guardar",
                    $"{RelativePath} tiene cambios sin guardar. ¿Descartarlos y volver a solo lectura?", "Descartar cambios"))
                return;
            SetText(_file?.Text ?? "");
        }
        IsEditing = false;
    }

    /// <summary>Asked by the window before closing; false keeps it open.</summary>
    public async Task<bool> ConfirmCloseAsync() => !IsDirty ||
        await _dialogs.Confirm("Cambios sin guardar", $"{RelativePath} tiene cambios sin guardar. Si cierras se perderán.",
            "Cerrar sin guardar", "Volver");

    [RelayCommand] private void OpenExternal() => _shell.OpenFile(FullPath);
}
