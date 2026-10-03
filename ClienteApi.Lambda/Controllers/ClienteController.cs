using ClienteApi.Application.Services;
using ClienteApi.Domain.DTOs;
using ClienteApi.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace ClienteApi.Lambda.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClienteController : ControllerBase
{
    private readonly ClienteService _service;

    public ClienteController(ClienteService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetAllAsync(page, pageSize, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken = default)
    {
        var cliente = await _service.GetByIdAsync(id, cancellationToken);
        return cliente is null ? NotFound() : Ok(cliente);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] ClienteCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var id = await _service.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (ClienteJaCadastradoException exception)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Cliente já cadastrado",
                Detail = exception.Message
            });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] ClienteUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        await _service.UpdateAsync(id, dto, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:int}/endereco")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchEndereco(
        int id,
        [FromBody] EnderecoPatchDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _service.PatchEnderecoAsync(id, dto, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Nenhum campo para atualizar",
                Detail = exception.Message
            });
        }
    }

    [HttpPut("{id:int}/endereco")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplaceEndereco(
        int id,
        [FromBody] EnderecoDto dto,
        CancellationToken cancellationToken = default)
    {
        return await _service.ReplaceEnderecoAsync(id, dto, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpPatch("{id:int}/contatos/{contatoId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchContato(
        int id,
        int contatoId,
        [FromBody] ContatoPatchDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var batchDto = new ContatoBatchPatchDto
            {
                Id = contatoId,
                Tipo = dto.Tipo,
                Valor = dto.Valor,
                Ativo = dto.Ativo
            };

            return await _service.PatchContatosAsync(id, [batchDto], cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Nenhum campo para atualizar",
                Detail = exception.Message
            });
        }
    }

    [HttpPost("{id:int}/contatos")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateContatos(
        int id,
        [FromBody] List<ContatoCreateDto> contatos,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var contatoIds = await _service.InsertContatosAsync(id, contatos, cancellationToken);
            return contatoIds is null
                ? NotFound()
                : CreatedAtAction(nameof(GetById), new { id }, new { contatoIds });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Nenhum contato para cadastrar",
                Detail = exception.Message
            });
        }
    }

    [HttpPatch("{id:int}/contatos")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatchContatos(
        int id,
        [FromBody] List<ContatoBatchPatchDto> contatos,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _service.PatchContatosAsync(id, contatos, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Nenhum contato para atualizar",
                Detail = exception.Message
            });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
