namespace PolyMod;

public record Identifier(string @namespace, string id)
{
    public static implicit operator string(Identifier identifier) => $"{identifier.@namespace.Replace('_', '-')}:{identifier.id}";
}