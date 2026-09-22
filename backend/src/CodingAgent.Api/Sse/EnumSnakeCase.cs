namespace CodingAgent.Api.Sse;

/// <summary>枚举名 → lower_snake_case 字符串助手（如 OpenAiCompatible → "openai_compatible"）。</summary>
public static class EnumSnakeCase
{
    public static string ToSnake(Enum value) => value switch
    {
        Domain.Enums.ProviderType.OpenAiCompatible => "openai_compatible",
        _ => Default(value.ToString()),
    };

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
