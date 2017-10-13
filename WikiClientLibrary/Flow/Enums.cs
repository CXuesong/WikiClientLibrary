using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary.Flow
{
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
            switch (value)
            {
                case ModerationAction.Delete: return "delete";
                case ModerationAction.Hide: return "hide";
                case ModerationAction.Suppress: return "suppress";
                case ModerationAction.Restore: return "restore";
                case ModerationAction.Unhide: return "unhide";
                case ModerationAction.Undelete: return "undelete";
                case ModerationAction.Unsuppress: return "unsuppress";
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        public static string ToString(LockAction value)
        {
            switch (value)
            {
                case LockAction.Lock: return "lock";
                case LockAction.Unlock: return "unlock";
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        public static ModerationState ParseModerationState(string value)
        {
            switch (value)
            {
                case "delete": return ModerationState.Deleted;
                case "hide": return ModerationState.Hidden;
                case "suppress": return ModerationState.Suppressed;
                default: return ModerationState.Unknown;
            }
        }

    }

}
