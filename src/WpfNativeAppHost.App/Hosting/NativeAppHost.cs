using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

using WpfNativeAppHost.Interop;

namespace WpfNativeAppHost.Hosting;

/// <summary>
/// A WPF element that displays another process's top-level window in place, by reparenting that
/// window underneath the WPF window and letting <see cref="HwndHost"/> position and size it.
/// </summary>
/// <remarks>
/// <para>
/// The window is borrowed, not owned. On teardown it is handed back to the desktop and its original
/// style restored <em>before</em> the hosted process is asked to quit, so the guest application
/// never has its window destroyed underneath it by the WPF window going away.
/// </para>
/// <para>
/// Adapted from <see href="https://gist.github.com/itsho/8b0e761d9114e27c8570fbf95465bbfc"/> and
/// <see href="https://stackoverflow.com/q/30186930/426315"/>; see the README for full credits.
/// </para>
/// </remarks>
internal sealed class NativeAppHost : HwndHost
{
    private readonly HostedProcess _process;
    private readonly IntPtr _childWindow;

    private WindowStyles _originalStyle;
    private bool _adopted;

    /// <param name="process">The running application whose window is to be embedded.</param>
    /// <param name="childWindow">
    /// That application's top-level window, as returned by <see cref="HostedProcess.WaitForMainWindow"/>.
    /// </param>
    public NativeAppHost(HostedProcess process, IntPtr childWindow)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (childWindow == IntPtr.Zero)
        {
            throw new ArgumentException("A valid window handle is required.", nameof(childWindow));
        }

        _process = process;
        _childWindow = childWindow;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// The guest destroyed its window between it being found and WPF getting round to adopting it.
    /// </exception>
    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        // WPF calls this during layout, some time after the window was located, and the guest owns
        // that window throughout. Check rather than let SetWindowLongPtr fail obscurely.
        if (!NativeMethods.IsWindow(_childWindow))
        {
            throw new InvalidOperationException(
                $"'{_process.ExecutablePath}' closed window 0x{_childWindow:X} before it could be embedded.");
        }

        _originalStyle = (WindowStyles)(uint)NativeMethods.GetWindowLongPtr(_childWindow, NativeMethods.GwlStyle);

        NativeMethods.SetWindowLongPtr(
            _childWindow,
            NativeMethods.GwlStyle,
            (IntPtr)(uint)ChildWindowStyle.ForEmbedding(_originalStyle));

        NativeMethods.SetParent(_childWindow, hwndParent.Handle);
        _adopted = true;

        return new HandleRef(this, _childWindow);
    }

    /// <inheritdoc/>
    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        // Detach before shutting the guest down. If the WPF window were destroyed while still the
        // parent, Windows would destroy the guest's window with it and the application would be
        // torn down mid-frame rather than closing normally.
        if (_adopted)
        {
            _adopted = false;
            TryRestoreTopLevelWindow();
        }

        _process.Dispose();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // Covers the case where the element is disposed without ever having built its window;
        // HostedProcess.Dispose is idempotent, so the ordinary path is unaffected.
        if (disposing)
        {
            _process.Dispose();
        }
    }

    private void TryRestoreTopLevelWindow()
    {
        try
        {
            NativeMethods.SetParent(_childWindow, IntPtr.Zero);
            NativeMethods.SetWindowLongPtr(_childWindow, NativeMethods.GwlStyle, (IntPtr)(uint)_originalStyle);
        }
        catch (Win32Exception)
        {
            // The guest closed its own window first, so there is nothing left to hand back.
        }
    }
}
