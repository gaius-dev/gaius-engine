using System;

namespace Gaius.Worker
{
    [Flags]
    public enum WorkerTaskFlags
    {
        None = 0,
        IsSiteContainerDir = 1,
        IsSourceDir = 2,
        IsChildOfSourceDir = 4,
        IsPostsDir = 8,
        IsPost = 16,
        IsPostListing = 32,
        IsDraftsDir = 64,
        IsDraft = 128,
        IsTagListDir = 256,
        IsTagListing = 512,
        IsSkip = 1024,
        IsNamedThemeDir = 2048,
        IsChildOfNamedThemeDir = 4096,
        IsChildOfGenDir = 8192,
        IsKeep = 16384,
        IsInvalid = 32768
    }
}