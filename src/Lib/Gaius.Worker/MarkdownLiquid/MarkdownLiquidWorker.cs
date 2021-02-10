using System;
using System.IO;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Fluid;
using Gaius.Core.Configuration;
using Markdig;
using Gaius.Core.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Gaius.Worker.FrontMatter.Yaml;
using System.Collections.Generic;
using Gaius.Worker.Models;
using System.Text.RegularExpressions;
using System.Linq;
using Gaius.Worker.FrontMatter;

namespace Gaius.Worker.MarkdownLiquid
{
    public class MarkdownLiquidWorker : BaseWorker, IWorker
    {
        private const string _layoutsDirectory = "_layouts";
        private const string _defaultLayoutId = "default";
        private const string _datePrefixRegExStr = @"\d{4}-\d{2}-\d{2}";
        private static Regex _datePrefixRegEx = new Regex(_datePrefixRegExStr, RegexOptions.Compiled);
        private static DateTime _now = DateTime.UtcNow;
        private const string _siteUrlRegExStr = @"{{ *site.url *}}";
        private static Regex _siteUrlRegEx = new Regex(_siteUrlRegExStr, RegexOptions.Compiled);
        private static string _fileDirNameSanitizeRegExStr = @"[^A-Za-z0-9-]";
        private static Regex _fileDirNameSanitizeRegEx = new Regex(_fileDirNameSanitizeRegExStr, RegexOptions.Compiled);
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
            SiteData = new SiteData(GaiusConfiguration);
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

            var markdownLiquidViewModel = new MarkdownLiquidViewModel(viewModelData, SiteData, GaiusInformation);

            var layoutId = workerTask.LayoutId;
            if (string.IsNullOrEmpty(layoutId) || !LayoutDataDictionary.TryGetValue(layoutId, out var layoutData))
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
                context.MemberAccessStrategy.Register<MarkdownLiquidViewModel_Tag>();
                context.MemberAccessStrategy.Register<MarkdownLiquidViewModel_GaiusInfo>();
                context.Model = markdownLiquidViewModel;
                return liquidTemplate.Render(context);
            }

