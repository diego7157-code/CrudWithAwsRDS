using System.Text.Json.Serialization;

namespace ClienteApi.Domain.Entities;

public class Contato
{
    public int Id { get; set; }

    [JsonIgnore]
    public int ClienteId { get; set; }
    public string Tipo { get; set; }
    public string Valor { get; set; }
    public bool Ativo { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}