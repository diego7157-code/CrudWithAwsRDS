namespace ClienteApi.Infrastructure.Queries;

public static class EnderecoQueries
{
    public const string Insert = @"
        INSERT INTO Endereco (ClienteId, Rua, Numero, Complemento, Bairro, Cidade, Estado, Pais, CodigoPostal, Ativo, DataCriacao, DataAtualizacao)
        VALUES (@ClienteId, @Rua, @Numero, @Complemento, @Bairro, @Cidade, @Estado, @Pais, @CodigoPostal, 1, GETUTCDATE(), GETUTCDATE())";

    public const string Update = @"
        UPDATE Endereco SET
            Rua = @Rua, Numero = @Numero, Complemento = @Complemento,
            Bairro = @Bairro, Cidade = @Cidade, Estado = @Estado,
            Pais = @Pais, CodigoPostal = @CodigoPostal,
            DataAtualizacao = GETUTCDATE()
        WHERE ClienteId = @ClienteId AND Ativo = 1";

    public const string Patch = @"
        SET NOCOUNT ON;
        UPDATE Endereco SET
            Rua = COALESCE(@Rua, Rua),
            Numero = COALESCE(@Numero, Numero),
            Complemento = COALESCE(@Complemento, Complemento),
            Bairro = COALESCE(@Bairro, Bairro),
            Cidade = COALESCE(@Cidade, Cidade),
            Estado = COALESCE(@Estado, Estado),
            Pais = COALESCE(@Pais, Pais),
            CodigoPostal = COALESCE(@CodigoPostal, CodigoPostal),
            DataAtualizacao = GETUTCDATE()
        WHERE ClienteId = @ClienteId AND Ativo = 1;
        SELECT CAST(CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS BIT);";

    public const string Replace = @"
        SET NOCOUNT ON;
        IF EXISTS (SELECT 1 FROM Cliente WHERE Id = @ClienteId AND Ativo = 1)
        BEGIN
            UPDATE Endereco
            SET Ativo = 0, DataAtualizacao = GETUTCDATE()
            WHERE ClienteId = @ClienteId AND Ativo = 1;

            INSERT INTO Endereco
                (ClienteId, Rua, Numero, Complemento, Bairro, Cidade, Estado, Pais, CodigoPostal, Ativo, DataCriacao, DataAtualizacao)
            VALUES
                (@ClienteId, @Rua, @Numero, @Complemento, @Bairro, @Cidade, @Estado, @Pais, @CodigoPostal, 1, GETUTCDATE(), GETUTCDATE());

            SELECT CAST(1 AS BIT);
        END
        ELSE
        BEGIN
            SELECT CAST(0 AS BIT);
        END";
}
