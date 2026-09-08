namespace CMS.API.Infrastructure;

/// <summary>
/// Thrown by a repository when an INSERT/UPDATE violates a unique key (SQL Server errors 2627 / 2601), e.g. the
/// FeaturedPromoItem <c>(ScheduleOn, TrainingCenter_pkid, Slot)</c> index. Controllers translate it to
/// <c>409 Conflict</c>.
/// </summary>
public sealed class SlotOccupiedException(string message) : Exception(message);
