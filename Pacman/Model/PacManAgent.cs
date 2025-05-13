using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;

namespace Pacman.Model;

public class PacManAgent : MovingAgent
{
    private int _initialPowerPelletCount;

    public override void Init(MazeLayer layer)
    {
        Layer = layer;
        Position = new Position(StartX, StartY);
        Layer.PacManAgentEnvironment.Insert(this);
        _initialPowerPelletCount = 4; //only for this grid

    }

    private double GetDistanceBetween(Position a, Position b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    private int CountNearbyPellets(Position center, List<Position> allPellets, int radius = 2)
    {
        return allPellets.Count(p => p != center && GetDistanceBetween(center, p) <= radius);
    }

    private bool IsPacManCloserThanGhost(Position pellet, List<Position> ghosts)
    {
        // PacMan distance to pellet
        var pacManDistance = GetDistance(pellet);

        // If any ghost can reach the pellet equally fast or faster, it's unsafe
        return !ghosts.Any(g => GetDistanceBetween(g, pellet) <= pacManDistance);
    }


    private Position GetSafestPosition(List<Position> options, List<Position> ghostPositions)
    {
        // Select the position with the maximum distance to the nearest ghost
        return options
            .OrderByDescending(p => ghostPositions.Min(GetDistance))
            .FirstOrDefault();
    }

    private bool IsGhostNearby(List<Position> ghosts, int dangerRadius = 3)
    {
        return ghosts.Any(g => GetDistance(g) <= dangerRadius);
    }


    private Dictionary<Position, int> _visitedTiles = new();

    private Position GetLeastVisitedPosition(List<Position> candidates)
    {
        return candidates
            .OrderBy(p => _visitedTiles.ContainsKey(p) ? _visitedTiles[p] : 0)
            .FirstOrDefault();
    }


    private int _usedPowerPellets = 0;
    private bool WasPoweredUpLastTick = false;


    public override void Tick()
    {
        var powerPelletPositions = ExplorePowerPelletPositions();
        var pelletPositions = ExplorePelletPositions();
        // var ghostPositions = ExploreGhostPositions();
        var ghostPositions = ExploreGhosts().Select(agent => agent.Position).ToList();
        var occupiablePositions = ExploreOccupiablePositions();

        // Track how often this tile has been visited
        if (_visitedTiles.ContainsKey(Position))
            _visitedTiles[Position]++;
        else
            _visitedTiles[Position] = 1;
        // Detect if a power pellet was just consumed
        if (!PoweredUp && WasPoweredUpLastTick)
        {
            _usedPowerPellets++;
        }
        WasPoweredUpLastTick = PoweredUp;

        // Rule 6: If powered up, chase the nearest ghost within range
        if (PoweredUp && _usedPowerPellets <= 2)
        {
           var edibleGhosts = ExploreGhosts()
                .Where(g =>
                    GetDistance(g.Position) <= 5 &&
                    g.Mode != GhostMode.Eaten) // ignore ghosts that are just eyes returning to spawn
                .OrderBy(g => GetDistance(g.Position))
                .ToList();

            if (edibleGhosts.Count > 0)
            {
                // Go hunt!
                MoveTowardsGoal(edibleGhosts.First().Position);
                return;
            }

            // Rule 7: No edible ghosts nearby → fallback to pellet logic
        }

        // Rule 5: Tactical use of power pellets when ghosts are nearby
        if (IsGhostNearby(ghostPositions) && powerPelletPositions.Count > 0)
        {
            // Move toward the nearest power pellet
            var bestPowerPellet = powerPelletPositions
                .OrderBy(GetDistance)
                .First();

            MoveTowardsGoal(bestPowerPellet);
            return; // Skip other decisions this tick
        }

        // Rules 1–4: Pellet logic with danger avoidance
        if (pelletPositions.Count > 0)
        {
            //Rule 3: avoid danger zones
            var safePellets = pelletPositions
                .Where(p => IsPacManCloserThanGhost(p, ghostPositions))
                .ToList();

            if (safePellets.Count > 0)
            {
                // Rule 1 + Rule 2: Prioritize nearest pellet, prefer dense clusters
                var bestPellet = safePellets
                    .Select(p => new
                    {
                        Pellet = p,
                        Distance = GetDistance(p),
                        ClusterScore = CountNearbyPellets(p, safePellets)
                    })
                    .OrderBy(x => x.Distance)
                    .ThenByDescending(x => x.ClusterScore)
                    .First().Pellet;

                MoveTowardsGoal(bestPellet);
            }
            else
            {
                //RULE 4:  No safe pellet available – fallback behavior will apply

                var safestTile = GetSafestPosition(occupiablePositions, ghostPositions);

                if (safestTile != null)
                {
                    MoveTowardsGoal(safestTile);
                    return;
                }
            }
        }

        // Rule 8: No pellets or power pellets → explore least visited area
        var explorationTarget = GetLeastVisitedPosition(occupiablePositions);
        if (explorationTarget != null)
        {
        //    MoveTowardsGoal(explorationTarget);
        }
        
    }
    
    

    /// <summary>
    /// Explores the environment and returns a list of positions of the ghosts.
    /// </summary>
    /// <returns></returns>
    private List<Position> ExploreGhostPositions()
    {
        return Layer.GhostAgentEnvironment.Explore(Position, VisualRange, -1).Select(agent => agent.Position)
            .ToList();
    }

    /// <summary>
    /// Explores the environment and returns a list of GhostAgent instances.
    /// </summary>
    private List<GhostAgent> ExploreGhosts()
    {
        return Layer.GhostAgentEnvironment.Explore(Position, VisualRange, -1).ToList();
    }

    private int GetScore() =>
        Layer.Score;

    private readonly Random _random = new();

    public Direction Direction { get; set; }

    public bool PoweredUp { get; set; }

    public int PoweredUpTime { get; set; }

    [PropertyDescription] public int Lives { get; set; }
}