            return viewModelData.Content;
        }

        #region worker task creation

        public override WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo)
        {
            return CreateWorkerTaskInternal(fileSystemInfo, null);
        }

        public override void AddTagDataToWorker(List<TagData> tagData, string tagListPageFileName = null)
        {
            if(!string.IsNullOrEmpty(tagListPageFileName))
            {
                foreach(var td in tagData)
                {
                    var tagUrl = $"{GaiusConfiguration.GetGenerationUrlRootPrefix()}/{GaiusConfiguration.TagUrlPrefix}/{GetSanitizedName(td.Name)}/{tagListPageFileName}";
                    td.SetTagUrl(tagUrl);
                }
            }
            
            SiteData.SetTagData(tagData);
        }
        public override WorkerTask CreateWorkerTask(FileSystemInfo fileSystemInfo, Paginator paginator, List<WorkerTask> paginatorWorkerTasks)
        {
            var workerTask = CreateWorkerTaskInternal(fileSystemInfo, paginator);

            AddPrevAndNextUrlsToPaginator(paginator, workerTask);
            workerTask.Paginator = paginator;
            workerTask.PaginatorWorkerTasks = paginatorWorkerTasks;

            return workerTask;
        }

        private WorkerTask CreateWorkerTaskInternal(FileSystemInfo fileSystemInfo, Paginator paginator)
        {
            IWorkerLayout layout = null;
            IFrontMatter frontMatter = null;

            var taskFlags = GetTaskFlags(fileSystemInfo);
            var workType = GetWorkType(fileSystemInfo, taskFlags);
            var sourceDisplay = GetSourceDisplay(fileSystemInfo, taskFlags);
            (frontMatter, layout, taskFlags) = GetFrontMatterAndLayout(fileSystemInfo, taskFlags);

            List<string> taskPathSegments = null;
            List<string> relativePathSegments = null;

            var siteContainerDirectoryInfo = new DirectoryInfo(GaiusConfiguration.SiteContainerFullPath);

            if(taskFlags.HasFlag(WorkerTaskFlags.IsSiteContainerDir))
                taskPathSegments = siteContainerDirectoryInfo.GetPathSegments();
            
            else if(taskFlags.HasFlag(WorkerTaskFlags.IsChildOfGenDir))
                taskPathSegments = fileSystemInfo.GetPathSegments();

            else
            {
                var genDirectoryInfo = new DirectoryInfo(GaiusConfiguration.GenerationDirectoryFullPath);
                relativePathSegments = GetRelativeTaskPathSegments(fileSystemInfo, taskFlags, paginator);
                taskPathSegments = genDirectoryInfo.GetPathSegments().Concat(relativePathSegments).ToList();
            }

            var outputDisplay = GetOutputDisplay(taskPathSegments, taskFlags, paginator);
            var generationUrl = GetGenerationUrl(fileSystemInfo, relativePathSegments, taskFlags);
            var generationId = GetGenerationId(fileSystemInfo, relativePathSegments, taskFlags);
            var date = GetDate(fileSystemInfo, taskFlags);

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
                Date = date,
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

                if(fileSystemInfo.FullName.Equals(GaiusConfiguration.TagListDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    taskFlags = taskFlags | WorkerTaskFlags.IsChildOfSourceDir;
                    taskFlags = taskFlags | WorkerTaskFlags.IsSkip;
                    taskFlags = taskFlags | WorkerTaskFlags.IsTagListDir;
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

            else if(GetIsTagList(fileSystemInfo, taskFlags))
                taskFlags = taskFlags | WorkerTaskFlags.IsTagListing;

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
                    && fileSystemInfo.GetParentDirectory().FullName.Equals(GaiusConfiguration.PostsDirectoryFullPath);
        }
        private bool GetIsDraft(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags)
        {
            return taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir)
                    && fileSystemInfo.IsMarkdownFile()
                    && fileSystemInfo.GetParentDirectory().FullName.Equals(GaiusConfiguration.DraftsDirectoryFullPath);
        }
        private bool GetIsTagList(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags)
        {
            return taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir)
                    && fileSystemInfo.IsMarkdownFile()
                    && fileSystemInfo.GetParentDirectory().FullName.Equals(GaiusConfiguration.TagListDirectoryFullPath);
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
                && !_datePrefixRegEx.IsMatch(fileSystemInfo.Name))
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
        private (IFrontMatter, IWorkerLayout, WorkerTaskFlags) GetFrontMatterAndLayout(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags)
        {
            if(taskFlags.HasFlag(WorkerTaskFlags.IsInvalid))
                return (null, null, taskFlags);

            if(!taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir))
                return (null, null, taskFlags);

            if(!fileSystemInfo.IsMarkdownFile())
                return (null, null, taskFlags);

            IFrontMatter frontMatter;
            IWorkerLayout layout;

            frontMatter = _frontMatterParser.DeserializeFromContent(File.ReadAllText(fileSystemInfo.FullName));

            if(frontMatter == null)
            {
                taskFlags = taskFlags | WorkerTaskFlags.IsInvalid;
                return (null, null, taskFlags);
            }

            var layoutIdToLookup = string.IsNullOrEmpty(frontMatter.Layout)
                                    ? _defaultLayoutId 
                                    : frontMatter.Layout;

            if(!LayoutDataDictionary.TryGetValue(layoutIdToLookup, out layout))
            {
                taskFlags = taskFlags | WorkerTaskFlags.IsInvalid;
                return (null, null, taskFlags);
            }

            if(layout.IsPostListing)
                taskFlags = taskFlags | WorkerTaskFlags.IsPostListing;
            
            return (frontMatter, layout, taskFlags);
        }

        private DateTime GetDate(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags)
        {
            if(taskFlags.HasFlag(WorkerTaskFlags.IsPost) || taskFlags.HasFlag(WorkerTaskFlags.IsDraft))
            {
                var dateMatch = _datePrefixRegEx.Match(fileSystemInfo.Name);

                if(!dateMatch.Success)
                    throw new Exception($"Unable to extract date string from {fileSystemInfo.FullName}");

                if(!DateTime.TryParse(dateMatch.Value, out var date))
                    return _now;

                return date;
            }

            if(taskFlags.HasFlag(WorkerTaskFlags.IsPostListing) || taskFlags.HasFlag(WorkerTaskFlags.IsTagListDir))
                return _now;

            if(taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir) && fileSystemInfo.IsMarkdownFile())
                return fileSystemInfo.LastWriteTimeUtc;

            return _now;
        }
        private List<string> GetRelativeTaskPathSegments(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags, Paginator paginator, int pageAdjustment = 0)
        {
            if(taskFlags.HasFlag(WorkerTaskFlags.IsSourceDir) 
                || taskFlags.HasFlag(WorkerTaskFlags.IsNamedThemeDir))
                return new List<string>();

            if(taskFlags.HasFlag(WorkerTaskFlags.IsPostsDir))
                return new List<string>();

            if(taskFlags.HasFlag(WorkerTaskFlags.IsDraftsDir))
                return new List<string>();

            if(taskFlags.HasFlag(WorkerTaskFlags.IsTagListDir))
                return new List<string>();

            if(taskFlags.HasFlag(WorkerTaskFlags.IsInvalid))
                return new List<string>();

            var skipAmt = -1;

            var fullPathSegments = fileSystemInfo.GetPathSegments();

            if(taskFlags.HasFlag(WorkerTaskFlags.IsChildOfNamedThemeDir))
                skipAmt = GetSkipAmtForChildOfNamedThemeDirectory(fullPathSegments);

            else if(taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir))
                skipAmt = GetSkipAmtForChildOfSourceDirectory(fullPathSegments);

            skipAmt++;

            if(skipAmt > fullPathSegments.Count)
                throw new Exception($"Unable to calculate task path segments for {fileSystemInfo.FullName}.");

            var relativePathSegments = fullPathSegments.Skip(skipAmt).ToList();

            if(taskFlags.HasFlag(WorkerTaskFlags.IsChildOfNamedThemeDir))
                return relativePathSegments;

            var pageNumber = paginator?.PageNumber ?? 1;
            pageNumber += pageAdjustment;

            //posts and drafts
            if(taskFlags.HasFlag(WorkerTaskFlags.IsPost) || taskFlags.HasFlag(WorkerTaskFlags.IsDraft))
            {
                var dateTimeMatch = _datePrefixRegEx.Match(fileSystemInfo.Name);

                if(!dateTimeMatch.Success)
                    throw new Exception($"Unable to calculate task path segments for {fileSystemInfo.FullName}.");

                var dateTimePrefix = dateTimeMatch.Value;
                var dateTimePathSegments = dateTimePrefix.Split('-', StringSplitOptions.RemoveEmptyEntries).ToList();

                if(taskFlags.HasFlag(WorkerTaskFlags.IsPost))
                    relativePathSegments.Remove(GaiusConfiguration.PostsDirectoryName);

                else if(taskFlags.HasFlag(WorkerTaskFlags.IsDraft))
                    relativePathSegments.Remove(GaiusConfiguration.DraftsDirectoryName);

                relativePathSegments = dateTimePathSegments.Concat(relativePathSegments).ToList();
                relativePathSegments = SanitizeRelativeTaskPathSegments(fileSystemInfo, relativePathSegments);
                relativePathSegments[relativePathSegments.Count - 1]
                    = GetTaskFileOrDirectoryName(fileSystemInfo, taskFlags, 1);

                return relativePathSegments;
            }
            
            //taglists
            if(taskFlags.HasFlag(WorkerTaskFlags.IsTagListing))
            {
                var tagName = GetSanitizedName(paginator?.AssociatedTagName ?? "temp-tag");
                
                relativePathSegments.Remove(GaiusConfiguration.TagListDirectoryName);
                relativePathSegments.Insert(0, tagName);
                relativePathSegments.Insert(0, GaiusConfiguration.TagUrlPrefix);
                relativePathSegments = SanitizeRelativeTaskPathSegments(fileSystemInfo, relativePathSegments);
                relativePathSegments[relativePathSegments.Count - 1] 
                    = GetTaskFileOrDirectoryName(fileSystemInfo, taskFlags, pageNumber);

                return relativePathSegments;
            }

            //any other children in the _source dir
            relativePathSegments = SanitizeRelativeTaskPathSegments(fileSystemInfo, relativePathSegments);
            relativePathSegments[relativePathSegments.Count - 1] 
                = GetTaskFileOrDirectoryName(fileSystemInfo, taskFlags, pageNumber);

            return relativePathSegments;
        }

        private string GetOutputDisplay(List<string> taskPathSegments, WorkerTaskFlags taskFlags, Paginator paginator)
        {
            if(taskFlags.HasFlag(WorkerTaskFlags.IsSiteContainerDir))
                return null;
            
            if(taskFlags.HasFlag(WorkerTaskFlags.IsSourceDir) 
                || taskFlags.HasFlag(WorkerTaskFlags.IsNamedThemeDir))
                return GaiusConfiguration.GenerationDirectoryName;

            if(taskFlags.HasFlag(WorkerTaskFlags.IsPostsDir))
                return "/yyyy/MM/dd";

            if(taskFlags.HasFlag(WorkerTaskFlags.IsDraftsDir))
                return "/yyyy/MM/dd [drafts]";

            if(taskFlags.HasFlag(WorkerTaskFlags.IsTagListDir))
                return $"/{GaiusConfiguration.TagUrlPrefix}/[tag name]";

            if(taskFlags.HasFlag(WorkerTaskFlags.IsInvalid))
                return null;

            if(taskFlags.HasFlag(WorkerTaskFlags.IsPost) || taskFlags.HasFlag(WorkerTaskFlags.IsDraft))
                return $"/{string.Join('/', taskPathSegments.TakeLast(4))}";

            if(taskFlags.HasFlag(WorkerTaskFlags.IsTagListing))
                return $"/{string.Join('/', taskPathSegments.TakeLast(3))} {paginator?.OutputDisplayLabel}";
            
            if(paginator != null)
                return $"{taskPathSegments.Last()} {paginator.OutputDisplayLabel}";

            return taskPathSegments.Last();
        }

        private string GetGenerationUrl(FileSystemInfo fileSystemInfo, List<string> relativePathSegments, WorkerTaskFlags taskFlags)
        {
            if(relativePathSegments == null)
                return null;

            if(!fileSystemInfo.IsFile() || !taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir))
                return null;

            return GetGenerationUrlFromPath(relativePathSegments);
        }

        private string GetGenerationId(FileSystemInfo fileSystemInfo, List<string> relativePathSegments, WorkerTaskFlags taskFlags)
        {
            if(relativePathSegments == null)
                return null;

            if(!fileSystemInfo.IsFile() || !taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir))
                return null;

            return GetGenerationIdFromPath(relativePathSegments);
        }
        private string GetTaskFileOrDirectoryName(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags, int pageNumber)
        {
            if(taskFlags.HasFlag(WorkerTaskFlags.IsChildOfGenDir))
                throw new ArgumentException($"{nameof(taskFlags)} must contain {nameof(WorkerTaskFlags.IsChildOfSourceDir)} || {nameof(WorkerTaskFlags.IsChildOfNamedThemeDir)}");

            //rs: anything in the named theme directory should not have it's name altered in any way
            if(taskFlags.HasFlag(WorkerTaskFlags.IsChildOfNamedThemeDir))
                return fileSystemInfo.Name;

            //rs: anything below this point would be in the source directory and should therefore have it's name sanitized
            var sanitizedNameWithoutExt
                    = GetSanitizedName(Path.GetFileNameWithoutExtension(fileSystemInfo.Name));

            if(fileSystemInfo.IsMarkdownFile())
            {
                if(taskFlags.HasFlag(WorkerTaskFlags.IsPost))
                    return $"{_datePrefixRegEx.Replace(sanitizedNameWithoutExt, string.Empty).TrimStart('-')}.html";

                if(taskFlags.HasFlag(WorkerTaskFlags.IsDraft))
                    return $"{_datePrefixRegEx.Replace(sanitizedNameWithoutExt, string.Empty).TrimStart('-')}-draft.html";

                return pageNumber > 1 
                    ? $"{sanitizedNameWithoutExt}{pageNumber}.html"
                    : $"{sanitizedNameWithoutExt}.html";
            }
            
            if(fileSystemInfo.IsFile())
                return $"{sanitizedNameWithoutExt}{Path.GetExtension(fileSystemInfo.Name)}";

            return sanitizedNameWithoutExt;
        }
        private string GetSanitizedName(string nameWithoutExtension)
        {
            return _fileDirNameSanitizeRegEx.Replace(nameWithoutExtension, "-").ToLowerInvariant().TrimStart('-');
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
        private List<string> SanitizeRelativeTaskPathSegments(FileSystemInfo fileSystemInfo, List<string> relativePathSegments)
        {
            var sanitizedPathSegments = new List<string>();

            var segmentsToSanitize = fileSystemInfo.IsFile()
                ? relativePathSegments.Count - 1
                : relativePathSegments.Count;

            foreach(var segment in relativePathSegments.Take(segmentsToSanitize))
            {
                sanitizedPathSegments.Add(GetSanitizedName(segment));
            }

            if(fileSystemInfo.IsFile())
                sanitizedPathSegments.Add(relativePathSegments.Last());

            return sanitizedPathSegments;
        }
        private string GetGenerationUrlFromPath(List<string> relativeTaskPathSegments)
            => $"{GaiusConfiguration.GetGenerationUrlRootPrefix()}/{string.Join("/", relativeTaskPathSegments)}";
        private static string GetGenerationIdFromPath(List<string> relativeTaskPathSegments)
            => Path.GetFileNameWithoutExtension(string.Join(".", relativeTaskPathSegments));
        private void AddPrevAndNextUrlsToPaginator(Paginator paginator, WorkerTask workerTask)
        {
            if(!workerTask.TaskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir))
                throw new Exception($"Unable to calculate prev and next URLs {workerTask.FileSystemInfo.FullName}. It is not child of the {GaiusConfiguration.SourceDirectoryName} directory.");

            string prevUrl = null;
            string nextUrl = null;

            if(!paginator.HasPrev && !paginator.HasNext)
                return;

            if (paginator.HasPrev)
            {
                var relativePathSegments = GetRelativeTaskPathSegments(workerTask.FileSystemInfo, workerTask.TaskFlags, paginator, -1);
                prevUrl = GetGenerationUrlFromPath(relativePathSegments);
            }

            if (paginator.HasNext)
            {
                var relativePathSegments = GetRelativeTaskPathSegments(workerTask.FileSystemInfo, workerTask.TaskFlags, paginator, 1);
                nextUrl = GetGenerationUrlFromPath(relativePathSegments);
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

                var layoutInfo = new MarkdownLiquidLayout(file, GaiusConfiguration);
                LayoutDataDictionary.Add(layoutInfo.Id, layoutInfo);
            }
        }
    }
}