using System.Data;
using System.Data.Common;
using ClienteApi.Domain.DTOs;
using ClienteApi.Domain.Entities;
using ClienteApi.Domain.Exceptions;
using ClienteApi.Infrastructure.Queries;
using ClienteApi.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;

namespace ClienteApi.Tests.Infrastructure;

public sealed class ClienteRepositoryTests
{
    private readonly ScriptedDbConnection _connection = new();
    private ClienteRepository CreateRepository() => new(new ScriptedConnectionFactory(_connection));

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenQueryIsEmpty()
    {
        _connection.ReaderHandler = _ => JoinedRows([]);

        Assert.Null(await CreateRepository().GetByIdAsync(9));
    }

    [Fact]
    public async Task GetByIdAsync_AggregatesJoinedRowsAndSkipsMissingContact()
    {
        _connection.ReaderHandler = _ => JoinedRows(
            [ClientRow(1, 10, 1), ClientRow(1, 10, 2), ClientRow(1, 10, null)]);

        var client = await CreateRepository().GetByIdAsync(1);

        Assert.NotNull(client);
        Assert.Equal(1, client.Id);
        Assert.Equal(2, client.Contatos.Count());
        Assert.Equal(10, client.Endereco.Id);
        Assert.Equal(new[] { 1, 2 }, client.Contatos.Select(contact => contact.Id));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsDistinctClientsAndUsesPageParameters()
    {
        _connection.ReaderHandler = _ => JoinedRows(
            [ClientRow(1, 10, 1), ClientRow(1, 10, 2), ClientRow(2, null, null)]);

        var clients = (await CreateRepository().GetAllAsync(3, 7)).ToArray();

        Assert.Equal(2, clients.Length);
        Assert.Equal(2, clients[0].Contatos.Count());
        Assert.Null(clients[1].Endereco);
        Assert.Empty(clients[1].Contatos);
        Assert.Contains("PaginatedClientes", _connection.Commands[0].CommandText);
        Assert.Equal(14, _connection.Commands[0].Parameter("Offset"));
        Assert.Equal(7, _connection.Commands[0].Parameter("PageSize"));
    }

    [Fact]
    public async Task CreateAsync_InsertsClientAddressAndContactBatchesAndCommits()
    {
        _connection.ScalarHandler = command =>
            command.CommandText.Contains("OUTPUT INSERTED.Id", StringComparison.Ordinal) ? 31 : true;
        _connection.ReaderHandler = command => command.CommandText.Contains("INSERT INTO Contato", StringComparison.Ordinal)
            ? InsertedContactRows(101)
            : ScriptedDataReader.Empty;

        var id = await CreateRepository().CreateAsync(TestData.Cliente(), TestData.Endereco(), [TestData.Contato()]);

        Assert.Equal(31, id);
        Assert.True(_connection.LastTransaction!.Committed);
        Assert.False(_connection.LastTransaction.RolledBack);
        Assert.Equal(3, _connection.Commands.Count);
        Assert.Contains("@Tipo0", _connection.Commands[2].CommandText);
    }

    [Fact]
    public async Task CreateAsync_WithNoContactsCommitsWithoutEmptyInsertStatement()
    {
        _connection.ScalarHandler = _ => 31;

        await CreateRepository().CreateAsync(TestData.Cliente(), TestData.Endereco(), []);

        Assert.True(_connection.LastTransaction!.Committed);
        Assert.Equal(2, _connection.Commands.Count);
    }

    [Fact]
    public async Task CreateAsync_RollsBackForUnexpectedClientInsertFailure()
    {
        _connection.ScalarHandler = _ => throw new InvalidOperationException("insert failed");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateRepository().CreateAsync(TestData.Cliente(), TestData.Endereco(), []));

        Assert.True(_connection.LastTransaction!.RolledBack);
        Assert.False(_connection.LastTransaction.Committed);
    }

