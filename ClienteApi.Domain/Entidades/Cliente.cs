namespace ClienteApi.Domain.Entities;

public class Cliente
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public string Cpf { get; set; }
    public string Rg { get; set; }
    public DateTime DataNascimento { get; set; }
    public string Sexo { get; set; }
    public string NomeMae { get; set; }
    public bool Ativo { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
    public Endereco Endereco { get; set; }
    public IEnumerable<Contato> Contatos { get; set; }
}
