using System.Text.Json.Serialization;

namespace ClienteApi.Domain.DTOs;

public sealed record ContatoPatchDto
{
    public string? Tipo { get; init; }
    public string? Valor { get; init; }
    public bool? Ativo { get; init; }

    [JsonIgnore]
    public bool HasChanges =>
        Tipo is not null ||
        Valor is not null ||
        Ativo.HasValue;
}
