using System;
using System.Collections.Generic;

namespace ClienteApi.Domain.DTOs
{
    public record ClienteUpdateDto(
        string Nome,
        string Rg,
        DateTime DataNascimento,
        string Sexo,
        string NomeMae,
        bool Ativo,
        EnderecoDto Endereco,
        IEnumerable<ContatoDto> Contatos
    );
}
