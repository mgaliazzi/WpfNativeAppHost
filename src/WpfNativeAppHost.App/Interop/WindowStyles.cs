namespace WpfNativeAppHost.Interop;

/// <summary>
/// Standard window styles (the <c>WS_*</c> constants) as read from and written to
/// <see cref="NativeMethods.GwlStyle"/>.
/// </summary>
/// <remarks>
/// Only the plain <c>WS_*</c> styles live here. The <c>WS_EX_*</c> extended styles occupy a
/// separate bit space reached through <see cref="NativeMethods.GwlExStyle"/> and deliberately are
/// not mixed into this enum; nothing in this project needs them.
/// See <see href="https://learn.microsoft.com/windows/win32/winmsg/window-styles"/>.
/// </remarks>
[Flags]
internal enum WindowStyles : uint
{
    /// <summary>A top-level window with a title bar and border. Value is 0, so it is the default.</summary>
    Overlapped = 0x0000_0000,

    /// <summary>A pop-up window. Cannot be combined with <see cref="Child"/>.</summary>
    Popup = 0x8000_0000,

    /// <summary>A child window, clipped to its parent's client area. Cannot be combined with <see cref="Popup"/>.</summary>
    Child = 0x4000_0000,

    /// <summary>The window is initially minimized.</summary>
    Minimize = 0x2000_0000,

    /// <summary>The window is initially visible.</summary>
    Visible = 0x1000_0000,

    /// <summary>The window is initially disabled and receives no input.</summary>
    Disabled = 0x0800_0000,

    /// <summary>Clips sibling windows out of this window's drawing area.</summary>
    ClipSiblings = 0x0400_0000,

    /// <summary>Excludes child windows from the parent's drawing area, which avoids repaint flicker.</summary>
    ClipChildren = 0x0200_0000,

    /// <summary>The window is initially maximized.</summary>
    Maximize = 0x0100_0000,

    /// <summary>The window has a thin-line border.</summary>
    Border = 0x0080_0000,

    /// <summary>The window has the border style typical of a dialog box.</summary>
    DlgFrame = 0x0040_0000,

    /// <summary>The window has a vertical scroll bar.</summary>
    VScroll = 0x0020_0000,

    /// <summary>The window has a horizontal scroll bar.</summary>
    HScroll = 0x0010_0000,

    /// <summary>The window has a window menu on its title bar.</summary>
    SysMenu = 0x0008_0000,

    /// <summary>The window has a sizing border.</summary>
    ThickFrame = 0x0004_0000,

    /// <summary>The window has a minimize button.</summary>
    MinimizeBox = 0x0002_0000,

    /// <summary>The window has a maximize button.</summary>
    MaximizeBox = 0x0001_0000,

    /// <summary>The window has a title bar (implies <see cref="Border"/>).</summary>
    Caption = Border | DlgFrame,

    /// <summary>The usual style combination for a top-level application window.</summary>
    OverlappedWindow = Overlapped | Caption | SysMenu | ThickFrame | MinimizeBox | MaximizeBox,
}
