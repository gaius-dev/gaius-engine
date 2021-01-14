namespace Gaius.Core.Models
{
    public interface IFrontMatter
    {
        string Layout { get; set; }
        string Title { get; set; }
        string Author { get; set; }
        string Keywords { get; set; }
        string Description { get; set; }
        bool IsDraft { get; set; }
    }
}