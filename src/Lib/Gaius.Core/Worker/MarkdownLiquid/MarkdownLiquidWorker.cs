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
using System.Text.RegularExpressions;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class MarkdownLiquidWorker : BaseWorker, IWorker
    {
        private const string _layoutsDirectory = "_layouts";
        private const string _defaultLayoutId = "default";
        private readonly IFrontMatterParser _frontMatterParser;
        private readonly Dictionary<string, MarkdownLiquidLayoutInfo> LayoutInfoDictionary = new Dictionary<string, MarkdownLiquidLayoutInfo>();
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

        public MarkdownLiquidWorker(IFrontMatterParser frontMatterParser, IOptions<GaiusConfiguration> gaiusConfigurationOptions)
        {
            _frontMatterParser = frontMatterParser;
            GaiusConfiguration = gaiusConfigurationOptions.Value;
            RequiredDirectories = new List<string> { GetLayoutsDirFullPath(GaiusConfiguration) };
            BuildLayoutInfoDictionary();
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
                
            var markdownFile = task.FSInfo.FileInfo;
            var markdownContent = MarkdownPreProcess(File.ReadAllText(markdownFile.FullName));

            var yamlFrontMatter = task.FSInfo.FrontMatter;
            var layoutId = !string.IsNullOrEmpty(yamlFrontMatter.Layout) ? yamlFrontMatter.Layout : _defaultLayoutId;

            var html = Markdown.ToHtml(markdownContent, _markdownPipeline);
            var pageData = new PageData(yamlFrontMatter, html, task);
            var viewModel = new MarkdownLiquidViewModel(pageData, GenerationInfo, GaiusConfiguration);

            if (!LayoutInfoDictionary.TryGetValue(layoutId, out var layoutInfo))
                throw new Exception($"Unable to find layout: {layoutId}");

            if (FluidTemplate.TryParse(layoutInfo.LayoutContent, out var liquidTemplate))
            {
                var context = new TemplateContext();
                context.FileProvider = _liquidTemplatePhysicalFileProvider;
                context.MemberAccessStrategy.Register<MarkdownLiquidViewModel>();
                context.MemberAccessStrategy.Register<MarkdownLiquidViewModel_Page>();
                context.MemberAccessStrategy.Register<MarkdownLiquidViewModel_Site>();
                context.MemberAccessStrategy.Register<MarkdownLiquidViewModel_GaiusInfo>();
                context.Model = viewModel;
                return liquidTemplate.Render(context);
            }

            return html;
        }

        public override string GetTarget(FileSystemInfo fileSystemInfo)
        {
            var targetName = base.GetTarget(fileSystemInfo);

            if(fileSystemInfo.IsMarkdownFile())
                return $"{Path.GetFileNameWithoutExtension(fileSystemInfo.Name)}.html";

            return targetName;
        }

        public override WorkerFSMetaInfo GetWorkerFSMetaInfo(FileSystemInfo fileSystemInfo)
        {
            var fsMetaInfo = new WorkerFSMetaInfo()
            {
                ShouldSkip = ShouldSkip(fileSystemInfo),
                ShouldKeep = ShouldKeep(fileSystemInfo),
            };

            if (fileSystemInfo.IsMarkdownFile())
            {
                fsMetaInfo.IsPost = IsPost(fileSystemInfo);
                fsMetaInfo.FrontMatter = _frontMatterParser.DeserializeFromContent(File.ReadAllText(fileSystemInfo.FullName));

                if (!LayoutInfoDictionary.TryGetValue(fsMetaInfo.FrontMatter.Layout, out var layoutInfo)
                    && !LayoutInfoDictionary.TryGetValue(_defaultLayoutId, out layoutInfo))
                    throw new Exception($"Unable to find layout information for: {fsMetaInfo.FrontMatter.Layout} or {_defaultLayoutId}");

                fsMetaInfo.PaginatorIds = layoutInfo.PaginatorIds;
            }

            return fsMetaInfo;
        }

        protected override bool ShouldSkip(FileSystemInfo fileSystemInfo)
        {
            if(base.ShouldSkip(fileSystemInfo))
                return true;

            if(fileSystemInfo.Name.Equals(_layoutsDirectory, StringComparison.InvariantCultureIgnoreCase))
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
            return Path.Combine(gaiusConfiguration.NamedThemeDirectoryFullPath, _layoutsDirectory);
        }

        private string MarkdownPreProcess(string markdownContent)
        {
            return GenerationUrlRootPrefixPreProcessor(markdownContent);
        }

        private const string _siteUrlRegExStr = @"{{ *site.url *}}";
        private static Regex _siteUrlRegEx = new Regex(_siteUrlRegExStr, RegexOptions.Compiled);
        private string GenerationUrlRootPrefixPreProcessor(string markdownContent)
        {
            return _siteUrlRegEx.Replace(markdownContent, GaiusConfiguration.GetGenerationUrlRootPrefix());
        }

        private void BuildLayoutInfoDictionary()
        {
            var layoutsDirectory = new DirectoryInfo(GetLayoutsDirFullPath(GaiusConfiguration));

            foreach(var file in layoutsDirectory.EnumerateFiles())
            {
                if(!file.IsLiquidFile())
                    continue;

                var layoutInfo = new MarkdownLiquidLayoutInfo(file);
                LayoutInfoDictionary.Add(layoutInfo.Id, layoutInfo);
            }
        }
    }
}