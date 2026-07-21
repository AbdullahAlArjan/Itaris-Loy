using System.Text.Json;
using System.Text.Json.Serialization;

namespace Itaris.Modules.Loyalty.Domain;

/// <summary>Shared serializer for the rule-config jsonb column (enums as strings, camelCase).</summary>
public static class LoyaltyJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(RuleConfig config) => JsonSerializer.Serialize(config, Options);

    public static RuleConfig Deserialize(string json) =>
        JsonSerializer.Deserialize<RuleConfig>(json, Options)
        ?? throw new InvalidOperationException("Rule config is empty.");
}
