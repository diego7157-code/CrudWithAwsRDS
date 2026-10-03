namespace ClienteApi.Infrastructure.Queries;

public static class ContatoQueries
{
    public const int BatchSize = 500;

    public static string BuildInsertMany(int count)
    {
        var values = string.Join(
            ", ",
            Enumerable.Range(0, count).Select(index =>
                $"(@ClienteId, @Tipo{index}, @Valor{index}, 1, GETUTCDATE(), GETUTCDATE())"));

        return $@"
            SET NOCOUNT ON;
            IF NOT EXISTS (SELECT 1 FROM Cliente WHERE Id = @ClienteId AND Ativo = 1)
            BEGIN
                SELECT CAST(0 AS BIT) AS ClientExists, CAST(0 AS INT) AS Id;
            END
            ELSE
            BEGIN
                INSERT INTO Contato (ClienteId, Tipo, Valor, Ativo, DataCriacao, DataAtualizacao)
                OUTPUT CAST(1 AS BIT) AS ClientExists, INSERTED.Id
                VALUES {values};
            END";
    }

    public static string BuildPatchMany(int count)
    {
        var values = string.Join(
            ", ",
            Enumerable.Range(0, count).Select(index =>
                $"(@Id{index}, @Tipo{index}, @Valor{index}, @Ativo{index})"));

        return $@"
            SET NOCOUNT ON;
            UPDATE target SET
                Tipo = COALESCE(source.Tipo, target.Tipo),
                Valor = COALESCE(source.Valor, target.Valor),
                Ativo = COALESCE(source.Ativo, target.Ativo),
                DataAtualizacao = GETUTCDATE()
            FROM Contato AS target
            INNER JOIN (VALUES {values}) AS source (Id, Tipo, Valor, Ativo)
                ON target.Id = source.Id
            WHERE target.ClienteId = @ClienteId AND target.Ativo = 1;
            SELECT @@ROWCOUNT;";
    }

    public static string BuildUpsertMany(int count)
    {
        var values = string.Join(
            ", ",
            Enumerable.Range(0, count).Select(index =>
                $"(@Id{index}, @Tipo{index}, @Valor{index}, @Ativo{index})"));

        return $@"
            SET NOCOUNT ON;
            MERGE Contato AS target
            USING (VALUES {values}) AS source (Id, Tipo, Valor, Ativo)
                ON target.Id = source.Id AND target.ClienteId = @ClienteId
            WHEN MATCHED THEN
                UPDATE SET
                    Tipo = source.Tipo,
                    Valor = source.Valor,
                    Ativo = source.Ativo,
                    DataAtualizacao = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (ClienteId, Tipo, Valor, Ativo, DataCriacao, DataAtualizacao)
                VALUES (@ClienteId, source.Tipo, source.Valor, source.Ativo, GETUTCDATE(), GETUTCDATE());
            SELECT @@ROWCOUNT;";
    }
}
