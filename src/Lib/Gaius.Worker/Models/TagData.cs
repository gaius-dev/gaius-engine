namespace Gaius.Worker.Models
{
    public class TagData 
    {
        public TagData(string tagName)
        {
            Name = tagName;
        }

        public string Name { get; private set; }
    }
}