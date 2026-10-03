using ClienteApi.Application.Services;
using ClienteApi.Domain.DTOs;

namespace ClienteApi.Tests.Application;

public sealed class ClienteServiceTests
{
    private readonly StubClienteRepository _repository = new();
    private ClienteService CreateService() => new(_repository);

    [Fact]
    public async Task GetByIdAsync_DelegatesIdAndReturnsClient()
    {
        var expected = TestData.Cliente();
        _repository.GetById = id => Task.FromResult<ClienteApi.Domain.Entities.Cliente?>(id == 4 ? expected : null);

        Assert.Same(expected, await CreateService().GetByIdAsync(4));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenMissing()
    {
        Assert.Null(await CreateService().GetByIdAsync(99));
    }

    [Fact]
    public async Task GetAllAsync_DelegatesPagingAndReturnsRows()
    {
        var expected = new[] { TestData.Cliente(), TestData.Cliente(2) };
        (int Page, int PageSize) received = default;
        _repository.GetAll = (page, size) =>
        {
            received = (page, size);
            return Task.FromResult<IEnumerable<ClienteApi.Domain.Entities.Cliente>>(expected);
        };

        Assert.Same(expected, await CreateService().GetAllAsync(2, 10));
        Assert.Equal((2, 10), received);
    }

    [Fact]
    public async Task CreateAsync_MapsClientAddressAndContacts()
    {
        var contacts = new[] { new ContatoCreateDto("Email", "a@example.com"), new("Telefone", "123") };
        var id = await CreateService().CreateAsync(TestData.ClienteCreateDto(contacts));

        Assert.Equal(1, id);
        Assert.Equal("Cliente", _repository.CapturedClient!.Nome);
        Assert.Equal("12345678900", _repository.CapturedClient.Cpf);
        Assert.Equal(new DateTime(1988, 3, 3), _repository.CapturedClient.DataNascimento);
        Assert.Equal("Rua", _repository.CapturedAddress!.Rua);
        Assert.Equal("270", _repository.CapturedAddress.Numero);
        Assert.Equal(2, _repository.CapturedContacts.Count);
        Assert.All(_repository.CapturedContacts, contact => Assert.True(contact.Ativo));
        Assert.Equal("Telefone", _repository.CapturedContacts[1].Tipo);
    }

    [Fact]
    public async Task CreateAsync_PropagatesRepositoryFailure()
    {
        _repository.Create = (_, _, _) => Task.FromException<int>(new IOException("failure"));

        await Assert.ThrowsAsync<IOException>(() => CreateService().CreateAsync(TestData.ClienteCreateDto()));
    }

    [Fact]
    public async Task UpdateAsync_MapsAllFieldsAndContactIds()
    {
        _repository.GetById = _ => Task.FromResult<ClienteApi.Domain.Entities.Cliente?>(TestData.Cliente());
        var contacts = new[] { new ContatoDto(8, "Email", "updated@example.com", false), new(null, "Telefone", "456", true) };

        await CreateService().UpdateAsync(5, TestData.ClienteUpdateDto(contacts));

        Assert.Equal(5, _repository.CapturedClient!.Id);
        Assert.Equal("Cliente Atualizado", _repository.CapturedClient.Nome);
        Assert.Equal("RG2", _repository.CapturedClient.Rg);
        Assert.True(_repository.CapturedClient.Ativo);
        Assert.Equal(5, _repository.CapturedAddress!.ClienteId);
        Assert.Equal(8, _repository.CapturedContacts[0].Id);
        Assert.Equal(0, _repository.CapturedContacts[1].Id);
        Assert.All(_repository.CapturedContacts, contact => Assert.Equal(5, contact.ClienteId));
        Assert.False(_repository.CapturedContacts[0].Ativo);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsWhenClientDoesNotExist()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => CreateService().UpdateAsync(99, TestData.ClienteUpdateDto()));
    }

    [Fact]
    public async Task UpdateAsync_PropagatesRepositoryFailure()
    {
        _repository.GetById = _ => Task.FromResult<ClienteApi.Domain.Entities.Cliente?>(TestData.Cliente());
        _repository.Update = (_, _, _) => Task.FromException(new IOException("failure"));

        await Assert.ThrowsAsync<IOException>(
            () => CreateService().UpdateAsync(1, TestData.ClienteUpdateDto()));
    }

