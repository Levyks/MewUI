namespace Aprillz.MewUI.Platform;

/// <summary>
/// Separates a tray icon's single click from the first half of a double-click. The platform reports a
/// double-click as click, double-click, click; this defers the first click until the system double-click
/// time has passed so a double-click raises only the double-click, and swallows the click that trails it.
/// Pure state with no timer of its own: the backend runs the timer and reports its outcome.
/// </summary>
internal sealed class TrayClickDisambiguator
{
    private bool _clickPending;
    private bool _swallowRelease;

    /// <summary>Gets whether a click is waiting for the double-click time to pass.</summary>
    public bool IsClickPending => _clickPending;

    /// <summary>
    /// Reports a primary button press that starts a new click. A release swallowed after a double-click
    /// that never arrived must not eat this click's release.
    /// </summary>
    public void OnPrimaryDown() => _swallowRelease = false;

    /// <summary>
    /// Reports a primary button release (a selection). Returns true when a click is now pending and the
    /// backend must start the double-click timer.
    /// </summary>
    public bool OnPrimaryRelease()
    {
        if (_swallowRelease)
        {
            _swallowRelease = false;
            return false;
        }

        _clickPending = true;
        return true;
    }

    /// <summary>
    /// Reports a primary double-click. Cancels the pending click, whose timer the backend must stop, and
    /// swallows the release that follows the second press.
    /// </summary>
    public void OnPrimaryDoubleClick()
    {
        _clickPending = false;
        _swallowRelease = true;
    }

    /// <summary>
    /// Reports a keyboard activation. It is raised at once, never part of a double-click, and a selection
    /// the shell sends alongside it is swallowed so one key press is one click.
    /// </summary>
    public void OnKeyboardSelect()
    {
        _clickPending = false;
        _swallowRelease = true;
    }

    /// <summary>
    /// Reports that the double-click time passed. Returns true when the pending click must now be raised.
    /// </summary>
    public bool OnDoubleClickTimeElapsed()
    {
        if (!_clickPending)
        {
            return false;
        }

        _clickPending = false;
        return true;
    }

    /// <summary>Drops all pending state.</summary>
    public void Reset()
    {
        _clickPending = false;
        _swallowRelease = false;
    }
}
