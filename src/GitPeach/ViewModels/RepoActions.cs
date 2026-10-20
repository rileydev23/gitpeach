using GitPeach.Git;
using GitPeach.Models;
using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>
/// User-level git workflows (ask, confirm, run, report) shared by the toolbar, the sidebar and the
/// commit list, so each flow is written once.
/// </summary>
public sealed class RepoActions(IRepoSession session, IGitRepository git, IDialogService dialogs)
{
    private RepoSnapshot Snap => session.Snapshot;

    // ---------- branches ----------

    public Task Checkout(string name) => session.Perform($"checkout {name}", () => git.Checkout(name));

    public Task CheckoutRemote(BranchInfo rb)
    {
        var local = Snap.Refs.Locals.FirstOrDefault(l => l.Name == rb.ShortRemoteName);
        return local is not null
            ? Checkout(local.Name)
            : session.Perform($"checkout {rb.ShortRemoteName}", () => git.CheckoutRemote(rb.Name, rb.ShortRemoteName));
    }

    public async Task NewBranch(string? startPoint)
    {
        var n = await dialogs.Prompt("Nueva rama", startPoint is null ? "Nombre de la rama (desde HEAD)" : "Nombre de la rama");
        if (!string.IsNullOrWhiteSpace(n)) await session.Perform("nueva rama", () => git.CreateBranch(n.Trim(), startPoint, true));
    }

    public async Task RenameBranch(BranchInfo b)
    {
        var n = await dialogs.Prompt("Renombrar rama", "Nuevo nombre", b.Name);
        if (!string.IsNullOrWhiteSpace(n) && n.Trim() != b.Name) await session.Perform("renombrar", () => git.RenameBranch(b.Name, n.Trim()));
    }

    public async Task DeleteBranch(BranchInfo b)
    {
        if (!await dialogs.Confirm("Eliminar rama", $"¿Eliminar la rama '{b.Name}'?", "Eliminar")) return;
        await session.Perform("eliminar rama", async () =>
        {
            var r = await git.DeleteBranch(b.Name, false);
            if (!r.Ok && await dialogs.Confirm("La rama no está fusionada", r.Message + "\n\n¿Forzar eliminación?", "Forzar"))
                r = await git.DeleteBranch(b.Name, true);
            return r;
        });
    }

    public async Task DeleteRemoteBranch(BranchInfo rb)
    {
        if (await dialogs.Confirm("Eliminar rama remota", $"¿Eliminar '{rb.ShortRemoteName}' de '{rb.Remote}'?", "Eliminar"))
            await session.Perform("eliminar rama remota", () => git.DeleteRemoteBranch(rb.Remote!, rb.ShortRemoteName));
    }

    public Task Merge(string name) => session.Perform($"merge {name}", () => git.Merge(name));
    public Task Rebase(string onto) => session.Perform($"rebase {onto}", () => git.Rebase(onto));

    // ---------- commits ----------

    public Task CherryPick(Commit c) => session.Perform("cherry-pick", () => git.CherryPick(c.Hash));
    public Task Revert(Commit c) => session.Perform("revert", () => git.Revert(c.Hash));

    public async Task ResetTo(Commit c, string mode)
    {
        if (mode == "hard" && !await dialogs.Confirm("Reset hard", $"Se perderán los cambios sin commit y se moverá la rama a {c.ShortHash}. ¿Continuar?", "Reset hard")) return;
        await session.Perform($"reset --{mode}", () => git.ResetTo(c.Hash, mode));
    }

    public Task Abort() => session.Perform("abortar", () => git.Abort(Snap.Operation));
    public Task Continue() => session.Perform("continuar", () => git.Continue(Snap.Operation));

    // ---------- tags ----------

    public async Task CreateTag(string target)
    {
        var n = await dialogs.Prompt("Nuevo tag", "Nombre del tag");
        if (!string.IsNullOrWhiteSpace(n)) await session.Perform("tag", () => git.CreateTag(n.Trim(), target));
    }

    public async Task DeleteTag(TagInfo t)
    {
        if (await dialogs.Confirm("Eliminar tag", $"¿Eliminar el tag local '{t.Name}'?", "Eliminar"))
            await session.Perform("eliminar tag", () => git.DeleteTag(t.Name));
    }

