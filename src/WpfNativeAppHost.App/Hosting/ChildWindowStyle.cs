using WpfNativeAppHost.Interop;

namespace WpfNativeAppHost.Hosting;

/// <summary>
/// Works out the style bits a borrowed top-level window needs in order to behave as an embedded
/// child window.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="NativeAppHost"/> - and free of any WPF types - because this is the
/// one piece of the reparenting that is pure arithmetic, and so the one piece that can be tested
/// without a window, a message loop or a second process.
/// </remarks>
internal static class ChildWindowStyle
{
    /// <summary>Styles that only make sense for a window the user can move, resize or minimise.</summary>
    private const WindowStyles TopLevelOnly =
        WindowStyles.Popup |
        WindowStyles.OverlappedWindow |
        WindowStyles.Maximize |
        WindowStyles.Minimize;

    /// <summary>
    /// Styles an embedded window must have. <see cref="WindowStyles.ClipChildren"/> and
    /// <see cref="WindowStyles.ClipSiblings"/> stop the parent painting over the guest, which is
    /// what otherwise shows up as flicker or a stale image when the WPF window redraws.
    /// </summary>
    /// <remarks>Add <see cref="WindowStyles.Border"/> for a thin frame around the embedded window.</remarks>
    private const WindowStyles EmbeddedChild =
        WindowStyles.Child |
        WindowStyles.Visible |
        WindowStyles.ClipChildren |
        WindowStyles.ClipSiblings;

    /// <summary>
    /// Rewrites a top-level window's style into that of an embedded child window, leaving styles
    /// unrelated to being top-level - scroll bars, for instance - exactly as the guest set them.
    /// </summary>
    internal static WindowStyles ForEmbedding(WindowStyles current) =>
        (current & ~TopLevelOnly) | EmbeddedChild;
}
