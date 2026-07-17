namespace Itaris.SharedKernel;

/// <summary>
/// Bilingual value per doc 05 global conventions: localized fields are returned as
/// <c>{ "ar": "...", "en": "..." }</c> and clients pick. Serializes with those exact keys.
/// </summary>
public sealed record LocalizedText(string Ar, string En);
