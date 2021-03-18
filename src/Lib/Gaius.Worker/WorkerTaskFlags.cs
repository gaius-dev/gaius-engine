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
        IsDraftPostsDir = 64,
        IsDraft = 128,
        IsDraftPost = 256,
        IsTagListDir = 512,
        IsTagListing = 1024,
        IsSkip = 2048,
        IsNamedThemeDir = 4096,
        IsChildOfNamedThemeDir = 8192,
        IsInvalid = 16384
    }
}