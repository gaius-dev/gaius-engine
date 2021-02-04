namespace Gaius.Worker.FrontMatter
{
    public interface IFrontMatterParser
    {
        IFrontMatter DeserializeFromContent(string textContent);
    }
}