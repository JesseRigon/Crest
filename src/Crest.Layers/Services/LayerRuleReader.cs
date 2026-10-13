using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Json;
using Crest.Rules;
using Crest.Rules.Services;
using Microsoft.Extensions.Options;

namespace Crest.Layers.Services;

/// <summary>
/// Reads a layer rule's conditions from JSON (<c>[ { "Name": "BooleanCondition", "Value": true } ]</c>),
/// each by its condition factory, for the recipe step and the API alike. A condition whose
/// feature is not enabled is reported, never dropped silently.
/// </summary>
public sealed class LayerRuleReader(IConditionIdGenerator idGenerator, IEnumerable<IConditionFactory> factories, IOptions<DocumentJsonSerializerOptions> serializationOptions)
{
    private readonly JsonSerializerOptions _options = serializationOptions.Value.SerializerOptions;

    public Rule Read(JsonArray? conditions, ICollection<string> unknown)
    {
        var rule = new Rule();
        idGenerator.GenerateUniqueId(rule);
        var byName = factories.ToDictionary(factory => factory.Name);
        foreach (var condition in conditions ?? [])
        {
            var name = condition?["Name"]?.ToString();
            if (name is null || !byName.TryGetValue(name, out var factory))
            {
                unknown.Add(name ?? "(unnamed)");
                continue;
            }

            var read = (Condition)condition!.ToObject(factory.Create().GetType(), _options);
            if (string.IsNullOrEmpty(read.ConditionId))
            {
                idGenerator.GenerateUniqueId(read);
            }

            rule.Conditions.Add(read);
        }

        return rule;
    }

    public JsonArray Write(Rule? rule) => rule is null ? [] : JArray.FromObject(rule.Conditions, _options);
}
