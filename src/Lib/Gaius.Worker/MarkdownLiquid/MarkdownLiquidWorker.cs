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
        private const string _indexHtml = "index.html";
        private const string _indexMd = "index.md";
        private readonly IFrontMatterParser _frontMatterParser;
        private readonly Dictionary<string, IWorkerLayout> _layoutDictionary;
        private readonly Dictionary<string, BaseViewModel> _viewModelDictionary;
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
            _layoutDictionary = new Dictionary<string, IWorkerLayout>();
            _viewModelDictionary = new Dictionary<string, BaseViewModel>();

        }

        public override void InitWorker()
        {
            _layoutDictionary.Clear();
            _viewModelDictionary.Clear();
            BuildLayoutDictionary();
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
            if (string.IsNullOrEmpty(layoutId) || !_layoutDictionary.TryGetValue(layoutId, out var layoutData))
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

        public override void AddTagDataToWorker(List<TagData> tagData, bool tagListPageExists)
        {
            foreach(var td in tagData)
            {
                var tagUrl = tagListPageExists ? GetTagListUrl(td) : "#";
                td.SetTagUrl(tagUrl);
            }
            
            SiteData.SetTagData(tagData);
        }

        private string GetTagListUrl(TagData tagData)
            => $"{GaiusConfiguration.GetGenerationUrlRootPrefix()}/{GaiusConfiguration.TagUrlPrefix}/{GetSanitizedName(tagData.Name)}/";

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

            var outputDisplay = GetOutputDisplay(fileSystemInfo, taskFlags, taskPathSegments, paginator);
            var generationUrl = GetGenerationUrl(fileSystemInfo, taskFlags, relativePathSegments);
            var generationId = GetGenerationId(fileSystemInfo, taskFlags, relativePathSegments);
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

            if(!_layoutDictionary.TryGetValue(layoutIdToLookup, out layout))
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

            //rs: Do we have a folder that should auto insert into the relative path segments?
            var autoInsertedFolder = GetAutoInsertedFolderName(fileSystemInfo, taskFlags);
            if(autoInsertedFolder != null)
                relativePathSegments.Insert(relativePathSegments.Count - 1, autoInsertedFolder);

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

                var postUrlPrefix = string.IsNullOrWhiteSpace(GaiusConfiguration.PostUrlPrefix)
                                    ? new string[] { }
                                    : new string[] { GaiusConfiguration.PostUrlPrefix };

                relativePathSegments = postUrlPrefix.Concat(dateTimePathSegments.Concat(relativePathSegments)).ToList();
                relativePathSegments = SanitizeRelativeTaskPathSegments(fileSystemInfo, relativePathSegments);
                relativePathSegments[relativePathSegments.Count - 1]
                    = GetTaskFileOrDirectoryName(fileSystemInfo, taskFlags);

                return relativePathSegments;
            }
            
            //taglists
            if(taskFlags.HasFlag(WorkerTaskFlags.IsTagListing))
            {
                var tagName = GetSanitizedName(paginator?.AssociatedTagName ?? "temp-tag");
                
                relativePathSegments.Remove(GaiusConfiguration.TagListDirectoryName);
                relativePathSegments.Insert(0, GaiusConfiguration.TagUrlPrefix);
                relativePathSegments.Insert(1, tagName);

                if(pageNumber > 1)
                    relativePathSegments.Insert(2, pageNumber.ToString());

                relativePathSegments = SanitizeRelativeTaskPathSegments(fileSystemInfo, relativePathSegments);
                relativePathSegments[relativePathSegments.Count - 1] 
                    = GetTaskFileOrDirectoryName(fileSystemInfo, taskFlags);

                return relativePathSegments;
            }

            //postlists
            if(taskFlags.HasFlag(WorkerTaskFlags.IsPostListing))
            {
                if(pageNumber > 1)
                    relativePathSegments.Insert(relativePathSegments.Count - 1, pageNumber.ToString());

                relativePathSegments = SanitizeRelativeTaskPathSegments(fileSystemInfo, relativePathSegments);
                relativePathSegments[relativePathSegments.Count - 1] 
                    = GetTaskFileOrDirectoryName(fileSystemInfo, taskFlags);

                return relativePathSegments;
            }

            //any other children in the _source dir
            relativePathSegments = SanitizeRelativeTaskPathSegments(fileSystemInfo, relativePathSegments);
            relativePathSegments[relativePathSegments.Count - 1] 
                = GetTaskFileOrDirectoryName(fileSystemInfo, taskFlags);

            return relativePathSegments;
        }

        private string GetOutputDisplay(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags, List<string> taskPathSegments, Paginator paginator)
        {
            if(taskFlags.HasFlag(WorkerTaskFlags.IsSiteContainerDir))
                return null;
            
            if(taskFlags.HasFlag(WorkerTaskFlags.IsSourceDir) 
                || taskFlags.HasFlag(WorkerTaskFlags.IsNamedThemeDir))
                return GaiusConfiguration.GenerationDirectoryName;

            if(taskFlags.HasFlag(WorkerTaskFlags.IsPostsDir) || taskFlags.HasFlag(WorkerTaskFlags.IsDraftsDir))
            {
                return string.IsNullOrWhiteSpace(GaiusConfiguration.PostUrlPrefix)
                        ? $"/yyyy/MM/dd/[title]/"
                        : $"/{GaiusConfiguration.PostUrlPrefix}/yyyy/MM/dd/[title]/";
            }

            if(taskFlags.HasFlag(WorkerTaskFlags.IsTagListDir))
                return $"/{GaiusConfiguration.TagUrlPrefix}/[tag]/";

            if(taskFlags.HasFlag(WorkerTaskFlags.IsInvalid))
                return null;

            if(taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir) && fileSystemInfo.IsMarkdownFile())
            {
                var segmentCount = taskPathSegments.Count;
                var startIndex = GetOutputDisplayStartIndexForMarkdownFile(fileSystemInfo, taskFlags, taskPathSegments, paginator);
                var takeCount = segmentCount - startIndex - 1;

                if(takeCount == 0)
                    return $"[root] {paginator?.OutputDisplayLabel}";

                return $"/{string.Join('/', taskPathSegments.GetRange(startIndex, takeCount))}/ {paginator?.OutputDisplayLabel}";
            }
            
            return taskPathSegments.Last();
        }

        private int GetOutputDisplayStartIndexForMarkdownFile(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags, List<string> taskPathSegments, Paginator paginator)
        {
            if(!fileSystemInfo.IsMarkdownFile())
                throw new ArgumentException($"{fileSystemInfo.FullName} is not a markdown file.");

            var segmentCount = taskPathSegments.Count;

            //rs: most markdown files will have an auto inserted folder
            //e.g. /my-dedicated-page/index.html
            var startIndex = segmentCount - 2;

            //rs: all taglists will have at least /tag/{tagname}/index.html
            if (taskFlags.HasFlag(WorkerTaskFlags.IsTagListing))
                startIndex = segmentCount - 3;

            //rs: all posts will have at least /yyyy/MM/dd/{title}/index.html
            if (taskFlags.HasFlag(WorkerTaskFlags.IsPost) || taskFlags.HasFlag(WorkerTaskFlags.IsDraft))
            {
                startIndex = segmentCount - 5;

                //rs: we have a post URL prefix
                //e.g. /blogs/yyyy/MM/dd/{title}/index.html
                //go back one further
                if (!string.IsNullOrEmpty(GaiusConfiguration.PostUrlPrefix))
                    startIndex--;
            }

            //rs: we have a page number URL segment, go back one further
            //e.g. /tag/{tagname}/2/index.html
            if (paginator != null && paginator.PageNumber > 1)
                startIndex--;

            //rs: we're dealing with a .md file named index.md that is not a post/draft or taglist
            //adjust the start index to account since auto insert folder wasn't created
            if (fileSystemInfo.Name.Equals(_indexMd, StringComparison.InvariantCultureIgnoreCase)
                && !taskFlags.HasFlag(WorkerTaskFlags.IsTagListing)
                && !taskFlags.HasFlag(WorkerTaskFlags.IsPost)
                && !taskFlags.HasFlag(WorkerTaskFlags.IsDraft))
                startIndex++;

            return startIndex;
        }

        private string GetGenerationUrl(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags, List<string> relativePathSegments)
        {
            if(relativePathSegments == null)
                return null;

            if(!fileSystemInfo.IsFile()
                || !taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir)
                || taskFlags.HasFlag(WorkerTaskFlags.IsInvalid))
                return null;

            return GetGenerationUrlFromPath(fileSystemInfo, taskFlags, relativePathSegments);
        }

        private string GetGenerationId(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags, List<string> relativePathSegments)
        {
            if(relativePathSegments == null)
                return null;

            if(!fileSystemInfo.IsFile()
                || !taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir)
                || taskFlags.HasFlag(WorkerTaskFlags.IsInvalid))
                return null;

            return GetGenerationIdFromPath(fileSystemInfo, taskFlags, relativePathSegments);
        }

        private string GetAutoInsertedFolderName(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags)
        {
            if(!taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir))
                throw new ArgumentException($"{nameof(taskFlags)} must contain {nameof(WorkerTaskFlags.IsChildOfSourceDir)}");

            if(!fileSystemInfo.IsMarkdownFile())
                return null;

            //The source file name is already index.md *and* it's not a post/draft, no need to auto insert folder
            if(fileSystemInfo.Name.Equals(_indexMd, StringComparison.InvariantCultureIgnoreCase)
                && !taskFlags.HasFlag(WorkerTaskFlags.IsPost)
                && !taskFlags.HasFlag(WorkerTaskFlags.IsDraft))
                return null;

            var sanitizedNameWithoutExt = GetSanitizedName(Path.GetFileNameWithoutExtension(fileSystemInfo.Name));

            if(taskFlags.HasFlag(WorkerTaskFlags.IsPost))
                return $"{_datePrefixRegEx.Replace(sanitizedNameWithoutExt, string.Empty).TrimStart('-')}";

            if(taskFlags.HasFlag(WorkerTaskFlags.IsDraft))
                return $"{_datePrefixRegEx.Replace(sanitizedNameWithoutExt, string.Empty).TrimStart('-')}-draft";

            return sanitizedNameWithoutExt;
        }

        private string GetTaskFileOrDirectoryName(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags)
        {
            if(taskFlags.HasFlag(WorkerTaskFlags.IsChildOfGenDir))
                throw new ArgumentException($"{nameof(taskFlags)} must cannot contain {nameof(WorkerTaskFlags.IsChildOfGenDir)}");

            //rs: anything in the named theme directory should not have it's name altered in any way
            if(taskFlags.HasFlag(WorkerTaskFlags.IsChildOfNamedThemeDir))
                return fileSystemInfo.Name;

            //rs: if we're dealing with a .md file, we'll always be generating an index.html file (in the appropriate auto inserted folder)
            if(fileSystemInfo.IsMarkdownFile())
                return _indexHtml;

            //rs: anything below this point would be a non .md file in the source directory
            //(e.g. sub directories) and should therefore have it's name sanitized
            var sanitizedNameWithoutExt = GetSanitizedName(Path.GetFileNameWithoutExtension(fileSystemInfo.Name));
            
            //rs: there shouldn't be two many items in the source folder that are not .md files, but if there are:
            if(fileSystemInfo.IsFile())
                return $"{sanitizedNameWithoutExt}{Path.GetExtension(fileSystemInfo.Name)}";

            return sanitizedNameWithoutExt;
        }

        private string GetSanitizedName(string nameWithoutExtension)
            => _fileDirNameSanitizeRegEx.Replace(nameWithoutExtension, "-").ToLowerInvariant().TrimStart('-');

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

        private string GetGenerationUrlFromPath(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags, List<string> relativeTaskPathSegments)
        {
            if(!taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir))
                throw new ArgumentException($"{fileSystemInfo.FullName} is not a child of the source directory.");

            if(taskFlags.HasFlag(WorkerTaskFlags.IsInvalid))
                throw new ArgumentException($"{fileSystemInfo.FullName} is invalid.");

            if(relativeTaskPathSegments.Last().Equals(_indexHtml))
            {
                return relativeTaskPathSegments.Count == 1
                    ? $"{GaiusConfiguration.GetGenerationUrlRootPrefix()}/"
                    : $"{GaiusConfiguration.GetGenerationUrlRootPrefix()}/{string.Join("/", relativeTaskPathSegments.Take(relativeTaskPathSegments.Count - 1))}/";
            }

            return $"{GaiusConfiguration.GetGenerationUrlRootPrefix()}/{string.Join("/", relativeTaskPathSegments)}";
        }

        private static string GetGenerationIdFromPath(FileSystemInfo fileSystemInfo, WorkerTaskFlags taskFlags, List<string> relativeTaskPathSegments)
        {
            if(!taskFlags.HasFlag(WorkerTaskFlags.IsChildOfSourceDir))
                throw new ArgumentException($"{fileSystemInfo.FullName} is not a child of the source directory.");

            if(taskFlags.HasFlag(WorkerTaskFlags.IsInvalid))
                throw new ArgumentException($"{fileSystemInfo.FullName} is invalid.");

            if(relativeTaskPathSegments.Last().Equals(_indexHtml))
            {
                return relativeTaskPathSegments.Count == 1
                    ? "."
                    : string.Join(".", relativeTaskPathSegments.Take(relativeTaskPathSegments.Count - 1));
            }

            return Path.GetFileNameWithoutExtension(string.Join(".", relativeTaskPathSegments));
        }

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
                prevUrl = GetGenerationUrlFromPath(workerTask.FileSystemInfo, workerTask.TaskFlags, relativePathSegments);
            }

            if (paginator.HasNext)
            {
                var relativePathSegments = GetRelativeTaskPathSegments(workerTask.FileSystemInfo, workerTask.TaskFlags, paginator, 1);
                nextUrl = GetGenerationUrlFromPath(workerTask.FileSystemInfo, workerTask.TaskFlags, relativePathSegments);
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
            if(_viewModelDictionary.TryGetValue(workerTask.GenerationId, out var baseViewModel))
                return baseViewModel;

            var markdownFile = workerTask.FileInfo;
            var markdownContent = MarkdownPreProcess(File.ReadAllText(markdownFile.FullName));
            var html = Markdown.ToHtml(markdownContent, _markdownPipeline);
            baseViewModel = new BaseViewModel(workerTask, html);

            _viewModelDictionary.Add(workerTask.GenerationId, baseViewModel);
            return baseViewModel;
        }

        private static string GetLayoutsDirFullPath(GaiusConfiguration gaiusConfiguration)
            => Path.Combine(gaiusConfiguration.NamedThemeDirectoryFullPath, _layoutsDirectory);

        private string MarkdownPreProcess(string markdownContent)
            => GenerationUrlRootPrefixPreProcessor(markdownContent);

        private string GenerationUrlRootPrefixPreProcessor(string markdownContent)
            => _siteUrlRegEx.Replace(markdownContent, GaiusConfiguration.GetGenerationUrlRootPrefix());

        private void BuildLayoutDictionary()
        {
            var layoutsDirectory = new DirectoryInfo(GetLayoutsDirFullPath(GaiusConfiguration));

            foreach(var file in layoutsDirectory.EnumerateFiles())
            {
                if(!file.IsLiquidFile())
                    continue;

                var layoutInfo = new MarkdownLiquidLayout(file, GaiusConfiguration);
                _layoutDictionary.Add(layoutInfo.Id, layoutInfo);
            }
        }
    }
}