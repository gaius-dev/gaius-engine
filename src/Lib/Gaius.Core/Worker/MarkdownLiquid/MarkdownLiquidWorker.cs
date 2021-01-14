using System;
using System.IO;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Fluid;
using Gaius.Core.Configuration;
using Markdig;
using Gaius.Core.Parsing;
using Gaius.Utilities.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Gaius.Core.Parsing.Yaml;
using System.Collections.Generic;
using Gaius.Core.Models;
using Gaius.Core.Processing.FileSystem;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class MarkdownLiquidWorker : BaseWorker, IWorker
    {
        private const string LAYOUTS_DIRECTORY = "_layouts";
        private static readonly MarkdownPipeline _markdownPipeline 
                                                        = new MarkdownPipelineBuilder()
                                                                .UseYamlFrontMatter() //Markdig extension to parse YAML
                                                                .UseCustomContainers() // Markdig custom contain extension
                                                                .UseEmphasisExtras() // Markdig extension for extra emphasis extension
                                                                .UseListExtras() // Markdig extension for extra bullet lists
                                                                .UseFigures() //Markdig extension for figures & footers
                                                                .UsePipeTables() // Markdig extension for Github style pipe tables
                                                                .UseMediaLinks() //Markdig extension for media links (e.g. youtube)
                                                                .UseBootstrap() //Markdig extension to apply bootstrap CSS classes automatically
                                                                .UseGenericAttributes() //Markdig extension to allow application of generic HTML attributes
                                                                .Build();
                                                                
        private static IFileProvider _liquidTemplatePhysicalFileProvider;

        public MarkdownLiquidWorker(IOptions<GaiusConfiguration> gaiusConfigurationOptions)
        {
            GaiusConfiguration = gaiusConfigurationOptions.Value;
            RequiredDirectories = new List<string> { GetLayoutsDirFullPath(GaiusConfiguration) };
        }

        public static IServiceCollection ConfigureServicesForWorker(IServiceCollection serviceCollection)
        {
            serviceCollection
                .AddSingleton<IFrontMatterParser, YamlFrontMatterParser>()
                .AddSingleton<IWorker, MarkdownLiquidWorker>();
            return serviceCollection;
        }

        public override WorkerTask GenerateWorkerTask(FSInfo fsInfo)
        {
            return new WorkerTask(fsInfo, GetTransformType(fsInfo.FileSystemInfo), GetTarget(fsInfo.FileSystemInfo), GaiusConfiguration);
        }

        public override string PerformWork(WorkerTask task)
        {
            if(task.WorkType != WorkType.Transform)
                throw new Exception("The MarkdownLiquidWorker can only be assigned WorkerTasks where WorkerTask.WorkType == Transform");

            if(!task.FSInfo.FileSystemInfo.IsMarkdownFile())
                throw new Exception("The MarkdownLiquidWorker can only be assigned WorkerTasks where WorkerTask.FSInput is a markdown file");
            
            if(_liquidTemplatePhysicalFileProvider == null)
                _liquidTemplatePhysicalFileProvider = new PhysicalFileProvider(GetLayoutsDirFullPath(GaiusConfiguration));
                
            var markdownFile = task.FSInfo.FileSystemInfo as FileInfo;
            var markdownContent = MarkdownPreProcess(File.ReadAllText(markdownFile.FullName));

            var yamlFrontMatter = task.FSInfo.FrontMatter;
            var layoutName = !string.IsNullOrEmpty(yamlFrontMatter.Layout) ? yamlFrontMatter.Layout : "default";

            var markdownHtml = Markdown.ToHtml(markdownContent, _markdownPipeline);

            var pageData = new PageData(yamlFrontMatter, markdownHtml, task);

            var liquidModel = new LiquidTemplateModel(pageData, GenerationInfo, GaiusConfiguration);

            var liquidSourcePath = Path.Combine(GetLayoutsDirFullPath(GaiusConfiguration), $"{layoutName}.liquid");
            var liquidSource = File.ReadAllText(liquidSourcePath);

            if (FluidTemplate.TryParse(liquidSource, out var template))
            {
                var context = new TemplateContext();
                context.FileProvider = _liquidTemplatePhysicalFileProvider;
                context.MemberAccessStrategy.Register<LiquidTemplateModel>();
                context.MemberAccessStrategy.Register<LiquidTemplateModel_Page>();
                context.MemberAccessStrategy.Register<LiquidTemplateModel_Site>();
                context.MemberAccessStrategy.Register<LiquidTemplateModel_GaiusInfo>();
                context.Model = liquidModel;
                return template.Render(context);
            }

            return markdownHtml;
        }

        public override string GetTarget(FileSystemInfo fileSystemInfo)
        {
            var targetName = base.GetTarget(fileSystemInfo);

            if(fileSystemInfo.IsMarkdownFile())
                return $"{Path.GetFileNameWithoutExtension(fileSystemInfo.Name)}.html";

            return targetName;
        }

        public override bool HasFrontMatter(FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo.IsMarkdownFile();
        }

        public override bool IsPost(FileSystemInfo fileSystemInfo)
        {
            return base.IsPost(fileSystemInfo) && fileSystemInfo.IsMarkdownFile();
        }

        public override bool ShouldSkip(FileSystemInfo fileSystemInfo)
        {
            if(base.ShouldSkip(fileSystemInfo))
                return true;

            if(fileSystemInfo.Name.Equals(LAYOUTS_DIRECTORY, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if(fileSystemInfo.IsLiquidFile())
                return true;

            return false;
        }

        private static WorkType GetTransformType(FileSystemInfo fileSystemInfo)
        {
            if (fileSystemInfo.IsMarkdownFile())
                return WorkType.Transform;

            return WorkType.None;
        }

        private static string GetLayoutsDirFullPath(GaiusConfiguration gaiusConfiguration)
        {
            return Path.Combine(gaiusConfiguration.NamedThemeDirectoryFullPath, LAYOUTS_DIRECTORY);
        }

        private string MarkdownPreProcess(string markdownContent)
        {
            return GenerationUrlRootPrefixPreProcessor(markdownContent);
        }

        private static readonly string ROOT_PREFIX_LIQUID_TAG = "{{site.url}}";
        private string GenerationUrlRootPrefixPreProcessor(string markdownContent)
        {
            return markdownContent.Replace(ROOT_PREFIX_LIQUID_TAG, GaiusConfiguration.GetGenerationUrlRootPrefix());
        }
    }
}