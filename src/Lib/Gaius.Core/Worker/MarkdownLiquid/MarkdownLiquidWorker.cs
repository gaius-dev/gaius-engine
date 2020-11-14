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
using System.Linq;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class MarkdownLiquidWorker : BaseWorker, IWorker
    {
        private const string LAYOUTS_DIRECTORY = "_layouts";
        private readonly IFrontMatterParser _frontMatterParser;
        private static readonly MarkdownPipeline _markdownPipeline 
                                                        = new MarkdownPipelineBuilder()
                                                                .UseYamlFrontMatter() //Markdig extension to parse YAML
                                                                .UseBootstrap() //Markdig extension to apply bootstrap CSS classes automatically
                                                                .UseGenericAttributes() //Markdig extension to allow application of generic HTML attributes
                                                                .Build();
        private static IFileProvider _liquidTemplatePhysicalFileProvider;

        public MarkdownLiquidWorker(IFrontMatterParser frontMatterParser, IOptions<GaiusConfiguration> gaiusConfigurationOptions)
        {
            _frontMatterParser = frontMatterParser;
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

        public override WorkerTask GenerateWorkerTask(FileSystemInfo fsInfo)
        {
            return new WorkerTask(fsInfo, GetTransformType(fsInfo), GetTarget(fsInfo));
        }

        public override string PerformTransform(WorkerTask task)
        {
            if(task.TransformType != WorkType.Transform)
                throw new Exception("The MarkdownLiquidWorker can only be assigned WorkerTasks where WorkerTask.WorkerTransformType == TransformConvert");

            if(!task.FSInfo.IsMarkdownFile())
                throw new Exception("The MarkdownLiquidWorker can only be assigned WorkerTasks where WorkerTask.FSInput is a markdown file");
            
            if(_liquidTemplatePhysicalFileProvider == null)
                _liquidTemplatePhysicalFileProvider = new PhysicalFileProvider(GetLayoutsDirFullPath(GaiusConfiguration));
                
            var markdownFile = task.FSInfo as FileInfo;
            var markdownContent = MarkdownPreProcess(File.ReadAllText(markdownFile.FullName));

            var yamlFrontMatter = _frontMatterParser.DeserializeFromContent(markdownContent);
            var layoutName = !string.IsNullOrEmpty(yamlFrontMatter.Layout) ? yamlFrontMatter.Layout : "default";

            var html = Markdown.ToHtml(markdownContent, _markdownPipeline);

            var liquidSourcePath = Path.Combine(GetLayoutsDirFullPath(GaiusConfiguration), $"{layoutName}.liquid");
            var liquidSource = File.ReadAllText(liquidSourcePath);

            var transformId = GetTransformId(task.FSInfo);
            var liquidModel = new LiquidTemplateModel(transformId, yamlFrontMatter, html, GaiusConfiguration);

            if (FluidTemplate.TryParse(liquidSource, out var template))
            {
                var context = new TemplateContext();
                context.FileProvider = _liquidTemplatePhysicalFileProvider;
                context.MemberAccessStrategy.Register(liquidModel.GetType());
                context.Model = liquidModel;
                return template.Render(context);
            }

            return html;
        }

        public override string GetTarget(FileSystemInfo fsInfo)
        {
            var targetName = base.GetTarget(fsInfo);

            if(fsInfo.IsMarkdownFile())
                return $"{Path.GetFileNameWithoutExtension(fsInfo.Name)}.html";

            return targetName;
        }

        public override bool ShouldKeep(FileSystemInfo fsInfo)
        {
            return base.ShouldKeep(fsInfo);
        }

        public override bool ShouldSkip(FileSystemInfo fsInfo, bool checkDraft = false)
        {
            if(base.ShouldSkip(fsInfo, checkDraft))
                return true;

            if(fsInfo.Name.Equals(LAYOUTS_DIRECTORY, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if(fsInfo.IsLiquidFile())
                return true;

            return false;
        }

        public override bool IsDraft(FileSystemInfo fsInfo)
        {
            if (!fsInfo.IsMarkdownFile())
                return false;

            var markdownFile = fsInfo as FileInfo;
            var markdownContent = File.ReadAllText(markdownFile.FullName);

            var yamlFrontMatter = _frontMatterParser.DeserializeFromContent(markdownContent);

            return yamlFrontMatter.Draft;
        }

        private static WorkType GetTransformType(FileSystemInfo fsInfo)
        {
            if (fsInfo.IsMarkdownFile())
                return WorkType.Transform;

            return WorkType.None;
        }

        private static string GetLayoutsDirFullPath(GaiusConfiguration gaiusConfiguration)
        {
            return Path.Combine(gaiusConfiguration.NamedThemeDirectoryFullPath, LAYOUTS_DIRECTORY);
        }

        private static readonly string ROOT_PREFIX_LIQUID_TAG = "{{" + nameof(LiquidTemplateModel.root) + "}}";
        private string MarkdownPreProcess(string markdownContent)
        {
            return markdownContent.Replace(ROOT_PREFIX_LIQUID_TAG, GaiusConfiguration.GenerationUrlRootPrefix);
        }

        private string GetTransformId(FileSystemInfo fsInfo)
        {
            var nameWithoutExt = fsInfo.GetNameWithoutExtension();
            var pathSegments = fsInfo.GetPathSegments();
            pathSegments[pathSegments.Length - 1] = nameWithoutExt;

            var sourceDirIndex = Array.IndexOf(pathSegments, GaiusConfiguration.SourceDirectoryName);
            return string.Join(".", pathSegments.Skip(sourceDirIndex + 1));
        }
    }
}