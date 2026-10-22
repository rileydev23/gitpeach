# Contributing to GitPeach

[English](#english) · [Español](#español)

---

## English

Thanks for helping! GitPeach is a desktop git client for Windows and Linux (Avalonia + .NET 10). Small, focused
contributions are the easiest to review and the most likely to be merged.

### Before you start

- **Bugs:** open an [issue](../../issues/new/choose) with steps to reproduce, what you expected and what
  happened. Screenshots help a lot for anything visual. If git printed something, the command log (Ctrl+L)
  shows the exact command and its output.
- **New features:** open an issue first and wait for a reply before writing code. GitPeach deliberately stays
  small; some good ideas will not fit, and it is better to know before you spend time on them.
- Small fixes (typos, an obvious one-line bug) can go straight to a pull request.

### Workflow

1. **Fork** the repository and clone your fork.
2. Create a branch from `main` named after the kind of change:

   | Prefix | For | Example |
   |---|---|---|
   | `feature/` | something new | `feature/interactive-rebase` |
   | `fix/` | a bug | `fix/graph-merge-lanes` |
   | `docs/` | README, DISENO.md, comments only | `docs/shortcuts-table` |
   | `refactor/` | internal change, same behavior | `refactor/split-repo-actions` |
   | `chore/` | build, CI, dependencies | `chore/update-avalonia` |

   Lowercase, words separated by `-`. A check on every pull request rejects other names.
3. Make your change. Keep the pull request about **one thing**; unrelated cleanups go in their own PR.
4. Check that it builds and the tests pass:

   ```
   dotnet build
   dotnet test
   ```

   `dev.cmd` builds in Debug and runs the app. The integration tests need git on the `PATH`.
5. Open a pull request against `main` and fill in the template. Nobody pushes to `main` directly, including
   the maintainer: every change goes through a pull request.

### Commit messages

A short summary of what the change does, in English or Spanish, like the existing history:
`Graph keeps merge lanes straight`. No prefix needed (the branch name already says the kind of change). Add a
blank line and more detail when the *why* is not obvious.

### Code conventions

- **The interface is in English and Spanish.** Every visible text lives in `Services/Strings.cs` as an
  (English, Spanish) pair and is used with `Loc.T("key")` in code or `{l:L key}` in XAML; a test fails if a key
  is missing. Spanish: neutral, *tuteo*, capital letter only at the start. Code, names and comments are in
  English.
- Follow the architecture in [docs/ARQUITECTURA.md](docs/ARQUITECTURA.md): view models never touch Avalonia,
  `Process` or the clipboard directly, only service interfaces; **git only runs through `IGitRunner`**; output
  is parsed by pure functions in `Git/Parsers`.
- Follow [docs/DISENO.md](docs/DISENO.md): only palette colors, one primary button per area, read-only
  information is never shown in an editable field. If you add a new UI pattern, document it there.
- Disk and git work runs off the UI thread.
- **Anything that can lose work asks first** (discard, hard reset, force push, deleting branches or tags…).
- Parsers and other pure logic come with unit tests; git flows come with an integration test that creates a
  temporary repository (see `GitRepositoryTests`).
- Match the style of the surrounding code; no new dependencies without discussing them in an issue first.

### Releases

Versions follow [SemVer](https://semver.org) and nobody edits a version number by hand:

- Each pull request gets a label from its branch name (`feature/` → `enhancement`, `fix/` → `bug`, …), and
  every merge updates a **draft release** with the notes grouped by kind.
- The next version comes from those labels: any `enhancement` raises the minor version (0.3.0 → 0.4.0),
  otherwise the patch (0.3.0 → 0.3.1).
- GitPeach is still at **0.x**, so breaking changes raise the minor version too. The maintainer sets
  **1.0.0** by hand on the draft when the time comes; after that, the `breaking` label raises the major one.
- A merge is not a release: the draft collects several pull requests and is published when it is worth it.
- When the maintainer publishes the draft, the tag `vX.Y.Z` is created and a workflow builds `GitPeach.exe` and
  `GitPeach-linux-x64.tar.gz` with that version and attaches them to the release.

### License of contributions

GitPeach is licensed under the [GNU GPL v3.0 or later](LICENSE). By sending a pull request:

1. Your contribution is licensed under the GPL-3.0-or-later, like the rest of the project.
2. You also grant rileydev23 a perpetual, worldwide, non-exclusive, royalty-free license to use, modify and
   relicense your contribution under other terms. This is what lets the author offer commercial licenses to
   companies that cannot use the GPL; the GPL version stays available to everyone.
3. You keep the copyright of your work, and you confirm that you wrote it yourself or have the right to
   contribute it.

---

## Español

¡Gracias por ayudar! GitPeach es un cliente de git de escritorio para Windows y Linux (Avalonia + .NET 10). Las
contribuciones pequeñas y enfocadas son las más fáciles de revisar y las que más probablemente se integren.

### Antes de empezar

- **Errores:** abre un [issue](../../issues/new/choose) con los pasos para reproducirlo, qué esperabas y qué
  pasó. Las capturas ayudan mucho en todo lo visual. Si git mostró algo, el log de comandos (Ctrl+L) tiene el
  comando exacto y su salida.
- **Funciones nuevas:** abre primero un issue y espera respuesta antes de escribir código. GitPeach se mantiene
  pequeño a propósito; algunas buenas ideas no van a encajar, y es mejor saberlo antes de invertir tiempo.
- Los arreglos chicos (una errata, un error obvio de una línea) pueden ir directo a un *pull request*.

### Flujo de trabajo

1. Haz un **fork** del repositorio y clona tu fork.
2. Crea una rama desde `main` con el prefijo del tipo de cambio:

   | Prefijo | Para | Ejemplo |
   |---|---|---|
   | `feature/` | algo nuevo | `feature/interactive-rebase` |
   | `fix/` | un error | `fix/graph-merge-lanes` |
   | `docs/` | solo README, DISENO.md o comentarios | `docs/shortcuts-table` |
   | `refactor/` | cambio interno, mismo comportamiento | `refactor/split-repo-actions` |
   | `chore/` | compilación, CI, dependencias | `chore/update-avalonia` |

   En minúsculas y con las palabras separadas por `-`. Una revisión automática en cada *pull request*
   rechaza otros nombres.
3. Haz tu cambio. Que el *pull request* trate de **una sola cosa**; las limpiezas que no tienen que ver van en
   otro.
4. Comprueba que compila y que los tests pasan:

   ```
   dotnet build
   dotnet test
   ```

   `dev.cmd` compila en Debug y abre la app. Los tests de integración necesitan git en el `PATH`.
5. Abre un *pull request* contra `main` y completa la plantilla. Nadie sube cambios directo a `main`, ni
   siquiera el mantenedor: todo pasa por un *pull request*.

### Mensajes de commit

Un resumen corto de lo que hace el cambio, en español o inglés, como el historial actual:
`El grafo mantiene rectas las líneas de merge`. No hace falta prefijo (el nombre de la rama ya dice el tipo de
cambio). Agrega una línea en blanco y más detalle cuando el *porqué* no sea obvio.

### Convenciones de código

- **La interfaz está en inglés y español.** Todo texto visible va en `Services/Strings.cs` como par (inglés,
  español) y se usa con `Loc.T("clave")` en el código o `{l:L clave}` en XAML; un test falla si falta una
  clave. Español neutro, tuteo, mayúscula solo al inicio. Código, nombres y comentarios en inglés.
- Respeta la arquitectura de [docs/ARQUITECTURA.md](docs/ARQUITECTURA.md): los view models nunca tocan
  Avalonia, `Process` ni el portapapeles directamente, solo interfaces de servicios; **git solo se ejecuta a
  través de `IGitRunner`**; su salida la interpretan funciones puras en `Git/Parsers`.
- Respeta [docs/DISENO.md](docs/DISENO.md): solo colores de la paleta, un botón principal por zona, lo que
  solo se lee nunca va en un campo editable. Si agregas un patrón de interfaz nuevo, documéntalo ahí.
- Todo lo que toque disco o git va fuera del hilo de la interfaz.
- **Todo lo que pueda perder trabajo pide confirmación** (descartar, reset duro, push forzado, borrar ramas o
  tags…).
- Los parsers y la demás lógica pura van con tests unitarios; los flujos de git, con un test de integración
  que crea un repositorio temporal (ver `GitRepositoryTests`).
- Sigue el estilo del código que rodea tu cambio; nada de dependencias nuevas sin hablarlo antes en un issue.

### Versiones y releases

Las versiones siguen [SemVer](https://semver.org) y nadie cambia el número de versión a mano:

- Cada *pull request* recibe una etiqueta según el nombre de su rama (`feature/` → `enhancement`, `fix/` →
  `bug`, …), y cada merge actualiza un **borrador del próximo release** con las notas agrupadas por tipo.
- La versión siguiente sale de esas etiquetas: si hay algún `enhancement` sube la versión menor
  (0.3.0 → 0.4.0); si no, el parche (0.3.0 → 0.3.1).
- GitPeach todavía está en **0.x**, así que los cambios incompatibles también suben la versión menor. El
  mantenedor pone la **1.0.0** a mano en el borrador cuando llegue el momento; desde ahí, la etiqueta
  `breaking` sube la versión mayor.
- Un merge no es un release: el borrador junta varios *pull requests* y se publica cuando vale la pena.
- Cuando el mantenedor publica el borrador, se crea el tag `vX.Y.Z` y un workflow compila `GitPeach.exe` y
  `GitPeach-linux-x64.tar.gz` con esa versión y los adjunta al release.

### Licencia de las contribuciones

GitPeach se distribuye bajo la [GNU GPL v3.0 o posterior](LICENSE). Al enviar un *pull request*:

1. Tu contribución queda bajo la GPL-3.0-or-later, como el resto del proyecto.
2. Además, le das a rileydev23 una licencia perpetua, mundial, no exclusiva y gratuita para usar, modificar y
   relicenciar tu contribución bajo otros términos. Esto permite que el autor ofrezca licencias comerciales a
   empresas que no pueden usar la GPL; la versión GPL sigue disponible para todos.
3. Conservas los derechos de autor de tu trabajo y confirmas que lo escribiste tú o que tienes derecho a
   aportarlo.

Si hay diferencias entre las dos versiones de esta sección, vale la versión en inglés.
