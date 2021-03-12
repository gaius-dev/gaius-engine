using System.Collections.Generic;

namespace Gaius.Worker.FrontMatter
{
    public interface IFrontMatter
    {
        bool Draft { get; }
        string Layout { get; }
        string Title { get; }
        string Author { get; }
        string AuthorPage { get; }
        string AuthorImage { get; }
        string Keywords { get; }
        string Description { get; }
        string TeaserImage { get; }
        string Image { get; }

        string NavTitle { get; }
        string NavOrder { get; }
        int NavLevel { get; }
        string SidebarTitle { get; }
        string SidebarOrder { get; }
        int SidebarLevel { get; }
        string GetOrder(bool forSidebar);
        int GetLevel(bool forSidebar);

        List<string> Tags { get; }
        List<string> GetTags();
    }
}