    [Fact]
    public async Task CreateAsync_MapsSqlUniqueViolationToDuplicateClient()
    {
        _connection.ScalarHandler = _ => throw CreateSqlException(2627);

        var exception = await Assert.ThrowsAsync<ClienteJaCadastradoException>(
            () => CreateRepository().CreateAsync(TestData.Cliente(), TestData.Endereco(), []));

        Assert.IsType<SqlException>(exception.InnerException);
        Assert.True(_connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task CreateAsync_DoesNotTranslateNonUniqueSqlErrors()
    {
        _connection.ScalarHandler = _ => throw CreateSqlException(1205);

        await Assert.ThrowsAsync<SqlException>(
            () => CreateRepository().CreateAsync(TestData.Cliente(), TestData.Endereco(), []));

        Assert.True(_connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task UpdateAsync_UpsertsContactsInBatchAndCommits()
    {
        _connection.ScalarHandler = command =>
            command.CommandText.Contains("MERGE Contato", StringComparison.Ordinal) ? 2 : 1;

        await CreateRepository().UpdateAsync(
            TestData.Cliente(5),
            TestData.Endereco(),
            [TestData.Contato(), TestData.Contato(8)]);

        Assert.True(_connection.LastTransaction!.Committed);
        Assert.Equal(3, _connection.Commands.Count);
        Assert.Contains("VALUES (@Id0", _connection.Commands[2].CommandText);
        Assert.Equal(5, _connection.Commands[2].Parameter("ClienteId"));
    }

    [Fact]
    public async Task UpdateAsync_RollsBackWhenNotAllContactsAreSaved()
    {
        _connection.ScalarHandler = command =>
            command.CommandText.Contains("MERGE Contato", StringComparison.Ordinal) ? 1 : 1;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRepository().UpdateAsync(
                TestData.Cliente(5),
                TestData.Endereco(),
                [TestData.Contato(), TestData.Contato(8)]));

        Assert.True(_connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task UpdateAsync_RollsBackWhenCommandFails()
    {
        _connection.NonQueryHandler = command =>
            command.CommandText.Contains("UPDATE Endereco", StringComparison.Ordinal)
                ? throw new IOException("update failed")
                : 1;

        await Assert.ThrowsAsync<IOException>(() =>
            CreateRepository().UpdateAsync(TestData.Cliente(), TestData.Endereco(), []));

        Assert.True(_connection.LastTransaction!.RolledBack);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PatchEnderecoAsync_ReturnsDatabaseResult(bool exists)
    {
        _connection.ScalarHandler = _ => exists;

        var result = await CreateRepository().PatchEnderecoAsync(
            4, new EnderecoPatchDto { Cidade = "Campinas", Numero = "270A" });

        Assert.Equal(exists, result);
        Assert.Contains("@Cidade", _connection.Commands[0].CommandText);
        Assert.Equal("270A", _connection.Commands[0].Parameter("Numero"));
    }

    [Fact]
    public async Task PatchEnderecoAsync_ForwardsCancellationToCommand()
    {
        using var cancellation = new CancellationTokenSource();
        _connection.ScalarHandler = _ => true;

        await CreateRepository().PatchEnderecoAsync(
            4,
            new EnderecoPatchDto { Cidade = "Campinas" },
            cancellation.Token);

        Assert.Equal(cancellation.Token, _connection.CommandCancellationToken);
    }

    [Fact]
    public async Task ReplaceEnderecoAsync_CommitsAndReturnsTrue()
    {
        _connection.ScalarHandler = _ => true;

        Assert.True(await CreateRepository().ReplaceEnderecoAsync(TestData.Endereco()));
        Assert.True(_connection.LastTransaction!.Committed);
        Assert.Contains("Ativo = 0", _connection.Commands[0].CommandText);
    }

    [Fact]
    public async Task ReplaceEnderecoAsync_ReturnsFalseForMissingClientAndCommitsNoChanges()
    {
        _connection.ScalarHandler = _ => false;

        Assert.False(await CreateRepository().ReplaceEnderecoAsync(TestData.Endereco()));
        Assert.True(_connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task ReplaceEnderecoAsync_RollsBackWhenDatabaseFails()
    {
        _connection.ScalarHandler = _ => throw new IOException("SQL failed");

        await Assert.ThrowsAsync<IOException>(
            () => CreateRepository().ReplaceEnderecoAsync(TestData.Endereco()));

        Assert.True(_connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task InsertContatosAsync_InsertsMultipleItemsAndReturnsGeneratedIds()
    {
        var nextId = 20;
        _connection.ReaderHandler = command =>
        {
            var count = command.ParametersCount() / 2;
            return InsertedContactRows(Enumerable.Range(nextId, count).ToArray());
        };

        var ids = await CreateRepository().InsertContatosAsync(
            4, [TestData.Contato(), TestData.Contato(8)]);

        Assert.Equal(new[] { 20, 21 }, ids);
        Assert.True(_connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task InsertContatosAsync_ReturnsNullAndRollsBackForMissingClient()
    {
        _connection.ReaderHandler = _ => MissingClientRows();

        Assert.Null(await CreateRepository().InsertContatosAsync(99, [TestData.Contato()]));
        Assert.True(_connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task InsertContatosAsync_RollsBackForDatabaseFailure()
    {
        _connection.ReaderHandler = _ => throw new IOException("insert contact failed");

        await Assert.ThrowsAsync<IOException>(
            () => CreateRepository().InsertContatosAsync(4, [TestData.Contato()]));

        Assert.True(_connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task InsertContatosAsync_ChunksLargeBatches()
    {
        var total = ContatoQueries.BatchSize + 1;
        var calls = 0;
        _connection.ReaderHandler = command =>
        {
            calls++;
            var count = command.ParametersCount() / 2;
            return InsertedContactRows(Enumerable.Range(calls * 1000, count).ToArray());
        };

        var ids = await CreateRepository().InsertContatosAsync(
            4, Enumerable.Range(0, total).Select(i => TestData.Contato(i + 1)).ToArray());

        Assert.Equal(total, ids!.Count);
        Assert.Equal(2, calls);
        Assert.True(_connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task PatchContatosAsync_UpdatesMultipleRowsAndCommits()
    {
        _connection.ScalarHandler = command => command.ParametersCount() == 9 ? 2 : 0;

        var updated = await CreateRepository().PatchContatosAsync(
            3, [new() { Id = 4, Valor = "updated" }, new() { Id = 5, Ativo = false }]);

        Assert.True(updated);
        Assert.True(_connection.LastTransaction!.Committed);
        Assert.Equal(4, _connection.Commands[0].Parameter("Id0"));
        Assert.Equal(5, _connection.Commands[0].Parameter("Id1"));
    }

    [Fact]
    public async Task PatchContatosAsync_RollsBackIfAnyItemIsMissing()
    {
        _connection.ScalarHandler = _ => 1;

        Assert.False(await CreateRepository().PatchContatosAsync(
            3, [new() { Id = 4, Valor = "updated" }, new() { Id = 5, Valor = "updated" }]));
        Assert.True(_connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task PatchContatosAsync_RollsBackForDatabaseFailure()
    {
        _connection.ScalarHandler = _ => throw new IOException("patch failed");

        await Assert.ThrowsAsync<IOException>(
            () => CreateRepository().PatchContatosAsync(3, [new() { Id = 4, Valor = "updated" }]));

        Assert.True(_connection.LastTransaction!.RolledBack);
    }

    [Fact]
    public async Task PatchContatosAsync_ChunksLargeBatches()
    {
        var total = ContatoQueries.BatchSize + 1;
        var calls = 0;
        _connection.ScalarHandler = command =>
        {
            calls++;
            return (command.ParametersCount() - 1) / 4;
        };

        var updated = await CreateRepository().PatchContatosAsync(
            3,
            Enumerable.Range(1, total).Select(id => new ContatoBatchPatchDto { Id = id, Valor = "x" }).ToArray());

        Assert.True(updated);
        Assert.Equal(2, calls);
        Assert.True(_connection.LastTransaction!.Committed);
    }

    [Fact]
    public async Task DeleteAsync_CommitsSoftDelete()
    {
        await CreateRepository().DeleteAsync(12);

        Assert.True(_connection.LastTransaction!.Committed);
        Assert.Contains("UPDATE Contato SET Ativo = 0", _connection.Commands[0].CommandText);
        Assert.DoesNotContain("DELETE FROM", _connection.Commands[0].CommandText);
        Assert.Equal(12, _connection.Commands[0].Parameter("Id"));
    }

    [Fact]
    public async Task DeleteAsync_RollsBackOnFailure()
    {
        _connection.NonQueryHandler = _ => throw new IOException("delete failed");

        await Assert.ThrowsAsync<IOException>(() => CreateRepository().DeleteAsync(12));

        Assert.True(_connection.LastTransaction!.RolledBack);
    }

    private static ScriptedDataReader InsertedContactRows(params int[] ids) =>
        new(["ClientExists", "Id"], [typeof(bool), typeof(int)],
            ids.Select(id => new object?[] { true, id }).ToArray());

    private static ScriptedDataReader MissingClientRows() =>
        new(["ClientExists", "Id"], [typeof(bool), typeof(int)], [[false, 0]]);

    private static ScriptedDataReader JoinedRows(object?[][] rows)
    {
        string[] names =
        [
            "Id", "Nome", "Cpf", "Rg", "DataNascimento", "Sexo", "NomeMae", "Ativo", "DataCriacao", "DataAtualizacao",
            "Id", "ClienteId", "Rua", "Numero", "Complemento", "Bairro", "Cidade", "Estado", "Pais", "CodigoPostal", "Ativo", "DataCriacao", "DataAtualizacao",
            "Id", "ClienteId", "Tipo", "Valor", "Ativo", "DataCriacao", "DataAtualizacao"
        ];
        Type[] types =
        [
            typeof(int), typeof(string), typeof(string), typeof(string), typeof(DateTime), typeof(string), typeof(string), typeof(bool), typeof(DateTime), typeof(DateTime),
            typeof(int), typeof(int), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(bool), typeof(DateTime), typeof(DateTime),
            typeof(int), typeof(int), typeof(string), typeof(string), typeof(bool), typeof(DateTime), typeof(DateTime)
        ];

        return new ScriptedDataReader(names, types, rows);
    }

    private static object?[] ClientRow(int clientId, int? addressId, int? contactId)
    {
        var now = new DateTime(2026, 1, 1);
        return
        [
            clientId, "Client", "123", "RG", new DateTime(1988, 3, 3), "M", "Mother", true, now, now,
            addressId, addressId is null ? null : clientId,
            addressId is null ? null : "Street", addressId is null ? null : "1",
            addressId is null ? null : "", addressId is null ? null : "District",
            addressId is null ? null : "City", addressId is null ? null : "SP",
            addressId is null ? null : "BR", addressId is null ? null : "00000",
            addressId is null ? null : true, addressId is null ? null : now, addressId is null ? null : now,
            contactId, contactId is null ? null : clientId,
            contactId is null ? null : "Email", contactId is null ? null : $"contact{contactId}@example.com",
            contactId is null ? null : true, contactId is null ? null : now, contactId is null ? null : now
        ];
    }

    private static SqlException CreateSqlException(int number)
    {
        const System.Reflection.BindingFlags nonPublicInstance =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        const System.Reflection.BindingFlags nonPublicStatic =
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var assembly = typeof(SqlException).Assembly;
        var errorType = assembly.GetType("Microsoft.Data.SqlClient.SqlError")!;
        var errorsType = assembly.GetType("Microsoft.Data.SqlClient.SqlErrorCollection")!;
        var error = errorType.GetConstructors(nonPublicInstance)
            .Single(constructor => constructor.GetParameters().Length == 9 &&
                constructor.GetParameters()[7].ParameterType == typeof(int))
            .Invoke([number, (byte)1, (byte)14, "server", "message", "procedure", 1, 0, null]);
        var errors = Activator.CreateInstance(errorsType, nonPublic: true)!;
        errorsType.GetMethod("Add", nonPublicInstance)!.Invoke(errors, [error]);

        return (SqlException)typeof(SqlException)
            .GetMethods(nonPublicStatic)
            .Single(method => method.Name == "CreateException" &&
                method.GetParameters().Length == 2 &&
                method.GetParameters()[0].ParameterType == errorsType)
            .Invoke(null, [errors, "test"])!;
    }
}

internal static class ScriptedDbCommandExtensions
{
    public static int ParametersCount(this ScriptedDbCommand command) =>
        ((DbParameterCollection)command.Parameters).Count;
}
