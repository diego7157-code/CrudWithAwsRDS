using ClienteApi.Domain.DTOs;
using ClienteApi.Domain.Entities;
using ClienteApi.Domain.Interfaces;

namespace ClienteApi.Application.Services;

public class ClienteService
{
    private readonly IClienteRepository _repository;

    public ClienteService(IClienteRepository repository)
    {
        _repository = repository;
    }

    public Task<Cliente?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public Task<IEnumerable<Cliente>> GetAllAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(page, pageSize, cancellationToken);

    public async Task<int> CreateAsync(
        ClienteCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        var cliente = new Cliente
        {
            Nome = dto.Nome,
            Cpf = dto.Cpf,
            Rg = dto.Rg,
            DataNascimento = dto.DataNascimento,
            Sexo = dto.Sexo,
            NomeMae = dto.NomeMae
        };

        var endereco = new Endereco
        {
            Rua = dto.Endereco.Rua,
            Numero = dto.Endereco.Numero,
            Complemento = dto.Endereco.Complemento,
            Bairro = dto.Endereco.Bairro,
            Cidade = dto.Endereco.Cidade,
            Estado = dto.Endereco.Estado,
            Pais = dto.Endereco.Pais,
            CodigoPostal = dto.Endereco.CodigoPostal
        };

        var contatos = dto.Contatos.Select(contato => new Contato
        {
            Tipo = contato.Tipo,
            Valor = contato.Valor,
            Ativo = true
        }).ToArray();

        return await _repository.CreateAsync(cliente, endereco, contatos, cancellationToken);
    }

    public async Task UpdateAsync(
        int id,
        ClienteUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        _ = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Cliente não encontrado.");

        var cliente = new Cliente
        {
            Id = id,
            Nome = dto.Nome,
            Rg = dto.Rg,
            DataNascimento = dto.DataNascimento,
            Sexo = dto.Sexo,
            NomeMae = dto.NomeMae,
            Ativo = dto.Ativo
        };

        var endereco = new Endereco
        {
            ClienteId = id,
            Rua = dto.Endereco.Rua,
            Numero = dto.Endereco.Numero,
            Complemento = dto.Endereco.Complemento,
            Bairro = dto.Endereco.Bairro,
            Cidade = dto.Endereco.Cidade,
            Estado = dto.Endereco.Estado,
            Pais = dto.Endereco.Pais,
            CodigoPostal = dto.Endereco.CodigoPostal
        };

        var contatos = dto.Contatos.Select(contato => new Contato
        {
            Id = contato.Id ?? 0,
            ClienteId = id,
            Tipo = contato.Tipo,
            Valor = contato.Valor,
            Ativo = contato.Ativo
        });

        await _repository.UpdateAsync(cliente, endereco, contatos, cancellationToken);
    }

    public Task<bool> PatchEnderecoAsync(
        int clienteId,
        EnderecoPatchDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!dto.HasChanges)
            throw new ArgumentException("Informe ao menos um campo de endereço para atualizar.");

        return _repository.PatchEnderecoAsync(clienteId, dto, cancellationToken);
    }

    public Task<bool> ReplaceEnderecoAsync(
        int clienteId,
        EnderecoDto dto,
        CancellationToken cancellationToken = default)
    {
        var endereco = new Endereco
        {
            ClienteId = clienteId,
            Rua = dto.Rua,
            Numero = dto.Numero,
            Complemento = dto.Complemento,
            Bairro = dto.Bairro,
            Cidade = dto.Cidade,
            Estado = dto.Estado,
            Pais = dto.Pais,
            CodigoPostal = dto.CodigoPostal
        };

        return _repository.ReplaceEnderecoAsync(endereco, cancellationToken);
    }

    public async Task<IReadOnlyList<int>?> InsertContatosAsync(
        int clienteId,
        IReadOnlyCollection<ContatoCreateDto> dtos,
        CancellationToken cancellationToken = default)
    {
        if (dtos.Count == 0)
            throw new ArgumentException("Informe ao menos um contato para cadastrar.");

        var contatos = dtos.Select(dto => new Contato
        {
            Tipo = dto.Tipo,
            Valor = dto.Valor,
            Ativo = true
        }).ToArray();

        return await _repository.InsertContatosAsync(clienteId, contatos, cancellationToken);
    }

    public Task<bool> PatchContatosAsync(
        int clienteId,
        IReadOnlyCollection<ContatoBatchPatchDto> dtos,
        CancellationToken cancellationToken = default)
    {
        if (dtos.Count == 0)
            throw new ArgumentException("Informe ao menos um contato para atualizar.");

        if (dtos.Any(dto => !dto.HasChanges))
            throw new ArgumentException("Cada contato deve conter ao menos um campo para atualizar.");

        if (dtos.Any(dto => dto.Id <= 0))
            throw new ArgumentException("Cada contato deve informar um ID válido.");

        if (dtos.Select(dto => dto.Id).Distinct().Count() != dtos.Count)
            throw new ArgumentException("A lista contém IDs de contato duplicados.");

        return _repository.PatchContatosAsync(clienteId, dtos, cancellationToken);
    }

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, cancellationToken);
}
