using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;
using System.Linq;

public class Generator2D : MonoBehaviour
{
    // Enum to define different types of cells
    enum CellType
    {
        None,
        Room,
        Hallway
    }

    // Class that defines a Room with a position and size
    class Room
    {
        public RectInt bounds;
        public GameObject assignedRoomObj;

        public Room(Vector2Int location, Vector2Int size)
        {
            bounds = new RectInt(location, size);
        }

        // Check if two rooms intersect
        public static bool Intersect(Room a, Room b)
        {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x)
                || (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y));
        }
    }

    // Serialized fields are editable from the Unity editor
    [SerializeField]
    [Tooltip("If none defaut to {0, 0, 0}")]
    GameObject spawnPoint;
    [SerializeField]
    Vector2Int size; // Overall grid size
    [SerializeField]
    int roomCount; // Number of rooms to generate
    [SerializeField]
    [Min(0)]
    Vector2Int roomMaxSize; // Maximum size for any room
    [SerializeField]
    Vector2Int roomMinSize; // Maximum size for any room
    [SerializeField]
    GameObject cubePrefab; // Prefab for visualization
    [SerializeField]
    Material redMaterial; // Material for rooms
    [SerializeField]
    Material blueMaterial; // Material for hallways
    [SerializeField]
    Material startRoomMaterial; // Material for hallways
    [SerializeField]
    Material endRoomMaterial; // Material for hallways
    [SerializeField]
    string[] tags = { "Room", "Hallway" }; // Tags for identification

    // Private fields
    Random random; // Random generator
    Grid2D<CellType> grid; // 2D grid to represent our world
    List<Room> rooms; // List to store rooms
    List<RoomSettings> roomWalls; // List to store rooms
    Room startRoom;
    Room endRoom;
    Delaunay2D delaunay; // Delaunay triangulation for inter-room connections
    HashSet<Prim.Edge> selectedEdges; // Hallway paths

    // Fields to store the empty GameObjects for organization
    private GameObject dungeonHolder;
    private GameObject roomParent;
    private GameObject hallwayParent;
    void Start()
    {
        // Initialize the empty GameObjects for organization in the scene hierarchy
        dungeonHolder = new GameObject("Dungeon Holder");
        roomParent = new GameObject("Rooms");
        hallwayParent = new GameObject("Hallways");

        roomParent.transform.SetParent(dungeonHolder.transform);
        hallwayParent.transform.SetParent(dungeonHolder.transform);
        Generate();
    }

    // Main generation method
    void Generate()
    {
        random = new Random(0); // Initialize random with a seed
        grid = new Grid2D<CellType>(size, Vector2Int.zero); // Create a new grid
        rooms = new List<Room>(); // Initialize rooms list
        roomWalls = new();

        // Different steps in the generation process
        PlaceRooms(); // Place rooms on the grid
        Triangulate(); // Delaunay triangulation
        CreateHallways(); // Create hallways between rooms
        PathfindHallways(); // Pathfinding for hallways
        CreateStartAndEnd();
        AdjustRoomWalls();
        PrintGrid();
    }
    void PrintGrid()
    {
        string gridRepresentation = "";

        for (int y = size.y - 1; y >= 0; y--)
        {
            for (int x = 0; x < size.x; x++)
            {
                CellType cellType = grid[new Vector2Int(x, y)];
                char cellChar = GetCellChar(cellType);
                gridRepresentation += $"{cellChar} "; // Append cell char with space for alignment
            }
            gridRepresentation += "\n";
        }

        Debug.Log(gridRepresentation);
    }

    char GetCellChar(CellType cellType)
    {
        switch (cellType)
        {
            case CellType.Room:
                return '8'; // Represent room cell
            case CellType.Hallway:
                return '5'; // Represent hallway cell
            default:
                return '2'; // Represent empty cell and any other undefined types
        }
    }



    // Method to place rooms on the grid
    void PlaceRooms()
    {
        for (int i = 0; i < roomCount; i++)
        {
            // Randomize room location and size
            Vector2Int location = new Vector2Int(random.Next(0, size.x), random.Next(0, size.y));
            Vector2Int roomSize = new Vector2Int(random.Next(roomMinSize.x, roomMaxSize.x + 1), random.Next(roomMinSize.y, roomMaxSize.y + 1));

            bool add = true;
            Room newRoom = new Room(location, roomSize);
            Room buffer = new Room(location + new Vector2Int(-1, -1), roomSize + new Vector2Int(2, 2));

            // Check if the room intersects with any other rooms
            foreach (var room in rooms)
            {
                if (Room.Intersect(room, buffer))
                {
                    add = false;
                    break;
                }
            }

            // Check if the room bounds exceed the grid boundaries
            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
                || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y)
            {
                add = false;
            }

            if (add)
            {
                rooms.Add(newRoom);
                PlaceRoom(newRoom.bounds.position, newRoom.bounds.size, rooms.Count - 1);

                // Mark the room positions on the grid
                foreach (var pos in newRoom.bounds.allPositionsWithin)
                {
                    grid[pos] = CellType.Room;
                }
            }
        }
    }
    // Create Delaunay triangulation for the rooms
    void Triangulate()
    {
        List<Vertex> vertices = new List<Vertex>();
        foreach (var room in rooms)
        {
            vertices.Add(new Vertex<Room>((Vector2)room.bounds.position + ((Vector2)room.bounds.size) / 2, room));
        }
        delaunay = Delaunay2D.Triangulate(vertices);
    }

    // Create hallways based on the Delaunay triangulation
    void CreateHallways()
    {
        List<Prim.Edge> edges = new List<Prim.Edge>();
        foreach (var edge in delaunay.Edges)
        {
            edges.Add(new Prim.Edge(edge.U, edge.V));
        }

        // Generate minimum spanning tree from edges
        List<Prim.Edge> mst = Prim.MinimumSpanningTree(edges, edges[0].U);
        selectedEdges = new HashSet<Prim.Edge>(mst);
        var remainingEdges = new HashSet<Prim.Edge>(edges);
        remainingEdges.ExceptWith(selectedEdges);

        // Optionally add some remaining edges to our hallways
        foreach (var edge in remainingEdges)
        {
            if (random.NextDouble() < 0.125)
            {
                selectedEdges.Add(edge);
            }
        }
    }

    void PathfindHallways()
    {
        DungeonPathfinder2D aStar = new DungeonPathfinder2D(size);

        foreach (var edge in selectedEdges)
        {
            var startRoom = (edge.U as Vertex<Room>).Item;
            var endRoom = (edge.V as Vertex<Room>).Item;

            var startPosf = startRoom.bounds.center;
            var endPosf = endRoom.bounds.center;
            var startPos = new Vector2Int((int)startPosf.x, (int)startPosf.y);
            var endPos = new Vector2Int((int)endPosf.x, (int)endPosf.y);

            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder2D.Node a, DungeonPathfinder2D.Node b) =>
            {
                var pathCost = new DungeonPathfinder2D.PathCost();

                pathCost.cost = Vector2Int.Distance(b.Position, endPos);    //heuristic

                if (grid[b.Position] == CellType.Room)
                {
                    pathCost.cost += 10;
                }
                else if (grid[b.Position] == CellType.None)
                {
                    pathCost.cost += 5;
                }
                else if (grid[b.Position] == CellType.Hallway)
                {
                    pathCost.cost += 1;
                }

                pathCost.traversable = true;

                return pathCost;
            });

            if (path != null)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    var current = path[i];

                    if (grid[current] == CellType.None)
                    {
                        grid[current] = CellType.Hallway;
                    }

                    if (i > 0)
                    {
                        var prev = path[i - 1];

                        var delta = current - prev;
                    }
                }

                foreach (var pos in path)
                {
                    if (grid[pos] == CellType.Hallway)
                    {
                        PlaceHallway(pos);
                    }
                }
            }
        }
    }

    void PlaceCube(Vector2Int location, Vector2Int size, Material material, int roomId, string tag = "Default")
    {
        //GameObject go = Instantiate(cubePrefab, new Vector3(location.x, 0, location.y), Quaternion.identity);
        //go.GetComponent<Transform>().localScale = new Vector3(size.x, 1, size.y);
        // Assuming each model is 1x1 units
        int modelsInX = (int)size.x;
        int modelsInY = (int)size.y;

        for (int i = 0; i < modelsInX; i++)
        {
            for (int j = 0; j < modelsInY; j++)
            {
                // Calculate the position for each model
                Vector3 spawnPosition = new Vector3(location.x + i, 0, location.y + j);
                GameObject go = Instantiate(cubePrefab, spawnPosition, Quaternion.identity);
                RoomSettings rs= go.GetComponent<RoomSettings>();
                rs.gridPos = new Vector2Int(location.x + i, location.y + j);
                roomWalls.Add(go.GetComponent<RoomSettings>());
                MeshRenderer goMR;
                goMR = go.GetComponent<MeshRenderer>() != null ? go.GetComponent<MeshRenderer>() : go.GetComponentInChildren<MeshRenderer>();
                goMR.material = material;
                if (roomId > -1)
                {
                    rooms[roomId].assignedRoomObj = go;
                }
                go.tag = tag;

                // Set parent based on tag
                if (go.CompareTag(tags[0])) // "Room"
                {
                    go.transform.SetParent(roomParent.transform);
                }
                else if (go.CompareTag(tags[1])) // "Hallway"
                {
                    go.transform.SetParent(hallwayParent.transform);
                }
            }
        }
        
    }

    void AdjustRoomWalls()
    {
        foreach (var room in roomWalls)
        {
            room.northWall.SetActive(!IsNonEmptyCell(new Vector2Int(room.gridPos.x, room.gridPos.y + 1)));
            room.southWall.SetActive(!IsNonEmptyCell(new Vector2Int(room.gridPos.x, room.gridPos.y - 1)));
            room.westWall.SetActive(!IsNonEmptyCell(new Vector2Int(room.gridPos.x - 1, room.gridPos.y)));
            room.eastWall.SetActive(!IsNonEmptyCell(new Vector2Int(room.gridPos.x + 1, room.gridPos.y)));
        }
    }

    bool IsNonEmptyCell(Vector2Int position)
    {
        if (position.x < 0 || position.x >= size.x || position.y < 0 || position.y >= size.y)
            return false;

        return grid[position] != CellType.None;
    }

    void PlaceRoom(Vector2Int location, Vector2Int size, int roomId)
    {
        PlaceCube(location, size, redMaterial, roomId, tags[0]);
    }

    void PlaceHallway(Vector2Int location)
    {
        PlaceCube(location, new Vector2Int(1, 1), blueMaterial, -1, tags[1]);
    }

    void CreateStartAndEnd()
    {
        // Find bounding box center
        RectInt boundingBoxMaze = GetBoundingBox(rooms);
        Vector2 center = new Vector2(boundingBoxMaze.center.x, boundingBoxMaze.center.y);

        startRoom = GetFarthestRoom(center, rooms);
        endRoom = GetFarthestRoom(startRoom.bounds.position, rooms);
        MeshRenderer goMR;
        goMR = startRoom.assignedRoomObj.GetComponent<MeshRenderer>() != null ? startRoom.assignedRoomObj.GetComponent<MeshRenderer>() : startRoom.assignedRoomObj.GetComponentInChildren<MeshRenderer>();
        goMR.material = startRoomMaterial;
        goMR = endRoom.assignedRoomObj.GetComponent<MeshRenderer>() != null ? endRoom.assignedRoomObj.GetComponent<MeshRenderer>() : endRoom.assignedRoomObj.GetComponentInChildren<MeshRenderer>();
        goMR.material = endRoomMaterial;

        MoveMazeToSpawnPoint(startRoom);
    }

    void MoveMazeToSpawnPoint(Room startRoom)
    {
        if (!spawnPoint)
        {
            dungeonHolder.transform.position -= startRoom.assignedRoomObj.transform.position + (startRoom.assignedRoomObj.transform.localScale / 2); //Set StartRoom pos to 0,0,0
        }
        else
        {
            dungeonHolder.transform.position -= startRoom.assignedRoomObj.transform.position + (startRoom.assignedRoomObj.transform.localScale / 2) - spawnPoint.transform.position;
        }
    }
    private static RectInt GetBoundingBox(List<Room> rects)
    {
        if (rects.Count == 0)
        {
            Debug.Log("List of RectInts is empty");
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (var rect in rects)
        {
            if (rect.bounds.xMin < minX) minX = rect.bounds.xMin;
            if (rect.bounds.yMin < minY) minY = rect.bounds.yMin;
            if (rect.bounds.xMax > maxX) maxX = rect.bounds.xMax;
            if (rect.bounds.yMax > maxY) maxY = rect.bounds.yMax;
        }

        return new RectInt(minX, minY, maxX - minX, maxY - minY);
    }
    static Room GetFarthestRoom(Vector2 referencePoint, List<Room> rooms)
    {
        Vector2 referenceCenter = referencePoint;
        float maxDistance = float.MinValue;
        Room farthestRoom = null;

        foreach (Room room in rooms)
        {
            float currentDistance = Vector2.Distance(referenceCenter, room.bounds.center);
            if (currentDistance > maxDistance)
            {
                maxDistance = currentDistance;
                farthestRoom = room;
            }
        }

        return farthestRoom;
    }
    //private void OnDrawGizmos()
    //{
    //    if (!Application.isPlaying)
    //    {
    //        return;
    //    }
    //    RectInt boundingBoxMaze = GetBoundingBox(rooms);
    //    Vector2 center = new Vector2(boundingBoxMaze.center.x, boundingBoxMaze.center.y);

    //    Gizmos.color = Color.black;
    //    Gizmos.DrawWireCube(new Vector3(center.x, 0, center.y), new Vector3(boundingBoxMaze.size.x, 1, boundingBoxMaze.size.y));

    //    Gizmos.color = Color.red;
    //    Gizmos.DrawWireSphere(new Vector3(center.x, 0, center.y), 1);

    //    Gizmos.DrawCube(new Vector3(startRoom.bounds.position.x, 0, startRoom.bounds.position.y), new Vector3(startRoom.bounds.size.x, 1, startRoom.bounds.size.y));
    //    Gizmos.DrawCube(new Vector3(endRoom.bounds.position.x, 0, endRoom.bounds.position.y), new Vector3(endRoom.bounds.size.x, 1, endRoom.bounds.size.y));
    //}
}
