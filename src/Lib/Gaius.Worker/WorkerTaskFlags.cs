using System;

namespace Gaius.Worker
{
    [Flags]
    public enum WorkerTaskFlags
    {
        None = 0,
        IsChildOfSourceDir = 1,
        IsPost = 2,
        IsDraft = 4,
        IsSkip = 8,
        IsChildOfGenDir = 16,
        IsChildOfNamedThemeDir = 32,
        IsKeep = 64,
        IsSiteContainerDir = 128,
        IsSourceDir = 256,
        IsNamedThemeDir = 512,
        IsPostsDir = 1024,
        IsDraftsDir = 2048,
        IsInvalid = 4096,
    }
}