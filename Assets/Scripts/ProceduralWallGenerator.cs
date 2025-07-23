using System.Collections.Generic;
using UnityEngine;

public class TacticalWallGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    [SerializeField] private int numberOfVerticalWalls = 8;
    [SerializeField] private int numberOfHorizontalWalls = 8;
    [SerializeField] private int numberOfLShapedWalls = 5;
    [SerializeField] private Vector2 mapSize = new Vector2(20, 20);

    [Header("Wall Properties")]
    [SerializeField] private float wallHeight = 2f;
    [SerializeField] private float wallThickness = 0.5f;
    [SerializeField] private Vector2 straightWallLengthRange = new Vector2(3f, 8f);
    [SerializeField] private Vector2 lShapeArmLengthRange = new Vector2(2f, 5f);

    [Header("Spacing")]
    [SerializeField] private float minDistanceBetweenWalls = 2f;
    [SerializeField] private bool keepPlayerSpawnClear = true;
    [SerializeField] private float playerSpawnRadius = 4f;

    [Header("Materials")]
    [SerializeField] private Material wallMaterial;

    // Generated objects
    private List<GameObject> generatedWalls = new List<GameObject>();
    private List<Vector3> occupiedPositions = new List<Vector3>();

    void Start()
    {
        GenerateWalls();
    }

    [ContextMenu("Generate New Walls")]
    public void GenerateWalls()
    {
        ClearExistingWalls();
        occupiedPositions.Clear();

        // Reserve player spawn area
        if (keepPlayerSpawnClear)
        {
            ReservePlayerSpawnArea();
        }

        // Generate different wall types
        GenerateVerticalWalls();
        GenerateHorizontalWalls();
        GenerateLShapedWalls();

        Debug.Log($"Generated {generatedWalls.Count} wall objects");
        Debug.Log("Please rebake NavMesh: Window > AI > Navigation > Bake");
    }

    void ReservePlayerSpawnArea()
    {
        // Reserve a circular area around origin for player spawn
        for (float angle = 0; angle < 360; angle += 20f)
        {
            for (float radius = 0; radius <= playerSpawnRadius; radius += 0.5f)
            {
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    0,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * radius
                );
                occupiedPositions.Add(pos);
            }
        }
    }

    void GenerateVerticalWalls()
    {
        for (int i = 0; i < numberOfVerticalWalls; i++)
        {
            Vector3 position = GetRandomValidPosition(2f);
            if (position != Vector3.zero)
            {
                float length = Random.Range(straightWallLengthRange.x, straightWallLengthRange.y);
                CreateStraightWall(position, Vector3.forward, length, "VerticalWall");
            }
        }
    }

    void GenerateHorizontalWalls()
    {
        for (int i = 0; i < numberOfHorizontalWalls; i++)
        {
            Vector3 position = GetRandomValidPosition(2f);
            if (position != Vector3.zero)
            {
                float length = Random.Range(straightWallLengthRange.x, straightWallLengthRange.y);
                CreateStraightWall(position, Vector3.right, length, "HorizontalWall");
            }
        }
    }

    void GenerateLShapedWalls()
    {
        for (int i = 0; i < numberOfLShapedWalls; i++)
        {
            Vector3 position = GetRandomValidPosition(3f);
            if (position != Vector3.zero)
            {
                CreateLShapedWall(position);
            }
        }
    }

    void CreateStraightWall(Vector3 startPosition, Vector3 direction, float length, string wallName)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.tag = "Cover";
        wall.transform.parent = transform;

        // Position at center of wall
        Vector3 wallCenter = startPosition + (direction * length / 2f);
        wall.transform.position = wallCenter + Vector3.up * wallHeight / 2f;

        // Scale based on direction
        if (direction == Vector3.forward) // Vertical wall (runs north-south)
        {
            wall.transform.localScale = new Vector3(wallThickness, wallHeight, length);
        }
        else if (direction == Vector3.right) // Horizontal wall (runs east-west)
        {
            wall.transform.localScale = new Vector3(length, wallHeight, wallThickness);
        }

        // Apply material
        ApplyWallMaterial(wall);

        generatedWalls.Add(wall);

        // Mark positions as occupied
        MarkWallAreaAsOccupied(startPosition, direction, length);
    }

    void CreateLShapedWall(Vector3 cornerPosition)
    {
        // Create L-shaped wall with two perpendicular segments
        float arm1Length = Random.Range(lShapeArmLengthRange.x, lShapeArmLengthRange.y);
        float arm2Length = Random.Range(lShapeArmLengthRange.x, lShapeArmLengthRange.y);

        // Randomly choose L-shape orientation (4 possible orientations)
        int orientation = Random.Range(0, 4);
        Vector3 arm1Direction, arm2Direction;

        switch (orientation)
        {
            case 0: // L opens to bottom-right
                arm1Direction = Vector3.right;   // Horizontal arm
                arm2Direction = Vector3.back;    // Vertical arm going down
                break;
            case 1: // L opens to bottom-left
                arm1Direction = Vector3.left;    // Horizontal arm
                arm2Direction = Vector3.back;    // Vertical arm going down
                break;
            case 2: // L opens to top-left
                arm1Direction = Vector3.left;    // Horizontal arm
                arm2Direction = Vector3.forward; // Vertical arm going up
                break;
            default: // L opens to top-right
                arm1Direction = Vector3.right;   // Horizontal arm
                arm2Direction = Vector3.forward; // Vertical arm going up
                break;
        }

        // Create first arm (horizontal)
        CreateStraightWall(cornerPosition, arm1Direction, arm1Length, "LWall_Horizontal");

        // Create second arm (vertical) - starts from corner
        CreateStraightWall(cornerPosition, arm2Direction, arm2Length, "LWall_Vertical");
    }

    void MarkWallAreaAsOccupied(Vector3 startPosition, Vector3 direction, float length)
    {
        // Mark several points along the wall as occupied
        int points = Mathf.CeilToInt(length);
        for (int i = 0; i <= points; i++)
        {
            Vector3 pos = startPosition + direction * (length * i / points);
            occupiedPositions.Add(pos);
        }
    }

    void ApplyWallMaterial(GameObject wall)
    {
        Renderer renderer = wall.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (wallMaterial != null)
            {
                renderer.material = wallMaterial;
            }
            else
            {
                // Default gray material
                renderer.material.color = new Color(0.7f, 0.7f, 0.7f);
            }
        }
    }

    Vector3 GetRandomValidPosition(float clearanceRadius)
    {
        int attempts = 0;
        int maxAttempts = 100;

        while (attempts < maxAttempts)
        {
            Vector3 randomPos = new Vector3(
                Random.Range(-mapSize.x / 2f + clearanceRadius, mapSize.x / 2f - clearanceRadius),
                0,
                Random.Range(-mapSize.y / 2f + clearanceRadius, mapSize.y / 2f - clearanceRadius)
            );

            if (IsPositionValid(randomPos, clearanceRadius))
            {
                return randomPos;
            }
            attempts++;
        }

        return Vector3.zero; // Failed to find valid position
    }

    bool IsPositionValid(Vector3 position, float minDistance)
    {
        foreach (Vector3 occupied in occupiedPositions)
        {
            if (Vector3.Distance(position, occupied) < minDistance + minDistanceBetweenWalls)
            {
                return false;
            }
        }
        return true;
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
        // Draw map boundaries
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(mapSize.x, 0.1f, mapSize.y));

        // Draw player spawn area
        if (keepPlayerSpawnClear)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, playerSpawnRadius);
        }

        // Draw wall preview
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Vector3.up * wallHeight / 2f, new Vector3(1f, wallHeight, wallThickness));
    }

    [ContextMenu("Clear All Walls")]
    public void ClearAllWalls()
    {
        ClearExistingWalls();
        occupiedPositions.Clear();
    }
}