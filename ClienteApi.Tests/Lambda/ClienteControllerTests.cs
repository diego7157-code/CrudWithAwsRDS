using ClienteApi.Application.Services;
using ClienteApi.Domain.DTOs;
using ClienteApi.Domain.Exceptions;
using ClienteApi.Lambda.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace ClienteApi.Tests.Lambda;

public sealed class ClienteControllerTests
{
    private readonly StubClienteRepository _repository = new();
    private ClienteController CreateController() => new(new ClienteService(_repository));

    [Fact]
    public async Task GetAll_ReturnsOkWithClients()
    {
        var clients = new[] { TestData.Cliente() };
        _repository.GetAll = (_, _) => Task.FromResult<IEnumerable<ClienteApi.Domain.Entities.Cliente>>(clients);

        var result = await CreateController().GetAll(3, 5);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(clients, ok.Value);
    }

    [Fact]
    public async Task GetById_ReturnsOkWhenFound()
    {
        var client = TestData.Cliente();
        _repository.GetById = _ => Task.FromResult<ClienteApi.Domain.Entities.Cliente?>(client);

        var result = await CreateController().GetById(1);

        Assert.Same(client, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundWhenMissing()
    {
        Assert.IsType<NotFoundResult>(await CreateController().GetById(42));
    }

    [Fact]
    public async Task GetById_ForwardsRequestCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();

        await CreateController().GetById(1, cancellation.Token);

        Assert.Equal(cancellation.Token, _repository.LastCancellationToken);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction()
    {
        _repository.Create = (_, _, _) => Task.FromResult(15);

        var result = await CreateController().Create(TestData.ClienteCreateDto());

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(ClienteController.GetById), created.ActionName);
        Assert.Equal(15, created.RouteValues!["id"]);
    }

    [Fact]
    public async Task Create_ReturnsConflictForDuplicateClient()
    {
        _repository.Create = (_, _, _) =>
            Task.FromException<int>(new ClienteJaCadastradoException());

        var result = await CreateController().Create(TestData.ClienteCreateDto());

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(409, problem.Status);
        Assert.Equal("Cliente já cadastrado", problem.Title);
    }

    [Fact]
    public async Task Create_PropagatesUnexpectedErrors()
    {
        _repository.Create = (_, _, _) => Task.FromException<int>(new IOException("database unavailable"));

        await Assert.ThrowsAsync<IOException>(
            () => CreateController().Create(TestData.ClienteCreateDto()));
    }

    [Fact]
    public async Task Update_ReturnsNoContent()
    {
        _repository.GetById = _ => Task.FromResult<ClienteApi.Domain.Entities.Cliente?>(TestData.Cliente());

        Assert.IsType<NoContentResult>(
            await CreateController().Update(1, TestData.ClienteUpdateDto()));
    }

    [Fact]
    public async Task Update_PropagatesMissingClient()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => CreateController().Update(42, TestData.ClienteUpdateDto()));
    }

    [Fact]
    public async Task PatchEndereco_ReturnsNoContent()
    {
        Assert.IsType<NoContentResult>(
            await CreateController().PatchEndereco(1, new EnderecoPatchDto { Cidade = "Campinas" }));
    }

    [Fact]
    public async Task PatchEndereco_ReturnsNotFoundWhenRepositoryReportsMissingAddress()
    {
        _repository.PatchAddress = (_, _) => Task.FromResult(false);

        Assert.IsType<NotFoundResult>(
            await CreateController().PatchEndereco(1, new EnderecoPatchDto { Cidade = "Campinas" }));
    }

    [Fact]
    public async Task PatchEndereco_ReturnsBadRequestForEmptyPatch()
    {
        var result = await CreateController().PatchEndereco(1, new EnderecoPatchDto());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, Assert.IsType<ProblemDetails>(badRequest.Value).Status);
    }

    [Fact]
    public async Task ReplaceEndereco_ReturnsNoContent()
    {
        Assert.IsType<NoContentResult>(
            await CreateController().ReplaceEndereco(1, TestData.EnderecoDto()));
    }

    [Fact]
    public async Task ReplaceEndereco_ReturnsNotFound()
    {
        _repository.ReplaceAddress = _ => Task.FromResult(false);

        Assert.IsType<NotFoundResult>(
            await CreateController().ReplaceEndereco(1, TestData.EnderecoDto()));
    }

    [Fact]
    public async Task PatchContato_ReturnsNoContent()
    {
        Assert.IsType<NoContentResult>(
            await CreateController().PatchContato(1, 7, new ContatoPatchDto { Valor = "updated" }));
        Assert.Equal(7, Assert.Single(_repository.CapturedContactPatches!).Id);
    }

    [Fact]
    public async Task PatchContato_ReturnsNotFound()
    {
        _repository.PatchContacts = (_, _) => Task.FromResult(false);

        Assert.IsType<NotFoundResult>(
            await CreateController().PatchContato(1, 7, new ContatoPatchDto { Valor = "updated" }));
    }

    [Fact]
    public async Task PatchContato_ReturnsBadRequestForEmptyPatch()
    {
        Assert.IsType<BadRequestObjectResult>(
            await CreateController().PatchContato(1, 7, new ContatoPatchDto()));
    }

    [Fact]
    public async Task CreateContatos_ReturnsCreatedAndIds()
    {
        _repository.InsertContacts = (_, contacts) =>
            Task.FromResult<IReadOnlyList<int>?>(Enumerable.Range(10, contacts.Count).ToArray());

        var result = await CreateController().CreateContatos(1, [new("Email", "a@example.com")]);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(ClienteController.GetById), created.ActionName);
        Assert.Equal(10, Assert.IsType<int[]>(created.Value!.GetType()
            .GetProperty("contatoIds")!.GetValue(created.Value))[0]);
    }

    [Fact]
    public async Task CreateContatos_ReturnsNotFoundForMissingClient()
    {
        _repository.InsertContacts = (_, _) => Task.FromResult<IReadOnlyList<int>?>(null);

        Assert.IsType<NotFoundResult>(
            await CreateController().CreateContatos(42, [new("Email", "a@example.com")]));
    }

    [Fact]
    public async Task CreateContatos_ReturnsBadRequestForEmptyList()
    {
        Assert.IsType<BadRequestObjectResult>(await CreateController().CreateContatos(1, []));
    }

    [Fact]
    public async Task PatchContatos_ReturnsNoContent()
    {
        Assert.IsType<NoContentResult>(
            await CreateController().PatchContatos(1, [new() { Id = 3, Valor = "updated" }]));
    }

    [Fact]
    public async Task PatchContatos_ReturnsNotFoundForUnmatchedItems()
    {
        _repository.PatchContacts = (_, _) => Task.FromResult(false);

        Assert.IsType<NotFoundResult>(
            await CreateController().PatchContatos(1, [new() { Id = 3, Valor = "updated" }]));
    }

    [Fact]
    public async Task PatchContatos_ReturnsBadRequestForInvalidBatch()
    {
        Assert.IsType<BadRequestObjectResult>(
            await CreateController().PatchContatos(1, [new() { Id = 0 }]));
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        Assert.IsType<NoContentResult>(await CreateController().Delete(1));
    }
}
