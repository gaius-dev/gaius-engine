using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Gaius.Core.Configuration;
using Gaius.Core.Terminal;

namespace GaiusServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if(args.Length >= 1 && args[0] == "version")
            {
                TerminalUtilities.PrintApplicationNameAndVersion();
                return;
            }

            var hostBuilder = CreateHostBuilder(args);

            if(hostBuilder == null)
                return;

            hostBuilder.Build().Run();
        }

        public static IConfigurationRoot BuildConfiguration(string basePath)
        {
            basePath = !string.IsNullOrEmpty(basePath) ? basePath : Directory.GetCurrentDirectory();

            return new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("gaius.json", optional : true, reloadOnChange : true)
                .Build();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var pathArg = args.Length >= 1 ? args[0] : ".";
            var basePath = Path.GetFullPath(pathArg);

            if(!Directory.Exists(basePath))
                return null;

            var configRoot = BuildConfiguration(basePath);

            var gaiusConfiguration = new GaiusConfiguration();
            gaiusConfiguration.SiteContainerFullPath = basePath;
            gaiusConfiguration.IsTestMode = true;
            configRoot.GetSection("GaiusEngine").Bind(gaiusConfiguration);

            var hostBuilder = Host.CreateDefaultBuilder()
                .UseContentRoot(basePath)
                .ConfigureAppConfiguration((hostBuilderContext, configBuilder) => {
                    configBuilder.AddJsonFile("gaius.json", optional : true, reloadOnChange : true);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseWebRoot(gaiusConfiguration.GenerationDirectoryName);
                    webBuilder.UseStartup<Startup>();
                });

            return hostBuilder;
        }
    }
}
