using System;
using System.IO;

namespace Gaius.Utilities.FileSystem
{
    public static class FileSystemInfoExtensions
    {
        public static bool IsFile(this FileSystemInfo fsInfo)
        {
            return fsInfo is FileInfo;
        }

        public static bool IsDirectory(this FileSystemInfo fsInfo)
        {
            return fsInfo is DirectoryInfo;
        }

        public static bool FileHasExtension(this FileSystemInfo fsInfo, string extension)
        {
            if(!fsInfo.IsFile())
                return false;

            return (fsInfo as FileInfo).Extension.Equals(extension, StringComparison.InvariantCultureIgnoreCase);
        }

        public static string GetNameWithoutExtension(this FileSystemInfo fsInfo)
        {
            if(!fsInfo.IsFile())
                return fsInfo.Name;

            return Path.GetFileNameWithoutExtension(fsInfo.Name);
        }

        public static bool IsMarkdownFile(this FileSystemInfo fsInfo)
        {
            return fsInfo.FileHasExtension(".md");
        }

        public static bool IsJSFile(this FileSystemInfo fsInfo)
        {
            return fsInfo.FileHasExtension(".js");
        }

        public static bool IsCSSFile(this FileSystemInfo fsInfo)
        {
            return fsInfo.FileHasExtension(".css");
        }

        public static bool IsLiquidFile(this FileSystemInfo fsInfo)
        {
            return fsInfo.FileHasExtension(".liquid");
        }

        public static bool IsLessFile(this FileSystemInfo fsInfo)
        {
            return fsInfo.FileHasExtension(".less");
        }
        
        public static bool IsSassFile(this FileSystemInfo fsInfo)
        {
            return fsInfo.FileHasExtension(".sass");
        }
    }
}