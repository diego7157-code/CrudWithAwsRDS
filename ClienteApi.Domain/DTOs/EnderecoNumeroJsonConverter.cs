using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClienteApi.Domain.DTOs;

public sealed class EnderecoNumeroJsonConverter : JsonConverter<string>
{
    public override string Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString()!;

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var number))
            return number.ToString(CultureInfo.InvariantCulture);

        throw new JsonException("O campo numero deve ser um texto ou um número inteiro.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        string value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
