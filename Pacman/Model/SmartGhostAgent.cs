// SmartGhostAgent.cs - Q-Learning Ghost

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Mars.Interfaces.Environments;

namespace Pacman.Model;

public class SmartGhostAgent : GhostAgent
{
    public string GhostName { get; set; } = "Ghost";

    // Q-Learning Tabelle im Format (state, action) => Q-Wert
    private Dictionary<(string state, string action), double> _qTable = new();
    
    // Mögliche Bewegungsaktionen
    private readonly List<string> _actions = new() { "Up", "Down", "Left", "Right" };
    
    private readonly Random _random = new();
    private double _epsilon = 0.3; // Entdeckungsrate

    // Initialisierung beim Start
    public override void Init(MazeLayer layer)
    {
        Layer = layer;
        Position = new Position(StartX, StartY);
        Layer.GhostAgentEnvironment.Insert(this);
        GhostName = Name ?? "Ghost" + StartX + "_" + StartY;
        LoadQTable(); // Lade bestehende Q-Werte
    }

    // Haupttick-Methode, wird bei jedem Zeitschritt aufgerufen
    public override void Tick()
    {
        if (ProcessGhostState()) return; // Falls Ghost im "Eaten"-Modus ist, abbrechen

        string state = BuildState(); // Aktuellen Zustand ermitteln
        string action = ChooseAction(state); // Beste (oder zufällige) Aktion wählen
        var target = GetTileInDirection(action); // Zielposition berechnen

        MoveTowardsGoal(target); // Bewegung durchführen

        string nextState = BuildState(); // Nächster Zustand nach Bewegung
        double reward = CalculateReward(); // Belohnung berechnen

        UpdateQValue(state, action, reward, nextState); // Q-Wert anpassen
    }

    // Zustand zusammenbauen: Modus + PacMan Sichtbarkeit + Distanzkategorie
    private string BuildState()
    {
        var pacMan = ExplorePacMan();
        bool seesPacMan = pacMan != null;
        double dist = seesPacMan ? GetDistance(pacMan.Position) : 10;
        string distance = dist < 3 ? "near" : dist < 6 ? "mid" : "far";
        return $"Mode:{Mode}_Sees:{seesPacMan}_Dist:{distance}";
    }

    // Aktion auswählen: zufällig oder beste bekannte Aktion
    private string ChooseAction(string state)
    {
        var occupiable = ExploreOccupiablePositions();
        var possibleActions = _actions
            .Where(a => occupiable.Contains(GetTileInDirection(a)))
            .ToList();

        if (possibleActions.Count == 0)
            return "Stay"; // Keine Bewegung möglich

        // Mit Wahrscheinlichkeit _epsilon zufällige Aktion (Exploration)
        if (_random.NextDouble() < _epsilon)
            return possibleActions[_random.Next(possibleActions.Count)];

        // Ansonsten beste bekannte Aktion (Exploitation)
        return possibleActions
            .OrderByDescending(a => GetQValue(state, a))
            .First();
    }

    // Q-Wert auslesen
    private double GetQValue(string state, string action)
    {
        return _qTable.TryGetValue((state, action), out var value) ? value : 0.0;
    }

    // Q-Wert aktualisieren mit Q-Learning Formel
    private void UpdateQValue(string state, string action, double reward, string nextState, double alpha = 0.1, double gamma = 0.9)
    {
        double oldQ = GetQValue(state, action);
        double maxNextQ = _actions.Max(a => GetQValue(nextState, a));
        double newQ = oldQ + alpha * (reward + gamma * maxNextQ - oldQ);
        _qTable[(state, action)] = newQ;
    }

    // Belohnungsfunktion abhängig vom Ghost-Modus
    private double CalculateReward()
    {
        return Mode switch
        {
            GhostMode.Eaten => -100,
            GhostMode.Frightened => -5,
            GhostMode.Chase when ExplorePacMan() != null => +10,
            _ => -0.1 // kleine Strafe fürs Rumstehen
        };
    }

    // Q-Tabelle speichern
    // Q-Tabelle speichern
    public void SaveQTable()
    {
        var path = $@"C:\Users\alsho\RiderProjects\pacman2\Pacman\Resources\qtable_{GhostName}.csv";
        var lines = _qTable.Select(kvp =>
            $"{kvp.Key.state};{kvp.Key.action};{kvp.Value.ToString(CultureInfo.InvariantCulture)}");

        File.WriteAllLines(path, lines);
    }



    // Q-Tabelle laden
    // Q-Tabelle laden
    private void LoadQTable()
    {
        var path = $@"C:\Users\alsho\RiderProjects\pacman2\Pacman\Resources\qtable_{GhostName}.csv";
        if (!File.Exists(path)) return;

        foreach (var line in File.ReadAllLines(path))
        {
            var parts = line.Split(';');
            if (parts.Length == 3)
            {
                var state = parts[0];
                var action = parts[1];
                var value = double.Parse(parts[2], CultureInfo.InvariantCulture);
                    _qTable[(state, action)] = value; // Einfacher Key: (state, action)
            }
        }
        //Console.WriteLine("Loaded Q Table: " + path + "with " + _qTable.Count + " entries.");
    }



    // Zielposition aus Aktionsrichtung berechnen
    private Position GetTileInDirection(string action)
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

    // PacMan entdecken (im Sichtfeld)
    private PacManAgent? ExplorePacMan()
    {
        return Layer.PacManAgentEnvironment.Explore(Position, VisualRange).FirstOrDefault();
    }

    // Begehbare Positionen im Umfeld ermitteln
    private List<Position> ExploreOccupiablePositions()
    {
        return Layer.OccupiableSpotsEnvironment.Explore(Position, VisualRange, -1)
            .Select(agent => agent.Position)
            .ToList();
    }

    // Zustand des Ghosts prüfen (z. B. Eaten oder noch im Haus)
    private bool ProcessGhostState()
    {
        if (ReleaseTimer <= ReleaseTick)
        {
            ReleaseTimer++;
            return true;
        }

        if (Mode == GhostMode.Frightened)
            return Layer.GetCurrentTick() % 2 == 0;

        if (Mode == GhostMode.Eaten)
        {
            MoveTowardsGoal(new Position(HouseCellX, HouseCellY));
            return true;
        }

        return false;
    }
}
