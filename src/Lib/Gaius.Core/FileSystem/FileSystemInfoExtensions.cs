using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Gaius.Core.FileSystem
{
    public static class FileSystemInfoExtensions
    {
        public static bool IsFile(this FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo is FileInfo;
        }

        public static bool IsDirectory(this FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo is DirectoryInfo;
        }

        public static bool FileHasExtension(this FileSystemInfo fileSystemInfo, string extension)
        {
            if(!fileSystemInfo.IsFile())
                return false;

            return (fileSystemInfo as FileInfo).Extension.Equals(extension, StringComparison.InvariantCultureIgnoreCase);
        }

        public static string GetNameWithoutExtension(this FileSystemInfo fileSystemInfo)
        {
            if(!fileSystemInfo.IsFile())
                return fileSystemInfo.Name;

            return Path.GetFileNameWithoutExtension(fileSystemInfo.Name);
        }

        public static bool IsMarkdownFile(this FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo.FileHasExtension(".md");
        }

        public static bool IsJSFile(this FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo.FileHasExtension(".js");
        }

        public static bool IsCSSFile(this FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo.FileHasExtension(".css");
        }

        public static bool IsLiquidFile(this FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo.FileHasExtension(".liquid");
        }

        public static bool IsLessFile(this FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo.FileHasExtension(".less");
        }
        
        public static bool IsSassFile(this FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo.FileHasExtension(".sass");
        }

        public static List<string> GetPathSegments(this FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo.FullName.Split(Path.DirectorySeparatorChar).ToList();
        }

        public static DirectoryInfo GetParentDirectory(this FileSystemInfo fileSystemInfo)
        {
            return Directory.GetParent(fileSystemInfo.FullName);
        }
    }
}