﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;

public class Generator3D : MonoBehaviour {
    // Define the different types of cells that can be present in the dungeon.
    enum CellType
    {
        None,
        Room,
        Hallway,
        Stairs
    }
    // The Room class defines the position and size of each room.
    class Room {
        public BoundsInt bounds;

        public Room(Vector3Int location, Vector3Int size) {
            bounds = new BoundsInt(location, size);
        }
        // Check if two rooms intersect with each other.
        public static bool Intersect(Room a, Room b) {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x)
                || (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y)
                || (a.bounds.position.z >= (b.bounds.position.z + b.bounds.size.z)) || ((a.bounds.position.z + a.bounds.size.z) <= b.bounds.position.z));
        }
    }
    // Serialized fields allow us to set these parameters in the Unity Editor.
    [SerializeField]
    int seed;
    [SerializeField]
    Vector3Int size;          // Overall size of the grid/dungeon.
    [SerializeField]
    int roomCount;            // Number of rooms to place.
    [SerializeField]
    Vector3Int roomMaxSize;   // Maximum size for each room.
    [SerializeField]
    GameObject cubePrefab;    // Prefab for visualization.
    [SerializeField]
    Material redMaterial;     // Material for rooms.
    [SerializeField]
    Material blueMaterial;    // Material for hallways.
    [SerializeField]
    Material greenMaterial;   // Material for stairs.
    [SerializeField]
    string[] tags = {"Room", "Hallway", "Stairs" };

    Random random;    // Random number generator.
    Grid3D<CellType> grid;  // The grid that holds the type of each cell.
    List<Room> rooms;      // List of all rooms that have been placed.
    Delaunay3D delaunay;   // Delaunay triangulation data.
    HashSet<Prim.Edge> selectedEdges;   // Edges selected from the triangulation.

    // Add these fields to store the empty GameObjects
    private GameObject roomParent;
    private GameObject hallwayParent;
    private GameObject stairsParent;

    void Start()
    {
        // Initialize the empty GameObjects
        roomParent = new GameObject("Rooms");
        hallwayParent = new GameObject("Hallways");
        stairsParent = new GameObject("Stairs");
        // Initialization
        random = new Random(seed);
        grid = new Grid3D<CellType>(size, Vector3Int.zero);
        rooms = new List<Room>();

        // Main generation steps
        PlaceRooms();       // Randomly place rooms.
        Triangulate();      // Connect rooms using Delaunay triangulation.
        CreateHallways();   // Create hallways based on the triangulated edges.
        PathfindHallways(); // Use pathfinding to connect rooms with hallways.
    }

    void PlaceRooms() {
        // Tries to place a number of rooms defined by 'roomCount'.
        for (int i = 0; i < roomCount; i++) {
            Vector3Int location = new Vector3Int(
                random.Next(0, size.x),
                random.Next(0, size.y),
                random.Next(0, size.z)
            );

            Vector3Int roomSize = new Vector3Int(
                random.Next(1, roomMaxSize.x + 1),
                random.Next(1, roomMaxSize.y + 1),
                random.Next(1, roomMaxSize.z + 1)
            );

            bool add = true;
            Room newRoom = new Room(location, roomSize);
            Room buffer = new Room(location + new Vector3Int(-1, 0, -1), roomSize + new Vector3Int(2, 0, 2));

            foreach (var room in rooms) {
                if (Room.Intersect(room, buffer)) {
                    add = false;
                    break;
                }
            }

            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
                || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y
                || newRoom.bounds.zMin < 0 || newRoom.bounds.zMax >= size.z) {
                add = false;
            }

            if (add) {
                rooms.Add(newRoom);
                PlaceRoom(newRoom.bounds.position, newRoom.bounds.size);

                foreach (var pos in newRoom.bounds.allPositionsWithin) {
                    grid[pos] = CellType.Room;
                }
            }
        }
    }

    void Triangulate() {
        // Uses Delaunay Triangulation to connect all rooms in a network.
        List<Vertex> vertices = new List<Vertex>();

        foreach (var room in rooms) {
            vertices.Add(new Vertex<Room>((Vector3)room.bounds.position + ((Vector3)room.bounds.size) / 2, room));
        }

        delaunay = Delaunay3D.Triangulate(vertices);
    }

    void CreateHallways() {
        // Creates hallways between rooms using Prim's algorithm on the edges 
        // from the Delaunay triangulation.
        List<Prim.Edge> edges = new List<Prim.Edge>();

        foreach (var edge in delaunay.Edges) {
            edges.Add(new Prim.Edge(edge.U, edge.V));
        }

        List<Prim.Edge> minimumSpanningTree = Prim.MinimumSpanningTree(edges, edges[0].U);

        selectedEdges = new HashSet<Prim.Edge>(minimumSpanningTree);
        var remainingEdges = new HashSet<Prim.Edge>(edges);
        remainingEdges.ExceptWith(selectedEdges);

        foreach (var edge in remainingEdges) {
            if (random.NextDouble() < 0.125) {
                selectedEdges.Add(edge);
            }
        }
    }

    void PathfindHallways() {
        // Use a 3D A* pathfinding algorithm to determine the exact path for 
        // the hallways between rooms, ensuring rooms are connected.
        DungeonPathfinder3D aStar = new DungeonPathfinder3D(size);

        foreach (var edge in selectedEdges) {
            var startRoom = (edge.U as Vertex<Room>).Item;
            var endRoom = (edge.V as Vertex<Room>).Item;

            var startPosf = startRoom.bounds.center;
            var endPosf = endRoom.bounds.center;
            var startPos = new Vector3Int((int)startPosf.x, (int)startPosf.y, (int)startPosf.z);
            var endPos = new Vector3Int((int)endPosf.x, (int)endPosf.y, (int)endPosf.z);

            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder3D.Node a, DungeonPathfinder3D.Node b) => {
                var pathCost = new DungeonPathfinder3D.PathCost();

                var delta = b.Position - a.Position;

                if (delta.y == 0) {
                    //flat hallway
                    pathCost.cost = Vector3Int.Distance(b.Position, endPos);    //heuristic

                    if (grid[b.Position] == CellType.Stairs) {
                        return pathCost;
                    } else if (grid[b.Position] == CellType.Room) {
                        pathCost.cost += 5;
                    } else if (grid[b.Position] == CellType.None) {
                        pathCost.cost += 1;
                    }

                    pathCost.traversable = true;
                } else {
                    //staircase
                    if ((grid[a.Position] != CellType.None && grid[a.Position] != CellType.Hallway)
                        || (grid[b.Position] != CellType.None && grid[b.Position] != CellType.Hallway)) return pathCost;

                    pathCost.cost = 100 + Vector3Int.Distance(b.Position, endPos);    //base cost + heuristic

                    int xDir = Mathf.Clamp(delta.x, -1, 1);
                    int zDir = Mathf.Clamp(delta.z, -1, 1);
                    Vector3Int verticalOffset = new Vector3Int(0, delta.y, 0);
                    Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);

                    if (!grid.InBounds(a.Position + verticalOffset)
                        || !grid.InBounds(a.Position + horizontalOffset)
                        || !grid.InBounds(a.Position + verticalOffset + horizontalOffset)) {
                        return pathCost;
                    }

                    if (grid[a.Position + horizontalOffset] != CellType.None
                        || grid[a.Position + horizontalOffset * 2] != CellType.None
                        || grid[a.Position + verticalOffset + horizontalOffset] != CellType.None
                        || grid[a.Position + verticalOffset + horizontalOffset * 2] != CellType.None) {
                        return pathCost;
                    }

                    pathCost.traversable = true;
                    pathCost.isStairs = true;
                }

                return pathCost;
            });

            if (path != null) {
                for (int i = 0; i < path.Count; i++) {
                    var current = path[i];

                    if (grid[current] == CellType.None) {
                        grid[current] = CellType.Hallway;
                    }

                    if (i > 0) {
                        var prev = path[i - 1];

                        var delta = current - prev;

                        if (delta.y != 0) {
                            int xDir = Mathf.Clamp(delta.x, -1, 1);
                            int zDir = Mathf.Clamp(delta.z, -1, 1);
                            Vector3Int verticalOffset = new Vector3Int(0, delta.y, 0);
                            Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);
                            
                            grid[prev + horizontalOffset] = CellType.Stairs;
                            grid[prev + horizontalOffset * 2] = CellType.Stairs;
                            grid[prev + verticalOffset + horizontalOffset] = CellType.Stairs;
                            grid[prev + verticalOffset + horizontalOffset * 2] = CellType.Stairs;

                            PlaceStairs(prev + horizontalOffset);
                            PlaceStairs(prev + horizontalOffset * 2);
                            PlaceStairs(prev + verticalOffset + horizontalOffset);
                            PlaceStairs(prev + verticalOffset + horizontalOffset * 2);
                        }

                        Debug.DrawLine(prev + new Vector3(0.5f, 0.5f, 0.5f), current + new Vector3(0.5f, 0.5f, 0.5f), Color.blue, 100, false);
                    }
                }

                foreach (var pos in path) {
                    if (grid[pos] == CellType.Hallway) {
                        PlaceHallway(pos);
                    }
                }
            }
        }
    }

    void PlaceCube(Vector3Int location, Vector3Int size, Material material, string tag = "Default") {
        // Instantiates a cube in the Unity scene for visualization.
        GameObject go = Instantiate(cubePrefab, location, Quaternion.identity);
        go.GetComponent<Transform>().localScale = size;
        go.GetComponent<MeshRenderer>().material = material;
        go.gameObject.tag = tag;

        // Set parent based on tag
        if (tag == tags[0]) // "Room"
        {
            go.transform.SetParent(roomParent.transform);
        }
        else if (tag == tags[1]) // "Hallway"
        {
            go.transform.SetParent(hallwayParent.transform);
        }
        else if (tag == tags[2]) // "Stairs"
        {
            go.transform.SetParent(stairsParent.transform);
        }
    }

    void PlaceRoom(Vector3Int location, Vector3Int size) {
        // Instantiates a room in the Unity scene.
        PlaceCube(location, size, redMaterial, tags[0]);
    }

    void PlaceHallway(Vector3Int location) {
        // Instantiates a hallway segment in the Unity scene.
        PlaceCube(location, new Vector3Int(1, 1, 1), blueMaterial, tags[1]);
    }

    void PlaceStairs(Vector3Int location) {
        // Instantiates stairs in the Unity scene.
        PlaceCube(location, new Vector3Int(1, 1, 1), greenMaterial, tags[2]);
    }
}
