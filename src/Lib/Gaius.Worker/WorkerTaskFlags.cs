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
        IsDraftsDir = 32,
        IsDraft = 64,
        IsTagListDir = 128,
        IsTagList = 256,
        IsSkip = 512,
        IsNamedThemeDir = 1024,
        IsChildOfNamedThemeDir = 2048,
        IsChildOfGenDir = 4096,
        IsKeep = 8192,
        IsInvalid = 16384
    }
}