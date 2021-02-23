using System;
using System.IO;

namespace Gaius.Server.BackgroundHostedService
{
    public class BuildRequest
    {
        public DateTime RequestDateTime { get; private set; }
        public FileSystemEventArgs FileSystemEventArgs { get; private set;}
        public BuildRequest(FileSystemEventArgs e)
        {
            RequestDateTime = DateTime.UtcNow;
            FileSystemEventArgs = e;
        }
    }
}