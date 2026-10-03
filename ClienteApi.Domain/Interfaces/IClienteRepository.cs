using ClienteApi.Domain.DTOs;
using ClienteApi.Domain.Entities;

namespace ClienteApi.Domain.Interfaces;

public interface IClienteRepository
{
    Task<Cliente?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Cliente>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Cliente cliente, Endereco endereco, IEnumerable<Contato> contatos, CancellationToken cancellationToken = default);
    Task UpdateAsync(Cliente cliente, Endereco endereco, IEnumerable<Contato> contatos, CancellationToken cancellationToken = default);
    Task<bool> PatchEnderecoAsync(int clienteId, EnderecoPatchDto dto, CancellationToken cancellationToken = default);
    Task<bool> ReplaceEnderecoAsync(Endereco endereco, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>?> InsertContatosAsync(int clienteId, IReadOnlyCollection<Contato> contatos, CancellationToken cancellationToken = default);
    Task<bool> PatchContatosAsync(int clienteId, IReadOnlyCollection<ContatoBatchPatchDto> contatos, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
