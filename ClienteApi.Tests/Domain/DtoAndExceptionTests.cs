using System.Text.Json;
using ClienteApi.Domain.DTOs;
using ClienteApi.Domain.Exceptions;
using ClienteApi.Lambda.Json;

namespace ClienteApi.Tests.Domain;

public sealed class DtoAndExceptionTests
{
    [Fact]
    public void ClienteJaCadastradoException_HasExpectedMessageAndInnerException()
    {
        var inner = new InvalidOperationException("duplicate");

        var plain = new ClienteJaCadastradoException();
        var wrapped = new ClienteJaCadastradoException(inner);

        Assert.Contains("CPF", plain.Message);
        Assert.Same(inner, wrapped.InnerException);
    }

    [Fact]
    public void EnderecoPatchDto_HasChangesWhenAnyFieldIsPresent()
    {
        Assert.True(new EnderecoPatchDto { Numero = "270" }.HasChanges);
        Assert.True(new EnderecoPatchDto { Complemento = "" }.HasChanges);
        Assert.False(new EnderecoPatchDto().HasChanges);
    }

    [Fact]
    public void ContatoPatchDto_HasChangesForValueOrActiveFalse()
    {
        Assert.True(new ContatoPatchDto { Valor = "x" }.HasChanges);
        Assert.True(new ContatoPatchDto { Ativo = false }.HasChanges);
        Assert.False(new ContatoPatchDto().HasChanges);
    }

    [Fact]
    public void ContatoBatchPatchDto_HasChangesForAnyMutableField()
    {
        Assert.True(new ContatoBatchPatchDto { Tipo = "Email" }.HasChanges);
        Assert.True(new ContatoBatchPatchDto { Valor = "x" }.HasChanges);
        Assert.True(new ContatoBatchPatchDto { Ativo = false }.HasChanges);
        Assert.False(new ContatoBatchPatchDto().HasChanges);
    }

    [Theory]
    [InlineData("\"03/03/1988\"", 1988, 3, 3)]
    [InlineData("\"1988-03-03T00:00:00\"", 1988, 3, 3)]
    public void BrazilianDateTimeConverter_ReadsBrazilianAndIsoDates(
        string json, int year, int month, int day)
    {
        var value = JsonSerializer.Deserialize<DateTime>(json, ConverterOptions());

        Assert.Equal(new DateTime(year, month, day), value);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("3")]
    [InlineData("\"not-a-date\"")]
    public void BrazilianDateTimeConverter_RejectsInvalidTokensAndDates(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DateTime>(json, ConverterOptions()));
    }

    [Fact]
    public void BrazilianDateTimeConverter_WritesRoundTripIsoFormat()
    {
        var value = new DateTime(1988, 3, 3, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal("\"1988-03-03T00:00:00.0000000Z\"",
            JsonSerializer.Serialize(value, ConverterOptions()));
    }

    [Theory]
    [InlineData("\"270\"", "270")]
    [InlineData("270", "270")]
    public void EnderecoNumeroJsonConverter_ReadsStringAndInteger(string json, string expected)
    {
        var dto = JsonSerializer.Deserialize<EnderecoDto>(
            $$"""{"rua":"Rua","numero":{{json}},"complemento":"","bairro":"Centro","cidade":"Campinas","estado":"SP","pais":"BR","codigoPostal":"00000"}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal(expected, dto!.Numero);
    }

    [Fact]
    public void EnderecoNumeroJsonConverter_WritesNumberAsString()
    {
        var json = JsonSerializer.Serialize(TestData.EnderecoDto());

        Assert.Contains("\"Numero\":\"270\"", json);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("true")]
    public void EnderecoNumeroJsonConverter_RejectsNonIntegerNonStringValues(string value)
    {
        var json = $$"""{"rua":"Rua","numero":{{value}},"complemento":"","bairro":"Centro","cidade":"Campinas","estado":"SP","pais":"BR","codigoPostal":"00000"}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EnderecoDto>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
    }

    private static JsonSerializerOptions ConverterOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new BrazilianDateTimeConverter());
        return options;
    }
}
