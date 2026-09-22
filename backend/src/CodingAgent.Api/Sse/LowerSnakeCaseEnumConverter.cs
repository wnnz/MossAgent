using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodingAgent.Api.Sse;

/// <summary>枚举 → lower_snake_case 字符串（如 OpenAiCompatible → "openai_compatible"，Socks5 → "socks5"）。</summary>
public sealed class LowerSnakeCaseEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(LowerSnakeCaseEnumConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}

public sealed class LowerSnakeCaseEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            foreach (var name in Enum.GetNames<T>())
            {
                if (ToSnakeCase(name).Equals(text, StringComparison.OrdinalIgnoreCase))
                {
                    return Enum.Parse<T>(name);
                }
            }
            throw new JsonException($"无法解析枚举值: {text}");
        }
        if (reader.TokenType == JsonTokenType.Number)
        {
            var underlying = Enum.GetUnderlyingType(typeof(T));
            return underlying == typeof(int)
                ? (T)Enum.ToObject(typeof(T), reader.GetInt32())
                : throw new JsonException("不支持的枚举底层类型");
        }
        throw new JsonException("枚举值必须是字符串或数字");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ToSnakeCase(value.ToString()));

    public static string ToSnakeCase(string name) => name == "OpenAiCompatible" ? "openai_compatible" : Default(name);

    private static string Default(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('_');
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
