using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitPeach.Git.Parsers;
using GitPeach.Models;
using GitPeach.Services;

namespace GitPeach.ViewModels;

public enum ConflictChoice { None, Ours, Theirs, OursThenTheirs, TheirsThenOurs, Custom }

/// <summary>
/// GitPeach's own merge tool: shows each conflict block of a file with both versions, lets the user
/// pick one, both or write the result, then saves the file and marks it as resolved.
/// </summary>
public sealed partial class ConflictResolverViewModel : ObservableObject
{
    private readonly FileActions _files;
    private readonly IDialogService _dialogs;
    private TextFile? _file;
    private bool _saved;

    public string FullPath { get; }
    public string FilePath { get; }
    public string Title => Loc.F("conflict.title", FilePath);
    public string OursName { get; }
    public string TheirsName { get; }
    public string AllOursText => Loc.F("conflict.all", OursName);
    public string AllTheirsText => Loc.F("conflict.all", TheirsName);

    [ObservableProperty] private IReadOnlyList<object> _segments = Array.Empty<object>();
    [ObservableProperty] private string? _message;
    [ObservableProperty] private string _progress = Loc.T("conflict.loading");
    [ObservableProperty] private bool _canSave;

    public bool HasMessage => Message is not null;
    private IEnumerable<ConflictBlockVM> Blocks => Segments.OfType<ConflictBlockVM>();

    /// <summary>The window closes itself when this fires (after saving).</summary>
    public event Action? CloseRequested;

    public ConflictResolverViewModel(string fullPath, string relativePath, bool isRebase, FileActions files, IDialogService dialogs)
    {
        FullPath = fullPath;
        FilePath = relativePath.Replace('\\', '/');
        _files = files;
        _dialogs = dialogs;
        // During a rebase git swaps the sides: "ours" is the branch being rebased onto.
        (OursName, TheirsName) = isRebase
            ? (Loc.T("conflict.side.base"), Loc.T("conflict.side.yourCommit"))
            : (Loc.T("conflict.side.mine"), Loc.T("conflict.side.incoming"));
        _ = LoadAsync();
    }

    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));

    [RelayCommand]
    private async Task LoadAsync()
    {
        Message = null;
        try
        {
            _file = File.Exists(FullPath) ? await TextFile.ReadAsync(FullPath) : null;
        }
        catch (Exception ex)
        {
            _file = null;
            Message = Loc.F("conflict.readError", ex.Message);
        }
        if (_file is null)
        {
            Segments = Array.Empty<object>();
            Message ??= File.Exists(FullPath)
                ? Loc.T("conflict.binary")
                : Loc.T("conflict.missing");
            UpdateProgress();
            return;
        }

        var parsed = ConflictParser.Parse(_file.Text);
        var list = new List<object>();
        int n = 0, count = parsed.Count;
        for (int i = 0; i < count; i++)
        {
            if (parsed[i] is ConflictBlock b)
            {
                var vm = new ConflictBlockVM(b, ++n, OursName, TheirsName);
                vm.Changed += UpdateProgress;
                list.Add(vm);
            }
            else if (parsed[i] is CommonSegment c)
                list.Add(new CommonSegmentVM(c, isFirst: i == 0, isLast: i == count - 1));
        }
        Segments = list;
        if (n == 0)
            Message = Loc.T("conflict.noMarkers");
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        int total = Blocks.Count(), done = Blocks.Count(b => b.IsResolved);
        Progress = total == 0 ? "" : Loc.F(total == 1 ? "conflict.progress.one" : "conflict.progress.many", done, total);
        CanSave = _file is not null && done == total;
    }

    [RelayCommand]
    private void UseAllOurs()
    {
        foreach (var b in Blocks) b.Choose(ConflictChoice.Ours);
    }

    [RelayCommand]
    private void UseAllTheirs()
    {
        foreach (var b in Blocks) b.Choose(ConflictChoice.Theirs);
    }

    /// <summary>The file with every block replaced by its result, using the file's own line endings.</summary>
    public string BuildResult()
    {
        var text = _file?.Text ?? "";
        var eol = text.Contains("\r\n") ? "\r\n" : "\n";
        var sb = new StringBuilder(text.Length);
        foreach (var s in Segments)
        {
            if (s is CommonSegmentVM c) sb.Append(c.Text);
            else if (s is ConflictBlockVM b) sb.Append(b.ResultFor(eol));
        }
        return sb.ToString();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_file is null) return;
        if (!CanSave)
        {
            await _dialogs.Message(Loc.T("conflict.unresolved.title"), Loc.T("conflict.unresolved.text"));
            return;
        }
        try
        {
            await _file.WriteAsync(FullPath, BuildResult());
        }
        catch (Exception ex)
        {
            await _dialogs.Message(Loc.T("conflict.saveError"), ex.Message);
            return;
        }
        _saved = true;
        await _files.MarkResolved(new[] { FilePath });
        CloseRequested?.Invoke();
    }

    /// <summary>For files without markers left (resolved by hand or elsewhere).</summary>
    [RelayCommand]
    private async Task MarkResolvedAsync()
    {
        await _files.MarkResolved(new[] { FilePath });
        CloseRequested?.Invoke();
    }

    [RelayCommand] private void EditWholeFile() => _files.ViewFile(FilePath, edit: true);
    [RelayCommand] private void OpenExternal() => _files.OpenWithDefaultApp(FilePath);

    /// <summary>Choices were made but not written to the file yet: closing asks first.</summary>
    public bool HasUnsavedChoices => !_saved && Blocks.Any(b => b.IsResolved);

    public Task<bool> ConfirmCloseAsync() => _dialogs.Confirm(Loc.T("conflict.unsaved.title"),
        Loc.T("conflict.unsaved.text"), Loc.T("conflict.unsaved.close"), Loc.T("conflict.unsaved.back"));
}

