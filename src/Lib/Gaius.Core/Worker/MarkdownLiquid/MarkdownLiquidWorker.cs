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
        private readonly Dictionary<string, IWorkerLayout> LayoutDataDictionary = new Dictionary<string, IWorkerLayout>();
        private readonly Dictionary<string, BaseViewModel> ViewModelDictionary = new Dictionary<string, BaseViewModel>();
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
            
            var viewModelData = CreateViewModel(workerTask);

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
        public override WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo)
        {
            return CreateWorkerTaskInternal(fileSystemInfo, 1);
        }
        public override void AddPaginatorDataToWorkerTask(WorkerTask workerTask, Paginator paginator, List<WorkerTask> paginatorWorkerTasks)
        {
            AddPrevAndNextUrlsToPaginator(paginator, workerTask);
            workerTask.Paginator = paginator;
            workerTask.PaginatorWorkerTasks = paginatorWorkerTasks;
        }
        public override WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo, Paginator paginator, List<WorkerTask> paginatorWorkerTasks)
        {
            var workerTask = CreateWorkerTaskInternal(fileSystemInfo, paginator.PageNumber);

            AddPrevAndNextUrlsToPaginator(paginator, workerTask);
            workerTask.Paginator = paginator;
            workerTask.PaginatorWorkerTasks = paginatorWorkerTasks;

            return workerTask;
        }

        private WorkerTask CreateWorkerTaskInternal(FileSystemInfo fileSystemInfo, int pageNumber)
        {
            IFrontMatter frontMatter = null;
            IWorkerLayout layout = null;
            (var targetPath, var targetDisplayName, var targetUrl, var targetId) = GetTargets(fileSystemInfo, pageNumber);

            if (fileSystemInfo.IsMarkdownFile())
            {
                frontMatter = _frontMatterParser.DeserializeFromContent(File.ReadAllText(fileSystemInfo.FullName));

                if (!LayoutDataDictionary.TryGetValue(frontMatter.Layout, out layout)
                    && !LayoutDataDictionary.TryGetValue(_defaultLayoutId, out layout))
                    throw new Exception($"Unable to find layout information for: {frontMatter.Layout} or {_defaultLayoutId}");
            }

            return new WorkerTask()
            {
                FileSystemInfo = fileSystemInfo,
                Layout = layout,
                FrontMatter = frontMatter,
                WorkType = GetWorkType(fileSystemInfo),
                IsPost = GetIsPost(fileSystemInfo),
                IsDraft = GetIsDraft(fileSystemInfo),
                IsSkip = GetIsSkip(fileSystemInfo),
                IsKeep = GetIsKeep(fileSystemInfo),
                TargetPathSegments = targetPath,
                TargetUrl = targetUrl,
                TargetId = targetId,
                SourceDisplayName = GetSourceDisplayName(fileSystemInfo),
                TargetDisplayName = targetDisplayName,
            };
        }
        private bool GetIsKeep(FileSystemInfo fileSystemInfo) =>
            fileSystemInfo.GetPathSegments().Contains(GaiusConfiguration.GenerationDirectoryName)
            && GaiusConfiguration.AlwaysKeep.Any(alwaysKeep => alwaysKeep.Equals(fileSystemInfo.Name, StringComparison.InvariantCultureIgnoreCase));

        private bool GetIsSkip(FileSystemInfo fileSystemInfo)
        {
            if(fileSystemInfo.Name.StartsWith("."))
                return true;

            if(fileSystemInfo.IsDirectory())
                if(fileSystemInfo.Name.Equals(_layoutsDirectory, StringComparison.InvariantCultureIgnoreCase)
                    || fileSystemInfo.Name.Equals(GaiusConfiguration.PostsDirectoryName, StringComparison.InvariantCultureIgnoreCase)
                    || fileSystemInfo.Name.Equals(GaiusConfiguration.DraftsDirectoryName, StringComparison.InvariantCultureIgnoreCase))
                    return true;

            if(fileSystemInfo.IsLiquidFile())
                return true;

            return false;
        }
        private bool GetIsPost(FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo.IsMarkdownFile() && fileSystemInfo.GetParentDirectory().Name.Equals(GaiusConfiguration.PostsDirectoryName);
        }
        private bool GetIsDraft(FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo.IsMarkdownFile() && fileSystemInfo.GetParentDirectory().Name.Equals(GaiusConfiguration.DraftsDirectoryName);
        }
        private void AddPrevAndNextUrlsToPaginator(Paginator paginator, WorkerTask workerTask)
        {
            string prevUrl = null;
            string nextUrl = null;

            if(!paginator.HasPrev && !paginator.HasNext)
                return;

            var pathToModify = new List<string>(workerTask.TargetPathSegments);

            if (paginator.HasPrev)
            {
                pathToModify[pathToModify.Count - 1] = GetTargetFileOrDirectoryName(workerTask.FileSystemInfo, paginator.PageNumber - 1);
                prevUrl = GetTargetUrlFromPath(pathToModify);
            }

            if (paginator.HasNext)
            {
                pathToModify[pathToModify.Count - 1] = GetTargetFileOrDirectoryName(workerTask.FileSystemInfo, paginator.PageNumber + 1);
                nextUrl = GetTargetUrlFromPath(pathToModify);
            }

            paginator.AddPrevAndNextUrls(prevUrl, nextUrl);
        }
        private ViewModel CreateViewModel(WorkerTask workerTask)
        {
            var baseViewModel = CreateBaseViewModel(workerTask);

            if(workerTask.HasPaginatorData)
            {
                var paginatorViewModels = new List<BaseViewModel>();

                foreach(var paginatorWorkerTask in workerTask.PaginatorWorkerTasks)
                {
                    var paginatorViewModel = CreateBaseViewModel(paginatorWorkerTask);
                    paginatorViewModels.Add(paginatorViewModel);
                }

                return new ViewModel(workerTask, baseViewModel.Content, workerTask.Paginator, paginatorViewModels);
            }
                
            return new ViewModel(workerTask, baseViewModel.Content);
        }
        private BaseViewModel CreateBaseViewModel(WorkerTask workerTask)
        {
            if(ViewModelDictionary.TryGetValue(workerTask.TargetId, out var baseViewModel))
                return baseViewModel;

            var markdownFile = workerTask.FileInfo;
            var markdownContent = MarkdownPreProcess(File.ReadAllText(markdownFile.FullName));
            var html = Markdown.ToHtml(markdownContent, _markdownPipeline);
            baseViewModel = new BaseViewModel(workerTask, html);

            ViewModelDictionary.Add(workerTask.TargetId, baseViewModel);
            return baseViewModel;
        }
        private string GetSourceDisplayName(FileSystemInfo fileSystemInfo)
        {
            //rs: override the operation name for the named theme directory (this is used when displaying the operation)
            if (fileSystemInfo.IsDirectory() && fileSystemInfo.FullName.Equals(GaiusConfiguration.NamedThemeDirectoryFullPath))
                return $"{GaiusConfiguration.ThemesDirectoryName}/{fileSystemInfo.Name}";

            return fileSystemInfo.Name;
        }
        private const string _dateTimePrefixRegExStr = @"\d{4}-\d{2}-\d{2}-";
        private static Regex _dateTimePrefixRegEx = new Regex(_dateTimePrefixRegExStr, RegexOptions.Compiled);
        private (List<string>, string, string, string) GetTargets(FileSystemInfo fileSystemInfo, int pageNumber = 1)
        {
            var siteContainerDirectoryInfo = new DirectoryInfo(GaiusConfiguration.SiteContainerFullPath);
            var genDirectoryInfo = new DirectoryInfo(GaiusConfiguration.GenerationDirectoryFullPath);

            if (fileSystemInfo.IsDirectory())
            {
                if(fileSystemInfo.FullName.Equals(GaiusConfiguration.SiteContainerFullPath, StringComparison.InvariantCultureIgnoreCase))
                    return (siteContainerDirectoryInfo.GetPathSegments(), null, null, null);

                if(fileSystemInfo.FullName.Equals(GaiusConfiguration.PostsDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase))
                    return (genDirectoryInfo.GetPathSegments(), "yyyy/MM/dd", null, null);

                if(fileSystemInfo.FullName.Equals(GaiusConfiguration.SourceDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase)
                    || fileSystemInfo.FullName.Equals(GaiusConfiguration.NamedThemeDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase)
                    || fileSystemInfo.FullName.Equals(GaiusConfiguration.PostsDirectoryName, StringComparison.InvariantCultureIgnoreCase)
                    || fileSystemInfo.FullName.Equals(GaiusConfiguration.DraftsDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase))
                    return (genDirectoryInfo.GetPathSegments(), GaiusConfiguration.GenerationDirectoryName, null, null);
            }

            var rawPath = fileSystemInfo.GetPathSegments();

            var skipAmt = -1;
            skipAmt = skipAmt > -1 ? skipAmt : GetSkipAmtForChildOfSourceDirectory(rawPath);
            skipAmt = skipAmt > -1 ? skipAmt : GetSkipAmtForChildOfNamedThemeDirectory(rawPath);

            //rs: we're dealing with a file / directory that's in the generation directory
            // don't make any adjustments or calculate URL and ID
            if(skipAmt == -1)
                return (rawPath, fileSystemInfo.Name, null, null);

            skipAmt++;
            
            if(skipAmt > rawPath.Count)
                throw new Exception($"Unable to calculate targets for {fileSystemInfo.FullName}.");

            var relativePath = rawPath.Skip(skipAmt).ToList();

            var targetFileOrDirectoryName = GetTargetFileOrDirectoryName(fileSystemInfo, pageNumber);
            var targetDisplayName = targetFileOrDirectoryName;

            if(GetIsPost(fileSystemInfo))
            {
                var dateTimePrefix = _dateTimePrefixRegEx.Match(fileSystemInfo.Name).Value;
                var dateTimePathSegments = dateTimePrefix.Split('-', StringSplitOptions.RemoveEmptyEntries).ToList();
                relativePath.Remove(GaiusConfiguration.PostsDirectoryName);
                relativePath = dateTimePathSegments.Concat(relativePath).ToList();
                targetDisplayName = string.Join('/', dateTimePathSegments.Concat(new string[] {targetDisplayName}));
            }

            relativePath[relativePath.Count - 1] = targetFileOrDirectoryName;

            var targetPath = genDirectoryInfo.GetPathSegments().Concat(relativePath).ToList();

            var targetUrl = string.Empty;
            var targetId = string.Empty;

            if(fileSystemInfo.IsFile())
            {
                targetUrl = GetTargetUrlFromPath(relativePath);
                targetId = Path.GetFileNameWithoutExtension(string.Join(".", relativePath));
            }

            return (targetPath, targetDisplayName, targetUrl, targetId);
        }

        private string GetTargetFileOrDirectoryName(FileSystemInfo fileSystemInfo, int pageNumber)
        {
            if(GetIsPost(fileSystemInfo))
                return $"{_dateTimePrefixRegEx.Replace(Path.GetFileNameWithoutExtension(fileSystemInfo.Name), string.Empty)}.html";

            if(fileSystemInfo.IsMarkdownFile())
            {
                return pageNumber > 1 
                    ? $"{Path.GetFileNameWithoutExtension(fileSystemInfo.Name)}{pageNumber}.html"
                    : $"{Path.GetFileNameWithoutExtension(fileSystemInfo.Name)}.html";
            }

            return fileSystemInfo.Name;
        }
        private string GetTargetUrlFromPath(List<string> targetPath) => $"{GaiusConfiguration.GetGenerationUrlRootPrefix()}/{string.Join("/", targetPath)}";
        private int GetSkipAmtForChildOfSourceDirectory(List<string> pathSegments)
        {
            return pathSegments.IndexOf(GaiusConfiguration.SourceDirectoryName);
        }
        private int GetSkipAmtForChildOfNamedThemeDirectory(List<string> pathSegments)
        {
            var indexOfThemesDir = pathSegments.IndexOf(GaiusConfiguration.ThemesDirectoryName);
            var indexOfNamedThemeDir = pathSegments.IndexOf(GaiusConfiguration.ThemeName);

            if(indexOfThemesDir > -1 && indexOfNamedThemeDir > -1 && (indexOfNamedThemeDir == indexOfThemesDir + 1))
                return indexOfNamedThemeDir;

            return -1;
        }
        private static WorkType GetWorkType(FileSystemInfo fileSystemInfo)
        {
            if (fileSystemInfo.IsMarkdownFile())
                return WorkType.Transform;

            return WorkType.Copy;
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

                var layoutInfo = new MarkdownLiquidLayout(file);
                LayoutDataDictionary.Add(layoutInfo.Id, layoutInfo);
            }
        }
    }
}