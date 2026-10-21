# Arquitectura de GitPeach

**Patrón:** MVVM (CommunityToolkit.Mvvm) + inyección de dependencias (Microsoft.Extensions.DependencyInjection).
Las vistas solo renderizan y enlazan; los view models no conocen Avalonia, `Process` ni el portapapeles,
sino interfaces de servicios. Todo se arma en un único *composition root*: `App.ConfigureServices()`.

```
Views (XAML + code-behind mínimo)
  │ bindings / MenuPresenter
ViewModels ──► Services (IDialogService, IShellService, IClipboardService, IWindowService, IAppStateStore)
  │
  └──────────► Git (IGitQueries, IGitWorkingTree, IGitConflicts, IGitBranching, IGitStash, IGitRemotes)
                 └─ GitRepository (fachada) ─► IGitRunner (único lugar que lanza git.exe) ─► Parsers (puros)
```

## Capas

| Carpeta | Responsabilidad |
|---|---|
| `Git/` | `GitRunner` ejecuta git. `GitRepository` es una fachada que arma argumentos y parsea salida. `Parsers/` son funciones puras (testeadas). |
| `Models/` | Datos inmutables o casi: commits, status, refs, diff, blame, opciones de push/pull. |
| `Services/` | Interfaces para la UI y el sistema (`Abstractions.cs`), estado persistente, watcher del disco. |
| `ViewModels/` | Estado y comportamiento de pantalla. |
| `Views/` | XAML, implementaciones de servicios que necesitan Avalonia (`DialogService`, `WindowService`, `ClipboardService`). |

## Pestaña de repositorio

`RepoViewModel` coordina el refresco e implementa `IRepoSession` para sus hijos, que dependen de esa interfaz y no de la clase:

- `SidebarViewModel`: ramas, remotos, tags y stashes, más sus menús.
- `HistoryViewModel`: lista de commits, grafo, paginación y búsqueda.
- `WorkingTreeViewModel`: archivos con y sin stage, conflictos y commit.
- `DetailsViewModel` + `DiffPaneViewModel`: detalle del commit y diff (con caché).
- `CommandLogViewModel`: log de comandos.
- `RepoActions`: flujos de git con confirmación (push, pull, ramas, tags, stash, reset…). Se escriben una sola vez y los usan la barra, el sidebar y el menú de commits.
- `FileActions`: todo lo que se hace sobre un archivo (stage, conflictos, .gitignore, abrir, historial, blame).
- `ConflictResolverViewModel`: herramienta de merge propia (bloques de `ConflictParser`, elegir mía/entrante/ambas o editar). Se usa también cuando no hay `merge.tool` configurado.
- `FileViewerViewModel`: visor de texto propio (AvaloniaEdit), de solo lectura hasta pulsar "Editar".

## Convenciones

- **Menús contextuales:** el VM devuelve `IReadOnlyList<MenuAction>` (patrón *command*) y `MenuPresenter` lo dibuja.
- **Ventanas secundarias:** el VM llama a `IWindowService.Show(vm)`. La ventana se resuelve con el registro de `App.ConfigureServices()` (*view locator*).
- **Ejecutar git desde un VM:** con `IRepoSession.Perform` (indicador de ocupado y refresco completo), `PerformQuiet` (stage/descartar, solo refresca el status) o `RunRemote` (muestra el progreso).
- **Atajos:** se declaran en el XAML (`KeyBindings`) y se listan en `ViewModels/Shortcuts.cs` para el diálogo F1.

## Tests

`tests/GitPeach.Tests` (xUnit):
- Unitarios de parsers, grafo, diff y helpers.
- De integración, que crean repos reales en `%TEMP%` aislados de la configuración global de git (`GIT_CONFIG_GLOBAL`).

```
dotnet test GitPeach.sln
```
