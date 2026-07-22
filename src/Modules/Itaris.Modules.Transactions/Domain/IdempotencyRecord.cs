namespace Itaris.Modules.Transactions.Domain;

/// <summary>
/// transactions.idempotency_records — replay protection (doc 04 Part 8). Frozen fragments:
/// key text PK (client key + route + actor), request_hash, response_status, response_body,
/// locked_at, expires_at. Lock-or-replay: insert-first with unique key; on conflict return the
/// stored response; mismatched request_hash → 409 IDEMPOTENCY_CONFLICT (doc 04 Part 9 rule 4).
/// Not an Entity: the composite string key IS the primary key.
/// </summary>
public sealed class IdempotencyRecord
{
    /// <summary>"{clientKey}:{route}:{actorId}".</summary>
    public required string Key { get; set; }

    public required string RequestHash { get; set; }

    public int? ResponseStatus { get; set; }

    /// <summary>Serialized JSON of the original successful response; null while in flight.</summary>
    public string? ResponseBody { get; set; }

    public DateTimeOffset LockedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
