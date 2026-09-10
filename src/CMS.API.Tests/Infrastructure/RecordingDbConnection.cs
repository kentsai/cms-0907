using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using CMS.API.Infrastructure;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// An always-open <see cref="DbConnection"/> that records every executed command (text, parameter values,
/// transaction) instead of talking to a database, so Dapper-based repositories and writers can be asserted
/// without SQL Server. Results are scripted per command through <see cref="OnQuery"/> (SELECTs, answered with a
/// <see cref="ListDataReader"/>), <see cref="OnScalar"/> (<c>SCOPE_IDENTITY()</c> and friends) and
/// <see cref="OnExecute"/> (affected rows; throw from it to simulate a failed statement). Every command is
/// recorded before its responder runs, so a failure still leaves its command in <see cref="Commands"/>.
/// </summary>
public sealed class RecordingDbConnection : DbConnection
{
    public List<RecordedCommand> Commands { get; } = [];

    /// <summary>Answers Dapper's <c>Query*</c> calls. Unset: throws, because a SELECT this test did not script is a bug.</summary>
    public Func<RecordedCommand, DbDataReader> OnQuery { get; set; } =
        command => throw new NotSupportedException($"Set {nameof(RecordingDbConnection)}.{nameof(OnQuery)} to answer: {command.CommandText}");

    /// <summary>Answers Dapper's <c>ExecuteScalar*</c> calls. Unset: throws.</summary>
    public Func<RecordedCommand, object?> OnScalar { get; set; } =
        command => throw new NotSupportedException($"Set {nameof(RecordingDbConnection)}.{nameof(OnScalar)} to answer: {command.CommandText}");

    /// <summary>Answers Dapper's <c>Execute*</c> calls with the affected-row count. Default: 1.</summary>
    public Func<RecordedCommand, int> OnExecute { get; set; } = _ => 1;

    /// <summary>The recorded commands that are the writer's <c>INSERT INTO RowAudit</c>.</summary>
    public IReadOnlyList<RecordedCommand> AuditRows =>
        Commands.Where(c => c.CommandText.Contains("INSERT INTO RowAudit", StringComparison.Ordinal)).ToList();

    [AllowNull]
    public override string ConnectionString { get; set; } = "Recording";
    public override string Database => "Recording";
    public override string DataSource => "Recording";
    public override string ServerVersion => "1.0";
    public override ConnectionState State => ConnectionState.Open;

    public override void ChangeDatabase(string databaseName) { }
    public override void Close() { }
    public override void Open() { }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new RecordingDbTransaction(this, isolationLevel);
    protected override DbCommand CreateDbCommand() => new RecordingDbCommand(this);
}

/// <summary>One executed command. Parameter names are stored without a leading <c>@</c>; <c>DBNull</c> becomes <c>null</c>.</summary>
public sealed record RecordedCommand(string CommandText, IReadOnlyDictionary<string, object?> Parameters, DbTransaction? Transaction)
{
    public object? this[string parameterName] => Parameters[parameterName.TrimStart('@')];

    /// <summary>True when the SQL starts with <paramref name="prefix"/> (leading whitespace ignored), e.g. <c>"UPDATE Partner"</c>.</summary>
    public bool StartsWith(string prefix) => CommandText.TrimStart().StartsWith(prefix, StringComparison.Ordinal);
}

/// <summary>Hands the same <see cref="RecordingDbConnection"/> to every repository call so a test can read back what ran.</summary>
public sealed class RecordingConnectionFactory(RecordingDbConnection connection) : IDbConnectionFactory
{
    public Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<DbConnection>(connection);
}

public sealed class RecordingDbTransaction(RecordingDbConnection connection, IsolationLevel isolationLevel) : DbTransaction
{
    protected override DbConnection DbConnection => connection;
    public override IsolationLevel IsolationLevel => isolationLevel;
    public override void Commit() { }
    public override void Rollback() { }
}

internal sealed class RecordingDbCommand(RecordingDbConnection connection) : DbCommand
{
    private readonly RecordingParameterCollection _parameters = [];

    [AllowNull]
    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; } = connection;
    protected override DbParameterCollection DbParameterCollection => _parameters;
    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel() { }
    public override void Prepare() { }
    protected override DbParameter CreateDbParameter() => new RecordingParameter();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => connection.OnQuery(Record());
    public override object? ExecuteScalar() => connection.OnScalar(Record());
    public override int ExecuteNonQuery() => connection.OnExecute(Record());

    private RecordedCommand Record()
    {
        var values = _parameters.Cast<DbParameter>().ToDictionary(
            p => p.ParameterName.TrimStart('@'),
            p => p.Value is DBNull ? null : p.Value);
        var command = new RecordedCommand(CommandText, values, DbTransaction);
        connection.Commands.Add(command);
        return command;
    }
}

internal sealed class RecordingParameter : DbParameter
{
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public override bool IsNullable { get; set; }
    [AllowNull]
    public override string ParameterName { get; set; } = string.Empty;
    public override int Size { get; set; }
    [AllowNull]
    public override string SourceColumn { get; set; } = string.Empty;
    public override bool SourceColumnNullMapping { get; set; }
    public override object? Value { get; set; }
    public override void ResetDbType() => DbType = DbType.Object;
}

internal sealed class RecordingParameterCollection : DbParameterCollection, IEnumerable<DbParameter>
{
    private readonly List<DbParameter> _items = [];

    public override int Count => _items.Count;
    public override object SyncRoot => _items;

    public override int Add(object value)
    {
        _items.Add((DbParameter)value);
        return _items.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
        {
            Add(value);
        }
    }

    public override void Clear() => _items.Clear();
    public override bool Contains(object value) => _items.Contains((DbParameter)value);
    public override bool Contains(string value) => IndexOf(value) >= 0;
    public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
    public override IEnumerator GetEnumerator() => _items.GetEnumerator();
    IEnumerator<DbParameter> IEnumerable<DbParameter>.GetEnumerator() => _items.GetEnumerator();
    public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
    public override int IndexOf(string parameterName) =>
        _items.FindIndex(p => string.Equals(p.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));
    public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
    public override void Remove(object value) => _items.Remove((DbParameter)value);
    public override void RemoveAt(int index) => _items.RemoveAt(index);
    public override void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));
    protected override DbParameter GetParameter(int index) => _items[index];
    protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
    protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
    protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
}
