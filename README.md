<p align="center">
  <img src="src/GitPeach/Assets/logo.png" width="128" alt="GitPeach" />
</p>

<h1 align="center">GitPeach</h1>

<p align="center">
  Cliente de git de escritorio para Windows y Linux.<br/>
  Historial con grafo, cambios, diffs, ramas, stashes y remotos de varios repositorios en pestañas, con una
  interfaz sobria que pide confirmación antes de cualquier cosa que pueda perder trabajo.
</p>

<p align="center">
  <b>Español</b> · <a href="README.en.md">English</a>
</p>

---

## Índice

- [¿Por qué GitPeach?](#por-qué-gitpeach)
- [Características](#características)
- [Requisitos](#requisitos)
- [Instalación](#instalación)
- [Atajos de teclado](#atajos-de-teclado)
- [Línea de comandos e integración](#línea-de-comandos-e-integración)
- [Datos y privacidad](#datos-y-privacidad)
- [Compilar desde el código](#compilar-desde-el-código)
- [Estructura del repositorio](#estructura-del-repositorio)
- [Contribuir](#contribuir)
- [Versiones y releases](#versiones-y-releases)
- [Licencia y créditos](#licencia-y-créditos)

---

## ¿Por qué GitPeach?

GitPeach es la mitad "git" de la familia Peach. [TextPeach](https://github.com/rileydev23/textpeach) muestra el
código y el estado de git sin tocar nada; GitPeach es donde se hace el trabajo con git: preparar cambios,
hacer commits, moverse entre ramas, traer y enviar.

- **Usa tu git.** Cada acción es un comando de git real que puedes ver en el **log de comandos** (Ctrl+L), con
  su salida. Nada de formatos propios ni magia escondida.
- **Lo destructivo nunca es por defecto.** Descartar cambios, *reset* duro, *push* forzado o borrar ramas y
  tags piden confirmación, y el *push* forzado usa `--force-with-lease`.
- **Varios repositorios a la vez,** en pestañas, organizados en grupos y favoritos.
- **Se actualiza solo** cuando el repositorio cambia en el disco, también si lo cambia otra herramienta.

Es un único ejecutable portable, sin instalador, sin cuentas y sin telemetría. La interfaz está en español.

## Características

| Área | Qué incluye |
|---|---|
| **Repositorios** | Lista persistente con favoritos ★, grupos plegables, filtro, escaneo de carpetas, clonar, arrastrar y soltar carpetas, abrir con búsqueda rápida (Ctrl+P), varias pestañas que se restauran al reiniciar. |
| **Historial** | Lista de commits con grafo de ramas, carga por páginas y búsqueda por mensaje, autor, hash o código. |
| **Cambios** | Archivos con y sin *stage*, *stage* o descarte por archivo o por bloque (*hunk*), commit y *amend*. |
| **Diffs** | Diff del commit seleccionado o de los cambios locales, con caché, y en ventana aparte. |
| **Ramas, tags y remotos** | Crear, renombrar, borrar, checkout (también de ramas remotas), merge, rebase, tags y su *push*, borrado remoto. |
| **Fetch, pull y push** | Pull normal, con rebase o solo *fast-forward*; push a otro remoto, de tags y forzado con `--force-with-lease`. |
| **Commits** | Cherry-pick, revert, reset suave, mixto o duro hasta un commit, checkout desacoplado, copiar hash o mensaje. |
| **Stashes** | Guardar, aplicar, aplicar y quitar, y borrar. |
| **Conflictos** | Herramienta de merge propia: elige la versión tuya, la entrante, ambas, o edita el resultado. También puedes abrir la herramienta de merge externa configurada. Continuar o abortar merge, rebase y cherry-pick. |
| **Archivos** | Historial de un archivo (siguiendo renombres), *blame*, visor de texto de solo lectura con edición opcional (Ctrl+E), agregar a `.gitignore`. |
| **Integración** | Abrir el repositorio o un archivo en TextPeach, en el explorador de archivos o en una terminal; instancia única para que otras apps le pidan abrir repositorios. |

## Requisitos

- **Para usarlo:** Windows 10 u 11 de 64 bits, o Linux x64 con escritorio (X11 o XWayland). No necesita .NET
  instalado (el ejecutable es autocontenido).
- **Git:** [Git para Windows](https://git-scm.com/download/win) o el `git` de tu distribución, en el `PATH`.
  Para remotos con usuario y contraseña, GitPeach usa tu configuración de credenciales de git (por ejemplo, Git
  Credential Manager).
- **Para compilarlo:** [SDK de .NET 10](https://dotnet.microsoft.com/download).

## Instalación

1. Descarga `GitPeach.exe` (Windows) o `GitPeach-linux-x64.tar.gz` (Linux) desde la sección *Releases* del
   repositorio (o compílalo, ver [Compilar desde el código](#compilar-desde-el-código)). En Linux,
   descomprímelo con `tar -xzf GitPeach-linux-x64.tar.gz`.
2. Ponlo en la carpeta que quieras y ejecútalo. No hay instalador.
3. *(Opcional)* **Modo portable:** crea un archivo vacío `portable.txt` junto al ejecutable y los datos se
   guardarán en `data/` a su lado en lugar de en tu perfil.

## Atajos de teclado

También están en **Acerca de** (F1).

| Atajo | Acción |
|---|---|
| `Ctrl+P` | Abrir repositorio (búsqueda rápida) |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | Pestaña siguiente / anterior |
| `Ctrl+1` … `Ctrl+9` | Ir a la pestaña N |
| `Ctrl+W` | Cerrar pestaña |
| `F1` | Acerca de y atajos de teclado |
| `F5` | Refrescar |
| `Ctrl+F` | Buscar en el historial |
| `Ctrl+Shift+F` / `Ctrl+Shift+L` / `Ctrl+Shift+P` | Fetch / Pull / Push |
| `Ctrl+B` | Nueva rama |
| `Ctrl+Shift+S` / `Ctrl+Shift+O` | Stash / Pop del último stash |
| `Ctrl+Shift+A` / `Ctrl+Shift+U` | Stage de todo / Unstage de todo |
| `Ctrl+Enter` | Commit |
| `Ctrl+L` | Mostrar u ocultar el log de comandos |
| `Ctrl+E` / `Ctrl+S` (en el visor) | Editar / Guardar |

## Línea de comandos e integración

```
GitPeach.exe [repositorio] ...
```

- Cada **repositorio** se agrega a la lista (si no estaba) y se abre en una pestaña.
- **Instancia única:** si GitPeach ya está abierto, la nueva ejecución le entrega las rutas por una *named
  pipe* local (solo del usuario actual) y termina. Así TextPeach puede pedirle que abra un repositorio.
- **TextPeach:** el botón **TextPeach** abre el repositorio para revisar el código en solo lectura. TextPeach se
  busca primero donde él mismo anotó que está (`peachapps/TextPeach/location.json` en la carpeta de datos de tu
  usuario), y si no, junto a GitPeach, en una carpeta hermana `TextPeach`, en las carpetas de instalación
  habituales y en el `PATH`.

## Datos y privacidad

- GitPeach **no tiene telemetría ni cuentas**. Solo se conecta a internet a través de git, cuando haces fetch,
  pull, push o clonas, y solo con tus remotos.
- Guarda su estado en `peachapps/GitPeach/state.json` dentro de la carpeta de datos de tu usuario
  (`%APPDATA%` en Windows, `~/.config` en Linux), o en `data/` junto al ejecutable con `portable.txt`: lista de
  repositorios y grupos, pestañas abiertas y tamaño de la ventana. La ruta exacta aparece en **Acerca de** (F1).

## Compilar desde el código

```bat
git clone https://github.com/rileydev23/gitpeach.git GitPeach
cd GitPeach

dev.cmd                       :: compila en Debug y ejecuta
dotnet test GitPeach.sln      :: ejecuta los tests (necesitan git en el PATH)
build.cmd                     :: genera dist\GitPeach.exe (single-file, autocontenido, win-x64)
```

En Linux, `./build.sh` genera `dist/GitPeach` (linux-x64). Los dos aceptan una versión opcional
(`build.cmd 0.4.0`) que queda grabada en el ejecutable.

## Estructura del repositorio

```
GitPeach/
├── build.cmd · build.sh · dev.cmd · GitPeach.sln
├── docs/
│   ├── ARQUITECTURA.md      capas, MVVM + DI, convenciones
│   └── DISENO.md            sistema de diseño (colores, tipografía, componentes, patrones)
├── src/GitPeach/
│   ├── Git/                 GitRunner (único lugar que ejecuta git), GitRepository, parsers puros, grafo
│   ├── Models/              commits, status, refs, diff, blame, opciones
│   ├── Services/            estado, grupos, watcher del disco, instancia única, shell, TextPeach
│   ├── ViewModels/          MVVM (CommunityToolkit.Mvvm)
│   └── Views/               XAML de Avalonia, diálogos y ventanas secundarias
└── tests/GitPeach.Tests/    xUnit: parsers, grafo, diff, conflictos, grupos y repos reales temporales
```

Más detalle en [docs/ARQUITECTURA.md](docs/ARQUITECTURA.md). Las reglas visuales están en
[docs/DISENO.md](docs/DISENO.md).

## Contribuir

¡Las contribuciones son bienvenidas! La guía completa está en [CONTRIBUTING.md](CONTRIBUTING.md).

1. Abre un *issue* describiendo el problema o la propuesta (con capturas si es algo visual).
2. Haz un *fork*, crea una rama `feature/…`, `fix/…`, `docs/…`, `refactor/…` o `chore/…` y envía un
   *pull request* pequeño y enfocado contra `main`.
3. Antes de enviarlo: `dotnet build` sin errores y `dotnet test` en verde.

## Versiones y releases

GitPeach está en **0.x**: ya se puede usar a diario, pero todavía puede cambiar bastante. La **1.0.0** llegará
cuando la app esté estable y tenga su icono definitivo.

Las versiones siguen [SemVer](https://semver.org) (`MAYOR.MENOR.PARCHE`) y el repositorio usa *GitHub Flow*:
una sola rama principal, `main`, sin ramas intermedias (`develop`, `release/…`).

```
feature/algo ──PR──┐
fix/otro ─────PR───┼──▶ main ──(cuando el mantenedor decide)──▶ release v0.4.0 (Windows + Linux)
docs/tal ─────PR───┘
```

1. **Cada cambio va en su rama** (`feature/…`, `fix/…`, `docs/…`, `refactor/…`, `chore/…`) y entra a `main`
   con un *pull request* revisado y con la CI en verde. Nadie sube directo a `main`.
2. **Cada merge actualiza un borrador del próximo release** con las notas agrupadas por tipo. La versión se
   calcula sola a partir de las ramas: si entró algún `feature/` sube la menor (0.3.2 → 0.4.0); si no, el
   parche (0.3.2 → 0.3.3).
3. **Un merge no es un release.** Se pueden juntar varios *pull requests* en el borrador y publicarlo cuando
   haya algo que valga la pena entregar. Publicarlo crea el tag `vX.Y.Z` y un workflow automáticamente corre
   los tests, compila `GitPeach.exe` y `GitPeach-linux-x64.tar.gz` con esa versión, comprueba que los dos
   arrancan y los adjunta al release.
4. Mientras sea 0.x, los cambios incompatibles también suben solo la versión menor. Para sacar la **1.0.0**,
   el mantenedor cambia la versión del borrador a mano justo antes de publicarlo.

`main` siempre debe compilar y pasar los tests: es la base de cada release.

## Licencia y créditos

Copyright © 2026 [rileydev23](https://github.com/rileydev23). GitPeach es software libre bajo la
**[GNU General Public License v3.0](LICENSE)** o posterior (GPL-3.0-or-later), la misma licencia de toda la
familia Peach. En resumen:

- **Úsalo gratis para lo que quieras**, también en empresas y con fines comerciales.
- **Puedes estudiarlo, modificarlo y compartir tu versión**, siempre que también sea GPLv3, con su código
  fuente disponible y sin quitar los avisos de copyright. Nadie puede tomarlo y distribuirlo como software
  cerrado.
- **¿Necesitas una versión cerrada** o integrarlo en un producto propietario? Escribe a
  [rileydev23](https://github.com/rileydev23) para una licencia comercial.

**Nombre y logo:** la GPL cubre el código, no la marca. "GitPeach" y su logo identifican a la versión oficial;
si publicas un fork, ponle otro nombre y otro logo.

**Sobre el icono:** el icono actual se hizo con IA. Si quieres proponer uno mejor, hecho por una persona,
¡encantado de cambiarlo! Abre un *issue* con tu propuesta.

El texto que vale es el de [LICENSE](LICENSE).

Construido con:

- [Avalonia UI](https://avaloniaui.net/) (MIT)
- [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) (MIT)
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (MIT)
- [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) (MIT)
- [Git](https://git-scm.com/) (GPL-2.0, se usa la instalación del sistema; no se incluye)