    public async Task PushTag(TagInfo t)
    {
        var remote = await PickRemote("Push del tag");
        if (remote is not null) await session.RunRemote("push tag", p => git.PushTag(remote, t.Name, p));
    }

    public async Task DeleteRemoteTag(TagInfo t)
    {
        var remote = await PickRemote("Eliminar tag remoto");
        if (remote is null) return;
        if (await dialogs.Confirm("Eliminar tag remoto", $"¿Eliminar el tag '{t.Name}' de '{remote}'?", "Eliminar"))
            await session.RunRemote("eliminar tag remoto", _ => git.DeleteRemoteTag(remote, t.Name));
    }

    // ---------- stash ----------

    public async Task Stash()
    {
        var m = await dialogs.Prompt("Stash", "Mensaje (opcional)");
        if (m is not null) await session.Perform("stash", () => git.StashPush(m));
    }

    public async Task PopLatest()
    {
        if (Snap.Stashes.Count == 0) { session.Notify("No hay stashes"); return; }
        await StashPop(Snap.Stashes[0]);
    }

    public Task StashPop(StashInfo s) => session.Perform("stash pop", () => git.StashPop(s.Name));
    public Task StashApply(StashInfo s) => session.Perform("stash apply", () => git.StashApply(s.Name));

    public async Task StashDrop(StashInfo s)
    {
        if (await dialogs.Confirm("Eliminar stash", $"¿Eliminar {s.Name}?", "Eliminar"))
            await session.Perform("stash drop", () => git.StashDrop(s.Name));
    }

    // ---------- remotes ----------

    public Task Fetch() => session.RunRemote("fetch", p => git.Fetch(p));

    public Task Pull(PullMode mode)
    {
        var text = mode switch { PullMode.Rebase => "pull --rebase", PullMode.FastForwardOnly => "pull --ff-only", _ => "pull" };
        return session.RunRemote(text, p => git.Pull(mode, p));
    }

    /// <summary>Pushes the current branch; sets the upstream on the first push.</summary>
    public async Task Push(bool force = false, string? remote = null)
    {
        var status = Snap.Status;
        if (status.IsDetached)
        {
            await dialogs.Message("Push", "No se puede hacer push con HEAD desacoplado.");
            return;
        }
        if (force && !await dialogs.Confirm("Push forzado",
                $"Se sobrescribirá '{status.Branch}' en el remoto con tu versión local (--force-with-lease: falla si alguien más subió cambios que no tienes).",
                "Forzar push"))
            return;

        PushOptions options;
        if (remote is not null || status.Upstream is null)
        {
            remote ??= Snap.Refs.RemoteNames.FirstOrDefault();
            if (remote is null) { await dialogs.Message("Push", "No hay remotos configurados."); return; }
            options = new PushOptions { Remote = remote, Branch = status.Branch, SetUpstream = status.Upstream is null, ForceWithLease = force };
        }
        else options = new PushOptions { ForceWithLease = force };

        await session.RunRemote(force ? "push forzado" : "push", p => git.Push(options, p));
    }

    public async Task PushToRemote()
    {
        var remote = await PickRemote("Push a remoto");
        if (remote is not null) await Push(remote: remote);
    }

    public async Task PushTags()
    {
        var remote = await PickRemote("Push de tags");
        if (remote is not null) await session.RunRemote("push --tags", p => git.Push(new PushOptions { Remote = remote, Tags = true }, p));
    }

    public async Task PushBranch(BranchInfo b)
    {
        if (b.IsCurrent) { await Push(); return; }
        var remote = Snap.Refs.RemoteNames.FirstOrDefault();
        if (b.Upstream is null && remote is null)
        {
            await dialogs.Message("Push", "No hay remotos configurados.");
            return;
        }
        var options = b.Upstream is null
            ? new PushOptions { Remote = remote, Branch = b.Name, SetUpstream = true }
            : new PushOptions { Remote = b.Upstream.Split('/')[0], Branch = b.Name };
        await session.RunRemote("push", p => git.Push(options, p));
    }

    /// <summary>The only remote, or the one the user picks; null when there are none or the user cancels.</summary>
    private async Task<string?> PickRemote(string title)
    {
        var remotes = Snap.Refs.RemoteNames;
        if (remotes.Count == 0) { await dialogs.Message(title, "No hay remotos configurados."); return null; }
        if (remotes.Count == 1) return remotes[0];
        return await dialogs.Choose(title, "Remoto", remotes);
    }
}
