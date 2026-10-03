using System.Text.Json.Serialization;

namespace ClienteApi.Domain.Entities;

public class Endereco
{
    [JsonIgnore]
    public int Id { get; set; }

    [JsonIgnore]
    public int ClienteId { get; set; }
    public string Rua { get; set; }
    public string Numero { get; set; }
    public string Complemento { get; set; }
    public string Bairro { get; set; }
    public string Cidade { get; set; }
    public string Estado { get; set; }
    public string Pais { get; set; }
    public string CodigoPostal { get; set; }
    public bool Ativo { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}