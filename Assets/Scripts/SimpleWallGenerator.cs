using System.Collections.Generic;
using UnityEngine;

public class MazeWallGenerator : MonoBehaviour
{
    [Header("Maze Settings")]
    [SerializeField] private int gridWidth = 15;
    [SerializeField] private int gridHeight = 15;
    [SerializeField] private float cellSize = 2f;
    [SerializeField] private float wallDensity = 0.3f; // Reduced from 0.6f to 0.3f for more sparse

    [Header("Wall Properties")]
    [SerializeField] private float wallHeight = 2f;
    [SerializeField] private float wallThickness = 0.5f;

    [Header("Maze Type")]
    [SerializeField] private MazeType mazeType = MazeType.GridBased;
    [SerializeField] private bool createOuterWalls = false; // Disabled outer walls to prevent dead areas
    [SerializeField] private bool guaranteePlayerPath = true;
    [SerializeField] private float playerSpawnRadius = 3f;
    [SerializeField] private bool ensureConnectivity = true; // New: ensure all areas are reachable

    [Header("Materials")]
    [SerializeField] private Material wallMaterial;

    // Grid data
    private bool[,] horizontalWalls; // Walls running east-west
    private bool[,] verticalWalls;   // Walls running north-south
    private List<GameObject> generatedWalls = new List<GameObject>();

    // Center of maze
    private Vector3 mazeCenter;

    public enum MazeType
    {
        GridBased,      // Sparse grid with connectivity checks
        Tactical,       // Tactical layout with cover positions
        Scattered,      // Scattered walls ensuring openness
        OpenCombat      // Open combat layout with strategic walls
    }

    void Start()
    {
        GenerateMaze();
    }

    [ContextMenu("Generate New Maze")]
    public void GenerateMaze()
    {
        ClearExistingWalls();

        // Initialize grid
        mazeCenter = transform.position;
        horizontalWalls = new bool[gridWidth, gridHeight + 1];
        verticalWalls = new bool[gridWidth + 1, gridHeight];

        // Generate maze based on type
        switch (mazeType)
        {
            case MazeType.GridBased:
                GenerateSparseConnectedMaze();
                break;
            case MazeType.Tactical:
                GenerateTacticalLayout();
                break;
            case MazeType.Scattered:
                GenerateScatteredWalls();
                break;
            case MazeType.OpenCombat:
                GenerateOpenCombatLayout();
                break;
        }

        // Create outer walls if enabled (but leave openings)
        if (createOuterWalls)
        {
            CreatePerimeterWithOpenings();
        }

        // Ensure connectivity if enabled
        if (ensureConnectivity)
        {
            EnsureAllAreasConnected();
        }

        // Ensure player spawn area is accessible
        if (guaranteePlayerPath)
        {
            ClearPlayerSpawnArea();
        }

        // Build the actual wall objects
        BuildWallObjects();

        Debug.Log($"Generated maze with {generatedWalls.Count} walls");
        Debug.Log("Please rebake NavMesh: Window > AI > Navigation > Bake");
    }

