using System.Collections;
using System.Data.Common;
using System.Reflection;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// A forward-only <see cref="DbDataReader"/> over in-memory rows, so Dapper's <c>Query*</c> calls can be answered
/// by <see cref="RecordingDbConnection.OnQuery"/> without SQL Server. Columns are <typeparamref name="T"/>'s public
/// properties in declaration order (nullable types unwrapped, a null value reads as DBNull); a string / primitive
/// <typeparamref name="T"/> becomes a single column named <c>Value</c>. Zero rows still expose the columns, which
/// Dapper needs to build its materialiser.
/// </summary>
public sealed class ListDataReader : DbDataReader
{
    private readonly string[] _names;
    private readonly Type[] _types;
    private readonly IReadOnlyList<object?[]> _rows;
    private int _index = -1;
    private bool _closed;

    private ListDataReader(string[] names, Type[] types, IReadOnlyList<object?[]> rows)
    {
        _names = names;
        _types = types;
        _rows = rows;
    }

    public static ListDataReader Of<T>(params T[] rows)
    {
        var type = typeof(T);
        if (IsScalar(type))
        {
            return new ListDataReader(["Value"], [Nullable.GetUnderlyingType(type) ?? type], rows.Select(r => new object?[] { r }).ToList());
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        return new ListDataReader(
            properties.Select(p => p.Name).ToArray(),
            properties.Select(p => Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType).ToArray(),
            rows.Select(r => properties.Select(p => p.GetValue(r)).ToArray()).ToList());
    }

    private static bool IsScalar(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(string) || underlying.IsPrimitive || underlying.IsEnum
               || underlying == typeof(decimal) || underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset)
               || underlying == typeof(DateOnly) || underlying == typeof(TimeOnly) || underlying == typeof(Guid);
    }

    private object?[] Current => _index >= 0 && _index < _rows.Count
        ? _rows[_index]
        : throw new InvalidOperationException("No current row. Call Read() first.");

    public override int Depth => 0;
    public override int FieldCount => _names.Length;
    public override bool HasRows => _rows.Count > 0;
    public override bool IsClosed => _closed;
    public override int RecordsAffected => -1;
    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool Read() => ++_index < _rows.Count;
    public override bool NextResult() => false;
    public override void Close() => _closed = true;

    public override string GetName(int ordinal) => _names[ordinal];
    public override int GetOrdinal(string name)
    {
        var index = Array.FindIndex(_names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : throw new IndexOutOfRangeException(name);
    }

    public override Type GetFieldType(int ordinal) => _types[ordinal];
    public override string GetDataTypeName(int ordinal) => _types[ordinal].Name;
    public override object GetValue(int ordinal) => Current[ordinal] ?? DBNull.Value;
    public override bool IsDBNull(int ordinal) => Current[ordinal] is null or DBNull;

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
    public override char GetChar(int ordinal) => (char)GetValue(ordinal);
    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);
    public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);
    public override double GetDouble(int ordinal) => (double)GetValue(ordinal);
    public override float GetFloat(int ordinal) => (float)GetValue(ordinal);
    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
    public override short GetInt16(int ordinal) => (short)GetValue(ordinal);
    public override int GetInt32(int ordinal) => (int)GetValue(ordinal);
    public override long GetInt64(int ordinal) => (long)GetValue(ordinal);
    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

    public override IEnumerator GetEnumerator()
    {
        while (Read())
        {
            yield return this;
        }
    }
}
