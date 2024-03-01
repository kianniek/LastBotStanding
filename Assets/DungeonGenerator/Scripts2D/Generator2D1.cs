using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;

public class Generator2D1 : MonoBehaviour
{
    enum CellType
    {
        None,
        Room,
        Hallway
    }

    class Room
    {
        public RectInt bounds;
        public GameObject assignedRoomObj;
        public GameObject floor;
        public GameObject northWall;
        public GameObject southWall;
        public GameObject westWall;
        public GameObject eastWall;

        public Room(Vector2Int location, Vector2Int size)
        {
            bounds = new RectInt(location, size);
        }


        public static bool Intersect(Room a, Room b)
        {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x)
                || (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y));
        }
    }

    [SerializeField] GameObject spawnPoint;
    [SerializeField] Vector2Int size;
    [SerializeField] int roomCount;
    [SerializeField][Min(0)] Vector2Int roomMaxSize;
    [SerializeField] Vector2Int roomMinSize;
    [SerializeField] GameObject cubePrefab;
    [SerializeField] Material redMaterial;
    [SerializeField] Material blueMaterial;
    [SerializeField] Material startRoomMaterial;
    [SerializeField] Material endRoomMaterial;
    [SerializeField] string[] tags = { "Room", "Hallway" };

    Random random;
    Grid2D<CellType> grid;
    List<Room> rooms;
    Room startRoom;
    Room endRoom;
    Delaunay2D delaunay;
    HashSet<Prim.Edge> selectedEdges;

    private GameObject dungeonHolder;
    private GameObject roomParent;
    private GameObject hallwayParent;

    void Start()
    {
        dungeonHolder = new GameObject("Dungeon Holder");
        roomParent = new GameObject("Rooms");
        hallwayParent = new GameObject("Hallways");

        roomParent.transform.SetParent(dungeonHolder.transform);
        hallwayParent.transform.SetParent(dungeonHolder.transform);
        Generate();
    }

    void Generate()
    {
        random = new Random(0);
        grid = new Grid2D<CellType>(size, Vector2Int.zero);
        rooms = new List<Room>();

        PlaceRooms();
        Triangulate();
        CreateHallways();
        PathfindHallways();
        CreateStartAndEnd();
        //AdjustRoomWalls();
    }

    void PlaceRooms()
    {
        for (int i = 0; i < roomCount; i++)
        {
            Vector2Int location = new Vector2Int(random.Next(0, size.x), random.Next(0, size.y));
            Vector2Int roomSize = new Vector2Int(random.Next(roomMinSize.x, roomMaxSize.x + 1), random.Next(roomMinSize.y, roomMaxSize.y + 1));

            Room newRoom = new Room(location, roomSize);
            Room buffer = new Room(location + new Vector2Int(-1, -1), roomSize + new Vector2Int(2, 2));


            bool add = true;
            foreach (var room in rooms)
            {
                if (Room.Intersect(room, buffer))
                {
                    add = false;
                    break;
                }
            }

            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y)
            {
                add = false;
            }

            if (add)
            {
                rooms.Add(newRoom);
                PlaceRoom(newRoom.bounds.position, newRoom.bounds.size, rooms.Count - 1);
                foreach (var pos in newRoom.bounds.allPositionsWithin)
                {
                    grid[pos] = CellType.Room;
                }
            }
        }
    }

    void Triangulate()
    {
        List<Vertex> vertices = new List<Vertex>();
        foreach (var room in rooms)
        {
            vertices.Add(new Vertex<Room>((Vector2)room.bounds.position + ((Vector2)room.bounds.size) / 2, room));
        }
        delaunay = Delaunay2D.Triangulate(vertices);
    }

    void CreateHallways()
    {
        List<Prim.Edge> edges = new List<Prim.Edge>();
        foreach (var edge in delaunay.Edges)
        {
            edges.Add(new Prim.Edge(edge.U, edge.V));
        }

        List<Prim.Edge> mst = Prim.MinimumSpanningTree(edges, edges[0].U);
        selectedEdges = new HashSet<Prim.Edge>(mst);
        var remainingEdges = new HashSet<Prim.Edge>(edges);
        remainingEdges.ExceptWith(selectedEdges);

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
        int modelsInX = (int)size.x;
        int modelsInY = (int)size.y;

        for (int i = 0; i < modelsInX; i++)
        {
            for (int j = 0; j < modelsInY; j++)
            {
                Vector3 spawnPosition = new Vector3(location.x + i, 0, location.y + j);
                GameObject go = Instantiate(cubePrefab, spawnPosition, Quaternion.identity);

                MeshRenderer goMR = go.GetComponent<MeshRenderer>() != null ? go.GetComponent<MeshRenderer>() : go.GetComponentInChildren<MeshRenderer>();
                goMR.material = material;
                if (roomId > -1)
                {
                    rooms[roomId].assignedRoomObj = go;
                }
                go.tag = tag;

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
        RectInt boundingBoxMaze = GetBoundingBox(rooms);
        Vector2 center = new Vector2(boundingBoxMaze.center.x, boundingBoxMaze.center.y);

        startRoom = GetFarthestRoom(center, rooms);
        endRoom = GetFarthestRoom(startRoom.bounds.position, rooms);

        MeshRenderer goMR = startRoom.assignedRoomObj.GetComponent<MeshRenderer>() != null ? startRoom.assignedRoomObj.GetComponent<MeshRenderer>() : startRoom.assignedRoomObj.GetComponentInChildren<MeshRenderer>();
        goMR.material = startRoomMaterial;
        goMR = endRoom.assignedRoomObj.GetComponent<MeshRenderer>() != null ? endRoom.assignedRoomObj.GetComponent<MeshRenderer>() : endRoom.assignedRoomObj.GetComponentInChildren<MeshRenderer>();
        goMR.material = endRoomMaterial;

        MoveMazeToSpawnPoint(startRoom);
    }

    void MoveMazeToSpawnPoint(Room startRoom)
    {
        if (!spawnPoint)
        {
            dungeonHolder.transform.position -= startRoom.assignedRoomObj.transform.position + (startRoom.assignedRoomObj.transform.localScale / 2);
        }
        else
        {
            dungeonHolder.transform.position -= startRoom.assignedRoomObj.transform.position + (startRoom.assignedRoomObj.transform.localScale / 2) - spawnPoint.transform.position;
        }
    }

    private static RectInt GetBoundingBox(List<Room> rooms)
    {
        if (rooms.Count == 0)
        {
            Debug.Log("List of RectInts is empty");
            return new RectInt();
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (var rect in rooms)
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
        float maxDistance = float.MinValue;
        Room farthestRoom = null;

        foreach (Room room in rooms)
        {
            float currentDistance = Vector2.Distance(referencePoint, room.bounds.center);
            if (currentDistance > maxDistance)
            {
                maxDistance = currentDistance;
                farthestRoom = room;
            }
        }

        return farthestRoom;
    }

    void AdjustRoomWalls()
    {
        foreach (var room in rooms)
        {
            GameObject roomPrefab = room.assignedRoomObj;
            if (roomPrefab != null)
            {
                room.floor = roomPrefab.transform.Find("Floor").gameObject;
                room.northWall = roomPrefab.transform.Find("North").gameObject;
                room.southWall = roomPrefab.transform.Find("South").gameObject;
                room.westWall = roomPrefab.transform.Find("West").gameObject;
                room.eastWall = roomPrefab.transform.Find("East").gameObject;
            }
            Vector2Int center = new Vector2Int(Mathf.RoundToInt(room.bounds.center.x), Mathf.RoundToInt(room.bounds.center.y));
            print(!IsNonEmptyCell(new Vector2Int(center.x, room.bounds.yMax + 1)));
            print(room.assignedRoomObj.transform.GetSiblingIndex());

            room.northWall.SetActive(!IsNonEmptyCell(new Vector2Int(center.x, room.bounds.yMax + 1)));
            room.southWall.SetActive(!IsNonEmptyCell(new Vector2Int(center.x, room.bounds.yMin - 1)));
            room.westWall.SetActive(!IsNonEmptyCell(new Vector2Int(room.bounds.xMin - 1, center.y)));
            room.eastWall.SetActive(!IsNonEmptyCell(new Vector2Int(room.bounds.xMax + 1, center.y)));
        }
    }

    bool IsNonEmptyCell(Vector2Int position)
    {
        if (position.x < 0 || position.x >= size.x || position.y < 0 || position.y >= size.y)
        {
            return false;
        }

        return grid[position] != CellType.Room && grid[position] != CellType.Hallway;
    }

    // Add any additional methods or logic here if necessary
}

