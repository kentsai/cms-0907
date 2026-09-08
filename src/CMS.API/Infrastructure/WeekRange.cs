namespace CMS.API.Infrastructure;

/// <summary>
/// A Monday-to-Sunday week. <see cref="For"/> snaps any date to the week that contains it, which is how the
/// FeaturedPromoItem board scopes its one-week <c>ScheduleOn</c> filter.
/// </summary>
public readonly record struct WeekRange(DateOnly Start, DateOnly End)
{
    public static WeekRange For(DateOnly date)
    {
        // DayOfWeek: Sunday = 0 … Saturday = 6. Shift so Monday = 0 … Sunday = 6.
        var offsetFromMonday = ((int)date.DayOfWeek + 6) % 7;
        var start = date.AddDays(-offsetFromMonday);
        return new WeekRange(start, start.AddDays(6));
    }

    public bool Contains(DateOnly date) => date >= Start && date <= End;
}
