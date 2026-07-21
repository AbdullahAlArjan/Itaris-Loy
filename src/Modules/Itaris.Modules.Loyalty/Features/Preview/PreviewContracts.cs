namespace Itaris.Modules.Loyalty.Features.Preview;

/// <summary>doc 05 E4 body: { programId, amountMinor } → { pointsEarned, stampsEarned }.</summary>
public sealed record PreviewRequest(Guid ProgramId, long AmountMinor);

public sealed record PreviewResponse(long PointsEarned, int StampsEarned);
