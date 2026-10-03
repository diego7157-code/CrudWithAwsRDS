namespace ClienteApi.Domain.DTOs
{
    public record ContatoDto(
        int? Id,
        string Tipo,
        string Valor,
        bool Ativo
    );
}
