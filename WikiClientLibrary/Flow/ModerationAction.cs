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