    [Theory]
    [InlineData(null, null)]
    public async Task PatchEnderecoAsync_RejectsEmptyChanges(string? rua, string? cidade)
    {
        var dto = new EnderecoPatchDto { Rua = rua, Cidade = cidade };
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().PatchEnderecoAsync(1, dto));
    }

    [Fact]
    public async Task PatchEnderecoAsync_DelegatesAndReturnsRepositoryResult()
    {
        var dto = new EnderecoPatchDto { Cidade = "Campinas" };
        _repository.PatchAddress = (id, passed) => Task.FromResult(id == 8 && ReferenceEquals(dto, passed));

        Assert.True(await CreateService().PatchEnderecoAsync(8, dto));
    }

    [Fact]
    public async Task ReplaceEnderecoAsync_MapsAddressAndReturnsResult()
    {
        _repository.ReplaceAddress = _ => Task.FromResult(false);

        Assert.False(await CreateService().ReplaceEnderecoAsync(6, TestData.EnderecoDto("270A")));
        Assert.Equal(6, _repository.CapturedAddress!.ClienteId);
        Assert.Equal("270A", _repository.CapturedAddress.Numero);
    }

    [Fact]
    public async Task ReplaceEnderecoAsync_PropagatesRepositoryFailure()
    {
        _repository.ReplaceAddress = _ => Task.FromException<bool>(new IOException("failure"));

        await Assert.ThrowsAsync<IOException>(
            () => CreateService().ReplaceEnderecoAsync(1, TestData.EnderecoDto()));
    }

    [Fact]
    public async Task InsertContatosAsync_RejectsEmptyCollection()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().InsertContatosAsync(1, []));
    }

    [Fact]
    public async Task InsertContatosAsync_MapsContactsAndReturnsIds()
    {
        var ids = await CreateService().InsertContatosAsync(
            12,
            [new("Email", "a@example.com"), new("Telefone", "123")]);

        Assert.Equal([1, 2], ids);
        Assert.Equal(2, _repository.CapturedContacts.Count);
        Assert.All(_repository.CapturedContacts, contact => Assert.True(contact.Ativo));
        Assert.Equal("Email", _repository.CapturedContacts[0].Tipo);
    }

    [Fact]
    public async Task InsertContatosAsync_ReturnsNullWhenClientIsMissing()
    {
        _repository.InsertContacts = (_, _) => Task.FromResult<IReadOnlyList<int>?>(null);

        Assert.Null(await CreateService().InsertContatosAsync(99, [new("Email", "a@example.com")]));
    }

    [Fact]
    public async Task InsertContatosAsync_PropagatesRepositoryFailure()
    {
        _repository.InsertContacts = (_, _) => Task.FromException<IReadOnlyList<int>?>(new IOException("failure"));

        await Assert.ThrowsAsync<IOException>(
            () => CreateService().InsertContatosAsync(1, [new("Email", "a@example.com")]));
    }

    [Fact]
    public async Task PatchContatosAsync_RejectsEmptyCollection()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().PatchContatosAsync(1, []));
    }

    [Fact]
    public async Task PatchContatosAsync_RejectsItemWithoutChanges()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().PatchContatosAsync(1, [new() { Id = 1 }]));
    }

    [Fact]
    public async Task PatchContatosAsync_RejectsInvalidId()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().PatchContatosAsync(1, [new() { Id = 0, Valor = "value" }]));
    }

    [Fact]
    public async Task PatchContatosAsync_RejectsDuplicateIds()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().PatchContatosAsync(1,
                [new() { Id = 1, Valor = "one" }, new() { Id = 1, Ativo = false }]));
    }

    [Fact]
    public async Task PatchContatosAsync_DelegatesValidBatch()
    {
        var updates = new[] { new ContatoBatchPatchDto { Id = 1, Valor = "updated" } };

        Assert.True(await CreateService().PatchContatosAsync(8, updates));
        Assert.Same(updates, _repository.CapturedContactPatches);
    }

    [Fact]
    public async Task PatchContatosAsync_PropagatesRepositoryFailure()
    {
        _repository.PatchContacts = (_, _) => Task.FromException<bool>(new IOException("failure"));

        await Assert.ThrowsAsync<IOException>(
            () => CreateService().PatchContatosAsync(1, [new() { Id = 1, Valor = "updated" }]));
    }

    [Fact]
    public async Task DeleteAsync_DelegatesId()
    {
        var received = 0;
        _repository.Delete = id => { received = id; return Task.CompletedTask; };

        await CreateService().DeleteAsync(11);

        Assert.Equal(11, received);
    }
}
