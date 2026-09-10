using System.Data;
using System.Globalization;
using System.Runtime.CompilerServices;
using Dapper;

namespace CMS.API.Infrastructure;

/// <summary>
/// Dapper 2.1.79 has no <see cref="DateOnly"/> mapping at all (its net8.0 build never references the type), so
/// without a handler every <c>date</c> column parameter throws <see cref="NotSupportedException"/> and every read
/// into a <see cref="DateOnly"/> property fails to convert. The handler is registered when this assembly loads
/// (module initialiser — covers the API and the test project alike); <c>Program.cs</c> calls
/// <see cref="Register"/> explicitly as well for readability. Registration is idempotent.
/// </summary>
public static class DapperTypeHandlers
{
    private static int _registered;

    [ModuleInitializer]
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        SqlMapper.AddTypeHandler(new DateOnlyHandler());
    }

    /// <summary>Parameters go out as <c>DbType.Date</c>; SqlClient hands <c>date</c> columns back as <see cref="DateTime"/>.</summary>
    private sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }

        public override DateOnly Parse(object value) => value switch
        {
            DateOnly date => date,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            string text => DateOnly.Parse(text, CultureInfo.InvariantCulture),
            _ => throw new DataException($"Cannot read a {value.GetType().Name} as DateOnly.")
        };
    }
}
