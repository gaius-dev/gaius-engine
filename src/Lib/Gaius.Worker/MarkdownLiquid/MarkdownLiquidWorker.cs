using System;
using System.IO;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Fluid;
using Gaius.Core.Configuration;
using Markdig;
using Gaius.Core.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Gaius.Worker.Parsing.Yaml;
using System.Collections.Generic;
using Gaius.Worker.Models;
using System.Text.RegularExpressions;
using System.Linq;
using Gaius.Worker.Parsing;

namespace Gaius.Worker.MarkdownLiquid
{
    public class MarkdownLiquidWorker : BaseWorker, IWorker
    {
        private const string _layoutsDirectory = "_layouts";
        private const string _defaultLayoutId = "default";
        private const string _dateTimePrefixRegExStr = @"\d{4}-\d{2}-\d{2}-";
        private static Regex _dateTimePrefixRegEx = new Regex(_dateTimePrefixRegExStr, RegexOptions.Compiled);
        private const string _siteUrlRegExStr = @"{{ *site.url *}}";
        private static Regex _siteUrlRegEx = new Regex(_siteUrlRegExStr, RegexOptions.Compiled);
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

        #region worker task creation

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
            IWorkerLayout layout = null;
            IFrontMatter frontMatter = null;
            var taskFlags = GetTaskFlags(fileSystemInfo);
            var workType = GetWorkType(fileSystemInfo, taskFlags);
            var sourceDisplay = GetSourceDisplay(fileSystemInfo, taskFlags);
            (frontMatter, layout) = GetFrontMatterAndLayout(fileSystemInfo, taskFlags);
            (var taskPathSegments, var outputDisplay, var generationUrl, var generationId) = GetAdditionalTaskParameters(fileSystemInfo, taskFlags, pageNumber);

            return new WorkerTask()
            {
                FileSystemInfo = fileSystemInfo,
                Layout = layout,
                FrontMatter = frontMatter,
                WorkType = workType,
                TaskFlags = taskFlags,
                TaskPathSegments = taskPathSegments,
                GenerationUrl = generationUrl,
                GenerationId = generationId,
                SourceDisplay = sourceDisplay,
                OutputDisplay = outputDisplay,
            };
        }
        
