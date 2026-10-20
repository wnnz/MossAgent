using System.Text;
using System.Text.Json.Nodes;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ModelImageBodyLimitTests
{
    [Fact]
    public void SerializedImageBody_HasAnExactUtf8ByteBoundary()
    {
        var root = new JsonObject { ["text"] = new string('a', ModelImageRequestValidation.MaximumJsonByteCount - 11) };

        var json = ModelRequestJson.Serialize(root, true);

        Assert.Equal(ModelImageRequestValidation.MaximumJsonByteCount, Encoding.UTF8.GetByteCount(json));
        root["text"] = root["text"]!.GetValue<string>() + "a";
        Assert.Throws<ArgumentException>(() => ModelRequestJson.Serialize(root, true));
    }

    [Theory]
    [MemberData(nameof(ModelImageTestFixture.Protocols), MemberType = typeof(ModelImageTestFixture))]
    public void WholeBodyLimit_IncludesEscapedNonAsciiTextAndAdvancedParameters(ProviderProtocol protocol)
    {
        var request = ModelImageTestFixture.Request(protocol);
        request = request with
        {
            Model = request.Model with
            {
                AdvancedParametersJson = "{\"fixture_metadata\":\"" + new string('中', 3_500_000) + "\"}"
            }
        };

        var error = Assert.Throws<ArgumentException>(() => ModelImageTestFixture.Build(request));

        Assert.DoesNotContain(request.Messages[0].Images[0].Base64Data, error.Message);
    }
}
