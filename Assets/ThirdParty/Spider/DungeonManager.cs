using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class DungeonManager : MonoBehaviour
{
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public Slider progressSlider;
    public Vector2Int gridSize = new Vector2Int(10, 10);
    public Vector2Int startPoint = new Vector2Int(0, 0);
    public Vector2Int endPoint = new Vector2Int(9, 9);

    private List<Vector2Int> fullPath = new List<Vector2Int>();
    private List<Vector2Int> currentPath = new List<Vector2Int>();

    void Start()
    {
        GenerateDungeonLayout();
        progressSlider.maxValue = gridSize.x * gridSize.y;
        progressSlider.onValueChanged.AddListener(UpdateDungeonProgress);
        UpdateDungeonProgress(0); // Set initial state
    }

    void GenerateDungeonLayout()
    {
        GenerateFullPath();
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                // Create floor
                Instantiate(floorPrefab, new Vector3(x, 0, y), Quaternion.identity, transform);

                // Create walls everywhere initially
                Instantiate(wallPrefab, new Vector3(x, 0.5f, y), Quaternion.identity, transform);
            }
        }
    }

    public void UpdateDungeonProgress(float value)
    {
        int steps = Mathf.FloorToInt(value);
        currentPath.Clear();

        for (int i = 0; i < steps && i < fullPath.Count; i++)
        {
            currentPath.Add(fullPath[i]);
        }

        // Clear previous path in visualization
        foreach (Transform child in transform)
        {
            if (child.gameObject.CompareTag("Wall"))
                Destroy(child.gameObject);
        }

        CreateCurrentPath();
    }

    void GenerateFullPath()
    {
        Vector2Int currentPosition = startPoint;
        Stack<Vector2Int> positionStack = new Stack<Vector2Int>();
        positionStack.Push(currentPosition);

        while (positionStack.Count > 0)
        {
            currentPosition = positionStack.Peek();
            fullPath.Add(currentPosition);

            List<Vector2Int> possibleDirections = GetPossibleDirections(currentPosition);

            if (possibleDirections.Count > 0)
            {
                Vector2Int chosenDirection = possibleDirections[Random.Range(0, possibleDirections.Count)];
                Vector2Int nextPosition = currentPosition + chosenDirection;

                fullPath.Add(nextPosition);
                positionStack.Push(nextPosition);

                if (nextPosition == endPoint)
                    break;
            }
            else
            {
                positionStack.Pop();
            }
        }
    }

    List<Vector2Int> GetPossibleDirections(Vector2Int currentPosition)
    {
        List<Vector2Int> possibleDirections = new List<Vector2Int>
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        possibleDirections = possibleDirections.FindAll(dir =>
        {
            Vector2Int nextPos = currentPosition + dir;
            return !fullPath.Contains(nextPos)
                && nextPos.x >= 0
                && nextPos.x < gridSize.x
                && nextPos.y >= 0
                && nextPos.y < gridSize.y;
        });

        return possibleDirections;
    }

    void CreateCurrentPath()
    {
        foreach (Vector2Int position in currentPath)
        {
            // Remove walls if they exist on our path
            Collider[] colliders = Physics.OverlapSphere(new Vector3(position.x, 0.5f, position.y), 0.25f);
            foreach (var collider in colliders)
            {
                if (collider.gameObject.CompareTag("Wall"))
                {
                    Destroy(collider.gameObject);
                }
            }
        }
    }
    void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawCube(new Vector3(startPoint.x, 0, startPoint.y), Vector3.one);
        Gizmos.color = Color.red;
        Gizmos.DrawCube(new Vector3(endPoint.x, 0, endPoint.y), Vector3.one);
    }
}
