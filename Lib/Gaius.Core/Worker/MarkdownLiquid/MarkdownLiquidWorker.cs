using System;
using System.IO;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Fluid;
using Gaius.Core.Configuration;
using Markdig;
using Gaius.Core.Parsing;
using Strube.Utilities.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Gaius.Core.Parsing.Yaml;
using System.Collections.Generic;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class MarkdownLiquidWorker : BaseWorker, IWorker
    {
        private const string LAYOUT_DIR = "_layouts";
        private readonly IFrontMatterParser _frontMatterParser;
        private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder().UseYamlFrontMatter().Build();
        private static IFileProvider _liquidTemplatePhysicalFileProvider;

        public MarkdownLiquidWorker(IFrontMatterParser frontMatterParser, IOptions<GaiusConfiguration> gaiusConfigurationOptions)
        {
            _frontMatterParser = frontMatterParser;
            GaiusConfiguration = gaiusConfigurationOptions.Value;
            RequiredDirectories = new List<string> { GetLayoutDirFullPath(GaiusConfiguration) };
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
                _liquidTemplatePhysicalFileProvider = new PhysicalFileProvider(GetLayoutDirFullPath(GaiusConfiguration));
                
            var markdownFile = task.FSInfo as FileInfo;
            var markdownContent = File.ReadAllText(markdownFile.FullName);

            var yamlFrontMatter = _frontMatterParser.DeserializeFromContent(markdownContent);
            var layoutName = !string.IsNullOrEmpty(yamlFrontMatter.Layout) ? yamlFrontMatter.Layout : "default";

            var html = Markdown.ToHtml(markdownContent, _markdownPipeline);
            var liquidSourcePath = Path.Combine(GetLayoutDirFullPath(GaiusConfiguration), $"{layoutName}.liquid");
            var liquidSource = File.ReadAllText(liquidSourcePath);

            var liquidModel = new LiquidTemplateModel(yamlFrontMatter, html);

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
            if(fsInfo.IsDirectory())
            {
                if(fsInfo.FullName.Equals(GaiusConfiguration.SourceDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase))
                    return GaiusConfiguration.GenerationDirectoryName;

                else return fsInfo.Name;
            }
            
            var file = fsInfo as FileInfo;

            if(file.IsMarkdownFile())
                return $"{Path.GetFileNameWithoutExtension(file.Name)}.html";

            return file.Name;
        }

        public override bool ShouldKeep(FileSystemInfo fsInfo)
        {
            return base.ShouldKeep(fsInfo);
        }

        public override bool ShouldSkip(FileSystemInfo fsInfo)
        {
            if(base.ShouldSkip(fsInfo))
                return true;
                
            if(fsInfo.Name.Equals(LAYOUT_DIR, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if(fsInfo.IsLiquidFile())
                return true;

            return false;
        }

        private static WorkType GetTransformType(FileSystemInfo fsInfo)
        {
            if (fsInfo.IsMarkdownFile())
                return WorkType.Transform;

            return WorkType.None;
        }

        private static string GetLayoutDirFullPath(GaiusConfiguration gaiusConfiguration)
        {
            return Path.Combine(gaiusConfiguration.SourceDirectoryFullPath, LAYOUT_DIR);
        }
    }
}