    void GenerateSparseConnectedMaze()
    {
        // Create sparse walls but avoid enclosed boxes
        float sparseDensity = wallDensity * 0.4f; // Even more sparse

        // First pass: place some walls randomly
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y <= gridHeight; y++)
            {
                if (Random.value < sparseDensity)
                {
                    horizontalWalls[x, y] = true;
                }
            }
        }

        for (int x = 0; x <= gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (Random.value < sparseDensity)
                {
                    verticalWalls[x, y] = true;
                }
            }
        }

        // Second pass: remove walls that create enclosed boxes
        RemoveEnclosingWalls();
    }

    void RemoveEnclosingWalls()
    {
        // Check each cell and ensure it's not completely enclosed
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (IsCellCompletelyEnclosed(x, y))
                {
                    // Remove one wall to create an opening
                    CreateOpeningInCell(x, y);
                }
            }
        }
    }

    bool IsCellCompletelyEnclosed(int cellX, int cellY)
    {
        // Check if a cell is surrounded by walls on all 4 sides
        bool topWall = cellY + 1 <= gridHeight && horizontalWalls[cellX, cellY + 1];
        bool bottomWall = cellY >= 0 && horizontalWalls[cellX, cellY];
        bool leftWall = cellX >= 0 && verticalWalls[cellX, cellY];
        bool rightWall = cellX + 1 <= gridWidth && verticalWalls[cellX + 1, cellY];

        return topWall && bottomWall && leftWall && rightWall;
    }

    void CreateOpeningInCell(int cellX, int cellY)
    {
        // Remove one random wall to create an opening
        int wallToRemove = Random.Range(0, 4);

        switch (wallToRemove)
        {
            case 0: // Remove top wall
                if (cellY + 1 <= gridHeight)
                    horizontalWalls[cellX, cellY + 1] = false;
                break;
            case 1: // Remove bottom wall
                if (cellY >= 0)
                    horizontalWalls[cellX, cellY] = false;
                break;
            case 2: // Remove left wall
                if (cellX >= 0)
                    verticalWalls[cellX, cellY] = false;
                break;
            case 3: // Remove right wall
                if (cellX + 1 <= gridWidth)
                    verticalWalls[cellX + 1, cellY] = false;
                break;
        }
    }

    void GenerateTacticalLayout()
    {
        // Create tactical cover positions without enclosed boxes
        int numCoverClusters = 4;

        for (int cluster = 0; cluster < numCoverClusters; cluster++)
        {
            int centerX = Random.Range(3, gridWidth - 3);
            int centerY = Random.Range(3, gridHeight - 3);

            // Create partial cover (not full enclosures)
            CreatePartialCover(centerX, centerY);
        }

        // Add some scattered individual walls
        int scatteredWalls = 8;
        for (int i = 0; i < scatteredWalls; i++)
        {
            int x = Random.Range(2, gridWidth - 2);
            int y = Random.Range(2, gridHeight - 2);

            // Only place if it won't enclose anything
            if (Random.value < 0.5f)
            {
                horizontalWalls[x, y] = true;
            }
            else
            {
                verticalWalls[x, y] = true;
            }
        }

        // Remove any accidental enclosures
        RemoveEnclosingWalls();
    }

    void CreatePartialCover(int centerX, int centerY)
    {
        // Create L-shaped or corner cover that's always open
        int coverType = Random.Range(0, 3);

        switch (coverType)
        {
            case 0: // L-shape opening to bottom-right
                horizontalWalls[centerX, centerY] = true;
                horizontalWalls[centerX + 1, centerY] = true;
                verticalWalls[centerX, centerY] = true;
                verticalWalls[centerX, centerY + 1] = true;
                // Deliberately leave bottom-right open
                break;

            case 1: // Corner cover
                horizontalWalls[centerX, centerY] = true;
                verticalWalls[centerX, centerY] = true;
                // Only two walls, can't enclose
                break;

            case 2: // Short wall line
                if (Random.value < 0.5f)
                {
                    // Horizontal line
                    horizontalWalls[centerX, centerY] = true;
                    if (centerX + 1 < gridWidth)
                        horizontalWalls[centerX + 1, centerY] = true;
                }
                else
                {
                    // Vertical line
                    verticalWalls[centerX, centerY] = true;
                    if (centerY + 1 < gridHeight)
                        verticalWalls[centerX, centerY + 1] = true;
                }
                break;
        }
    }

    void GenerateScatteredWalls()
    {
        // Scattered individual walls - never create boxes
        int numIndividualWalls = 15;

        for (int i = 0; i < numIndividualWalls; i++)
        {
            int x = Random.Range(2, gridWidth - 2);
            int y = Random.Range(2, gridHeight - 2);

            // Place single walls only
            if (Random.value < 0.5f && x < gridWidth)
            {
                horizontalWalls[x, y] = true;
            }
            else if (y < gridHeight)
            {
                verticalWalls[x, y] = true;
            }
        }

        // Create a few small corner covers (but not enclosed)
        int numCorners = 3;
        for (int i = 0; i < numCorners; i++)
        {
            int x = Random.Range(3, gridWidth - 3);
            int y = Random.Range(3, gridHeight - 3);

            // Create corner (only 2 walls, can't enclose)
            horizontalWalls[x, y] = true;
            verticalWalls[x, y] = true;
        }
    }

    void GenerateOpenCombatLayout()
    {
        // Very open layout with strategic cover points distributed across the map
        int numCoverPoints = 12; // Increased from 5 to spread walls more

        for (int i = 0; i < numCoverPoints; i++)
        {
            // Distribute across the whole map including center
            int x = Random.Range(2, gridWidth - 2);
            int y = Random.Range(2, gridHeight - 2);

            // Create different open cover types
            int coverType = Random.Range(0, 4);
            switch (coverType)
            {
                case 0: // Single wall
                    if (Random.value < 0.5f)
                        horizontalWalls[x, y] = true;
                    else
                        verticalWalls[x, y] = true;
                    break;

                case 1: // T-shape (always has openings)
                    horizontalWalls[x, y] = true;
                    verticalWalls[x, y] = true;
                    if (y + 1 < gridHeight)
                        verticalWalls[x, y + 1] = true;
                    break;

                case 2: // Short wall line (2 walls in a row)
                    if (Random.value < 0.5f)
                    {
                        horizontalWalls[x, y] = true;
                        if (x + 1 < gridWidth)
                            horizontalWalls[x + 1, y] = true;
                    }
                    else
                    {
                        verticalWalls[x, y] = true;
                        if (y + 1 < gridHeight)
                            verticalWalls[x, y + 1] = true;
                    }
                    break;

                case 3: // Corner only (just 2 perpendicular walls)
                    horizontalWalls[x, y] = true;
                    verticalWalls[x, y] = true;
                    break;
            }
        }

        // Make sure no accidental boxes were created
        RemoveEnclosingWalls();
    }

    void CreateLShapedCover(int cornerX, int cornerY)
    {
        // Create L-shaped cover formation
        if (cornerX < gridWidth && cornerY <= gridHeight)
            horizontalWalls[cornerX, cornerY] = true;
        if (cornerX + 1 < gridWidth && cornerY <= gridHeight)
            horizontalWalls[cornerX + 1, cornerY] = true;
        if (cornerX <= gridWidth && cornerY < gridHeight)
            verticalWalls[cornerX, cornerY] = true;
        if (cornerX <= gridWidth && cornerY + 1 < gridHeight)
            verticalWalls[cornerX, cornerY + 1] = true;
    }

    bool WouldCreateDeadArea(int x, int y, bool isHorizontal)
    {
        // Simple check to avoid creating completely enclosed areas
        // This is a basic heuristic - could be more sophisticated

        if (isHorizontal)
        {
            // Check if placing horizontal wall would create dead end
            int surroundingWalls = 0;
            if (x > 0 && horizontalWalls[x - 1, y]) surroundingWalls++;
            if (x < gridWidth - 1 && horizontalWalls[x + 1, y]) surroundingWalls++;
            if (y > 0 && verticalWalls[x, y - 1]) surroundingWalls++;
            if (y < gridHeight && verticalWalls[x + 1, y - 1]) surroundingWalls++;

            return surroundingWalls >= 3; // Don't place if too many surrounding walls
        }
        else
        {
            // Check if placing vertical wall would create dead end
            int surroundingWalls = 0;
            if (y > 0 && verticalWalls[x, y - 1]) surroundingWalls++;
            if (y < gridHeight - 1 && verticalWalls[x, y + 1]) surroundingWalls++;
            if (x > 0 && horizontalWalls[x - 1, y]) surroundingWalls++;
            if (x < gridWidth && horizontalWalls[x - 1, y + 1]) surroundingWalls++;

            return surroundingWalls >= 3;
        }
    }

    void CreatePerimeterWithOpenings()
    {
        // Create perimeter walls but leave several openings
        for (int x = 0; x < gridWidth; x++)
        {
            if (Random.value > 0.3f) // 70% chance for perimeter wall
            {
                horizontalWalls[x, 0] = true;           // Bottom edge
                horizontalWalls[x, gridHeight] = true;  // Top edge
            }
        }

        for (int y = 0; y < gridHeight; y++)
        {
            if (Random.value > 0.3f) // 70% chance for perimeter wall
            {
                verticalWalls[0, y] = true;            // Left edge
                verticalWalls[gridWidth, y] = true;    // Right edge
            }
        }
    }

    void EnsureAllAreasConnected()
    {
        // Just run the box removal check - don't clear the center
        // The RemoveEnclosingWalls() function already handles connectivity

        // Optional: just ensure we don't have any remaining enclosed boxes
        RemoveEnclosingWalls();
    }

    void CarveRoom(int startX, int startY, int width, int height)
    {
        // Remove walls to create open room
        for (int x = startX; x < startX + width && x < gridWidth; x++)
        {
            for (int y = startY; y < startY + height && y <= gridHeight; y++)
            {
                if (y > startY && y < startY + height)
                {
                    horizontalWalls[x, y] = false;
                }
            }
        }

        for (int x = startX; x < startX + width && x <= gridWidth; x++)
        {
            for (int y = startY; y < startY + height && y < gridHeight; y++)
            {
                if (x > startX && x < startX + width)
                {
                    verticalWalls[x, y] = false;
                }
            }
        }
    }

    void CreateRoomConnection(int roomX, int roomY, int roomSize)
    {
        // Create openings from room
        int connectionSide = Random.Range(0, 4);
        int connectionPos = Random.Range(1, roomSize - 1);

        switch (connectionSide)
        {
            case 0: // North
                if (roomY + roomSize <= gridHeight)
                    horizontalWalls[roomX + connectionPos, roomY + roomSize] = false;
                break;
            case 1: // East
                if (roomX + roomSize <= gridWidth)
                    verticalWalls[roomX + roomSize, roomY + connectionPos] = false;
                break;
            case 2: // South
                if (roomY > 0)
                    horizontalWalls[roomX + connectionPos, roomY] = false;
                break;
            case 3: // West
                if (roomX > 0)
                    verticalWalls[roomX, roomY + connectionPos] = false;
                break;
        }
    }

    void CreateCorridors()
    {
        // Create main corridors going across the maze
        int corridorY = gridHeight / 2;
        for (int x = 0; x < gridWidth; x++)
        {
            horizontalWalls[x, corridorY] = false;
            horizontalWalls[x, corridorY + 1] = false;
        }

        int corridorX = gridWidth / 2;
        for (int y = 0; y < gridHeight; y++)
        {
            verticalWalls[corridorX, y] = false;
            verticalWalls[corridorX + 1, y] = false;
        }
    }

    void CreateOuterWalls()
    {
        // Create walls around the perimeter
        for (int x = 0; x < gridWidth; x++)
        {
            horizontalWalls[x, 0] = true;           // Bottom edge
            horizontalWalls[x, gridHeight] = true;  // Top edge
        }

        for (int y = 0; y < gridHeight; y++)
        {
            verticalWalls[0, y] = true;            // Left edge
            verticalWalls[gridWidth, y] = true;    // Right edge
        }
    }

    void ClearPlayerSpawnArea()
    {
        // Only clear a small area near center for player spawn
        int centerX = gridWidth / 2;
        int centerY = gridHeight / 2;
        int clearRadius = 1; // Much smaller clear radius

        for (int x = centerX - clearRadius; x <= centerX + clearRadius; x++)
        {
            for (int y = centerY - clearRadius; y <= centerY + clearRadius; y++)
            {
                if (x >= 0 && x < gridWidth && y >= 0 && y <= gridHeight)
                {
                    horizontalWalls[x, y] = false;
                }
                if (x >= 0 && x <= gridWidth && y >= 0 && y < gridHeight)
                {
                    verticalWalls[x, y] = false;
                }
            }
        }
    }

    void BuildWallObjects()
    {
        // Create horizontal walls (running east-west)
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y <= gridHeight; y++)
            {
                if (horizontalWalls[x, y])
                {
                    Vector3 position = GetWorldPosition(x, y) + new Vector3(0, 0, -cellSize / 2f);
                    CreateWallObject(position, new Vector3(cellSize, wallHeight, wallThickness), "HorizontalWall");
                }
            }
        }

        // Create vertical walls (running north-south)
        for (int x = 0; x <= gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (verticalWalls[x, y])
                {
                    Vector3 position = GetWorldPosition(x, y) + new Vector3(-cellSize / 2f, 0, 0);
                    CreateWallObject(position, new Vector3(wallThickness, wallHeight, cellSize), "VerticalWall");
                }
            }
        }
    }

    Vector3 GetWorldPosition(int gridX, int gridY)
    {
        float worldX = (gridX - gridWidth / 2f) * cellSize;
        float worldZ = (gridY - gridHeight / 2f) * cellSize;
        return mazeCenter + new Vector3(worldX, wallHeight / 2f, worldZ);
    }

    void CreateWallObject(Vector3 position, Vector3 scale, string wallName)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.tag = "Cover";
        wall.transform.parent = transform;
        wall.transform.position = position;
        wall.transform.localScale = scale;

        // Apply material
        Renderer renderer = wall.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (wallMaterial != null)
            {
                renderer.material = wallMaterial;
            }
            else
            {
                renderer.material.color = new Color(0.7f, 0.7f, 0.7f);
            }
        }

        generatedWalls.Add(wall);
    }

    void ClearExistingWalls()
    {
        foreach (GameObject wall in generatedWalls)
        {
            if (wall != null)
            {
                DestroyImmediate(wall);
            }
        }
        generatedWalls.Clear();
    }

    void OnDrawGizmosSelected()
    {
        // Draw grid
        Gizmos.color = Color.yellow;
        Vector3 totalSize = new Vector3(gridWidth * cellSize, 0.1f, gridHeight * cellSize);
        Gizmos.DrawWireCube(mazeCenter, totalSize);

        // Draw player spawn area
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(mazeCenter, playerSpawnRadius);
    }

    [ContextMenu("Clear All Walls")]
    public void ClearAllWalls()
    {
        ClearExistingWalls();
    }
}