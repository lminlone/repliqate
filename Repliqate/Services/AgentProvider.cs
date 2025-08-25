using Microsoft.Extensions.DependencyInjection;
using Repliqate.Plugins;
using Repliqate.Plugins.AgentRestic;

namespace Repliqate.Services;

public class AgentProvider
{
    private readonly Dictionary<string, Type> _internalAgents = new()
    {
        { "restic", typeof(AgentRestic) }
    };
    
    private readonly IServiceProvider _serviceProvider;
    
    public AgentProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public IAgent? GetAgentForMethod(string method)
    {
        IAgent? agent = null;
        
        if (_internalAgents.TryGetValue(method, out Type? agentType))
        {
            return (IAgent?)ActivatorUtilities.CreateInstance(_serviceProvider, agentType);
        }
        
        return agent;
    }
}