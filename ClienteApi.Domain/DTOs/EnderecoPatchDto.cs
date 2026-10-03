using System.Text.Json.Serialization;

namespace ClienteApi.Domain.DTOs;

public sealed record EnderecoPatchDto
{
    public string? Rua { get; init; }

    [JsonConverter(typeof(EnderecoNumeroJsonConverter))]
    public string? Numero { get; init; }
    public string? Complemento { get; init; }
    public string? Bairro { get; init; }
    public string? Cidade { get; init; }
    public string? Estado { get; init; }
    public string? Pais { get; init; }
    public string? CodigoPostal { get; init; }

    [JsonIgnore]
    public bool HasChanges =>
        Rua is not null ||
        Numero is not null ||
        Complemento is not null ||
        Bairro is not null ||
        Cidade is not null ||
        Estado is not null ||
        Pais is not null ||
        CodigoPostal is not null;
}
