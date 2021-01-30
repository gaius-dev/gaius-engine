namespace Gaius.Core.Processing.FileSystem
{
    public enum FSOperationType
    {
        Undefined,
        Root,
        Skip,
        CreateNew,
        Overwrite,
        Keep,
        Delete
    }
}