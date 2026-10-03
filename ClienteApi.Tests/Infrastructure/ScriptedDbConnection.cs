using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Data;
using System.Data.Common;

namespace ClienteApi.Tests.Infrastructure;

internal sealed class ScriptedDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    public Exception? OpenException { get; set; }
    public CancellationToken? OpenCancellationToken { get; private set; }
    public CancellationToken? CommandCancellationToken { get; set; }
    public Func<ScriptedDbCommand, object?>? ScalarHandler { get; set; }
    public Func<ScriptedDbCommand, int>? NonQueryHandler { get; set; }
    public Func<ScriptedDbCommand, DbDataReader>? ReaderHandler { get; set; }
    public List<ScriptedDbCommand> Commands { get; } = [];
    public ScriptedDbTransaction? LastTransaction { get; private set; }

    [AllowNull]
    public override string ConnectionString { get; set; } = "scripted";
    public override string Database => "scripted";
    public override string DataSource => "scripted";
    public override string ServerVersion => "1.0";
    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

    public override void Close() => _state = ConnectionState.Closed;

    public override void Open() => _state = ConnectionState.Open;

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        OpenCancellationToken = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        if (OpenException is not null)
            return Task.FromException(OpenException);

        _state = ConnectionState.Open;
        return Task.CompletedTask;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        LastTransaction = new ScriptedDbTransaction(this, isolationLevel);

    protected override DbCommand CreateDbCommand()
    {
        var command = new ScriptedDbCommand(this);
        Commands.Add(command);
        return command;
    }
}

internal sealed class ScriptedDbTransaction(
    ScriptedDbConnection connection,
    IsolationLevel isolationLevel) : DbTransaction
{
    public bool Committed { get; private set; }
    public bool RolledBack { get; private set; }
    public override IsolationLevel IsolationLevel => isolationLevel;
    protected override DbConnection DbConnection => connection;

    public override void Commit() => Committed = true;
    public override void Rollback() => RolledBack = true;
}

internal sealed class ScriptedDbCommand(ScriptedDbConnection connection) : DbCommand
{
    private readonly ScriptedDbParameterCollection _parameters = new();

    [AllowNull]
    public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    [AllowNull]
    protected override DbConnection DbConnection { get; set; } = connection;
    protected override DbParameterCollection DbParameterCollection => _parameters;
    protected override DbTransaction? DbTransaction { get; set; }

    public object? Scalar() => connection.ScalarHandler?.Invoke(this);
    public int NonQuery() => connection.NonQueryHandler?.Invoke(this) ?? 1;
    public DbDataReader Reader() => connection.ReaderHandler?.Invoke(this) ?? ScriptedDataReader.Empty;
    public object? Parameter(string name) => _parameters.Cast<DbParameter>()
        .FirstOrDefault(parameter => string.Equals(parameter.ParameterName, name, StringComparison.OrdinalIgnoreCase))
        ?.Value;
    public void SetTransaction(DbTransaction? transaction) => DbTransaction = transaction;

    public override void Cancel() { }
    public override int ExecuteNonQuery() => NonQuery();
    public override object? ExecuteScalar() => Scalar();
    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
    {
        connection.CommandCancellationToken = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Scalar());
    }
    public override void Prepare() { }

    protected override DbParameter CreateDbParameter() => new ScriptedDbParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        Reader();
}

internal sealed class ScriptedDbParameter : DbParameter
{
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public override bool IsNullable { get; set; }
    [AllowNull]
    public override string ParameterName { get; set; } = "";
    [AllowNull]
    public override string SourceColumn { get; set; } = "";
    public override object? Value { get; set; }
    public override bool SourceColumnNullMapping { get; set; }
    public override int Size { get; set; }
    public override void ResetDbType() { }
}

internal sealed class ScriptedDbParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _parameters = [];
    public override int Count => _parameters.Count;
    public override object SyncRoot => ((System.Collections.ICollection)_parameters).SyncRoot;
    public override int Add(object value) { _parameters.Add((DbParameter)value); return _parameters.Count - 1; }
    public override void AddRange(Array values) { foreach (var value in values) Add(value!); }
    public override void Clear() => _parameters.Clear();
    public override bool Contains(object value) => _parameters.Contains((DbParameter)value);
    public override bool Contains(string value) => IndexOf(value) >= 0;
    public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_parameters).CopyTo(array, index);
    public override System.Collections.IEnumerator GetEnumerator() => _parameters.GetEnumerator();
    public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);
    public override int IndexOf(string parameterName) =>
        _parameters.FindIndex(parameter => string.Equals(parameter.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));
    public override void Insert(int index, object value) => _parameters.Insert(index, (DbParameter)value);
    public override void Remove(object value) => _parameters.Remove((DbParameter)value);
    public override void RemoveAt(int index) => _parameters.RemoveAt(index);
    public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));
    protected override DbParameter GetParameter(int index) => _parameters[index];
    protected override DbParameter GetParameter(string parameterName) => _parameters[IndexOf(parameterName)];
    protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;
    protected override void SetParameter(string parameterName, DbParameter value)
    {
        var index = IndexOf(parameterName);
        if (index < 0) _parameters.Add(value);
        else _parameters[index] = value;
    }
}

internal sealed class ScriptedConnectionFactory(ScriptedDbConnection connection) :
    ClienteApi.Infrastructure.DbContext.IDbConnectionFactory
{
    public async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

internal sealed class ScriptedDataReader(string[] names, Type[] types, object?[][] rows) : DbDataReader
{
    private int _rowIndex = -1;
    private bool _closed;

    public static ScriptedDataReader Empty { get; } = new([], [], []);
    public override int FieldCount => names.Length;
    public override bool HasRows => rows.Length > 0;
    public override bool IsClosed => _closed;
    public override int RecordsAffected => -1;
    public override int Depth => 0;
    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));
    public override bool Read() => ++_rowIndex < rows.Length;
    public override bool NextResult() => false;
    public override void Close() => _closed = true;
    public override string GetName(int ordinal) => names[ordinal];
    public override int GetOrdinal(string name) => Array.FindIndex(names,
        column => string.Equals(column, name, StringComparison.OrdinalIgnoreCase));
    public override Type GetFieldType(int ordinal) => types[ordinal];
    public override string GetDataTypeName(int ordinal) => types[ordinal].Name;
    public override object GetValue(int ordinal) => rows[_rowIndex][ordinal] ?? DBNull.Value;
    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++) values[i] = GetValue(i);
        return count;
    }
    public override bool IsDBNull(int ordinal) => GetValue(ordinal) is DBNull;
    public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));
    public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));
    public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal));
    public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal));
    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));
    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));
    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));
    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
    public override string GetString(int ordinal) => Convert.ToString(GetValue(ordinal))!;
    public override IEnumerator GetEnumerator() => new DbEnumerator(this, closeReader: false);
    public override DataTable? GetSchemaTable() => null;
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
        throw new NotSupportedException();
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
        throw new NotSupportedException();
}
