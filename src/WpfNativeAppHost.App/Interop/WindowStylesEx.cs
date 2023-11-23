namespace WpfNativeAppHost.Interop;

/// <summary>
/// Extended window styles (the <c>WS_EX_*</c> constants), reached through
/// <see cref="NativeMethods.GwlExStyle"/> - a different bit space from <see cref="WindowStyles"/>.
/// </summary>
/// <remarks>
/// Only the styles this project inspects are listed. See
/// <see href="https://learn.microsoft.com/windows/win32/winmsg/extended-window-styles"/>.
/// </remarks>
[Flags]
internal enum WindowStylesEx : uint
{
    /// <summary>No extended styles.</summary>
    None = 0x0000_0000,

    /// <summary>
    /// A floating toolbar, tooltip or splash screen: no taskbar button and no place in the Alt+Tab
    /// order. Applications set this on transient windows, which is what makes it a reliable way to
    /// tell a splash screen apart from the real main window.
    /// </summary>
    ToolWindow = 0x0000_0080,

    /// <summary>Forces a taskbar button even when the window would not otherwise get one.</summary>
    AppWindow = 0x0004_0000,
}
