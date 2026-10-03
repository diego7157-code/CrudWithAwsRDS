using System.Text.Json.Serialization;

namespace ClienteApi.Domain.DTOs
{
    public record EnderecoDto(
        string Rua,
        [property: JsonConverter(typeof(EnderecoNumeroJsonConverter))]
        string Numero,
        string Complemento,
        string Bairro,
        string Cidade,
        string Estado,
        string Pais,
        string CodigoPostal
    );
}
