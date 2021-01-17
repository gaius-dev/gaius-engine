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
using System.Text.RegularExpressions;
using System.Linq;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class MarkdownLiquidWorker : BaseWorker, IWorker
    {
        private const string _layoutsDirectory = "_layouts";
        private const string _defaultLayoutId = "default";
        private readonly IFrontMatterParser _frontMatterParser;
        private readonly Dictionary<string, IWorkerLayoutInfo> LayoutInfoDictionary = new Dictionary<string, IWorkerLayoutInfo>();
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

        public override string PerformWork(WorkerTask workerTask)
        {
            if(workerTask.WorkType != WorkType.Transform)
                throw new Exception("The MarkdownLiquidWorker can only be assigned WorkerTasks where WorkerTask.WorkType == Transform");

            if(!workerTask.FileSystemInfo.IsMarkdownFile())
                throw new Exception("The MarkdownLiquidWorker can only be assigned WorkerTasks where WorkerTask.FSInput is a markdown file");
            
            if(_liquidTemplatePhysicalFileProvider == null)
                _liquidTemplatePhysicalFileProvider = new PhysicalFileProvider(GetLayoutsDirFullPath(GaiusConfiguration));
                
            var markdownFile = workerTask.FileInfo;
            var markdownContent = MarkdownPreProcess(File.ReadAllText(markdownFile.FullName));

            var layoutId = !string.IsNullOrEmpty(workerTask.LayoutId) ? workerTask.LayoutId : _defaultLayoutId;

            var html = Markdown.ToHtml(markdownContent, _markdownPipeline);

            var viewModelData = new ViewModelData(workerTask, GenerationInfo, html);
            var viewModel = new MarkdownLiquidViewModel(viewModelData, GaiusConfiguration);

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
        public override WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo)
        {
            IFrontMatter frontMatter = null;
            IWorkerLayoutInfo layoutInfo = null;
            var workType = GetWorkType(fileSystemInfo);
            var isPost = GetIsPost(fileSystemInfo);
            var shouldSkip = GetShouldSkip(fileSystemInfo);
            var shouldKeep = GetShouldKeep(fileSystemInfo);
            var targetFSName = GetTarget(fileSystemInfo);
            (var targetUrl, var targetId) = GetTargetUrlAndId(fileSystemInfo, targetFSName);

            if (fileSystemInfo.IsMarkdownFile())
            {
                frontMatter = _frontMatterParser.DeserializeFromContent(File.ReadAllText(fileSystemInfo.FullName));

                if (!LayoutInfoDictionary.TryGetValue(frontMatter.Layout, out layoutInfo)
                    && !LayoutInfoDictionary.TryGetValue(_defaultLayoutId, out layoutInfo))
                    throw new Exception($"Unable to find layout information for: {frontMatter.Layout} or {_defaultLayoutId}");
            }

            return new WorkerTask(fileSystemInfo, layoutInfo, frontMatter, workType, isPost, shouldSkip, shouldKeep, targetFSName, targetUrl, targetId);
        }

        protected override bool GetShouldSkip(FileSystemInfo fileSystemInfo)
        {
            if(base.GetShouldSkip(fileSystemInfo))
                return true;

            if(fileSystemInfo.Name.Equals(_layoutsDirectory, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if(fileSystemInfo.IsLiquidFile())
                return true;

            return false;
        }
        protected override bool GetIsPost(FileSystemInfo fileSystemInfo)
        {
            return base.GetIsPost(fileSystemInfo) && fileSystemInfo.IsMarkdownFile();
        }

        private (string, string) GetTargetUrlAndId(FileSystemInfo fileSystemInfo, string targetFSName)
        {
            if(fileSystemInfo.IsDirectory())
                return (string.Empty, string.Empty);

            var pathSegments = fileSystemInfo.GetPathSegments();

            var sourceDirIndex = Array.IndexOf(pathSegments, GaiusConfiguration.SourceDirectoryName);
            var skipAmt = sourceDirIndex + 1;

            if(skipAmt > pathSegments.Length)
                return (string.Empty, string.Empty);

            var pathSegmentsToKeep = pathSegments.Skip(skipAmt).ToList();

            if(pathSegmentsToKeep.Count == 0)
                return (string.Empty, string.Empty);
            
            pathSegmentsToKeep[pathSegmentsToKeep.Count - 1] = targetFSName;

            var targetUrl = $"{GaiusConfiguration.GetGenerationUrlRootPrefix()}/{string.Join("/", pathSegmentsToKeep)}";

            var targetWithoutExt = Path.GetFileNameWithoutExtension(targetFSName);
            pathSegmentsToKeep[pathSegmentsToKeep.Count - 1] = targetWithoutExt;

            var targetId = $"{string.Join(".", pathSegmentsToKeep)}";

            return (targetUrl, targetId);
        }
        private static WorkType GetWorkType(FileSystemInfo fileSystemInfo)
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