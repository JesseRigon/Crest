using System.Xml.Linq;

namespace Crest.Feeds.Models;

public class FeedResponse
{
    public XElement Element { get; set; }
    public IList<FeedItem> Items { get; } = [];
    public IList<Action<ContextualizeContext>> Contextualizers { get; } = [];
    public void Contextualize(Action<ContextualizeContext> contextualizer)
    {
        Contextualizers.Add(contextualizer);
    }
}
