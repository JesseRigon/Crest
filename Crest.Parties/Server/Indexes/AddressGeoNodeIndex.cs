using Crest.Fields;
using Crest.Regions.Fields;
using Crest.Parties.Constants;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;
using YesSql.Indexes;

namespace Crest.Parties.Indexes;

/// <summary>One row per (address, geo node): the address's stack, queryable by node. "Every address in Cook County" is one indexed read.</summary>
public sealed class AddressGeoNodeIndex : MapIndex
{
    public string PartyId { get; set; } = string.Empty;
    public string AddressId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public int Level { get; set; }
    /// <summary>The address-kind option id (billing, shipping...), so a consumer can pick the right address per node.</summary>
    public string? KindId { get; set; }
    public bool Latest { get; set; }
}

public sealed class AddressGeoNodeIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<AddressGeoNodeIndex>()
            .Map(contentItem =>
            {
                if (!contentItem.Latest && !contentItem.Published)
                {
                    return [];
                }

                var bag = contentItem.Get<BagPart>(PartiesConstants.Bags.Addresses);
                if (bag is null)
                {
                    return [];
                }

                var rows = new List<AddressGeoNodeIndex>();
                foreach (var address in bag.ContentItems)
                {
                    var part = address.Get<ContentPart>(PartiesConstants.ContentTypes.Address);
                    var geo = part?.Get<GeoStackField>("Geo");
                    if (geo is null)
                    {
                        continue;
                    }

                    var kindId = part?.Get<OptionPickerField>("Kind")?.SelectedIds.FirstOrDefault();
                    foreach (var entry in geo.Stack)
                    {
                        rows.Add(new AddressGeoNodeIndex
                        {
                            PartyId = contentItem.ContentItemId,
                            AddressId = address.ContentItemId,
                            NodeId = entry.NodeId,
                            Level = entry.Level,
                            KindId = kindId,
                            Latest = contentItem.Latest,
                        });
                    }

                    foreach (var boundary in geo.BoundaryNodeIds)
                    {
                        rows.Add(new AddressGeoNodeIndex
                        {
                            PartyId = contentItem.ContentItemId,
                            AddressId = address.ContentItemId,
                            NodeId = boundary,
                            Level = 0,
                            KindId = kindId,
                            Latest = contentItem.Latest,
                        });
                    }
                }

                return rows;
            });
    }
}
