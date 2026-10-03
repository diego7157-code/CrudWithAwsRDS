using ClienteApi.Domain.DTOs;
using ClienteApi.Domain.Entities;
using ClienteApi.Domain.Exceptions;
using ClienteApi.Domain.Interfaces;
using ClienteApi.Infrastructure.DbContext;
using ClienteApi.Infrastructure.Queries;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ClienteApi.Infrastructure.Repositories;

public sealed class ClienteRepository : IClienteRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ClienteRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Cliente?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var clientes = new Dictionary<int, Cliente>();

        await connection.QueryAsync<Cliente, Endereco, Contato, Cliente>(
            new CommandDefinition(
                ClienteQueries.GetById,
                new { Id = id },
                cancellationToken: cancellationToken),
            (cliente, endereco, contato) =>
            {
                if (!clientes.TryGetValue(cliente.Id, out var clienteEntry))
                {
                    clienteEntry = cliente;
                    clienteEntry.Contatos = new List<Contato>();
                    clientes.Add(cliente.Id, clienteEntry);
                }

                if (endereco is not null)
                    clienteEntry.Endereco = endereco;

                if (contato?.Id > 0)
                    ((List<Contato>)clienteEntry.Contatos).Add(contato);

                return clienteEntry;
            },
            splitOn: "Id,Id");

        return clientes.Values.FirstOrDefault();
    }

    public async Task<IEnumerable<Cliente>> GetAllAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var clientes = new Dictionary<int, Cliente>();

        await connection.QueryAsync<Cliente, Endereco, Contato, Cliente>(
            new CommandDefinition(
                ClienteQueries.GetAll,
                new { Offset = (page - 1) * pageSize, PageSize = pageSize },
                cancellationToken: cancellationToken),
            (cliente, endereco, contato) =>
            {
                if (!clientes.TryGetValue(cliente.Id, out var clienteEntry))
                {
                    clienteEntry = cliente;
                    clienteEntry.Contatos = new List<Contato>();
                    clientes.Add(cliente.Id, clienteEntry);
                }

                if (endereco is not null)
                    clienteEntry.Endereco = endereco;

                if (contato?.Id > 0)
                    ((List<Contato>)clienteEntry.Contatos).Add(contato);

                return clienteEntry;
            },
            splitOn: "Id,Id");

        return clientes.Values;
    }

    public async Task<int> CreateAsync(
        Cliente cliente,
        Endereco endereco,
        IEnumerable<Contato> contatos,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            int clienteId;
            try
            {
                clienteId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    ClienteQueries.InsertCliente,
                    cliente,
                    transaction,
                    cancellationToken: cancellationToken));
            }
            catch (SqlException exception) when (exception.Number is 2601 or 2627)
            {
                throw new ClienteJaCadastradoException(exception);
            }

            endereco.ClienteId = clienteId;
            await connection.ExecuteAsync(new CommandDefinition(
                EnderecoQueries.Insert,
                endereco,
                transaction,
                cancellationToken: cancellationToken));

            await InsertContatosAsync(
                connection,
                transaction,
                clienteId,
                contatos.ToArray(),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return clienteId;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task UpdateAsync(
        Cliente cliente,
        Endereco endereco,
        IEnumerable<Contato> contatos,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ClienteQueries.UpdateCliente,
                cliente,
                transaction,
                cancellationToken: cancellationToken));

            endereco.ClienteId = cliente.Id;
            await connection.ExecuteAsync(new CommandDefinition(
                EnderecoQueries.Update,
                endereco,
                transaction,
                cancellationToken: cancellationToken));

            await UpsertContatosAsync(
                connection,
                transaction,
                cliente.Id,
                contatos.ToArray(),
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> PatchEnderecoAsync(
        int clienteId,
        EnderecoPatchDto dto,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            EnderecoQueries.Patch,
            new
            {
                ClienteId = clienteId,
                dto.Rua,
                dto.Numero,
                dto.Complemento,
                dto.Bairro,
                dto.Cidade,
                dto.Estado,
                dto.Pais,
                dto.CodigoPostal
            },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> ReplaceEnderecoAsync(
        Endereco endereco,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var replaced = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                EnderecoQueries.Replace,
                endereco,
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return replaced;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<int>?> InsertContatosAsync(
        int clienteId,
        IReadOnlyCollection<Contato> contatos,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var insertedIds = await InsertContatosAsync(
                connection,
                transaction,
                clienteId,
                contatos,
                cancellationToken);
            if (insertedIds is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            await transaction.CommitAsync(cancellationToken);
            return insertedIds;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> PatchContatosAsync(
        int clienteId,
        IReadOnlyCollection<ContatoBatchPatchDto> contatos,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var batch in contatos.Chunk(ContatoQueries.BatchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var parameters = new DynamicParameters();
                parameters.Add("ClienteId", clienteId);

                for (var index = 0; index < batch.Length; index++)
                {
                    var contato = batch[index];
                    parameters.Add($"Id{index}", contato.Id);
                    parameters.Add($"Tipo{index}", contato.Tipo);
                    parameters.Add($"Valor{index}", contato.Valor);
                    parameters.Add($"Ativo{index}", contato.Ativo);
                }

                var updatedCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    ContatoQueries.BuildPatchMany(batch.Length),
                    parameters,
                    transaction,
                    cancellationToken: cancellationToken));

                if (updatedCount != batch.Length)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return false;
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<IReadOnlyList<int>?> InsertContatosAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        int clienteId,
        IReadOnlyCollection<Contato> contatos,
        CancellationToken cancellationToken)
    {
        var insertedIds = new List<int>(contatos.Count);

        foreach (var batch in contatos.Chunk(ContatoQueries.BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parameters = new DynamicParameters();
            parameters.Add("ClienteId", clienteId);

            for (var index = 0; index < batch.Length; index++)
            {
                parameters.Add($"Tipo{index}", batch[index].Tipo);
                parameters.Add($"Valor{index}", batch[index].Valor);
            }

            var rows = await connection.QueryAsync<ContatoInsertResult>(new CommandDefinition(
                ContatoQueries.BuildInsertMany(batch.Length),
                parameters,
                transaction,
                cancellationToken: cancellationToken));

            var results = rows.AsList();
            if (results.Count == 1 && !results[0].ClientExists)
                return null;

            insertedIds.AddRange(results.Select(result => result.Id));
        }

        return insertedIds;
    }

    private static async Task UpsertContatosAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        int clienteId,
        IReadOnlyCollection<Contato> contatos,
        CancellationToken cancellationToken)
    {
        foreach (var batch in contatos.Chunk(ContatoQueries.BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parameters = new DynamicParameters();
            parameters.Add("ClienteId", clienteId);

            for (var index = 0; index < batch.Length; index++)
            {
                var contato = batch[index];
                parameters.Add($"Id{index}", contato.Id);
                parameters.Add($"Tipo{index}", contato.Tipo);
                parameters.Add($"Valor{index}", contato.Valor);
                parameters.Add($"Ativo{index}", contato.Ativo);
            }

            var affectedCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                ContatoQueries.BuildUpsertMany(batch.Length),
                parameters,
                transaction,
                cancellationToken: cancellationToken));

            if (affectedCount != batch.Length)
                throw new InvalidOperationException("Não foi possível salvar todos os contatos do cliente.");
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                ClienteQueries.SoftDeleteCliente,
                new { Id = id },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private sealed class ContatoInsertResult
    {
        public bool ClientExists { get; set; }
        public int Id { get; set; }
    }
}
