using ClienteApi.Domain.DTOs;
using ClienteApi.Domain.Entities;
using ClienteApi.Domain.Interfaces;

namespace ClienteApi.Tests;

internal static class TestData
{
    public static EnderecoDto EnderecoDto(string numero = "270") =>
        new("Rua", numero, "Casa", "Centro", "Campinas", "SP", "BR", "13000000");

    public static ClienteCreateDto ClienteCreateDto(IEnumerable<ContatoCreateDto>? contatos = null) =>
        new("Cliente", "12345678900", "RG1", new DateTime(1988, 3, 3), "M", "Mae",
            EnderecoDto(), contatos ?? [new("Email", "a@example.com")]);

    public static ClienteUpdateDto ClienteUpdateDto(IEnumerable<ContatoDto>? contatos = null) =>
        new("Cliente Atualizado", "RG2", new DateTime(1988, 3, 3), "F", "Mae Atualizada", true,
            EnderecoDto(), contatos ?? [new(7, "Email", "b@example.com", true)]);

    public static Cliente Cliente(int id = 1) => new()
    {
        Id = id, Nome = "Cliente", Cpf = "12345678900", Rg = "RG1",
        DataNascimento = new DateTime(1988, 3, 3), Sexo = "M", NomeMae = "Mae",
        Ativo = true, Endereco = Endereco(), Contatos = [Contato()]
    };

    public static Endereco Endereco() => new()
    {
        Rua = "Rua", Numero = "270", Complemento = "Casa", Bairro = "Centro",
        Cidade = "Campinas", Estado = "SP", Pais = "BR", CodigoPostal = "13000000"
    };

    public static Contato Contato(int id = 7) => new()
    {
        Id = id, ClienteId = 1, Tipo = "Email", Valor = "a@example.com", Ativo = true
    };
}

internal sealed class StubClienteRepository : IClienteRepository
{
    public Func<int, Task<Cliente?>> GetById { get; set; } = _ => Task.FromResult<Cliente?>(null);
    public Func<int, int, Task<IEnumerable<Cliente>>> GetAll { get; set; } =
        (_, _) => Task.FromResult<IEnumerable<Cliente>>([]);
    public Func<Cliente, Endereco, IEnumerable<Contato>, Task<int>> Create { get; set; } =
        (_, _, _) => Task.FromResult(1);
    public Func<Cliente, Endereco, IEnumerable<Contato>, Task> Update { get; set; } =
        (_, _, _) => Task.CompletedTask;
    public Func<int, EnderecoPatchDto, Task<bool>> PatchAddress { get; set; } =
        (_, _) => Task.FromResult(true);
    public Func<Endereco, Task<bool>> ReplaceAddress { get; set; } = _ => Task.FromResult(true);
    public Func<int, IReadOnlyCollection<Contato>, Task<IReadOnlyList<int>?>> InsertContacts { get; set; } =
        (_, contacts) => Task.FromResult<IReadOnlyList<int>?>(Enumerable.Range(1, contacts.Count).ToArray());
    public Func<int, IReadOnlyCollection<ContatoBatchPatchDto>, Task<bool>> PatchContacts { get; set; } =
        (_, _) => Task.FromResult(true);
    public Func<int, Task> Delete { get; set; } = _ => Task.CompletedTask;

    public Cliente? CapturedClient { get; private set; }
    public Endereco? CapturedAddress { get; private set; }
    public IReadOnlyList<Contato> CapturedContacts { get; private set; } = [];
    public IReadOnlyCollection<ContatoBatchPatchDto>? CapturedContactPatches { get; private set; }
    public CancellationToken LastCancellationToken { get; private set; }

    public Task<Cliente?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return GetById(id);
    }

    public Task<IEnumerable<Cliente>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return GetAll(page, pageSize);
    }

    public Task<int> CreateAsync(
        Cliente client,
        Endereco address,
        IEnumerable<Contato> contacts,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        CapturedClient = client;
        CapturedAddress = address;
        CapturedContacts = contacts.ToArray();
        return Create(client, address, CapturedContacts);
    }

    public Task UpdateAsync(
        Cliente client,
        Endereco address,
        IEnumerable<Contato> contacts,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        CapturedClient = client;
        CapturedAddress = address;
        CapturedContacts = contacts.ToArray();
        return Update(client, address, CapturedContacts);
    }

    public Task<bool> PatchEnderecoAsync(
        int clientId,
        EnderecoPatchDto dto,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return PatchAddress(clientId, dto);
    }

    public Task<bool> ReplaceEnderecoAsync(Endereco address, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        CapturedAddress = address;
        return ReplaceAddress(address);
    }

    public Task<IReadOnlyList<int>?> InsertContatosAsync(
        int clientId,
        IReadOnlyCollection<Contato> contacts,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        CapturedContacts = contacts.ToArray();
        return InsertContacts(clientId, contacts);
    }

    public Task<bool> PatchContatosAsync(
        int clientId,
        IReadOnlyCollection<ContatoBatchPatchDto> contacts,
        CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        CapturedContactPatches = contacts;
        return PatchContacts(clientId, contacts);
    }

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        LastCancellationToken = cancellationToken;
        return Delete(id);
    }
}