        private WorkerTaskFlags GetTaskFlags(FileSystemInfo fileSystemInfo)
        {
            var taskFlags = WorkerTaskFlags.None;

            bool isDirectory = fileSystemInfo.IsDirectory();

            //rs: handle task flags for special directories
            if(isDirectory)
            {
                if(fileSystemInfo.FullName.Equals(GaiusConfiguration.SiteContainerFullPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    taskFlags = taskFlags | WorkerTaskFlags.IsSiteContainerDir;
                    return taskFlags;
                }

                if(fileSystemInfo.FullName.Equals(GaiusConfiguration.SourceDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    taskFlags = taskFlags | WorkerTaskFlags.IsSourceDir;
                    return taskFlags;
                }

                if(fileSystemInfo.FullName.Equals(GaiusConfiguration.NamedThemeDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    taskFlags = taskFlags | WorkerTaskFlags.IsNamedThemeDir;
                    return taskFlags;
                }

                if(fileSystemInfo.FullName.Equals(GaiusConfiguration.PostsDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    taskFlags = taskFlags | WorkerTaskFlags.IsChildOfSourceDir;
                    taskFlags = taskFlags | WorkerTaskFlags.IsSkip;
                    taskFlags = taskFlags | WorkerTaskFlags.IsPostsDir;
                    return taskFlags;
                }

                if(fileSystemInfo.FullName.Equals(GaiusConfiguration.DraftsDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    taskFlags = taskFlags | WorkerTaskFlags.IsChildOfSourceDir;
                    taskFlags = taskFlags | WorkerTaskFlags.IsSkip;
                    taskFlags = taskFlags | WorkerTaskFlags.IsDraftsDir;
                    return taskFlags;
                }
            }

            if(GetIsChildOfSourceDir(fileSystemInfo))
                taskFlags = taskFlags | WorkerTaskFlags.IsChildOfSourceDir;

            else if(GetIsChildOfNamedThemeDir(fileSystemInfo))
                taskFlags = taskFlags | WorkerTaskFlags.IsChildOfNamedThemeDir;

            else if(GetIsChildOfGenDir(fileSystemInfo))
                taskFlags = taskFlags | WorkerTaskFlags.IsChildOfGenDir;

            if(GetIsPost(fileSystemInfo, taskFlags))
                taskFlags = taskFlags | WorkerTaskFlags.IsPost;

            else if(GetIsDraft(fileSystemInfo, taskFlags))
                taskFlags = taskFlags | WorkerTaskFlags.IsDraft;

            if(GetIsSkip(fileSystemInfo, taskFlags))
                taskFlags = taskFlags | WorkerTaskFlags.IsSkip;

            else if(GetIsKeep(fileSystemInfo, taskFlags))
                taskFlags = taskFlags | WorkerTaskFlags.IsKeep;

            else if(GetIsInvalid(fileSystemInfo, taskFlags))
            {
                taskFlags = taskFlags | WorkerTaskFlags.IsSkip;
                taskFlags = taskFlags | WorkerTaskFlags.IsInvalid;
            }

            return taskFlags;
        }
        private bool GetIsChildOfSourceDir(FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo.GetPathSegments().Contains(GaiusConfiguration.SourceDirectoryName);
        }
        private bool GetIsChildOfNamedThemeDir(FileSystemInfo fileSystemInfo)
        {
            var pathSegments = fileSystemInfo.GetPathSegments();
            return pathSegments.Contains(GaiusConfiguration.ThemesDirectoryName)
                    && pathSegments.Contains(GaiusConfiguration.ThemeName);
        }
        private bool GetIsChildOfGenDir(FileSystemInfo fileSystemInfo)
        {
            return fileSystemInfo.GetPathSegments().Contains(GaiusConfiguration.GenerationDirectoryName);
        }
        private bool GetIsPost(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags)
        {
            return taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir)
                    && fileSystemInfo.IsMarkdownFile()
                    && fileSystemInfo.GetParentDirectory().Name.Equals(GaiusConfiguration.PostsDirectoryName);
        }
        private bool GetIsDraft(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags)
        {
            return taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir)
                    && fileSystemInfo.IsMarkdownFile()
                    && fileSystemInfo.GetParentDirectory().Name.Equals(GaiusConfiguration.DraftsDirectoryName);
        }
        private bool GetIsSkip(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags)
        {
            if(!taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir)
                && !taskFlags.HasFlag(WorkerTaskFlags.IsChildOfNamedThemeDir))
                return false;

            if(fileSystemInfo.Name.StartsWith("."))
                return true;

            if(fileSystemInfo.IsDirectory() && fileSystemInfo.Name.Equals(_layoutsDirectory, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if(fileSystemInfo.IsLiquidFile())
                return true;

            return false;
        }
        private bool GetIsKeep(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags)
        {
            return taskFlags.HasFlag(WorkerTaskFlags.IsChildOfGenDir)
                    && GaiusConfiguration.AlwaysKeep.Any(alwaysKeep => alwaysKeep.Equals(fileSystemInfo.Name, StringComparison.InvariantCultureIgnoreCase));
        }
        private bool GetIsInvalid(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags)
        {
            if((taskFlags.HasFlag(WorkerTaskFlags.IsPost) || taskFlags.HasFlag(WorkerTaskFlags.IsDraft))
                && !_dateTimePrefixRegEx.IsMatch(fileSystemInfo.Name))
                    return true;

            return false;
        }
        private static WorkType GetWorkType(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags)
        {
            if(!taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir))
                return WorkType.None;

            if(fileSystemInfo.IsMarkdownFile())
                return WorkType.Transform;

            return WorkType.None;
        }
        private string GetSourceDisplay(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags)
        {
            //rs: override the operation name for the named theme directory (this is used when displaying the operation)
            if (taskFlags.HasFlag(WorkerTaskFlags.IsNamedThemeDir))
                return $"{GaiusConfiguration.ThemesDirectoryName}/{fileSystemInfo.Name}";

            return fileSystemInfo.Name;
        }
        private (IFrontMatter, IWorkerLayout) GetFrontMatterAndLayout(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags)
        {
            if(taskFlags.HasFlag(WorkerTaskFlags.IsInvalid))
                return (null, null);

            if(!taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir))
                return (null, null);

            IFrontMatter frontMatter;
            IWorkerLayout layout;

            if (fileSystemInfo.IsMarkdownFile())
            {
                frontMatter = _frontMatterParser.DeserializeFromContent(File.ReadAllText(fileSystemInfo.FullName));

                if (!LayoutDataDictionary.TryGetValue(frontMatter.Layout, out layout)
                    && !LayoutDataDictionary.TryGetValue(_defaultLayoutId, out layout))
                    throw new Exception($"Unable to find layout information for: {frontMatter.Layout} or {_defaultLayoutId}");
                
                return (frontMatter, layout);
            }

            return (null, null);
        }
        private (List<string>, string, string, string) GetAdditionalTaskParameters(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags, int pageNumber = 1)
        {
            var siteContainerDirectoryInfo = new DirectoryInfo(GaiusConfiguration.SiteContainerFullPath);
            var genDirectoryInfo = new DirectoryInfo(GaiusConfiguration.GenerationDirectoryFullPath);

            if(taskFlags.HasFlag(WorkerTaskFlags.IsSiteContainerDir))
                return (siteContainerDirectoryInfo.GetPathSegments(), null, null, null);
            
            if(taskFlags.HasFlag(WorkerTaskFlags.IsSourceDir) 
                || taskFlags.HasFlag(WorkerTaskFlags.IsNamedThemeDir))
                return (genDirectoryInfo.GetPathSegments(), GaiusConfiguration.GenerationDirectoryName, null, null);

            if(taskFlags.HasFlag(WorkerTaskFlags.IsPostsDir))
                return (genDirectoryInfo.GetPathSegments(), "yyyy/MM/dd", null, null);

            if(taskFlags.HasFlag(WorkerTaskFlags.IsDraftsDir))
                return (genDirectoryInfo.GetPathSegments(), "(drafts) yyyy/MM/dd", null, null);

            if(taskFlags.HasFlag(WorkerTaskFlags.IsInvalid))
                return (genDirectoryInfo.GetPathSegments(), null, null, null);

            if(taskFlags.HasFlag(WorkerTaskFlags.IsChildOfGenDir))
                return (fileSystemInfo.GetPathSegments(), fileSystemInfo.Name, null, null);

            var rawTaskPathSegments = fileSystemInfo.GetPathSegments();

            var skipAmt = -1;
            skipAmt = skipAmt > -1 ? skipAmt : GetSkipAmtForChildOfSourceDirectory(rawTaskPathSegments);
            skipAmt = skipAmt > -1 ? skipAmt : GetSkipAmtForChildOfNamedThemeDirectory(rawTaskPathSegments);
            skipAmt++;
            
            if(skipAmt > rawTaskPathSegments.Count)
                throw new Exception($"Unable to calculate additional task parameters for {fileSystemInfo.FullName}.");

            var relativeTaskPathSegments = rawTaskPathSegments.Skip(skipAmt).ToList();

            var taskFileOrDirectoryName = GetTaskFileOrDirectoryName(fileSystemInfo, taskFlags, pageNumber);
            var outputDisplay = taskFileOrDirectoryName;

            if(taskFlags.HasFlag(WorkerTaskFlags.IsPost) || taskFlags.HasFlag(WorkerTaskFlags.IsDraft))
                (relativeTaskPathSegments, outputDisplay)
                    = UpdateRelativePathAndOutputDisplayForPostOrDraft(fileSystemInfo, relativeTaskPathSegments, outputDisplay);

            relativeTaskPathSegments[relativeTaskPathSegments.Count - 1] = taskFileOrDirectoryName;

            var taskPathSegments = genDirectoryInfo.GetPathSegments().Concat(relativeTaskPathSegments).ToList();

            var generationUrl = string.Empty;
            var generationId = string.Empty;

            if(fileSystemInfo.IsFile())
            {
                generationUrl = GetGenerationUrlFromPath(relativeTaskPathSegments);
                generationId = GetGenerationIdFromPath(relativeTaskPathSegments);
            }

            return (taskPathSegments, outputDisplay, generationUrl, generationId);
        }
        private string GetTaskFileOrDirectoryName(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags, int pageNumber)
        {
            if(taskFlags.HasFlag(WorkerTaskFlags.IsPost))
                return $"{_dateTimePrefixRegEx.Replace(Path.GetFileNameWithoutExtension(fileSystemInfo.Name), string.Empty)}.html";

            else if(taskFlags.HasFlag(WorkerTaskFlags.IsDraft))
                return $"{_dateTimePrefixRegEx.Replace(Path.GetFileNameWithoutExtension(fileSystemInfo.Name), string.Empty)}-draft.html";

            if(!fileSystemInfo.IsMarkdownFile())
                return fileSystemInfo.Name;

            return pageNumber > 1 
                ? $"{Path.GetFileNameWithoutExtension(fileSystemInfo.Name)}{pageNumber}.html"
                : $"{Path.GetFileNameWithoutExtension(fileSystemInfo.Name)}.html";
        }
        private int GetSkipAmtForChildOfSourceDirectory(List<string> pathSegments)
            => pathSegments.IndexOf(GaiusConfiguration.SourceDirectoryName);

        private int GetSkipAmtForChildOfNamedThemeDirectory(List<string> pathSegments)
        {
            var indexOfThemesDir = pathSegments.IndexOf(GaiusConfiguration.ThemesDirectoryName);
            var indexOfNamedThemeDir = pathSegments.IndexOf(GaiusConfiguration.ThemeName);

            if(indexOfThemesDir > -1 && indexOfNamedThemeDir > -1 && (indexOfNamedThemeDir == indexOfThemesDir + 1))
                return indexOfNamedThemeDir;

            return -1;
        }
        private (List<string>, string) UpdateRelativePathAndOutputDisplayForPostOrDraft(FileSystemInfo fileSystemInfo, List<string> relativeTaskPathSegments, string outputDisplay)
        {
            var dateTimePrefix = _dateTimePrefixRegEx.Match(fileSystemInfo.Name).Value;
            var dateTimePathSegments = dateTimePrefix.Split('-', StringSplitOptions.RemoveEmptyEntries).ToList();

            if(relativeTaskPathSegments.Contains(GaiusConfiguration.PostsDirectoryName))
                relativeTaskPathSegments.Remove(GaiusConfiguration.PostsDirectoryName);

            else if(relativeTaskPathSegments.Contains(GaiusConfiguration.DraftsDirectoryName))
                relativeTaskPathSegments.Remove(GaiusConfiguration.DraftsDirectoryName);

            relativeTaskPathSegments = dateTimePathSegments.Concat(relativeTaskPathSegments).ToList();
            outputDisplay = string.Join('/', dateTimePathSegments.Concat(new string[] {outputDisplay}));
            return (relativeTaskPathSegments, outputDisplay);
        }
        private string GetGenerationUrlFromPath(List<string> relativeTaskPathSegments)
            => $"{GaiusConfiguration.GetGenerationUrlRootPrefix()}/{string.Join("/", relativeTaskPathSegments)}";
        private static string GetGenerationIdFromPath(List<string> relativeTaskPathSegments)
            => Path.GetFileNameWithoutExtension(string.Join(".", relativeTaskPathSegments));
        private void AddPrevAndNextUrlsToPaginator(Paginator paginator, WorkerTask workerTask)
        {
            string prevUrl = null;
            string nextUrl = null;

            if(!paginator.HasPrev && !paginator.HasNext)
                return;

            var pathToModify = new List<string>(workerTask.TaskPathSegments);

            if (paginator.HasPrev)
            {
                pathToModify[pathToModify.Count - 1] = GetTaskFileOrDirectoryName(workerTask.FileSystemInfo, workerTask.TaskFlags, paginator.PageNumber - 1);
                prevUrl = GetGenerationUrlFromPath(pathToModify);
            }

            if (paginator.HasNext)
            {
                pathToModify[pathToModify.Count - 1] = GetTaskFileOrDirectoryName(workerTask.FileSystemInfo, workerTask.TaskFlags, paginator.PageNumber + 1);
                nextUrl = GetGenerationUrlFromPath(pathToModify);
            }

            paginator.AddPrevAndNextUrls(prevUrl, nextUrl);
        }
        
        #endregion

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
            if(ViewModelDictionary.TryGetValue(workerTask.GenerationId, out var baseViewModel))
                return baseViewModel;

            var markdownFile = workerTask.FileInfo;
            var markdownContent = MarkdownPreProcess(File.ReadAllText(markdownFile.FullName));
            var html = Markdown.ToHtml(markdownContent, _markdownPipeline);
            baseViewModel = new BaseViewModel(workerTask, html);

            ViewModelDictionary.Add(workerTask.GenerationId, baseViewModel);
            return baseViewModel;
        }
        private static string GetLayoutsDirFullPath(GaiusConfiguration gaiusConfiguration)
        {
            return Path.Combine(gaiusConfiguration.NamedThemeDirectoryFullPath, _layoutsDirectory);
        }
        private string MarkdownPreProcess(string markdownContent)
        {
            return GenerationUrlRootPrefixPreProcessor(markdownContent);
        }
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