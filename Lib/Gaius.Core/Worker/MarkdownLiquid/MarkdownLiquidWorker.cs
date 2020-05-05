using System;
using System.IO;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Fluid;
using Gaius.Core.Configuration;
using Markdig;
using Gaius.Core.Parsing;
using Strube.Utilities.FileSystem;

namespace Gaius.Core.Worker.MarkdownLiquid
{
    public class MarkdownLiquidWorker : IWorker
    {
        private readonly GaiusConfiguration _gaiusConfiguration;
        private readonly IFrontMatterParser _frontMatterParser;
        private readonly MarkdownPipeline _markdownPipeline;
        private IFileProvider _liquidTemplatePhysicalFileProvider;

        public MarkdownLiquidWorker(IFrontMatterParser frontMatterParser, IOptions<GaiusConfiguration> gaiusConfigurationOptions)
        {
            _frontMatterParser = frontMatterParser;
            _gaiusConfiguration = gaiusConfigurationOptions.Value;
            _markdownPipeline = new MarkdownPipelineBuilder().UseYamlFrontMatter().Build();
        }

        public WorkerTask GenerateWorkerTask(FileSystemInfo fsInfo)
        {
            return new WorkerTask(fsInfo, GetTransformType(fsInfo), GetTarget(fsInfo));
        }

        public string PerformTransform(WorkerTask task)
        {
            if(task.TransformType != WorkType.Transform)
                throw new Exception("The MarkdownLiquidWorker can only be assigned WorkerTasks where WorkerTask.WorkerTransformType == TransformConvert");

            if(!task.FSInfo.IsMarkdownFile())
                throw new Exception("The MarkdownLiquidWorker can only be assigned WorkerTasks where WorkerTask.FSInput is a markdown file");
            
            if(_liquidTemplatePhysicalFileProvider == null)
                _liquidTemplatePhysicalFileProvider = new PhysicalFileProvider(_gaiusConfiguration.LayoutDirectorFullPath);
                
            var markdownFile = task.FSInfo as FileInfo;
            var markdownContent = File.ReadAllText(markdownFile.FullName);

            var yamlFrontMatter = _frontMatterParser.DeserializeFromContent(markdownContent);
            var layoutName = !string.IsNullOrEmpty(yamlFrontMatter.Layout) ? yamlFrontMatter.Layout : "default";

            var html = Markdown.ToHtml(markdownContent, _markdownPipeline);
            var liquidSourcePath = Path.Combine(_gaiusConfiguration.LayoutDirectorFullPath, $"{layoutName}.liquid");
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

        public string GetTarget(FileSystemInfo fsInfo)
        {
            if(fsInfo.IsDirectory())
            {
                if(fsInfo.FullName.Equals(_gaiusConfiguration.SourceDirectoryFullPath, StringComparison.InvariantCultureIgnoreCase))
                    return _gaiusConfiguration.GenerationDirectoryName;

                else return fsInfo.Name;
            }
            
            var file = fsInfo as FileInfo;

            if(file.IsMarkdownFile())
                return $"{Path.GetFileNameWithoutExtension(file.Name)}.html";

            return file.Name;
        }

        private static WorkType GetTransformType(FileSystemInfo fsInfo)
        {
            if (fsInfo.IsMarkdownFile())
                return WorkType.Transform;

            return WorkType.None;
        }
    }
}