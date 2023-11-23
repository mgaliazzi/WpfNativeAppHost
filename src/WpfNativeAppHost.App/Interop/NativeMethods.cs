using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WpfNativeAppHost.Interop;

/// <summary>
/// The handful of User32 entry points needed to adopt another process's top-level window as a
/// child of one of ours.
/// </summary>
internal static class NativeMethods
{
    /// <summary>Index passed to <c>Get/SetWindowLongPtr</c> to reach the window's style bits.</summary>
    internal const int GwlStyle = -16;

    /// <summary>Index passed to <c>Get/SetWindowLongPtr</c> to reach the window's extended style bits.</summary>
    internal const int GwlExStyle = -20;

    /// <summary>
    /// Reparents <paramref name="child"/> under <paramref name="newParent"/>. Passing
    /// <see cref="IntPtr.Zero"/> for <paramref name="newParent"/> returns the window to the desktop.
    /// </summary>
    /// <exception cref="Win32Exception">The window was not reparented.</exception>
    /// <remarks>
    /// Success is confirmed by reading the parent back rather than by inspecting the return value
    /// or the last error, neither of which can be trusted here. <c>SetParent</c> returns the
    /// previous parent, and the window being adopted is top-level, so <see cref="IntPtr.Zero"/> is
    /// the expected result of a successful call - it means only that there was no previous parent.
    /// Observed on Windows 11 with FreeCAD, a successful call also leaves
    /// <c>ERROR_INVALID_WINDOW_HANDLE</c> in the thread's last error, so treating a zero return plus
    /// a non-zero error as failure produces a crash on a call that in fact worked.
    /// </remarks>
    internal static void SetParent(IntPtr child, IntPtr newParent)
    {
        SetParentNative(child, newParent);

        IntPtr actualParent = GetParent(child);
        if (actualParent != newParent)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                $"Could not reparent window 0x{child:X} under 0x{newParent:X}; " +
                $"its parent is 0x{actualParent:X}.");
        }
    }

    /// <summary>Returns a window's parent, or <see cref="IntPtr.Zero"/> if it is top-level.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetParent(IntPtr window);

    /// <summary>Reads one of the window's <c>GWL_*</c> values.</summary>
    /// <exception cref="Win32Exception">The call failed.</exception>
    internal static IntPtr GetWindowLongPtr(IntPtr window, int index)
    {
        Marshal.SetLastSystemError(0);

        // GetWindowLongPtr only exists on 64-bit Windows; on 32-bit the 32-bit entry point is the
        // real thing and the pointer-sized wrapper is a macro over it.
        IntPtr value = IntPtr.Size == 8
            ? GetWindowLongPtr64(window, index)
            : new IntPtr(GetWindowLong32(window, index));

        ThrowIfCallFailed(value);
        return value;
    }

    /// <summary>Writes one of the window's <c>GWL_*</c> values.</summary>
    /// <returns>The previous value.</returns>
    /// <exception cref="Win32Exception">The call failed.</exception>
    internal static IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value)
    {
        Marshal.SetLastSystemError(0);

        IntPtr previous = IntPtr.Size == 8
            ? SetWindowLongPtr64(window, index, value)
            : new IntPtr(SetWindowLong32(window, index, value.ToInt32()));

        ThrowIfCallFailed(previous);
        return previous;
    }

    /// <summary>Passed to <see cref="GetWindow"/> to ask for a window's owner.</summary>
    internal const uint GwOwner = 4;

    /// <summary>Called once per top-level window by <see cref="EnumWindows"/>.</summary>
    /// <returns><see langword="false"/> to stop the enumeration early.</returns>
    internal delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    /// <summary>Visits every top-level window on the desktop.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    /// <summary>Identifies the process a window belongs to.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    /// <summary>True while the handle still refers to a window that exists.</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr window);

    /// <summary>True if the window has the <c>WS_VISIBLE</c> style.</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr window);

    /// <summary>Walks a window's relationships; used here with <see cref="GwOwner"/>.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetWindow(IntPtr window, uint relationship);

    /// <summary>
    /// These APIs signal failure by returning zero, which is also a perfectly valid result. The
    /// caller clears the thread's last error beforehand, so a zero result paired with a non-zero
    /// error code is the only reliable failure signature.
    /// </summary>
    private static void ThrowIfCallFailed(IntPtr result)
    {
        if (result != IntPtr.Zero)
        {
            return;
        }

        // GetLastPInvokeError, not GetLastSystemError: with SetLastError = true the runtime captures
        // the API's error into the managed slot as the call returns, and that is the copy which is
        // guaranteed not to have been overwritten since.
        int error = Marshal.GetLastPInvokeError();
        if (error != 0)
        {
            throw new Win32Exception(error);
        }
    }

    [DllImport("user32.dll", EntryPoint = "SetParent", SetLastError = true)]
    private static extern IntPtr SetParentNative(IntPtr child, IntPtr newParent);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr window, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);
}
