using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Core.Data;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;

namespace Pacman.Model;

public class MazeLayer : RasterLayer, ISteppedActiveLayer
{
    /// <summary>
    ///     The initialization method of the GridLayer which spawns and stores the specified number of each agent type
    /// </summary>
    /// <param name="layerInitData"> Initialization data that is passed to an agent manager which spawns the specified
    /// number of each agent type</param>
    /// <param name="registerAgentHandle">A handle for registering agents</param>
    /// <param name="unregisterAgentHandle">A handle for unregistering agents</param>
    /// <returns>A boolean that states if initialization was successful</returns>
    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle,
        UnregisterAgent unregisterAgentHandle)
    {
        if (Visualization) DataVisualizationServer.RunInBackground();

        var initLayer = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgentHandle);

        GhostAgentEnvironment = new SpatialHashEnvironment<GhostAgent>(Width, Height);
        PacManAgentEnvironment = new SpatialHashEnvironment<PacManAgent>(Width, Height);
        PelletEnvironment = new SpatialHashEnvironment<Pellet>(Width, Height);
        PowerPelletEnvironment = new SpatialHashEnvironment<PowerPellet>(Width, Height);
        OccupiableSpotsEnvironment = new SpatialHashEnvironment<OccupiableSpot>(Width, Height);

        AgentManager = layerInitData.Container.Resolve<IAgentManager>();

        if (ValidateGhostConfiguration(layerInitData))
        {
            GhostAgents = AgentManager.Spawn<SmartGhostAgent, MazeLayer>().Cast<GhostAgent>().ToList();
            SmartGhosts = true;
        }
        else
            GhostAgents = AgentManager.Spawn<GhostAgent, MazeLayer>().ToList();


        PacManAgent = AgentManager.Spawn<PacManAgent, MazeLayer>().ToList().First();
        PacManAgent.Direction = Direction.Right;

        Pellets = new List<Pellet>();
        PowerPellets = new List<PowerPellet>();

        OccupiableSpots = new List<Position>();
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                var type = this[x, y];
                var position = Position.CreatePosition(x, y);
                if (IsRoutable(position))
                {
                    var occupiableSpot = AgentManager.Spawn<OccupiableSpot, MazeLayer>(null, s => s.Position = position)
                        .Take(1).First();
                    OccupiableSpotsEnvironment.Insert(occupiableSpot);
                    OccupiableSpots.Add(position);
                }

                var item = CreateItem(type, position);
                if (item == null) continue;
                if (item is Pellet pellet)
                {
                    PelletEnvironment.Insert(pellet);
                    Pellets.Add(pellet);
                }
                else if (item is PowerPellet powerPellet)
                {
                    PowerPelletEnvironment.Insert(powerPellet);
                    PowerPellets.Add(powerPellet);
                }
            }
        }

        Score = 0;

        return initLayer;
    }

    /// <summary>
    ///     Checks if the grid cell (x,y) is accessible
    /// </summary>
    /// <param name="x">x-coordinate of grid cell</param>
    /// <param name="y">y-coordinate of grid cell</param>
    /// <returns>Boolean representing if (x,y) is accessible</returns>
    public override bool IsRoutable(int x, int y) => Math.Abs(this[x, y] - 1.0) > _epsilon;

    /// <summary>
    ///     The environment of the Ghostagent agents
    /// </summary>
    public SpatialHashEnvironment<GhostAgent> GhostAgentEnvironment { get; set; }

    /// <summary>
    ///     The environment of the Pacman agent
    /// </summary>
    public SpatialHashEnvironment<PacManAgent> PacManAgentEnvironment { get; set; }

    /// <summary>
    ///    The environment of the pellets
    /// </summary>
    public SpatialHashEnvironment<Pellet> PelletEnvironment { get; set; }

    /// <summary>
    ///    The environment of the power pellets
    /// </summary>
    public SpatialHashEnvironment<PowerPellet> PowerPelletEnvironment { get; set; }

    /// <summary>
    ///  The environment of the occupiable spots
    /// </summary>
    public SpatialHashEnvironment<OccupiableSpot> OccupiableSpotsEnvironment { get; set; }


    /// <summary>
    ///  A collection that holds the occupiable spots
    /// </summary>
    public List<Position> OccupiableSpots { get; private set; }

    /// <summary>
    /// A collection that holds the GhostAgent instances
    /// </summary>
    public List<GhostAgent> GhostAgents { get; private set; }

    /// <summary>
    /// Reference to the PacManAgent instance
    /// </summary>
    public PacManAgent PacManAgent { get; set; }

    /// <summary>
    ///  A collection that holds the Pellet instances
    /// </summary>
    public List<Pellet> Pellets { get; private set; }

    /// <summary>
    /// A collection that holds the PowerPellet instances
    /// </summary>
    public List<PowerPellet> PowerPellets { get; private set; }

    public IAgentManager AgentManager { get; private set; }

    public void Tick()
    {
        // do nothing
    }

    public void PreTick()
    {
        // do nothing
    }

    public void PostTick()
    {
        CheckCollisions();
        if (PacManAgent.PoweredUpTime > 0)
            PacManAgent.PoweredUpTime--;
        PacManAgent.PoweredUp = PacManAgent.PoweredUpTime > 0;

        PacManAgent.HasMoved = false;
        foreach (var ghostAgent in GhostAgents)
        {
            ghostAgent.HasMoved = false;
            if (ghostAgent.Mode == GhostMode.Frightened && !PacManAgent.PoweredUp) ghostAgent.Mode = GhostMode.Scatter;
            if (ghostAgent.Mode == GhostMode.Eaten && ghostAgent.Position.Equals(
                    Position.CreatePosition(ghostAgent.HouseCellX, ghostAgent.HouseCellY)))
                ghostAgent.Mode = GhostMode.Scatter;
        }

        if (Visualization)
        {
            while (!DataVisualizationServer.Connected())
            {
                Thread.Sleep(1000);
                Console.WriteLine("Waiting for live visualization to run.");
            }

            var agentData = new List<IAgent<MazeLayer>>();
            agentData.Add(PacManAgent);
            agentData.AddRange(GhostAgents);
            agentData.AddRange(Pellets);
            agentData.AddRange(PowerPellets);
            DataVisualizationServer.SendData(Score, agentData);

            while (DataVisualizationServer.CurrentTick != Context.CurrentTick + 1)
            {
                Thread.Sleep(VisualizationTimeout);
            }
        }

        if (PacManAgent.Lives == 0 || Context.CurrentTick >= Context.MaxTicks)
        {
            PacManAgent.saveTable();
            if (SmartGhosts)
            {
                Console.WriteLine("Saving Q-Tables of Smart Ghosts");
                foreach (var ghostAgent in GhostAgents)
                {
                    SmartGhostAgent smartGhostAgent = (SmartGhostAgent)ghostAgent;
                    smartGhostAgent.SaveQTable();
                }
            }

            Context.StepFlag = false;
            DataVisualizationServer.Stop();
        }
    }

    public int Score { get; private set; }

    public void PacManDie()
    {
        PacManAgent.Lives--;
        PacManAgentEnvironment.PosAt(PacManAgent, [PacManAgent.StartX, PacManAgent.StartY]);
        PacManAgent.Direction = Direction.Right;
        foreach (var ghostAgent in GhostAgents)
        {
            GhostAgentEnvironment.PosAt(ghostAgent, [ghostAgent.StartX, ghostAgent.StartY]);
            ghostAgent.Mode = GhostMode.Scatter;
            ghostAgent.ReleaseTimer = 0;
        }
    }

    private Item CreateItem(double type, Position position)
    {
        return type switch
        {
            2 => AgentManager.Spawn<Pellet, MazeLayer>(null, s => s.Position = position).Take(1).First(),
            3 => AgentManager.Spawn<PowerPellet, MazeLayer>(null, s => s.Position = position).Take(1).First(),
            _ => null
        };
    }

    private void CheckCollisions()
    {
        var pellet = PelletEnvironment.Explore(PacManAgent.Position, 0.0, 1).FirstOrDefault();
        var powerPellet = PowerPelletEnvironment.Explore(PacManAgent.Position, 0.0, 1).FirstOrDefault();
        var ghosts = GhostAgentEnvironment.Explore(PacManAgent.Position, 0.0).ToList();

        if (pellet != null)
        {
            Score += 10;
            pellet.RemoveFromSimulation();
        }

        if (powerPellet != null)
        {
            Score += 50;
            PacManAgent.PoweredUp = true;
            PacManAgent.PoweredUpTime = 20;
            foreach (var ghostAgent in GhostAgents)
            {
                ghostAgent.Mode = GhostMode.Frightened;
            }

            powerPellet.RemoveFromSimulation();
        }

        foreach (var ghost in ghosts)
        {
            if (PacManAgent.PoweredUp && ghost.Mode == GhostMode.Frightened)
            {
                Score += 200;
                ghost.Mode = GhostMode.Eaten;
            }
            else if (ghost.Mode != GhostMode.Eaten)
            {
                PacManDie();
                break;
            }
        }
    }

    private bool ValidateGhostConfiguration(LayerInitData layerInitData)
    {
        var ghostMapping =
            layerInitData.AgentInitConfigs.FirstOrDefault(m => m.ModelType.TypeName == "Pacman.Model.GhostAgent");
        var smartGhostMapping =
            layerInitData.AgentInitConfigs.FirstOrDefault(m => m.ModelType.TypeName == "Pacman.Model.SmartGhostAgent");

        if (ghostMapping is null)
            throw new ArgumentException("GhostAgent is missing in the configuration.");
        if (smartGhostMapping is null)
            throw new ArgumentException("SmartGhostAgent is missing in the configuration.");

        var smartGhosts = smartGhostMapping.InstanceCount == 4 && ghostMapping.InstanceCount == 0;

        if (!smartGhosts && !(ghostMapping.InstanceCount == 4 && smartGhostMapping.InstanceCount == 0))
            throw new ArgumentException(
                $"Invalid agent configuration. Expected 4:0 or 0:4 setup, but found GhostAgent={ghostMapping.InstanceCount}, SmartGhostAgent={smartGhostMapping.InstanceCount}.");

        return smartGhosts;
    }

    [PropertyDescription] public bool Visualization { get; set; }
    [PropertyDescription] public bool SmartGhosts { get; set; }

    [PropertyDescription] public int VisualizationTimeout { get; set; }

    private readonly double _epsilon = 0.0001;
}