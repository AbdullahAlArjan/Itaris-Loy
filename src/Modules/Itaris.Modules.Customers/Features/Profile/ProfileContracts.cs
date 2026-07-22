namespace Itaris.Modules.Customers.Features.Profile;

/// <summary>doc 05 B1 response.</summary>
public sealed record CustomerProfileDto(
    Guid Id,
    string? FirstName,
    string PhoneNumber,
    DateOnly? BirthDate,
    string? Gender,
    string PreferredLanguage,
    DateTimeOffset JoinedAt);

/// <summary>doc 05 B2 — partial update. Name 1–50 chars when present.</summary>
public sealed record UpdateProfileRequest(
    string? FirstName,
    DateOnly? BirthDate,
    string? Gender,
    string? PreferredLanguage);
