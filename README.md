# Pac-Man Simulation with Q-Learning Ghosts

## Authors

- Natalie Puls  
- Yamam Al Shoufani

### Game logic and original implementation

This project was developed by the MARS Group for the lecture “KI und Geo-Informatik”. More information below.

## Overview

This simulation implements the classic **Pac-Man** game using the **MARS framework**, now enhanced with a reinforcement learning component: the `SmartGhostAgent`. Ghosts use **Q-learning** to learn effective chasing and avoiding strategies during the simulation. The previous rule-based logic has been removed in favor of this adaptive approach.

Each SmartGhost learns individually using a Q-table that maps environmental states (e.g. Pac-Man distance, visibility, ghost mode) to movement actions. After each episode, the table is saved to disk and updated over multiple runs. To avoid interference, each ghost maintains its **own Q-table**, named after its individual identity (e.g. `ghost_qtable_Blinky.txt`).

---

## Project Structure

Here's an overview of the structure of the project:

- `Program.cs`: The main entry point that starts the simulation.
- `config.json`: Configures simulation parameters like runtime, visualization, and agent setup. See [Configuration](#configuration).
- `Model/`: Contains all the core logic including agent behavior, game rules, environment setup, and utility classes.
- `Resources/`: Contains initialization files for the grid and agents.
- `Visualization/main.py`: A Python-based visualization tool that connects via WebSocket to render the simulation in real-time.

## Model Description

### Agents

- **PacManAgent**: The main agent controlled by logic defined in its `Tick()` method. It can collect pellets, power pellets, and eat ghosts if powered up.
- **GhostAgent**: Classic ghosts with pre-defined AI behavior.
- **SmartGhostAgent**: A programmable ghost agent that learns via **Q-learning**. Learns from state transitions and rewards based on proximity to Pac-Man and ghost state.

  Each `SmartGhostAgent` operates independently and learns from its own perspective using its own `.txt` file to persist Q-values.

- **Pellet & PowerPellet**: Consumable items that increase the score. Power pellets grant Pac-Man the temporary ability to eat ghosts.

### Layers

- **MazeLayer**: The main layer where all agents interact. It manages the environment grid, agent spawning, and the game loop, including score, collisions, and lives.
- **Environments**: Spatial hash environments that manage agent placement and exploration for Pac-Man and ghosts.

## SmartGhostAgent – Learning Ghost AI

This agent type replaces the default ghosts and uses **Q-learning** to learn from in-game interactions:

- **Q-table**: Stored as a dictionary mapping `(state, action)` to Q-values.
- **States** include ghost mode, Pac-Man visibility, and distance category (e.g. "near", "mid", "far").
- **Actions** are directions: Up, Down, Left, Right.
- **Reward function**:
  - +10 for seeing Pac-Man while chasing
  - -5 in frightened mode
  - -100 if eaten
  - -0.1 default penalty per step

Each ghost reads and writes a **separate file** (e.g. `ghost_qtable_Inky.txt`) based on its name to avoid Q-table interference.

## Game Logic

- Pac-Man gains:
  - **10 points** for a pellet,
  - **50 points** for a power pellet,
  - **200 points** for eating a ghost (only when powered up).
- Pac-Man has **3 lives**. A life is lost when a ghost catches him while not powered up.
- If Pac-Man loses a life:
  - He and all ghosts are reset to their **starting positions**.
- If all lives are lost, the simulation ends.
- Agents only interact if they **end up on the same tile** after movement (not mid-path).

## Configuration

The simulation is controlled via the `config.json` file:

- `steps`: Total time steps of the simulation.
- `Visualization`: Set to `true` to enable the Python visualization client.
- `VisualizationTimeout`: Delay between ticks when visualization is active. A **lower number speeds up** the simulation visually.
- `agents` / `layers`: Defines which agents and layers are active and how many agents are spawned.

Only one of `GhostAgent` or `SmartGhostAgent` should be active (`count = 4`), the other must be `0`.

For more details on configuration, refer to the [MARS documentation](https://mars.haw-hamburg.de/articles/core/model-configuration/index.html).

## Simulation & Visualization Setup

### Prerequisites

- [.NET Core 8.0 or later](https://dotnet.microsoft.com/en-us/download)
- [Python 3.11](https://www.python.org/)
- A C# IDE (e.g. [JetBrains Rider](https://www.jetbrains.com/rider/) recommended)

### Run Instructions

1. Clone or download the repository.
2. Open the solution file (`.sln`) in your IDE.
3. Make sure `Visualization` is set to `true` in `config.json`.
4. Start the simulation (`Program.cs`). It will log:  
   `Waiting for live visualization to run.`
5. In a terminal, navigate to the `Visualization/` folder and run:

   ```bash
   pip install -r requirements.txt
   python main.py
  pip3 install -r requirements.txt
  python3 main.py
