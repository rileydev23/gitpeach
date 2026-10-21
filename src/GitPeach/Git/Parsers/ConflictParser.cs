using GitPeach.Models;

namespace GitPeach.Git.Parsers;

/// <summary>
/// Splits a file with conflict markers into common text and conflict blocks. Supports the default
/// style and diff3/zdiff3 (a "|||||||" base section). Line endings are kept exactly, so joining the
/// segments back reproduces the file. An unterminated block is treated as plain text.
/// </summary>
public static class ConflictParser
{
    private const int MarkerSize = 7;

    public static List<ConflictSegment> Parse(string text)
    {
        var segments = new List<ConflictSegment>();
        var lines = SplitKeepingEndings(text);
        var common = new System.Text.StringBuilder();
        int i = 0;
        while (i < lines.Count)
        {
            if (IsMarker(lines[i], '<') && TryReadBlock(lines, i, out var block, out var next))
            {
                if (common.Length > 0) { segments.Add(new CommonSegment(common.ToString())); common.Clear(); }
                segments.Add(block);
                i = next;
            }
            else common.Append(lines[i++]);
        }
        if (common.Length > 0) segments.Add(new CommonSegment(common.ToString()));
        return segments;
    }

    public static bool HasConflicts(string text) => Parse(text).OfType<ConflictBlock>().Any();

    private static bool TryReadBlock(List<string> lines, int start, out ConflictBlock block, out int next)
    {
        block = null!;
        next = start;
        var ours = new System.Text.StringBuilder();
        var bas = new System.Text.StringBuilder();
        var theirs = new System.Text.StringBuilder();
        bool hasBase = false;
        var section = ours;
        for (int i = start + 1; i < lines.Count; i++)
        {
            var l = lines[i];
            if (section == ours && IsMarker(l, '|')) { hasBase = true; section = bas; }
            else if (section != theirs && IsMarker(l, '=')) section = theirs;
            else if (section == theirs && IsMarker(l, '>'))
            {
                block = new ConflictBlock(ours.ToString(), hasBase ? bas.ToString() : null, theirs.ToString(),
                    Label(lines[start]), Label(l));
                next = i + 1;
                return true;
            }
            else if (IsMarker(l, '<')) return false; // a new block started before this one ended
            else section.Append(l);
        }
        return false;
    }

    /// <summary>Seven marker characters followed by a space or the end of the line.</summary>
    private static bool IsMarker(string line, char c)
    {
        if (line.Length < MarkerSize) return false;
        for (int k = 0; k < MarkerSize; k++) if (line[k] != c) return false;
        return line.Length == MarkerSize || line[MarkerSize] is ' ' or '\r' or '\n';
    }

    private static string Label(string markerLine) => markerLine[MarkerSize..].Trim();

    private static List<string> SplitKeepingEndings(string text)
    {
        var lines = new List<string>();
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            lines.Add(text[start..(i + 1)]);
            start = i + 1;
        }
        if (start < text.Length) lines.Add(text[start..]);
        return lines;
    }
}
