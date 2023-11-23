# WPF Native App Host

Embed a running Qt desktop application — FreeCAD, for example — inside a WPF window, so that a
.NET/C# shell can wrap a UI it did not write and cannot rebuild.

The application launches a second process, takes its main window, and reparents that window
underneath a WPF `HwndHost`. From then on WPF positions, sizes and clips it like any other element.

> **Status:** a working reference sample, not a library. It is small on purpose — read it, take the
> parts you need. The interesting code is about 400 lines across
> [`Hosting/`](src/WpfNativeAppHost.App/Hosting) and [`Interop/`](src/WpfNativeAppHost.App/Interop).

<!--
  Add a screenshot here once you have one - it is the fastest way to show what this does:
  ![The shell hosting FreeCAD](docs/screenshot.png)
-->

## How it works

Every top-level window on Windows is an `HWND` owned by a process. Nothing says an `HWND`'s parent
has to belong to the same process, so a window can be moved into another application's window
with `SetParent`, provided its style bits are rewritten to those of a child window first.

```
Before                                  After

Desktop                                 Desktop
├── WPF shell window                    └── WPF shell window
│   └── HwndHost placeholder                ├── (WPF content: the side panes)
└── FreeCAD main window                     └── FreeCAD main window   <- reparented
                                                (still owned by the FreeCAD process)
```

Three steps, in [`NativeAppHost.BuildWindowCore`](src/WpfNativeAppHost.App/Hosting/NativeAppHost.cs):

1. Read the guest window's current style and remember it.
2. Rewrite it for embedding — add `WS_CHILD`, `WS_CLIPCHILDREN`, `WS_CLIPSIBLINGS`; strip
   `WS_POPUP`, the caption, the sizing border and the maximise/minimise state. This is
   [`ChildWindowStyle.ForEmbedding`](src/WpfNativeAppHost.App/Hosting/ChildWindowStyle.cs), kept as
   a pure function so it can be unit tested.
3. `SetParent` the guest window under the `HwndHost`'s window and hand the handle back to WPF.

On teardown the steps are reversed *before* the guest is asked to quit, so its window is never
destroyed underneath it by the WPF window closing.

### Finding the right window is the hard part

The obvious approach — poll `Process.MainWindowHandle` until it is non-zero — looks fine and then
fails against real applications. `MainWindowHandle` returns the first visible, unowned, top-level
window it finds, and during startup that is very often a **splash screen**.

Measured against FreeCAD 1.1 on Windows 11:

| Time | `MainWindowHandle` | Window title | Extended style |
| --- | --- | --- | --- |
| 1.6s | splash screen | `FreeCAD` | `WS_EX_TOOLWINDOW`, `WS_EX_LAYERED` |
| 4.1s | *(none)* | — | splash closed |
| 5.1s | real main window | `FreeCAD 1.1.1` | `WS_EX_WINDOWEDGE` |

Adopt the window at 1.6s and the pane goes blank three seconds later, when the splash closes and
takes the embedded window with it. That is the failure this sample originally had.

[`MainWindowFinder`](src/WpfNativeAppHost.App/Hosting/MainWindowFinder.cs) enumerates the process's
top-level windows itself and skips anything that is invisible, owned by another window, or marked
`WS_EX_TOOLWINDOW` — the convention every toolkit uses for splash screens, tooltips and floating
palettes. `HostedProcessTests` reproduces the FreeCAD sequence with a purpose-built fake splash, so
the behaviour stays fixed.

### Checking whether `SetParent` worked is the second-hardest

`SetParent` returns the window's *previous* parent, so adopting a top-level window returns `NULL` on
success — the same value it returns on failure. The usual remedy is to clear the last error first
and inspect it afterwards, but that does not hold here either: on Windows 11, a **successful**
`SetParent` on FreeCAD's window still leaves `ERROR_INVALID_WINDOW_HANDLE` behind. Treating that as
failure crashes the shell on a call that actually worked.

[`NativeMethods.SetParent`](src/WpfNativeAppHost.App/Interop/NativeMethods.cs) therefore ignores both
signals and confirms the outcome directly, by reading the parent back with `GetParent`.

## Repository layout

```
src/WpfNativeAppHost.App/
  Interop/       P/Invoke declarations and the WS_* / WS_EX_* constants
  Hosting/       Launching the guest, finding its window, reparenting it
  Views/         The WPF shell: side panes around the hosted pane
  appsettings.json   Which application to host
tests/
  WpfNativeAppHost.TestTarget/   A small app the tests launch and adopt
  WpfNativeAppHost.Tests/        xUnit unit and integration tests
```

