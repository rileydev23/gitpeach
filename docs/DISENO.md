# Sistema de diseño de GitPeach

<!-- peach-hijo: DISENO v1.0 -->
> GitPeach comparte su sistema de diseño con las demás apps Peach (TextPeach, RestPeach): mismos colores,
> componentes, atajos y tono, para que pasar de una a otra no cueste nada. Muchas de esas reglas nacieron aquí.
> Este documento las aplica a GitPeach y agrega lo propio, marcado como **Propio de GitPeach**. Si cambias algo
> que también afecta a las otras apps (un color, un atajo común, un componente), dilo en el *pull request*: se
> cambia en las tres.

> **Regla de oro:** GitPeach parece un IDE sobrio para **trabajar con git**. Fondo gris carbón, texto gris claro,
> un solo azul de acento, y color solo cuando significa algo (estado de un archivo, rama, referencia, operación
> en curso). Nada decorativo.

Índice:

1. [Principios](#1-principios)
2. [Base técnica](#2-base-técnica)
3. [Color](#3-color)
4. [Tipografía](#4-tipografía)
5. [Espaciado, forma y bordes](#5-espaciado-forma-y-bordes)
6. [Anatomía de la ventana](#6-anatomía-de-la-ventana)
7. [Componentes](#7-componentes)
8. [Patrones de interacción](#8-patrones-de-interacción)
9. [Teclado](#9-teclado)
10. [Textos y tono](#10-textos-y-tono)
11. [Iconografía y marca](#11-iconografía-y-marca)
12. [Rendimiento percibido](#12-rendimiento-percibido)
13. [Diferencias pendientes con el resto de la familia](#13-diferencias-pendientes-con-el-resto-de-la-familia)
14. [Dónde está cada cosa en el código](#14-dónde-está-cada-cosa-en-el-código)

---

## 1. Principios

1. **Oscuro y denso, como un IDE.** Solo tema oscuro. Filas compactas, fuente base de 13 px, mucha información
   visible a la vez. No hay modo claro.
2. **El color es información.** El 90 % de la pantalla son grises. El azul de acento marca lo seleccionado y la
   acción principal. Los demás colores solo comunican estado o tipo, con el mismo significado que en TextPeach y
   RestPeach ([3.4](#34-colores-semánticos)).
3. **Una acción principal por zona.** Como máximo un botón `accent` por zona: **Agregar repositorio...** en la
   Home, **Commit** en los cambios.
4. **Lo destructivo nunca es por defecto (Propio de GitPeach).** Descartar cambios, *reset*, borrar ramas o tags,
   *force push* y abortar una operación piden confirmación con las consecuencias dichas en claro. "Quitar de la
   lista" dice que no borra nada del disco.
5. **Git a la vista.** Cada comando que GitPeach ejecuta queda en el **log de comandos** (`Ctrl+L`), para
   aprender y para saber exactamente qué pasó.
6. **Nada se mueve solo.** La ventana se abre donde la dejaste; las listas se reemplazan de una vez; al refrescar
   se conservan la selección y el scroll.
7. **Todo se puede hacer con teclado.** Cada acción frecuente tiene atajo y todos se listan en F1
   ([9](#9-teclado)).
8. **Estado persistente.** Repositorios, grupos, favoritos, pestañas, ventana y preferencias se guardan solos en
   `state.json`.
9. **Interfaz en inglés y español, código en inglés.** Todo texto visible está en `Services/Strings.cs`
   (par inglés/español) y se usa con `Loc.T`/`Loc.F`/`Loc.Plural` o `{l:L clave}`; el idioma sigue al del
   sistema y se cambia en "Acerca de" (se aplica al reiniciar).
10. **Sin nombres de terceros.** La interfaz no menciona ni lanza editores ni servicios de terceros. Lo
    permitido: las herramientas del sistema que hacen falta para trabajar (**Explorador**, **Terminal**), "abrir
    con la aplicación predeterminada" y la app hermana **TextPeach** para leer código.
11. **Lo que solo se lee nunca parece editable.** Mensajes, el log, atajos y diffs se muestran con
    `TextBlock`/`SelectableTextBlock`; `TextBox` es solo para escribir (mensaje de commit, búsqueda).
12. **Portable y autocontenido.** Un único `.exe` que funciona sin instalar; con `portable.txt` al lado, los
    datos se guardan junto al exe. El instalador (`GitPeach-Setup.exe`, `install.sh`) es opcional: copia ese mismo
    exe a una carpeta del usuario, lo registra en el buscador del sistema y deja `installed.txt` al lado.
13. **Actualizar no interrumpe.** Una versión nueva solo se nota como un botón **↑ x.y.z** junto al **?**; nada
    se descarga sin confirmar en su popup (Actualizar y reiniciar / Ahora no / Omitir esta versión).

---

## 2. Base técnica

| Pieza | Elección | Notas |
|---|---|---|
| UI | **Avalonia 11.3** + `Avalonia.Themes.Fluent` | `RequestedThemeVariant="Dark"`, estilos en `Styles.axaml`. |
| Runtime | **.NET 10**, C# `latest`, `Nullable` e `ImplicitUsings` | `InvariantGlobalization=true`. |
| MVVM + DI | **CommunityToolkit.Mvvm 8.4** + **Microsoft.Extensions.DependencyInjection** | Un único *composition root* (`App.ConfigureServices`). |
| Bindings | Compilados por defecto | Todo XAML declara `x:DataType`. |
| Render | Software | Se excluye `av_libglesv2.dll` al publicar. |
| Editor | **AvaloniaEdit 11.4** | Visor de archivos, blame y resolución de conflictos (sin resaltado de sintaxis todavía, ver [13](#13-diferencias-pendientes-con-el-resto-de-la-familia)). |
| Git | `git.exe` del sistema, lanzado solo desde `GitRunner` | Salidas leídas por *parsers* puros y testeados. |
| Publicación | `build.cmd` → single-file, self-contained, `win-x64` | `dev.cmd` compila Debug y ejecuta. |
| Estado | `%APPDATA%\peachapps\GitPeach\state.json` o `.\data\` | Escritura atómica. |
| Tests | xUnit | Parsers, grafo, diff y pruebas de integración con repos reales en `%TEMP%`. |

---

## 3. Color

Hex sRGB; en XAML en minúsculas; en C# desde `Controls/Palette.cs`. Nunca inventes un gris nuevo.

### 3.1 Superficies

| Token | Hex | Uso en GitPeach |
|---|---|---|
| (título) | `#111214` | Barra de título, botón Home. |
| `Chrome` | `#18191c` | Barra de pestañas de la ventana, barra de estado, **log de comandos**. |
| `Code` | `#1b1c1f` | Diffs, visor de archivos, blame. |
| `Bg` | `#1e1f22` | Fondo de ventana y contenido; historial. |
| `Sidebar` | `#222327` | Barra lateral de ramas, remotos, tags y stashes; panel de cambios. |
| `Panel` | `#26272b` | Toolbar, cabeceras de sección, diálogos, `TextBox`. |
| `Panel2` | `#2d2e33` | Botón normal, *hover* de filas. |
| `FileBg` | `#34363c` | Cabecera de archivo dentro de un diff. |
| `Border` | `#3a3c42` | Todos los bordes, separadores y `GridSplitter`. |

Cada franja horizontal cambia de superficie y se separa con un borde de 1 px `#3a3c42`. Nunca blanco ni grises
claros como fondo.

### 3.2 Texto

| Token | Hex | Uso |
|---|---|---|
| `Text` | `#d4d4d4` | Texto normal. |
| `ContextFg` | `#c8c8c8` | Líneas de contexto en diffs. |
| `Dim` | `#8a8d93` | Autor, fecha, hash corto, rutas, *watermarks*, cabeceras del sidebar. |
| (gutter) | `#5d6066` | Números de línea. |
| Blanco | `#ffffff` | Solo sobre fondos de color (botón `accent`, badges de refs, seleccionado). |

### 3.3 Acento y selección

Acento `#4a90d9` (botón principal, cursor, **rama actual**; *hover* `#5aa0e9`); seleccionado `#33507a`
(+ *hover* `#3a5a88`); *hover* de fila `#2d2e33`.

### 3.4 Colores semánticos

Significado fijo, igual en todas las apps Peach:

| Nombre | Hex | Significado en GitPeach |
|---|---|---|
| Verde | `#6cc070` | Rama actual en la Home, archivo agregado (`A`, `?`). |
| Rojo | `#e0645a` | Error, borrado (`D`), "carpeta no encontrada". |
| Ámbar | `#e0a24a` | Atención: favorito ★, ahead/behind, conflicto (`U`), operación en curso. |
| Ámbar claro | `#f0c890` | Texto de aviso sobre `#5a4318`. |
| Azul | `#4a90d9` | Modificado (`M`). |
| Morado | `#9a7ae0` | Renombrado o copiado (`R`, `C`). |

Letras de estado de archivo (`A M D R C U ?`): **Bold**, del color semántico, columna fija de 22 px.

### 3.5 Banners

| Tipo | Fondo | Cuándo |
|---|---|---|
| Atención | `#5a4318` (botones con borde `#8a6d1f`) | **Merge, rebase, cherry-pick o revert en curso**, con **Continuar** y **Abortar**. |
| Información | `#25364d` | Cabecera de *hunk* en los diffs. |

### 3.6 Diffs

| Elemento | Fondo | Texto |
|---|---|---|
| Línea agregada | `#1d3a2a` | `#a6e3b4` |
| Línea borrada | `#431f22` | `#f4a6a6` |
| Contexto | transparente | `#c8c8c8` |
| Cabecera de *hunk* | `#25364d` | `Dim` |
| Cabecera de archivo | `#34363c` | `Text`, SemiBold |
| Notas ("Archivo binario…") | — | `Dim`, cursiva |

Las listas de diff no se seleccionan ni se resaltan al pasar el mouse (`ListBox.diff`).

### 3.7 Grafo y referencias (Propio de GitPeach)

Carriles del grafo con la serie categórica, por índice (`i % 8`):

| # | Hex | # | Hex |
|---|---|---|---|
| 0 | `#4a90d9` azul | 4 | `#9a7ae0` morado |
| 1 | `#e0a24a` ámbar | 5 | `#4ac7c0` teal |
| 2 | `#6cc070` verde | 6 | `#e0645a` rojo |
| 3 | `#d96a9b` rosa | 7 | `#c8c84a` oliva |

Badges de referencias, texto blanco 11 px, `CornerRadius=3`, `Padding="5,1"`: rama local `#2f6f3e`, remota
`#2d5c8a`, tag `#8a6d1f`, HEAD `#7a3b8a`, rama actual `#4a90d9`.

### 3.8 Editor

Selección `#33507a`, línea actual `#232428`, cursor `#4a90d9`, *gutter* `#5d6066`. Si se agrega resaltado de
sintaxis, se usa AvaloniaEdit.TextMate con el tema `peach-dark.json` de la familia (palabras clave `#d96a9b`,
cadenas `#8fcf94`, números `#e0a24a`, tipos `#4ac7c0`, funciones `#7db4ea`, comentarios `#6f737a` en cursiva).

---

## 4. Tipografía

**UI:** `Segoe UI`. **Monoespaciada:** `Cascadia Mono, Consolas, Courier New` (diffs, hash, log de comandos,
editor).

| Tamaño | Uso |
|---|---|
| 11 | Badges de refs, metadatos, "conflicto". |
| 12 | Sidebar, historial, listas de archivos, diffs. |
| **13** | **Base** de la interfaz. |
| 14 | Nombre del repositorio en la Home; mensaje del commit seleccionado. |
| 16 | Título de un estado vacío. |

SemiBold para títulos de sección, la rama actual y la fila WIP; Bold solo para letras de estado; cursiva solo
para notas ("Cargando diff…").

---

## 5. Espaciado, forma y bordes

Unidad base de 2 px. Toolbar `Padding="8,6"`, botones con `Spacing="4"`, separadores verticales
`Border Width="1" Margin="6,2"`. Barra de estado `Padding="10,3"`. Filas del historial de 24 px de alto; filas
de archivos `Margin="8,2"` con columnas `22,*,Auto`. Diálogos `Margin="18"`, botones `MinWidth=84`, el principal
primero a la derecha. Botón `CornerRadius=3`, `Padding="10,4"`. `GridSplitter` de 4 px. Sin sombras ni
degradados (salvo el logo).

---

## 6. Anatomía de la ventana

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ 🍑 GitPeach  repo · C:\ruta                                   ─   ☐   ✕      │ título     #111214 (34 px)
├──────────────────────────────────────────────────────────────────────────────┤
│▌Repositorios▐│ repo-a ✕ │ repo-b ✕ │                                      ? │ pestañas   #18191c
├──────────────────────────────────────────────────────────────────────────────┤
│ Fetch  Pull ▾  Push ▾ │ Nueva rama  Stash  Pop │ Refrescar  main   Explorador Terminal TextPeach │ toolbar #26272b
├──────────────────────────────────────────────────────────────────────────────┤
│ Merge en curso: resuelve los conflictos…                  [Continuar] [Abortar]│ banner     #5a4318
├──────────────┬┬───────────────────────────────────────────┬┬─────────────────┤
│ RAMAS        ││ [Buscar en el historial (Ctrl+F)]         ││ Sin stage       │
│ ● main       ││ ○─ WIP                                    ││ M src/a.cs  ↶ + │
│   feature/x  ││ ●─ Mensaje  [main] [origin/main]  autor   ││ En stage        │
│ REMOTOS      ││ │                                          ││ [Mensaje…]      │
│ TAGS STASHES ││ grafo + commits                           ││ [Amend] [Commit]│
├──────────────┴┴───────────────────────────────────────────┴┴─────────────────┤
│ aviso…                                      Log de comandos   ▬▬▬            │ estado #18191c
└──────────────────────────────────────────────────────────────────────────────┘
```

- **Barra de título propia** (34 px, `#111214`), botones de ventana dibujados; en la barra de tareas el título es
  "repositorio — GitPeach".
- **Barra de pestañas:** botón fijo **Repositorios** (`Button.home`), una pestaña por repositorio y `?` (F1).
- **Toolbar** (`#26272b`): Fetch, **Pull** y **Push** como `SplitButton` (opciones en el menú), Nueva rama, Stash,
  Pop, Refrescar y la rama actual en verde. A la derecha **Explorador**, **Terminal** y **TextPeach**.
- **Banner** ámbar solo durante una operación en curso.
- **Cuerpo** en tres columnas: sidebar (ramas, remotos, tags, stashes) · historial con grafo · panel derecho, que
  muestra los **cambios** (sin stage, en stage, mensaje y Commit) cuando está elegida la fila WIP, o el
  **detalle del commit** y sus archivos en otro caso; debajo, el diff.
- **Barra de estado:** el último aviso, **Log de comandos** (`Ctrl+L`) y el progreso.
- **Ventanas secundarias** (diff aparte, blame, historial de un archivo, visor, resolución de conflictos): mismo
  icono, título "nombre — Qué", centradas en la principal, cabecera `#26272b` con el contexto.

**Colocación de la ventana** (`App.PlaceWindow`): 1200×760 DIP por defecto (máx. 92 % del área de trabajo),
mínimo 800×500, recuerda tamaño, posición, monitor y maximizado; arranca invisible y se muestra ya colocada.

---

## 7. Componentes

Estilos en `src/GitPeach/Styles.axaml`.

### 7.1 Botones

| Clase | Cuándo |
|---|---|
| (ninguna) | Acciones normales. |
| `accent` | **Commit**, Agregar repositorio..., el principal de un diálogo. Una por zona. |
| `flat` | Acciones en filas y cabeceras: Stage todo, Unstage todo, `↶` descartar, `+`/`−` stage/unstage, Log de comandos. |
| `warn` | Cambiar a un modo con riesgo ("✎ Editar" en el visor). |
| `SplitButton` | Pull y Push: acción por defecto + variantes en el menú. |

Cursor de mano en botones y enlaces; flecha en filas, árbol, pestañas y botones de ventana. Etiquetas cortas,
`...` si abren un diálogo. Glifos sueltos (`✕`, `↶`, `+`, `−`, `⧉`) siempre con tooltip.

### 7.2 Historial (Propio de GitPeach)

Filas de 24 px: grafo, mensaje con los badges de referencias, autor y fecha en `dim`. La primera fila es **WIP**
(cambios sin commit) en SemiBold. Búsqueda en el historial con `Ctrl+F` (en qué buscar: mensaje, autor, hash);
`Esc` limpia. Paginación al llegar al final.

### 7.3 Cambios y commit

Dos listas (**Sin stage**, **En stage**) con la letra de estado, ruta (`dim` + nombre) y acciones `flat`; los
conflictos dicen "conflicto" en ámbar y se resuelven con doble clic. Debajo, el mensaje de commit (`TextBox`
multilínea, "Mensaje de commit (Ctrl+Enter para confirmar)"), **Amend** y **Commit** (`accent`).

### 7.4 Sidebar

Secciones RAMAS, REMOTOS, TAGS y STASHES con cabeceras `dim` en mayúsculas; la rama actual con `●` y en el
acento. Menús contextuales con todas las acciones de cada referencia (checkout, merge, rebase, renombrar, borrar
al final tras un separador).

### 7.5 Log de comandos

Panel inferior plegable (`Ctrl+L`) sobre `#18191c`, monoespaciado: cada comando git con su salida y su
resultado; **Copiar** lo lleva al portapapeles.

### 7.6 Resolución de conflictos

Ventana propia: bloques de conflicto con **Mía**, **Entrante**, **Ambas** o editar a mano; `Ctrl+S` guarda.
Se usa también cuando no hay `merge.tool` configurado.

### 7.7 Visor de archivos

AvaloniaEdit de solo lectura hasta pulsar "✎ Editar" (`Ctrl+E`); `Ctrl+S` guarda. Para leer un repositorio con
calma está **TextPeach**.

### 7.8 Pestañas, listas, diálogos, menús, estados vacíos

- Pestañas de la ventana con `ListBox.tabstrip` (barra de 4 px debajo, rueda de lado).
- Listas: seleccionado `#33507a`, *hover* `#2d2e33`; las de diff, sin selección (`ListBox.diff`).
- Diálogos construidos en código (`DialogService`), fondo `#26272b`; `Message` nunca usa un `TextBox`; "Acerca
  de" (F1) con logo, versión y atajos.
- Menús: verbos cortos, lo destructivo al final tras un separador, `...` si abre otra ventana.
- Estados vacíos centrados con el logo, un título y qué hacer.

### 7.9 Barras de desplazamiento

Una sola para todo: 4 px, sin flechas, no crece, `AllowAutoHide=False`.

---

## 8. Patrones de interacción

### 8.1 Home de repositorios

Agregar repositorio... (accent), Escanear carpeta..., Nuevo grupo..., filtro, Expandir/Contraer, ★ Favoritos.
Fila: ★ · nombre 14 px SemiBold · **rama actual en verde** · "(carpeta no encontrada)" en rojo · ruta `dim` ·
fecha. Grupos plegables con "Abrir todos" (lógica en `RepoGrouping`, pura y testeada); arrastrar carpetas;
Supr quita de la lista sin tocar el disco. Los menús los arma el view model como `MenuAction` y los dibuja
`MenuPresenter`.

### 8.2 Pestañas persistentes

Los repositorios abiertos y el activo se restauran al iniciar.

### 8.3 Ocupado y avisos

Mientras corre un comando, progreso en la barra de estado y las acciones que no deben solaparse se deshabilitan
(`IsEnabled="{Binding !IsBusy}"`). El resultado va al aviso ("Fetch completado"); los errores de git abren además
un `Message` con la salida completa.

### 8.4 Vigilancia del disco

Un *watcher* por repositorio (`RepoWatcher`) con *debounce* refresca el estado cuando algo cambia afuera,
conservando selección y scroll.

### 8.5 Confirmaciones

Toda acción que pierde trabajo o reescribe historia confirma y dice qué se pierde (descartar, *reset --hard*,
borrar rama sin fusionar, *force push*, abortar).

### 8.6 Integración con el sistema y con TextPeach

- **Explorador** y **Terminal** abren la carpeta del repositorio con las herramientas del sistema.
- **TextPeach** abre el repositorio (o el archivo, desde su menú) para leerlo. `TextPeachLocator` la busca por su
  `location.json`, junto al exe, `..\TextPeach`, `%LOCALAPPDATA%\Programs`, `%ProgramFiles%` y `PATH`; si no
  está, el diálogo común explica qué es y ofrece **Ir al repositorio**.
- Instancia única: TextPeach o RestPeach abriendo un repositorio (clic en su rama) lo entregan a la ventana de
  GitPeach ya abierta.
- `%APPDATA%\peachapps\GitPeach\` guarda `state.json` y `location.json`. `state.json` también guarda la
  búsqueda de versiones (`CheckUpdates`, `LastUpdateCheck`, `LatestVersion`, `SkippedVersion`): como mucho una
  consulta a GitHub al día.

---

## 9. Teclado

Todos declarados en XAML y listados en `ViewModels/Shortcuts.cs` (F1).

| Atajo | Acción |
|---|---|
| `Ctrl+P` | Abrir repositorio (búsqueda rápida) |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | Pestaña siguiente / anterior |
| `Ctrl+1 … Ctrl+9` | Ir a la pestaña N |
| `Ctrl+W` | Cerrar pestaña |
| `F1` | Acerca de y atajos |
| `F5` | Refrescar |
| `Ctrl+F` | Buscar en el historial |
| `Ctrl+Shift+F` | Fetch |
| `Ctrl+Shift+L` | Pull |
| `Ctrl+Shift+P` | Push |
| `Ctrl+B` | Nueva rama |
| `Ctrl+Shift+S` | Stash |
| `Ctrl+Shift+O` | Pop del último stash |
| `Ctrl+Shift+A` / `Ctrl+Shift+U` | Stage / unstage de todo |
| `Ctrl+Enter` | Commit |
| `Ctrl+L` | Mostrar / ocultar el log de comandos |
| `Ctrl+E` · `Ctrl+S` | Editar en el visor · guardar (también al resolver conflictos) |
| `Esc` | Limpiar la búsqueda · cerrar una ventana secundaria |

---

## 10. Textos y tono

- Español neutro con tuteo, mayúscula solo al inicio, sin exclamaciones ni emojis.
- Los términos de git se dejan como se usan: stage, commit, fetch, pull, push, stash, hunk, rebase, merge.
- Avisos en pasado o descriptivos ("Fetch completado", "Rama creada: feature/x"); errores con qué pasó y el dato
  concreto, y la salida de git a un clic.
- Títulos de ventana y pestañas: el nombre del repositorio.
- Fechas `yyyy-MM-dd HH:mm`; hash corto de 7 caracteres en monoespaciada.

---

## 11. Iconografía y marca

- Glifos Unicode: `★` favorito, `✕` cerrar, `●` rama actual, `↶` descartar, `+`/`−` stage/unstage, `⧉` abrir en
  ventana, `⋯` más, `?` ayuda.
- **Logo:** durazno naranja-rosa (`#ffb668 → #ee4662`) con hoja verde y ramas de git blancas. Cada app Peach tiene
  su fruta y color. Va en `app.ico`, `icon64.png` y `logo.png`; en la barra de título, "Acerca de" y los estados
  vacíos, **nunca** en la barra de pestañas.
- El nombre es **GitPeach**, una palabra. Un fork debe usar otro nombre y logo.

---

## 12. Rendimiento percibido

- Todo comando de git corre fuera del hilo de la interfaz; las listas se reemplazan de una vez.
- Historial paginado; diffs con caché pequeña y límite de 20 000 filas; "Cargando diff…" en vez de un panel vacío.
- *Debounce* en la búsqueda (350 ms) y en el *watcher*.

---

## 13. Diferencias pendientes con el resto de la familia

Lo que GitPeach todavía hace distinto de TextPeach y RestPeach. Al cerrar una, bórrala de aquí.

| Qué | Familia | GitPeach hoy |
|---|---|---|
| `Ctrl+Shift+F` | Búsqueda amplia (en todo el proyecto) | Fetch |
| `Ctrl+Shift+O` | Abrir proyecto / repositorio (búsqueda rápida) | Pop del último stash |
| `Ctrl+W` | Cierra el documento actual o, si no hay, la pestaña; también `Ctrl+F4` | Cierra la pestaña |
| Resaltado de sintaxis | AvaloniaEdit.TextMate + `peach-dark.json` | El visor muestra el texto sin colores |

---

## 14. Dónde está cada cosa en el código

| Qué | Dónde |
|---|---|
| Estilos y clases | `src/GitPeach/Styles.axaml` |
| Colores en C# | `Controls/Palette.cs` |
| Atajos de F1 | `ViewModels/Shortcuts.cs` |
| Diálogos | `Views/DialogService.cs` |
| Menús contextuales | `ViewModels/MenuAction.cs` + `Views/MenuPresenter.cs` |
| Ventanas secundarias | `Views/*Window.axaml` (diff, blame, historial, visor, conflictos) |

Si agregas un patrón de interfaz nuevo, documéntalo aquí.
