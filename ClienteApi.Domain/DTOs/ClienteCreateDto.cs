using System;
using System.Collections.Generic;

namespace ClienteApi.Domain.DTOs
{
    public record ClienteCreateDto(
        string Nome,
        string Cpf,
        string Rg,
        DateTime DataNascimento,
        string Sexo,
        string NomeMae,
        EnderecoDto Endereco,
        IEnumerable<ContatoCreateDto> Contatos
    );
}
