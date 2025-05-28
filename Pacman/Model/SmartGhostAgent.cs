#nullable enable
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Mars.Interfaces.Environments;
using Mars.Numerics;


namespace Pacman.Model;

public class SmartGhostAgent : GhostAgent
{
    // Q-Learning Felder
    private readonly Dictionary<string, Dictionary<string, double>> QTable = new();
    private readonly Random rand = new();
    private string? previousState = null;
    private string? previousAction = null;
    private double learningRate = 0.1;
    private double discountFactor = 0.9;
    private double explorationRate = 0.1;
    public override void Tick()
    {
        if (ProcessGhostState()) return;

        // Q-Learning BEGIN
        string currentState = BuildState();
        string chosenAction = ChooseAction(currentState);
        Position target = DecideTarget(chosenAction);

        if (previousState != null && previousAction != null)
        {
            double reward = CalculateReward(); // Schritt 6
            UpdateQValue(previousState, previousAction, reward, currentState); // Schritt 7
        }

        previousState = currentState;
        previousAction = chosenAction;

        MoveTowardsGoal(target);
      
        
        if (Layer.GetCurrentTick() ==180 )  // letzter Tick = steps - 1
        {
            SaveQTableToCsv();
            Console.WriteLine("✔ Q-Table gespeichert!");
        }
        // Q-Learning END
      

        // Alte Logik 
        /*
        var pacMan = ExplorePacMan();
        var teammates = ExploreTeam();
        var foundPacMan = pacMan != null;
        var target = GetRandomCell();

        if (foundPacMan)
        {
            if (Frightened())
            {
                var pacManPosition = pacMan.Position;
                target = ExploreOccupiablePositions()
                    .OrderByDescending(spot => Distance.Euclidean(spot.X, spot.Y, pacManPosition.X, pacManPosition.Y))
                    .FirstOrDefault();
            }
            else
            {
                EnterChaseMode();
                target = pacMan.Position;
            }
        }
        else
        {
            EnterScatterMode();
        }
        MoveTowardsGoal(target);
        */
    }

    
    /// <summary> Natalie
    /// Builds a string representation of the current state for Q-learning.
    /// This state includes whether PacMan is visible, the current ghost mode,
    /// and a rough distance category to PacMan (if visible).
    /// </summary>
    /// <returns>A unique string describing the agent's current situation.</returns>
    private string BuildState()
    {
        // Try to detect PacMan
        var pacMan = ExplorePacMan();
        bool seesPacman = pacMan != null;

        // Get the current ghost mode (Chase, Scatter, Frightened, etc.)
        string mode = Mode.ToString();
    
        // Initialize distance category as unknown
        string distanceCategory = "unknown";

        // If PacMan is visible, calculate the distance to him
        if (seesPacman)
        {
            var dist = GetDistance(pacMan.Position);

            if (dist < 3)
                distanceCategory = "near";
            else if (dist < 6)
                distanceCategory = "medium";
            else
                distanceCategory = "far";
        }

        // Build a string that uniquely describes this situation
        return $"sees:{seesPacman};mode:{mode};distance:{distanceCategory}";
    }
    
    /// <summary>Natalie
    /// Chooses the best action based on the current state.
    /// Uses epsilon-greedy strategy to sometimes explore random actions.
    /// </summary>
    /// <param name="state">The current state string</param>
    /// <returns>The chosen action as string</returns>
    private string ChooseAction(string state)
    {
        var actions = new List<string> { "chase", "flee", "scatter", "random" };

        // If state not in Q-table, initialize it
        if (!QTable.ContainsKey(state))
            QTable[state] = actions.ToDictionary(a => a, a => 0.0);

        // Exploration: choose random action with probability ε
        if (rand.NextDouble() < explorationRate)
            return actions[rand.Next(actions.Count)];

        // Exploitation: choose the action with the highest Q-value
        return QTable[state]
            .OrderByDescending(entry => entry.Value)
            .First().Key;
    }
    /// <summary>Natalie
    /// Converts a chosen action into a target position.
    /// </summary>
    /// <param name="action">The chosen action</param>
    /// <returns>A position that the agent should move towards</returns>
    private Position DecideTarget(string action)
    {
        var pacMan = ExplorePacMan();
        var options = ExploreOccupiablePositions();

        switch (action)
        {
            case "chase":
                if (pacMan != null)
                    return pacMan.Position;
                break;

            case "flee":
                if (pacMan != null)
                {
                    // Move to the position farthest away from PacMan
                    return options
                        .OrderByDescending(pos => GetDistance(pacMan.Position))
                        .FirstOrDefault();
                }
                break;

            case "scatter":
                return new Position(ScatterCellX, ScatterCellY);

            case "random":
                return options[rand.Next(options.Count)];
        }

        // fallback: choose any reachable cell
        return GetRandomCell();
    }
    /// <summary>N
    /// Calculates a reward based on current mode and situation.
    /// </summary>
    /// <returns>A numeric reward used for Q-learning</returns>
    private double CalculateReward()
    {
        if (Mode == GhostMode.Eaten)
            return -100; // Got eaten = bad

        if (Mode == GhostMode.Frightened)
            return -1; // Running away

        if (Mode == GhostMode.Chase && ExplorePacMan() != null)
            return +10; // Saw PacMan while chasing

        if (Mode == GhostMode.Scatter)
            return 0; // Neutral

        return 0;
    }
    /// <summary>N
    /// Updates the Q-value for the previous state and action.
    /// </summary>
    /// <param name="prevState">The previous state</param>
    /// <param name="prevAction">The previous action</param>
    /// <param name="reward">The reward received</param>
    /// <param name="newState">The resulting state</param>
    private void UpdateQValue(string prevState, string prevAction, double reward, string newState)
    {
        var actions = new List<string> { "chase", "flee", "scatter", "random" };

        // Make sure both states exist in the Q-table
        if (!QTable.ContainsKey(prevState))
            QTable[prevState] = actions.ToDictionary(a => a, a => 0.0);

        if (!QTable.ContainsKey(newState))
            QTable[newState] = actions.ToDictionary(a => a, a => 0.0);

        double oldQ = QTable[prevState][prevAction];
        double maxFutureQ = QTable[newState].Values.Max();

        double updatedQ = oldQ + learningRate * (reward + discountFactor * maxFutureQ - oldQ);

        QTable[prevState][prevAction] = updatedQ;
    }