/// <summary>Unchanged text between conflicts; long runs are shown collapsed to a few lines of context.</summary>
public sealed class CommonSegmentVM
{
    private const int Context = 3;

    public string Text { get; }
    public string Display { get; }

    public CommonSegmentVM(CommonSegment s, bool isFirst, bool isLast)
    {
        Text = s.Text;
        var lines = s.Text.TrimEnd('\n').TrimEnd('\r').Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        // Before the first conflict only the lines just above matter, after the last one only those just below.
        int head = isFirst ? 0 : Context, tail = isLast ? 0 : Context;
        if (lines.Length <= head + tail + 1) { Display = string.Join('\n', lines); return; }
        var hidden = lines.Length - head - tail;
        var parts = lines.Take(head)
            .Append(Loc.Plural("conflict.hiddenLines", hidden))
            .Concat(lines.Skip(lines.Length - tail));
        Display = string.Join('\n', parts);
    }
}

public sealed partial class ConflictBlockVM : ObservableObject
{
    private readonly ConflictBlock _block;
    private bool _applying;

    public int Number { get; }
    public string NumberText => Loc.F("conflict.number", Number);
    public string BothOursFirst => Loc.F("conflict.both", OursName);
    public string BothTheirsFirst => Loc.F("conflict.both", TheirsName);
    public string Ours => Show(_block.Ours);
    public string Theirs => Show(_block.Theirs);
    public string? Base => _block.Base is null ? null : Show(_block.Base);
    public bool HasBase => _block.Base is not null;
    public string OursHeader { get; }
    public string TheirsHeader { get; }

    [ObservableProperty] private ConflictChoice _choice;
    [ObservableProperty] private string _result = "";

    public bool IsResolved => Choice != ConflictChoice.None;
    public bool IsOurs => Choice is ConflictChoice.Ours;
    public bool IsTheirs => Choice is ConflictChoice.Theirs;
    public string ChoiceText => Choice switch
    {
        ConflictChoice.Ours => Loc.F("conflict.choice.side", OursName.ToLowerInvariant()),
        ConflictChoice.Theirs => Loc.F("conflict.choice.side", TheirsName.ToLowerInvariant()),
        ConflictChoice.OursThenTheirs or ConflictChoice.TheirsThenOurs => Loc.T("conflict.choice.both"),
        ConflictChoice.Custom => Loc.T("conflict.choice.custom"),
        _ => Loc.T("conflict.choice.none"),
    };

    public string OursName { get; }
    public string TheirsName { get; }

    public event Action? Changed;

    public ConflictBlockVM(ConflictBlock block, int number, string oursName, string theirsName)
    {
        _block = block;
        Number = number;
        OursName = oursName;
        TheirsName = theirsName;
        OursHeader = string.IsNullOrEmpty(block.OursLabel) ? oursName : $"{oursName} · {block.OursLabel}";
        TheirsHeader = string.IsNullOrEmpty(block.TheirsLabel) ? theirsName : $"{theirsName} · {block.TheirsLabel}";
    }

    private static string Show(string s) => s.TrimEnd('\n').TrimEnd('\r').Replace("\r\n", "\n");

    partial void OnChoiceChanged(ConflictChoice value)
    {
        OnPropertyChanged(nameof(IsResolved));
        OnPropertyChanged(nameof(IsOurs));
        OnPropertyChanged(nameof(IsTheirs));
        OnPropertyChanged(nameof(ChoiceText));
        Changed?.Invoke();
    }

    // Typing in the result box makes it a hand-written resolution.
    partial void OnResultChanged(string value)
    {
        if (!_applying) Choice = ConflictChoice.Custom;
    }

    public void Choose(ConflictChoice choice)
    {
        _applying = true;
        Result = choice switch
        {
            ConflictChoice.Ours => Ours,
            ConflictChoice.Theirs => Theirs,
            ConflictChoice.OursThenTheirs => Join(Ours, Theirs),
            ConflictChoice.TheirsThenOurs => Join(Theirs, Ours),
            _ => Result,
        };
        _applying = false;
        Choice = choice;
    }

    private static string Join(string a, string b) => a.Length == 0 ? b : b.Length == 0 ? a : a + "\n" + b;

    [RelayCommand] private void UseOurs() => Choose(ConflictChoice.Ours);
    [RelayCommand] private void UseTheirs() => Choose(ConflictChoice.Theirs);
    [RelayCommand] private void UseOursThenTheirs() => Choose(ConflictChoice.OursThenTheirs);
    [RelayCommand] private void UseTheirsThenOurs() => Choose(ConflictChoice.TheirsThenOurs);

    [RelayCommand]
    private void Reset()
    {
        _applying = true;
        Result = "";
        _applying = false;
        Choice = ConflictChoice.None;
    }

    /// <summary>
    /// The text that replaces the block on disk. Picking a whole side reuses its exact bytes; anything
    /// else is normalised to the file's line endings and ends with a newline so it does not glue onto
    /// the following line.
    /// </summary>
    public string ResultFor(string eol) => Choice switch
    {
        ConflictChoice.Ours => _block.Ours,
        ConflictChoice.Theirs => _block.Theirs,
        _ when Result.Length == 0 => "",
        _ => Result.Replace("\r\n", "\n").Replace("\n", eol) + eol,
    };
}
