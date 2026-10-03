using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClienteApi.Lambda.Json;

public sealed class BrazilianDateTimeConverter : JsonConverter<DateTime>
{
    private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");

    public override DateTime Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(
                "Data inválida. Informe no formato dd/MM/yyyy ou ISO 8601 (yyyy-MM-dd).");
        }

        var value = reader.GetString();

        if (value is not null &&
            DateTime.TryParseExact(
                value,
                "dd/MM/yyyy",
                BrazilianCulture,
                DateTimeStyles.None,
                out var brazilianDate))
        {
            return brazilianDate;
        }

        if (value is not null &&
            DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var invariantDate))
        {
            return invariantDate;
        }

        throw new JsonException(
            "Data inválida. Informe no formato dd/MM/yyyy ou ISO 8601 (yyyy-MM-dd).");
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateTime value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
}