`Hosting/HostedProcess` deliberately has no WPF dependency. Everything that can realistically go
wrong — the executable is missing, the guest dies during startup, it never opens a window — happens
there, where it can be tested without a message loop.

## Prerequisites

- Windows 10 or 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

Visual Studio is optional; the command line is enough.

## Build and run

```bash
dotnet run --project src/WpfNativeAppHost.App
```

Out of the box it hosts **Character Map** (`charmap.exe`), which ships with every copy of Windows,
so this works on a fresh clone with no setup. You should see a plain Win32 application sitting
between two ordinary WPF panes.

## Hosting your own application

Either edit
[`appsettings.json`](src/WpfNativeAppHost.App/appsettings.json):

```json
{
  "HostedApp": {
    "ExecutablePath": "C:\\Program Files\\FreeCAD 1.1\\bin\\freecad.exe",
    "Arguments": "",
    "StartupTimeoutSeconds": 60
  }
}
```

…or override it without editing anything:

```bash
dotnet run --project src/WpfNativeAppHost.App -- --host-exe "C:\Program Files\FreeCAD 1.1\bin\freecad.exe"
```

`--host-args` and `--startup-timeout` are also accepted. Paths may be relative (resolved against the
shell's output directory) and may contain environment variables such as `%ProgramFiles%`. Remember
that backslashes have to be doubled inside JSON.

Notes for guests that are slow or awkward:

- **Give it time.** A cold FreeCAD start can take well over ten seconds; raise
  `StartupTimeoutSeconds` rather than assuming hosting is broken.
- **Working directory.** The guest is started in its own folder, because Qt applications resolve
  plugins and resources relative to the executable.
- **Splash screens** are handled, as described above. If your guest's splash is *not* marked as a
  tool window, it will be adopted instead of the real window — disabling the splash in the guest's
  own preferences is the simplest workaround.

## Running the tests

```bash
dotnet test
```

Integration tests launch real windows and adopt them, so they need an interactive desktop session.
To skip them — which is what CI does:

```bash
dotnet test --filter "Category!=Integration"
```

## Known limitations

These are properties of the technique, not bugs to be fixed. Read them before adopting it.

- **Airspace.** The embedded `HWND` always paints over WPF content. You cannot put WPF overlays,
  adorners, tooltips or popups on top of the hosted view, and you cannot make it transparent.
- **It is a separate process.** There is no in-process access to the guest's object model. Driving
  FreeCAD from C# needs IPC, or FreeCAD's own Python and add-on interfaces — not this.
- **Dialogs and menus escape.** The guest's modal dialogs, dropdown menus and floating tool windows
  are their own top-level windows. They appear outside the WPF shell and are not clipped by it.
- **Keyboard focus is fiddly.** Input crosses an `HwndHost` boundary. Accelerators, tab order and
  `IKeyboardInputSink` behaviour need care beyond what this sample does.
- **DPI.** The WPF process and the guest process negotiate DPI awareness independently. On a
  multi-monitor setup with mixed scaling, expect the embedded window to be scaled wrongly.
- **The guest can outlive its welcome.** Teardown asks politely (`WM_CLOSE`), waits, then kills the
  process. An application that refuses to close, or that prompts to save, will be killed.
- **The adopted window is borrowed, not owned.** If the guest destroys its main window between the
  moment it is found and the moment WPF adopts it, the host has nothing to embed and reports the
  failure rather than recovering. A shell that must survive that would need to create its own child
  window and reparent the guest into it, instead of handing the guest's window straight to WPF.

## Credits

The reparenting approach comes from work others published first:

- **[itsho's gist, "Hosting an app inside a WPF app"](https://gist.github.com/itsho/8b0e761d9114e27c8570fbf95465bbfc)**
  — the `HwndHost` subclass, the `Win32API` P/Invoke declarations and the child-window style
  combination all started here.
- **[FreeCAD](https://www.freecad.org/)** (LGPL-2.1-or-later) is used as the example guest
  application. It is not bundled or modified here — the sample just launches whatever you point it
  at.

Other web sources contributed to the original spike but were not recorded at the time. If you
recognise your work here, please open an issue and it will be credited.

Everything else — the process lifecycle, the splash-screen handling, the tests and this
documentation — was written for this repository.

## License

[MIT](LICENSE)