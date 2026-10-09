using System.Text.Json.Nodes;

namespace Crest.Entities;

public interface IEntity
{
    JsonObject Properties { get; }
}
