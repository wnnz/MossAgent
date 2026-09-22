using MossAgent.Domain;

namespace MossAgent.Application.Models;

public interface IModelProviderRegistry
{
    IModelProvider Get(ProviderProtocol protocol);
}

