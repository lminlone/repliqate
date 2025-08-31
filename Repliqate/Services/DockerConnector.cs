using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Repliqate.Services;

public class DockerConnector : BackgroundService
{
    private readonly ILogger<DockerConnector> _logger;
    private DockerClient _client;
    
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly DockerClientConfiguration _config;
    private readonly IConfiguration _appConfig;
    
    // An in-memory list of all containers, kept mostly up to date with Docker
    private IList<DockerContainer> _containers;

    public event Action<DockerContainer> OnContainerCreated;
    public event Action<DockerContainer> OnContainerDestroyed;
    
    public DockerConnector(ILogger<DockerConnector> logger, IConfiguration appConfig)
    {
        _logger = logger;
        _appConfig = appConfig;
        
        // Docker sock, specify one by default if none provided
        string dockerSockPath = _appConfig.GetValue<string>("DOCKER_SOCK_PATH", "/var/run/docker.sock");
        bool isUri = Uri.TryCreate(dockerSockPath, UriKind.Absolute, out _);

        if (isUri)
        {
            _config = new DockerClientConfiguration(new Uri(dockerSockPath));
        }
        else
        {
            // Handle as a Unix socket path
            _config = new DockerClientConfiguration(new Uri($"unix://{dockerSockPath}"));
        }
    }

    public async Task Initialize()
    {
        _logger.LogInformation("Attempting to connect to Docker daemon at {DockerSockPath}", _config.EndpointBaseUri);
        
        try
        {
            _client = _config.CreateClient();
            await _client.System.PingAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to connect to Docker daemon");
            throw;
        }
        
        _logger.LogInformation("Connected to Docker daemon");
        
        // Compile a list of all containers on startup
        _containers = new List<DockerContainer>();
        var listParams = new ContainersListParameters { All = true };
        var containerList = await _client.Containers.ListContainersAsync(listParams, CancellationToken.None);
        foreach (var container in containerList)
        {
            var inspectData = await _client.Containers.InspectContainerAsync(container.ID);
            var wrappedDockerContainer = new DockerContainer(inspectData);
            await wrappedDockerContainer.DiscoverVolumes(this);
            _containers.Add(wrappedDockerContainer);
            
            // Call create events per container found for late registrars
            OnContainerCreated?.Invoke(wrappedDockerContainer);
        }
        
        _logger.LogInformation("Discovered {ContainerCount} containers in total", _containers.Count);
        
        ListenForContainerEvents();
    }

    protected async void ListenForContainerEvents()
    {
        // Listen to ALL events
        // await _client.System.MonitorEventsAsync(new ContainerEventsParameters(), new Progress<Message>(m => _logger.LogInformation("Received event - Status: {Event} | Type: {Type}", m.Status, m.Type)));
        
        // Listen to container destroy and create events
        var eventParams = new ContainerEventsParameters()
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>()
            {
                {
                    "event", new Dictionary<string, bool>
                    {
                        { "create", true },
                        { "destroy", true },
                    }
                },
                {
                    "type", new Dictionary<string, bool>
                    {
                        { "container", true }
                    }
                }
            }
        };
        await _client.System.MonitorEventsAsync(eventParams, new Progress<Message>(OnContainerCreatedOrDestroyed));
        
        // If we reach here then something failed (like getting disconnected)
        _logger.LogError("Something went wrong");
    }

    protected async void OnContainerCreatedOrDestroyed(Message message)
    {
        switch (message.Status)
        {
            case "destroy":
            {
                _logger.LogInformation("Container {ContainerId} destroyed", message.Actor.ID);
                
                foreach (var container in _containers)
                {
                    if (container.ID != message.Actor.ID)
                        continue;
                    
                    _logger.LogInformation("Container {ContainerId} ({ContainerName}) removed", container.ID, container.GetName());
                    
                    OnContainerDestroyed?.Invoke(container);
                    _containers.Remove(container);
                    
                    break;
                }

                break;
            }
            case "create":
            {
                _logger.LogInformation("Container {ContainerId} created", message.Actor.ID);
                
                var inspectData = await _client.Containers.InspectContainerAsync(message.Actor.ID);
                var wrappedDockerContainer = new DockerContainer(inspectData);
                await wrappedDockerContainer.DiscoverVolumes(this);
                _containers.Add(wrappedDockerContainer);
                
                _logger.LogInformation("Container {ContainerId} ({ContainerName}) added", message.Actor.ID,  wrappedDockerContainer.Name);
            
                OnContainerCreated?.Invoke(_containers.Last());
                
                break;
            }
        }
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(60000, stoppingToken);
            await EnsureConnection();
        }
    }

    private async Task<bool> EnsureConnection()
    {
        try
        {
            await _client.System.PingAsync();
            return true;
        }
        catch (Exception)
        {
            return await ReconnectAsync();
        }
    }

    private async Task<bool> ReconnectAsync()
    {
        try
        {
            await _connectionLock.WaitAsync();
            try
            {
                // Double-check if connection is still needed after acquiring lock
                if (await EnsureConnection())
                    return true;

                _client.Dispose();
                _client = _config.CreateClient();
                return await EnsureConnection();
            }
            finally
            {
                _connectionLock.Release();
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task EnsureConnectedAsync()
    {
        if (!await EnsureConnection())
        {
            throw new InvalidOperationException("Failed to establish connection to Docker daemon");
        }
    }

    public async Task<bool> StopContainer(string containerId, uint timeoutSeconds = 60)
    {
        await EnsureConnectedAsync();
        
        var containerStopParams = new ContainerStopParameters();
        containerStopParams.WaitBeforeKillSeconds = timeoutSeconds;
        return await _client.Containers.StopContainerAsync(containerId, containerStopParams);
    }

    public async Task<bool> StartContainer(string containerId)
    {
        await EnsureConnectedAsync();
        
        var containerStartParams = new ContainerStartParameters();
        return await _client.Containers.StartContainerAsync(containerId, containerStartParams);
    }

    public IList<DockerContainer> GetContainers(Dictionary<string, string>? filterLabels = null)
    {
        List<DockerContainer> containers = [];

        if (filterLabels is null || filterLabels.Count == 0)
        {
            containers.AddRange(_containers);
        }
        else
        {
            foreach (var container in _containers)
            {
                foreach (var filterLabel in filterLabels)
                {
                    if (container.Config.Labels.TryGetValue(filterLabel.Key, out var labelValue))
                    {
                        if (labelValue == filterLabel.Value)
                        {
                            containers.Add(container);
                        }
                    }
                }
            }
        }
        
        return containers;
    }

    public async Task<IList<ContainerListResponse>> FetchContainers()
    {
        await EnsureConnectedAsync();

        ContainersListParameters parameters = new ContainersListParameters();
        parameters.All = true;
        return await _client.Containers.ListContainersAsync(parameters, CancellationToken.None);
    }

    public async Task<VolumesListResponse> FetchVolumes()
    {
        await EnsureConnectedAsync();
        
        return await _client.Volumes.ListAsync();
    }

    public async Task<VolumeResponse> InspectVolume(string volumeId)
    {
        await EnsureConnectedAsync();
        
        return await _client.Volumes.InspectAsync(volumeId);
    }

    public override void Dispose()
    {
        _connectionLock.Dispose();
        _client.Dispose();
    }
}

public class StackList
{
    public List<ContainerListResponse> Containers { get; set; } = [];
}
