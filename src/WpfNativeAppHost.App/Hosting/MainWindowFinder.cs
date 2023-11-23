using WpfNativeAppHost.Interop;

namespace WpfNativeAppHost.Hosting;

/// <summary>
/// Finds the window of a process that a user would call "the main window".
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Diagnostics.Process.MainWindowHandle"/> is not good enough here. It returns
/// the first visible top-level window it happens to find, which during startup is very often a
/// splash screen. Embedding one of those produces a pane that goes blank a few seconds later, when
/// the splash closes and takes the adopted window with it.
/// </para>
/// <para>
/// FreeCAD 1.1 is a concrete example: its splash appears about 1.5s in with the extended style
/// <c>WS_EX_TOOLWINDOW</c>, and the real window only replaces it around 5s. Skipping tool windows
/// and owned windows - the convention every toolkit follows for transient UI - picks the right one.
/// </para>
/// </remarks>
internal static class MainWindowFinder
{
    /// <summary>
    /// Returns the process's main window, or <see cref="IntPtr.Zero"/> if it has not opened one yet.
    /// </summary>
    public static IntPtr Find(int processId)
    {
        IntPtr found = IntPtr.Zero;

        NativeMethods.EnumWindows(
            (window, _) =>
            {
                if (!IsMainWindowOf(window, (uint)processId))
                {
                    return true;
                }

                found = window;
                return false;
            },
            IntPtr.Zero);

        return found;
    }

    private static bool IsMainWindowOf(IntPtr window, uint processId)
    {
        NativeMethods.GetWindowThreadProcessId(window, out uint owner);
        if (owner != processId || !NativeMethods.IsWindowVisible(window))
        {
            return false;
        }

        // Dialogs, tool palettes and message boxes are owned by another window; a main window is not.
        if (NativeMethods.GetWindow(window, NativeMethods.GwOwner) != IntPtr.Zero)
        {
            return false;
        }

        // Splash screens, tooltips and floating toolbars mark themselves as tool windows so that
        // they stay out of the taskbar and the Alt+Tab order.
        var extendedStyle = (WindowStylesEx)(uint)NativeMethods.GetWindowLongPtr(window, NativeMethods.GwlExStyle);
        return !extendedStyle.HasFlag(WindowStylesEx.ToolWindow);
    }
}
