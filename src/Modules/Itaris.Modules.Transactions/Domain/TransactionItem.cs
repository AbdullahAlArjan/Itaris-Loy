using Itaris.SharedKernel;

namespace Itaris.Modules.Transactions.Domain;

/// <summary>
/// transactions.transaction_items — line items (doc 04: "schema-ready; unused by MVP UI").
/// Frozen fragments: transaction_id, quantity, unit_amount. No MVP endpoint writes these.
/// </summary>
public sealed class TransactionItem : Entity
{
    public Guid TransactionId { get; set; }
    public string? Name { get; set; }
    public int Quantity { get; set; }
    public long UnitAmountMinor { get; set; }
}
