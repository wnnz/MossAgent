using System.Text.Json;

namespace MossAgent.Tools.BuiltIn.Execution;

internal static class ToolInputSchemaValidator
{
    private static readonly HashSet<string> SupportedTypes =
        ["object", "array", "string", "number", "integer", "boolean", "null"];

    public static bool TryValidate(
        string schemaJson,
        JsonElement value,
        out string error,
        out bool schemaInvalid)
    {
        try
        {
            using var document = JsonDocument.Parse(schemaJson);
            schemaInvalid = false;
            return ValidateNode(document.RootElement, value, "$", out error);
        }
        catch (JsonException exception)
        {
            error = $"工具参数架构不是有效 JSON：{exception.Message}";
            schemaInvalid = true;
            return false;
        }
        catch (InvalidDataException exception)
        {
            error = exception.Message;
            schemaInvalid = true;
            return false;
        }
    }

    private static bool ValidateNode(
        JsonElement schema,
        JsonElement value,
        string path,
        out string error)
    {
        EnsureSchemaObject(schema, path);
        if (!MatchesDeclaredType(schema, value))
        {
            error = $"{path} 的类型不符合工具参数架构。";
            return false;
        }

        if (!MatchesEnum(schema, value))
        {
            error = $"{path} 的值不在允许范围内。";
            return false;
        }

        if (value.ValueKind == JsonValueKind.Object
            && !ValidateObject(schema, value, path, out error))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.Array
            && !ValidateArray(schema, value, path, out error))
        {
            return false;
        }

        return ValidateNumber(schema, value, path, out error);
    }

    private static bool ValidateObject(
        JsonElement schema,
        JsonElement value,
        string path,
        out string error)
    {
        if (schema.TryGetProperty("required", out var required))
        {
            EnsureArray(required, $"{path}.required");
            foreach (var item in required.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException($"{path}.required 必须只包含字符串。");
                }

                var name = item.GetString()!;
                if (!value.TryGetProperty(name, out _))
                {
                    error = $"缺少必填参数：{name}";
                    return false;
                }
            }
        }

        if (!schema.TryGetProperty("properties", out var properties))
        {
            error = string.Empty;
            return true;
        }

        EnsureSchemaObject(properties, $"{path}.properties");
        foreach (var property in value.EnumerateObject())
        {
            if (properties.TryGetProperty(property.Name, out var propertySchema)
                && !ValidateNode(propertySchema, property.Value, $"{path}.{property.Name}", out error))
            {
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateArray(
        JsonElement schema,
        JsonElement value,
        string path,
        out string error)
    {
        if (!schema.TryGetProperty("items", out var itemSchema))
        {
            error = string.Empty;
            return true;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (!ValidateNode(itemSchema, item, $"{path}[{index}]", out error))
            {
                return false;
            }

            index++;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateNumber(
        JsonElement schema,
        JsonElement value,
        string path,
        out string error)
    {
        error = string.Empty;
        if (value.ValueKind != JsonValueKind.Number)
        {
            return true;
        }

        var number = value.GetDouble();
        if (schema.TryGetProperty("minimum", out var minimum) && number < minimum.GetDouble())
        {
            error = $"{path} 不能小于 {minimum.GetRawText()}。";
            return false;
        }

        if (schema.TryGetProperty("maximum", out var maximum) && number > maximum.GetDouble())
        {
            error = $"{path} 不能大于 {maximum.GetRawText()}。";
            return false;
        }

        return true;
    }

    private static bool MatchesDeclaredType(JsonElement schema, JsonElement value)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return true;
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            return MatchesType(ReadType(type), value);
        }

        EnsureArray(type, "schema.type");
        return type.EnumerateArray().Any(item => MatchesType(ReadType(item), value));
    }

    private static string ReadType(JsonElement type)
    {
        if (type.ValueKind != JsonValueKind.String
            || !SupportedTypes.Contains(type.GetString()!))
        {
            throw new InvalidDataException("工具参数架构包含无效的 type。");
        }

        return type.GetString()!;
    }

    private static bool MatchesType(string type, JsonElement value) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "number" => value.ValueKind == JsonValueKind.Number,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        _ => false
    };

    private static bool MatchesEnum(JsonElement schema, JsonElement value)
    {
        if (!schema.TryGetProperty("enum", out var choices))
        {
            return true;
        }

        EnsureArray(choices, "schema.enum");
        return choices.EnumerateArray().Any(choice => JsonElement.DeepEquals(choice, value));
    }

    private static void EnsureSchemaObject(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{path} 必须是 JSON 对象。");
        }
    }

    private static void EnsureArray(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{path} 必须是 JSON 数组。");
        }
    }
}
