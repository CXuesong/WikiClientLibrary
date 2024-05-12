namespace WikiClientLibrary.Flow;

/// <summary>
/// Actions used for moderating a Flow topic or post.
/// </summary>
public enum ModerationAction
{

    Delete,
    Hide,
    Suppress,
    Restore,
    Unhide,
    Undelete,
    Unsuppress,

}

/// <summary>
/// Moderation state of a Flow topic or post.
/// </summary>
public enum ModerationState
{
    /// <summary>Not moderated.</summary>
    None = 0,
    /// <summary>An unknown moderation state.</summary>
    Unknown,
    /// <summary>Deleted.</summary>
    Deleted,
    /// <summary>Hidden.</summary>
    Hidden,
    /// <summary>Suppressed.</summary>
    Suppressed,
}

/// <summary>
/// The locking operations to perform on a Flow topic.
/// </summary>
public enum LockAction
{
    Lock = 0,
    Unlock = 1,
}

internal static partial class EnumParser
{

    public static string ToString(ModerationAction value)
    {
        return value switch
        {
            ModerationAction.Delete => "delete",
            ModerationAction.Hide => "hide",
            ModerationAction.Suppress => "suppress",
            ModerationAction.Restore => "restore",
            ModerationAction.Unhide => "unhide",
            ModerationAction.Undelete => "undelete",
            ModerationAction.Unsuppress => "unsuppress",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    public static string ToString(LockAction value)
    {
        return value switch
        {
            LockAction.Lock => "lock",
            LockAction.Unlock => "unlock",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    public static ModerationState ParseModerationState(string value)
    {
        return value switch
        {
            "delete" => ModerationState.Deleted,
            "hide" => ModerationState.Hidden,
            "suppress" => ModerationState.Suppressed,
            _ => ModerationState.Unknown
        };
    }

}