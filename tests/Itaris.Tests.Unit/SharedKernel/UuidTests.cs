using Itaris.SharedKernel;

namespace Itaris.Tests.Unit.SharedKernel;

public class UuidTests
{
    [Fact]
    public void NewV7_produces_version_7_guids()
    {
        var id = Uuid.NewV7();

        Assert.Equal(7, id.Version);
    }

    [Fact]
    public void NewV7_is_time_ordered()
    {
        var earlier = Uuid.NewV7(DateTimeOffset.UtcNow.AddMinutes(-1));
        var later = Uuid.NewV7(DateTimeOffset.UtcNow);

        // UUIDv7 sorts by creation time when compared as strings/bytes.
        Assert.True(string.CompareOrdinal(earlier.ToString(), later.ToString()) < 0);
    }
}