    /// <summary>
    /// Saves the Q-table to a CSV file at Resources/ghost_qtable.csv
    /// </summary>
    private void SaveQTableToCsv()
    {
        var lines = new List<string> { "state,action,value" };

        foreach (var state in QTable)
        {
            foreach (var action in state.Value)
            {
                lines.Add($"{state.Key},{action.Key},{action.Value}");
            }
        }

        File.WriteAllLines("../../../Resources/ghost_qtable.csv", lines);
    }

    /// <summary>N
    /// Loads the Q-table from a CSV file at Resources/ghost_qtable.csv
    /// </summary>
    private void LoadQTableFromCsv()
    {
        var path = "Resources/ghost_qtable.csv";
        if (!System.IO.File.Exists(path)) return;

        var lines = System.IO.File.ReadAllLines(path).Skip(1); // Skip header
        foreach (var line in lines)
        {
            var parts = line.Split(',');
            if (parts.Length != 3) continue;

            var state = parts[0];
            var action = parts[1];
            if (!double.TryParse(parts[2], out double value)) continue;

            if (!QTable.ContainsKey(state))
                QTable[state] = new Dictionary<string, double>();

            QTable[state][action] = value;
        }
    }


    /// <summary>
    /// Processes the ghost state and returns true if the ghost is not controllable.
    /// </summary>
    /// <returns></returns>
    private bool ProcessGhostState()
    {
        if (ReleaseTimer <= ReleaseTick)
        {
            ReleaseTimer++;
            return true;
        }
        if (Mode == GhostMode.Frightened)
        {
            if (Layer.GetCurrentTick() % 2 == 0) return true;
            return false;
        }
        if (Mode == GhostMode.Eaten)
        {
            MoveTowardsGoal(new Position(HouseCellX, HouseCellY));
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Explores the environment and returns a list of the teammates.
    /// </summary>
    private List<GhostAgent> ExploreTeam()
    {
        return Layer.GhostAgents
            .Where(agent => agent != this)
            .ToList();;
    }
    
    /// <summary>
    /// Explores the environment and returns the PacManAgent instance.
    /// Can be null if no PacManAgent is found.
    /// </summary>
    private PacManAgent? ExplorePacMan() => Layer.PacManAgentEnvironment.Explore(Position, VisualRange).FirstOrDefault();
    
    
    /// <summary>
    /// Enters the chase mode if the ghost is in scatter mode.
    /// </summary>
    /// <returns></returns>
    private bool EnterChaseMode()
    {
        if (Mode == GhostMode.Scatter)
        {
            Mode = GhostMode.Chase;
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Enters the scatter mode if the ghost is in chase mode.
    /// </summary>
    /// <returns></returns>
    private bool EnterScatterMode()
    {
        if (Mode == GhostMode.Chase)
        {
            Mode = GhostMode.Scatter;
            return true;
        }
        return false;
    }
    
    private bool Frightened() => Mode == GhostMode.Frightened;
    
    public override void Init(MazeLayer layer)
    {
        Layer = layer;
        Position = new Position(StartX, StartY);
        Layer.GhostAgentEnvironment.Insert(this);

        LoadQTableFromCsv(); // ⬅️ Lade die Q-Tabelle beim Start
    }

}