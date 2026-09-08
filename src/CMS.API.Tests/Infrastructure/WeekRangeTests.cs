using CMS.API.Infrastructure;

namespace CMS.API.Tests.Infrastructure;

public class WeekRangeTests
{
    [Theory]
    [InlineData(2026, 3, 16)] // Monday
    [InlineData(2026, 3, 17)] // Tuesday
    [InlineData(2026, 3, 18)] // Wednesday
    [InlineData(2026, 3, 19)] // Thursday
    [InlineData(2026, 3, 20)] // Friday
    [InlineData(2026, 3, 21)] // Saturday
    [InlineData(2026, 3, 22)] // Sunday
    public void For_SnapsAnyDayOfTheWeek_ToMondayThroughSunday(int year, int month, int day)
    {
        var week = WeekRange.For(new DateOnly(year, month, day));

        Assert.Equal(new DateOnly(2026, 3, 16), week.Start);
        Assert.Equal(new DateOnly(2026, 3, 22), week.End);
        Assert.Equal(DayOfWeek.Monday, week.Start.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, week.End.DayOfWeek);
    }

    [Fact]
    public void For_SundayBelongsToThePrecedingMonday_NotTheFollowingWeek()
    {
        var week = WeekRange.For(new DateOnly(2026, 3, 22));

        Assert.Equal(new DateOnly(2026, 3, 16), week.Start);
    }

    [Fact]
    public void For_CrossesMonthAndYearBoundaries()
    {
        var week = WeekRange.For(new DateOnly(2027, 1, 1)); // Friday

        Assert.Equal(new DateOnly(2026, 12, 28), week.Start);
        Assert.Equal(new DateOnly(2027, 1, 3), week.End);
    }

    [Fact]
    public void Contains_IsInclusiveOnBothEnds()
    {
        var week = WeekRange.For(new DateOnly(2026, 3, 18));

        Assert.True(week.Contains(new DateOnly(2026, 3, 16)));
        Assert.True(week.Contains(new DateOnly(2026, 3, 22)));
        Assert.False(week.Contains(new DateOnly(2026, 3, 15)));
        Assert.False(week.Contains(new DateOnly(2026, 3, 23)));
    }
}
