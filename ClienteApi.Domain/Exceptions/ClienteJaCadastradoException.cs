namespace ClienteApi.Domain.Exceptions;

public sealed class ClienteJaCadastradoException : Exception
{
    public ClienteJaCadastradoException()
        : base("Já existe um cliente cadastrado com esse CPF.")
    {
    }

    public ClienteJaCadastradoException(Exception innerException)
        : base("Já existe um cliente cadastrado com esse CPF.", innerException)
    {
    }
}
