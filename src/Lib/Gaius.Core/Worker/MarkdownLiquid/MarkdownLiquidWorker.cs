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
        private readonly Dictionary<string, IWorkerLayoutData> LayoutDataDictionary = new Dictionary<string, IWorkerLayoutData>();
        private readonly Dictionary<string, BaseViewModelData> ViewModelDictionary = new Dictionary<string, BaseViewModelData>();
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
            
            var viewModelData = CreateViewModelData(workerTask);

            var markdownLiquidViewModel = new MarkdownLiquidViewModel(viewModelData, GenerationInfo, GaiusConfiguration);

            var layoutId = !string.IsNullOrEmpty(workerTask.LayoutId) ? workerTask.LayoutId : _defaultLayoutId;
            if (!LayoutDataDictionary.TryGetValue(layoutId, out var layoutData))
                throw new Exception($"Unable to find layout: {layoutId}");

            if(_liquidTemplatePhysicalFileProvider == null)
                _liquidTemplatePhysicalFileProvider = new PhysicalFileProvider(GetLayoutsDirFullPath(GaiusConfiguration));

            if (FluidTemplate.TryParse(layoutData.LayoutContent, out var liquidTemplate))
            {
                var context = new TemplateContext();
                context.FileProvider = _liquidTemplatePhysicalFileProvider;
                context.MemberAccessStrategy.Register<MarkdownLiquidViewModel>();
                context.MemberAccessStrategy.Register<MarkdownLiquidViewModel_Page>();
                context.MemberAccessStrategy.Register<MarkdownLiquidViewModel_Paginator>();
                context.MemberAccessStrategy.Register<MarkdownLiquidViewModel_Site>();
                context.MemberAccessStrategy.Register<MarkdownLiquidViewModel_GaiusInfo>();
                context.Model = markdownLiquidViewModel;
                return liquidTemplate.Render(context);
            }

            return viewModelData.Content;
        }
        public override string GetTarget(FileSystemInfo fileSystemInfo, int page)
        {
            var targetName = base.GetTarget(fileSystemInfo, 1);

            if(fileSystemInfo.IsMarkdownFile())
            {
                return page > 1 
                    ? $"{Path.GetFileNameWithoutExtension(fileSystemInfo.Name)}{page}.html"
                    : $"{Path.GetFileNameWithoutExtension(fileSystemInfo.Name)}.html";
            }
                
            return targetName;
        }

        public override WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo)
        {
            return CreateWorkerTaskInternal(fileSystemInfo, 1);
        }

        public override void AddPaginatorDataToWorkerTask(WorkerTask workerTask, PaginatorData paginatorData, List<WorkerTask> paginatorWorkerTasks)
        {
            AddPrevAndNextUrlsToPaginatorData(paginatorData, workerTask.FileSystemInfo);
            workerTask.AddPaginatorData(paginatorData);
            workerTask.AddPaginatorWorkerTasks(paginatorWorkerTasks);
        }

        public override WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo, PaginatorData paginatorData, List<WorkerTask> paginatorWorkerTasks)
        {
            var workerTask = CreateWorkerTaskInternal(fileSystemInfo, paginatorData.PageNumber);

            AddPrevAndNextUrlsToPaginatorData(paginatorData, fileSystemInfo);
            workerTask.AddPaginatorData(paginatorData);
            workerTask.AddPaginatorWorkerTasks(paginatorWorkerTasks);

            return workerTask;
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

        private WorkerTask CreateWorkerTaskInternal(FileSystemInfo fileSystemInfo, int page)
        {
            IFrontMatter frontMatter = null;
            IWorkerLayoutData layoutInfo = null;
            var workType = GetWorkType(fileSystemInfo);
            var isPost = GetIsPost(fileSystemInfo);
            var shouldSkip = GetShouldSkip(fileSystemInfo);
            var shouldKeep = GetShouldKeep(fileSystemInfo);
            var targetFSName = GetTarget(fileSystemInfo, page);
            (var targetUrl, var targetId) = GetTargetUrlAndId(fileSystemInfo, targetFSName);

            if (fileSystemInfo.IsMarkdownFile())
            {
                frontMatter = _frontMatterParser.DeserializeFromContent(File.ReadAllText(fileSystemInfo.FullName));

                if (!LayoutDataDictionary.TryGetValue(frontMatter.Layout, out layoutInfo)
                    && !LayoutDataDictionary.TryGetValue(_defaultLayoutId, out layoutInfo))
                    throw new Exception($"Unable to find layout information for: {frontMatter.Layout} or {_defaultLayoutId}");
            }

            return new WorkerTask(fileSystemInfo, layoutInfo, frontMatter, workType, isPost, shouldSkip, shouldKeep, targetFSName, targetUrl, targetId);
        }
        private void AddPrevAndNextUrlsToPaginatorData(PaginatorData paginatorData, FileSystemInfo fileSystemInfo)
        {
            string prevUrl = null;
            string nextUrl = null;

            if (paginatorData.HasPrev)
            {
                var targetPrev = GetTarget(fileSystemInfo, paginatorData.PageNumber - 1);
                (var targetPrevUrl, var targetPrevId) = GetTargetUrlAndId(fileSystemInfo, targetPrev);
                prevUrl = targetPrevUrl;
            }

            if (paginatorData.HasNext)
            {
                var targetNext = GetTarget(fileSystemInfo, paginatorData.PageNumber + 1);
                (var targetNextUrl, var targetNextId) = GetTargetUrlAndId(fileSystemInfo, targetNext);
                nextUrl = targetNextUrl;
            }

            paginatorData.AddPrevAndNextUrls(prevUrl, nextUrl);
        }
        private ViewModelData CreateViewModelData(WorkerTask workerTask)
        {
            var baseViewModel = CreateBaseViewModelData(workerTask);

            if(workerTask.HasPaginatorData && workerTask.HasPaginatorWorkerTasks)
            {
                var paginatorViewModels = new List<BaseViewModelData>();

                foreach(var paginatorWorkerTask in workerTask.GetPaginatorWorkerTasks())
                {
                    var paginatorViewModel = CreateBaseViewModelData(paginatorWorkerTask);
                    paginatorViewModels.Add(paginatorViewModel);
                }

                return new ViewModelData(workerTask, baseViewModel.Content, workerTask.GetPaginatorData(), paginatorViewModels);
            }
                
            return new ViewModelData(workerTask, baseViewModel.Content);
        }
        private BaseViewModelData CreateBaseViewModelData(WorkerTask workerTask)
        {
            if(ViewModelDictionary.TryGetValue(workerTask.TargetId, out var baseViewModel))
                return baseViewModel;

            var markdownFile = workerTask.FileInfo;
            var markdownContent = MarkdownPreProcess(File.ReadAllText(markdownFile.FullName));
            var html = Markdown.ToHtml(markdownContent, _markdownPipeline);
            baseViewModel = new BaseViewModelData(workerTask, html);

            ViewModelDictionary.Add(workerTask.TargetId, baseViewModel);
            return baseViewModel;
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

            //rs: special handling for IDs and URLs for posts
            var indexOfPostsSegment = pathSegmentsToKeep.IndexOf(GaiusConfiguration.PostsDirectoryName);
            if(indexOfPostsSegment > -1)
                pathSegmentsToKeep[indexOfPostsSegment] = GaiusConfiguration.PostsDirectoryName.TrimStart('_');

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

                var layoutInfo = new MarkdownLiquidLayoutData(file);
                LayoutDataDictionary.Add(layoutInfo.Id, layoutInfo);
            }
        }
    }
}