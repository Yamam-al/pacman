using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;

namespace Pacman.Model;

public class PacManAgent : MovingAgent
{

    public override void Init(MazeLayer layer)
    {
        Layer = layer;
        Position = new Position(StartX, StartY);
        Layer.PacManAgentEnvironment.Insert(this);
        LoadQTable("../../../Resources/qtable.txt");

    }
     public override void Tick()
     {
         int lastTickLives = Lives;
         int lasTickScore = GetScore();
         bool wasGhostNearby = IsGhostNearby(ExploreGhostPositions());
         var currentState = BuildState();
         var chosenAction = ChooseAction(currentState);
         var goal = getGoalPosition(chosenAction);
         MoveTowardsGoal(goal);
         var reward = GetReward(lastTickLives, lasTickScore, wasGhostNearby);
         var nextState = BuildState();

         UpdateQValue(currentState, chosenAction, reward, nextState);
     }

     //Q-Learning Logic
    
     private Dictionary<(string state, string action), double> _qTable = new();
     private readonly List<string> _actions = new() { "Up", "Down", "Left", "Right" };
     
     private double GetQValue(string state, string action)
     {
         return _qTable.TryGetValue((state, action), out var value) ? value : 0.0;
     }

     private void UpdateQValue(string state, string action, double reward, string nextState, double alpha = 0.1, double gamma = 0.9)
     {
         double oldQ = GetQValue(state, action);
         double maxNextQ = _actions.Max(a => GetQValue(nextState, a));

         double newQ = oldQ + alpha * (reward + gamma * maxNextQ - oldQ);

         _qTable[(state, action)] = newQ;
     }


     private string BuildState()
     {
         string direction = Direction.ToString();
         bool ghostInFront = IsGhostInFront();
         bool pelletInFront = IsPelletInFront();
         bool powerPelletInFront = IsPowerPelletInFront();
         double distToGhost = GetDistanceToClosestGhost();
         double distToPowerPellet = GetDistanceToClosestPowerPellet();
         bool poweredUp = PoweredUp;
         int score = GetScore();

         if (distToGhost > 5) distToGhost = 5;
         if (distToPowerPellet > 5) distToPowerPellet = 5;

         return $"D:{direction}_G:{ghostInFront}_P:{pelletInFront}_PPF:{powerPelletInFront}_DG:{distToGhost}_DPP:{distToPowerPellet}_PU:{poweredUp}_S:{score}";
     }
     
     private double _epsilon = 0.7; // Entdeckungsrate
     private string ChooseAction(string state)
     {
         var occupiable = ExploreOccupiablePositions();

         var possibleActions = _actions
             .Where(a => occupiable.Contains(GetTileInDirection(a)))
             .ToList();

         if (possibleActions.Count == 0)
         {
             // fallback: stay in place
             return "Stay";
         }

         if (_random.NextDouble() < _epsilon)
         {
             // Explore
             return possibleActions[_random.Next(possibleActions.Count)];
         }

         // Exploit: choose best known legal action
         return possibleActions
             .OrderByDescending(a => GetQValue(state, a))
             .First();
         
     }

     private double GetReward(int lastTickLives, int lasTickScore, bool wasGhostNearby)
     {
         var currentLives = Lives;
         var currentScore = GetScore();
         var isGhostNearby = IsGhostNearby(ExploreGhostPositions());
         int scoreDiff = currentScore - lasTickScore;
         if (currentLives < lastTickLives) return -100.0; // agent died 

         if (scoreDiff >= 200)
             return +10.0; // Ghost eaten
         else if (scoreDiff >= 50)
             return +1.0; // Power Pellet
         else if (scoreDiff >= 10)
             return +5.0; // Normal Pellet
         if (wasGhostNearby & !isGhostNearby)
             return +5.0; // Ghost eaten

         return -0.1; // kleine Strafe fürs Rumstehen
     }
     

     private void SaveQTable(string path)
     {
         // Export Q-table to clean lines (no duplicates)
         var lines = _qTable.Select(kvp =>
             $"{kvp.Key.state};{kvp.Key.action};{kvp.Value.ToString(CultureInfo.InvariantCulture)}");

         // Overwrite entire file with updated Q-table
         File.WriteAllLines(path, lines);

     }
     public void saveTable()
     {
         SaveQTable("../../../Resources/qtable.txt");
     }

     private void LoadQTable(string path)
     {
         if (!File.Exists(path)) return;

         foreach (var line in File.ReadAllLines(path))
         {
             var parts = line.Split(';');
             if (parts.Length == 3)
             {
                 var state = parts[0];
                 var action = parts[1];
                 var value = double.Parse(parts[2], CultureInfo.InvariantCulture);
                 _qTable[(state, action)] = value; // overwrites if already exists
             }
         }
         Console.WriteLine("Loaded Q Table: " + path + "with " + _qTable.Count + " entries.");
     }



     //Helper Methods
     private Position GetTileInDirection(string action)
     {
         return action switch
         {
             "Up"    => new Position(Position.X, Position.Y - 1),
             "Down"  => new Position(Position.X, Position.Y + 1),
             "Left"  => new Position(Position.X - 1, Position.Y),
             "Right" => new Position(Position.X + 1, Position.Y),
             _       => Position
         };
     }
     private double GetDistanceBetween(Position a, Position b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    private Position getGoalPosition(string action)
    {
        return action switch
        {
            "Up" => new Position(Position.X, Position.Y - 1),
            "Down" => new Position(Position.X, Position.Y + 1),
            "Left" => new Position(Position.X - 1, Position.Y),
            "Right" => new Position(Position.X + 1, Position.Y),
            _ => Position
        };
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
    
    private bool IsGhostInFront()
    {
        var nextTile = GetTileInFront();
        var ghostPositions = ExploreGhosts().Select(g => g.Position);

        return ghostPositions.Contains(nextTile);
    }
    
    private bool IsPelletInFront()
    {
        var nextTile = GetTileInFront();
        var pelletPositions = ExplorePelletPositions();

        return pelletPositions.Contains(nextTile);
    }
    
    private bool IsPowerPelletInFront()
    {
        var nextTile = GetTileInFront();
        var powerPellets = ExplorePowerPelletPositions();
        return powerPellets.Contains(nextTile);
    }

    private double GetDistanceToClosestPowerPellet()
    {
        var powerPellets = ExplorePowerPelletPositions();
        return powerPellets.Any()
            ? powerPellets.Min(p => GetDistance(p))
            : 10; // No power pellet visible
    }


    private Position GetTileInFront()
    {
        return Direction switch
        {
            Direction.Up    => new Position(Position.X, Position.Y - 1),
            Direction.Down  => new Position(Position.X, Position.Y + 1),
            Direction.Left  => new Position(Position.X - 1, Position.Y),
            Direction.Right => new Position(Position.X + 1, Position.Y),
            _ => Position
        };
    }

    
    private double GetDistanceToClosestGhost()
    {
        var ghostPositions = ExploreGhosts().Select(g => g.Position);
        return ghostPositions.Any()
            ? ghostPositions.Min(g => GetDistance(g))
            : 10; // kein Ghost sichtbar
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

    private int _usedPowerPellets = 0;
    private bool WasPoweredUpLastTick = false;
    private int GetScore() =>
        Layer.Score;

    private readonly Random _random = new();

    public Direction Direction { get; set; }

    public bool PoweredUp { get; set; }

    public int PoweredUpTime { get; set; }

    [PropertyDescription] public int Lives { get; set; }

   
}