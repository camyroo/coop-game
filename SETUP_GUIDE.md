# Unity Co-op Game Setup Guide

## Overview
This guide will walk you through setting up all the game objects and scripts in your Unity scene.

## Step 1: Create the GameConfig ScriptableObject

1. In Unity, right-click in the `Assets` folder (or create a `Resources` folder if you don't have one)
2. Select `Create > Game > Config`
3. Name it `GameConfig`
4. Configure the settings as needed:
   - **Network Settings**: Max players (default: 4), Tick rate (default: 60)
   - **Game Rules**: Round duration (default: 300s), Max rounds (default: 3)
   - **Player Settings**: Move speed (default: 5), Rotation speed (default: 10), Grab distance (default: 2)
   - **Object Settings**: Max placeable objects (default: 50)
   - **Grid Settings**: Tile size (default: 2), Grid size (default: 20x20)

## Step 2: Create Core Game Objects

### A. GameManager (Empty GameObject)
1. Create an empty GameObject: `GameObject > Create Empty`
2. Name it `GameManager`
3. Add the following scripts:
   - `GameStateManager` script
   - Assign the `GameConfig` asset you created to the Config field

### B. NetworkManager (Unity Netcode)
1. Create an empty GameObject: `GameObject > Create Empty`
2. Name it `NetworkManager`
3. Add component: `NetworkManager` (from Unity Netcode)
4. Add your custom `NetworkingManager` script
5. Configure NetworkManager:
   - Set the transport (usually Unity Transport)
   - Assign the Player Prefab (see Step 3)

### C. ObjectSpawner
1. Create an empty GameObject: `GameObject > Create Empty`
2. Name it `ObjectSpawner`
3. Add the `ObjectSpawner` script
4. Assign the `GameConfig` asset to the Config field
5. Add prefabs to spawn to the `Spawnable Objects` array (if applicable)

### D. LevelGrid
1. Create an empty GameObject: `GameObject > Create Empty`
2. Name it `LevelGrid`
3. Add the `LevelGrid` script
4. Assign the `GameConfig` asset to the Config field

## Step 3: Set Up the Player Prefab

Your `Player.prefab` already exists in `Assets/Prefabs/`. You need to ensure it has all the required components:

1. Open `Assets/Prefabs/Player.prefab`
2. Make sure it has these components:
   - `NetworkObject` (required for networking)
   - `PlayerController` script
   - `PlayerMovement` script
   - `PlayerInput` script
   - `PlayerGrabSystem` script
   - `Rigidbody` (for physics)
   - `CapsuleCollider` (or similar collider)
   - `Renderer` (for the visual mesh)

3. **Add a TextMeshPro component for the name tag:**
   - Create a child GameObject under the Player
   - Name it `NameTag`
   - Add a `TextMeshProUGUI` or `TextMeshPro` component
   - Position it above the player (e.g., Y = 2)
   - In the `PlayerController` script, assign this TextMeshPro component to the `Name Text` field

4. **Configure PlayerInput:**
   - Assign the `InputSystem_Actions` asset to the Actions field
   - This is located at `Assets/InputSystem_Actions.inputactions`

## Step 4: Set Up Placeable Objects

For objects that players can grab and place:

1. Open or create object prefabs (e.g., `Cylinder.prefab`, `Tree.prefab`)
2. Add these components to each:
   - `NetworkObject` (required for networking)
   - `PlaceableObject` script
   - `Rigidbody` (for physics)
   - Collider component (BoxCollider, SphereCollider, etc.)

3. Make sure to implement `IGrabbable` interface if not already done
4. Configure Rigidbody:
   - Mass: 1-10 depending on object
   - Drag: 0.5-1
   - Angular Drag: 0.05

## Step 5: Set Up UI

### A. Game UI (In-Game HUD)
1. Create a Canvas: `GameObject > UI > Canvas`
2. Name it `GameUI`
3. Add the `GameUI` script to the Canvas
4. Create child UI elements:
   - Timer Text (TextMeshProUGUI)
   - Round Counter (TextMeshProUGUI)
   - Any other HUD elements you need
5. Assign these elements to the `GameUI` script fields

### B. Menu UI (Main Menu)
1. Create another Canvas: `GameObject > UI > Canvas`
2. Name it `MenuUI`
3. Add the `MenuUI` script
4. Create child UI elements:
   - Host Button
   - Join Button
   - Settings Button
   - etc.
5. Assign these buttons to the `MenuUI` script fields

## Step 6: Configure Network Settings

1. Select the `NetworkManager` GameObject
2. In the NetworkManager component:
   - **Player Prefab**: Assign `Assets/Prefabs/Player.prefab`
   - **Network Prefabs List**: Add all networked prefabs:
     - Player.prefab
     - Cylinder.prefab
     - Tree.prefab
     - Any other networked objects
3. Also add these prefabs to the `DefaultNetworkPrefabs` asset at `Assets/DefaultNetworkPrefabs.asset`

## Step 7: Set Up the Scene

1. Create a ground plane:
   - `GameObject > 3D Object > Plane`
   - Scale it to match your grid size
   - Add a collider if not present

2. Add spawn points for players (optional):
   - Create empty GameObjects named `SpawnPoint1`, `SpawnPoint2`, etc.
   - Position them around the map
   - You can reference these in your spawning logic

3. Add any environment objects (walls, obstacles, etc.)

## Step 8: Configure Layers and Tags (if needed)

1. Go to `Edit > Project Settings > Tags and Layers`
2. Add any custom layers or tags your scripts might use:
   - Tag: `Player`
   - Tag: `Grabbable`
   - Layer: `Player`
   - Layer: `Objects`

3. Assign tags/layers to your prefabs accordingly

## Step 9: Test the Setup

### Testing Network Connection:
1. Go to `NetworkManager` GameObject
2. In Play mode, click "Start Host" to test locally
3. You can build and run a second instance to test multiplayer

### Testing Player Movement:
1. Ensure Input System package is installed
2. Your controls should work based on `InputSystem_Actions`
3. Check the Input System settings if controls don't work

### Testing Object Grabbing:
1. Place some `PlaceableObject` prefabs in the scene
2. In Play mode, move near them and press the grab button
3. Verify you can pick up and place objects

## Common Issues & Solutions

### Issue: "NetworkManager not found"
**Solution**: Make sure NetworkManager GameObject is in the scene and has the NetworkManager component

### Issue: "Player not spawning"
**Solution**: Check that Player.prefab is assigned to NetworkManager's Player Prefab field and has a NetworkObject component

### Issue: "Input not working"
**Solution**: Ensure InputSystem_Actions.inputactions is assigned in PlayerInput component

### Issue: "Objects not syncing across network"
**Solution**: Make sure all prefabs have NetworkObject component and are added to Network Prefabs List

### Issue: "GameConfig null reference"
**Solution**: Create the GameConfig ScriptableObject and assign it to all scripts that need it (GameStateManager, ObjectSpawner, LevelGrid)

## Hierarchy Overview

After setup, your scene hierarchy should look something like this:

```
SampleScene
├── GameManager (GameStateManager)
├── NetworkManager (NetworkManager + NetworkingManager)
├── ObjectSpawner
├── LevelGrid
├── MenuUI (Canvas)
│   ├── HostButton
│   ├── JoinButton
│   └── ...
├── GameUI (Canvas)
│   ├── TimerText
│   ├── RoundCounter
│   └── ...
├── Ground (Plane)
└── Environment
    ├── Walls
    └── Obstacles
```

## Next Steps

1. Configure your `GameConfig` values to match your desired gameplay
2. Create additional placeable object prefabs
3. Design your level layout
4. Add visual polish (materials, lighting, etc.)
5. Test multiplayer functionality
6. Build and distribute!

---

**Note**: Make sure you have the following Unity packages installed:
- Unity Netcode for GameObjects
- Input System
- TextMeshPro (should be built-in)
