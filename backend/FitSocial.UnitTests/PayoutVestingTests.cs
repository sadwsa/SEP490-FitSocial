using FitSocial.Domain.Constants;
using Xunit;

namespace FitSocial.UnitTests;

public class PayoutVestingTests
{
    [Theory]
    [InlineData(20, 1)]   // 20 days -> 1 month -> 100%
    [InlineData(30, 1)]   // 1 month -> 100%
    [InlineData(31, 2)]   // over a month -> 2 tranches
    [InlineData(60, 2)]   // 2 months -> 50%/month
    [InlineData(120, 4)]  // 4 months -> 25%/month
    [InlineData(null, 1)] // unknown duration -> 1 month
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    public void VestingMonths_MapsDurationToMonthCount(int? durationDays, int expected)
    {
        Assert.Equal(expected, PayoutConstants.VestingMonths(durationDays));
    }

    [Fact]
    public void TrancheAmount_SingleMonth_ReturnsFullGross()
    {
        Assert.Equal(2000m, PayoutConstants.TrancheAmount(2000m, 1, 0));
    }

    [Fact]
    public void TrancheAmount_FourMonths_SplitsEvenly()
    {
        Assert.Equal(2000m, PayoutConstants.TrancheAmount(8000m, 4, 0));
        Assert.Equal(2000m, PayoutConstants.TrancheAmount(8000m, 4, 3));
    }

    [Fact]
    public void TrancheAmount_Rounding_LastTrancheAbsorbsRemainder()
    {
        var total = 0m;
        for (var i = 0; i < 3; i++)
        {
            total += PayoutConstants.TrancheAmount(10000m, 3, i);
        }
        Assert.Equal(10000m, total);
    }

    [Fact]
    public void VestingMonths_CapsAtMaxVestingMonths()
    {
        Assert.Equal(PayoutConstants.MaxVestingMonths, PayoutConstants.VestingMonths(30 * 100));
    }
}
