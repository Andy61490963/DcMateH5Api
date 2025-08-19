using DynamicForm.Helper;
using Xunit;

namespace DynamicForm.Tests.HelperTest;

public class RandomHelperTests
{
    [Fact]
    public void GenerateRandomDecimal_ReturnsNonNegativeValueWithinRange()
    {
        var value = RandomHelper.GenerateRandomDecimal();
        Assert.True(value >= 0m && value <= 999_999_999_999_999_999m);
    }

    [Fact]
    public void NextSnowflakeId_ReturnsUniquePositiveIds()
    {
        var id1 = RandomHelper.NextSnowflakeId();
        var id2 = RandomHelper.NextSnowflakeId();
        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
        Assert.NotEqual(id1, id2);
    }
}
