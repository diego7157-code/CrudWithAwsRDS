namespace ClienteApi.Infrastructure.Queries;

public static class ClienteQueries
{
    public const string GetById = @"
        SELECT
            c.Id, c.Nome, c.Cpf, c.Rg, c.DataNascimento, c.Sexo,
            c.NomeMae, c.Ativo, c.DataCriacao, c.DataAtualizacao,
            e.Id, e.ClienteId, e.Rua, e.Numero, e.Complemento,
            e.Bairro, e.Cidade, e.Estado, e.Pais, e.CodigoPostal,
            e.Ativo, e.DataCriacao, e.DataAtualizacao,
            ct.Id, ct.ClienteId, ct.Tipo, ct.Valor,
            ct.Ativo, ct.DataCriacao, ct.DataAtualizacao
        FROM Cliente c
        LEFT JOIN Endereco e ON e.ClienteId = c.Id AND e.Ativo = 1
        LEFT JOIN Contato ct ON ct.ClienteId = c.Id AND ct.Ativo = 1
        WHERE c.Id = @Id AND c.Ativo = 1";

    public const string GetAll = @"
        WITH PaginatedClientes AS
        (
            SELECT c.Id
            FROM Cliente c
            WHERE c.Ativo = 1
            ORDER BY c.Id
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
        )
        SELECT
            c.Id, c.Nome, c.Cpf, c.Rg, c.DataNascimento, c.Sexo,
            c.NomeMae, c.Ativo, c.DataCriacao, c.DataAtualizacao,
            e.Id, e.ClienteId, e.Rua, e.Numero, e.Complemento,
            e.Bairro, e.Cidade, e.Estado, e.Pais, e.CodigoPostal,
            e.Ativo, e.DataCriacao, e.DataAtualizacao,
            ct.Id, ct.ClienteId, ct.Tipo, ct.Valor,
            ct.Ativo, ct.DataCriacao, ct.DataAtualizacao
        FROM PaginatedClientes pc
        INNER JOIN Cliente c ON c.Id = pc.Id
        LEFT JOIN Endereco e ON e.ClienteId = c.Id AND e.Ativo = 1
        LEFT JOIN Contato ct ON ct.ClienteId = c.Id AND ct.Ativo = 1
        ORDER BY c.Id, ct.Id";

    public const string InsertCliente = @"
        INSERT INTO Cliente (Nome, Cpf, Rg, DataNascimento, Sexo, NomeMae, Ativo, DataCriacao, DataAtualizacao)
        OUTPUT INSERTED.Id
        VALUES (@Nome, @Cpf, @Rg, @DataNascimento, @Sexo, @NomeMae, 1, GETUTCDATE(), GETUTCDATE())";

    public const string UpdateCliente = @"
        UPDATE Cliente SET
            Nome = @Nome, Rg = @Rg, DataNascimento = @DataNascimento,
            Sexo = @Sexo, NomeMae = @NomeMae, Ativo = @Ativo,
            DataAtualizacao = GETUTCDATE()
        WHERE Id = @Id";

    public const string SoftDeleteCliente = @"
        UPDATE Cliente SET Ativo = 0, DataAtualizacao = GETUTCDATE() WHERE Id = @Id;
        UPDATE Endereco SET Ativo = 0, DataAtualizacao = GETUTCDATE() WHERE ClienteId = @Id;
        UPDATE Contato SET Ativo = 0, DataAtualizacao = GETUTCDATE() WHERE ClienteId = @Id;";

}
