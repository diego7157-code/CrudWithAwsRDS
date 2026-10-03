using ClienteApi.Infrastructure.DbContext;

namespace ClienteApi.Tests.Infrastructure;

public sealed class DbConnectionFactoryTests
{
    [Fact]
    public async Task CreateConnectionAsync_OpensAndReturnsCreatedConnection()
    {
        var connection = new ScriptedDbConnection();
        var factory = new DbConnectionFactory("scripted", _ => connection);

        var result = await factory.CreateConnectionAsync();

        Assert.Same(connection, result);
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Fact]
    public async Task CreateConnectionAsync_PropagatesOpenFailure()
    {
        var expected = new IOException("open failed");
        var connection = new ScriptedDbConnection { OpenException = expected };
        var factory = new DbConnectionFactory("scripted", _ => connection);

        var actual = await Assert.ThrowsAsync<IOException>(() => factory.CreateConnectionAsync());
        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task CreateConnectionAsync_ForwardsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var connection = new ScriptedDbConnection();
        var factory = new DbConnectionFactory("scripted", _ => connection);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => factory.CreateConnectionAsync(cancellation.Token));

        Assert.Equal(cancellation.Token, connection.OpenCancellationToken);
    }
}
