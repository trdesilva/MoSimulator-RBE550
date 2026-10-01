using System.Collections.Generic;
using UnityEngine;

public class GridPlanner : MonoBehaviour
{
    [SerializeField] private FieldMap2D fieldMap;

    [SerializeField] private float cellSize = 0.20f;
    [SerializeField] private float robotClearance = 0.48f;
    [SerializeField] private float dynamicRobotClearance = 0.90f;
    [SerializeField] private float maxSnapDistance = 1.0f;

    [SerializeField] private bool drawPath = true;

    private Node[,] grid;
    private int width;
    private int height;

    private float minX;
    private float maxX;
    private float minZ;
    private float maxZ;

    private List<Vector2> lastPath = new();

    public IReadOnlyList<Vector2> LastPath => lastPath;


    private class Node
    {
        public int x;
        public int y;

        public Vector2 position;
        public bool blocked;

        public float g = float.PositiveInfinity;
        public float h;

        public Node parent;
        public bool closed;

        public float F => g + h;

        public Node(
            int x,
            int y,
            Vector2 position,
            bool blocked)
        {
            this.x = x;
            this.y = y;
            this.position = position;
            this.blocked = blocked;
        }
    }


    private void Awake()
    {
        if (fieldMap == null)
            fieldMap = GetComponent<FieldMap2D>();
    }


    public List<Vector2> Plan(
        Vector2 start,
        Vector2 goal,
        IReadOnlyList<Vector2> dynamicObstacles = null)
    {
        lastPath.Clear();

        if (fieldMap == null)
        {
            Debug.LogError("GridPlanner needs a FieldMap2D.");
            return null;
        }

        BuildGrid(dynamicObstacles);

        Node startNode = FindNearestFreeNode(start);
        Node goalNode = FindNearestFreeNode(goal);

        if (startNode == null || goalNode == null)
        {
            Debug.LogWarning("Could not find valid start or goal.");
            return null;
        }

        List<Vector2> path = AStar(startNode, goalNode);

        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("No path found.");
            return null;
        }

        path[0] = start;

        if (!IsBlocked(goal, dynamicObstacles))
            path[path.Count - 1] = goal;

        lastPath = SmoothPath(
            path,
            dynamicObstacles
        );

        return new List<Vector2>(lastPath);
    }


    public bool IsNavigable(
        Vector2 position,
        IReadOnlyList<Vector2> dynamicObstacles = null)
    {
        return !IsBlocked(
            position,
            dynamicObstacles
        );
    }


    private void BuildGrid(
        IReadOnlyList<Vector2> dynamicObstacles)
    {
        FindFieldBounds();

        width =
            Mathf.CeilToInt(
                (maxX - minX) / cellSize
            ) + 1;

        height =
            Mathf.CeilToInt(
                (maxZ - minZ) / cellSize
            ) + 1;

        grid = new Node[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2 position = new(
                    minX + x * cellSize,
                    minZ + y * cellSize
                );

                grid[x, y] = new Node(
                    x,
                    y,
                    position,
                    IsBlocked(
                        position,
                        dynamicObstacles
                    )
                );
            }
        }
    }


    private void FindFieldBounds()
    {
        minX = float.PositiveInfinity;
        maxX = float.NegativeInfinity;
        minZ = float.PositiveInfinity;
        maxZ = float.NegativeInfinity;

        foreach (Vector2 point in fieldMap.fieldBoundary)
        {
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);

            minZ = Mathf.Min(minZ, point.y);
            maxZ = Mathf.Max(maxZ, point.y);
        }
    }


    private bool IsBlocked(
        Vector2 position,
        IReadOnlyList<Vector2> dynamicObstacles)
    {
        if (!fieldMap.IsInsideField(position))
            return true;

        if (
            DistanceToPolygon(
                position,
                fieldMap.fieldBoundary
            ) < robotClearance)
        {
            return true;
        }

        foreach (
            FieldMap2D.NavigationObstacle obstacle
            in fieldMap.navigationObstacles)
        {
            if (
                obstacle.vertices == null ||
                obstacle.vertices.Count < 3)
            {
                continue;
            }

            if (PointInPolygon(
                position,
                obstacle.vertices))
            {
                return true;
            }

            if (
                DistanceToPolygon(
                    position,
                    obstacle.vertices
                ) < robotClearance)
            {
                return true;
            }
        }

        if (dynamicObstacles != null)
        {
            foreach (Vector2 obstacle in dynamicObstacles)
            {
                if (
                    Vector2.Distance(
                        position,
                        obstacle
                    ) < dynamicRobotClearance)
                {
                    return true;
                }
            }
        }

        return false;
    }


    private List<Vector2> AStar(
        Node start,
        Node goal)
    {
        List<Node> open = new();

        start.g = 0;
        start.h = Heuristic(start, goal);

        open.Add(start);

        while (open.Count > 0)
        {
            Node current = GetLowestCost(open);

            open.Remove(current);

            if (current.closed)
                continue;

            current.closed = true;

            if (current == goal)
                return BuildPath(goal);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int nx = current.x + dx;
                    int ny = current.y + dy;

                    if (!Valid(nx, ny))
                        continue;

                    Node neighbor = grid[nx, ny];

                    if (
                        neighbor.blocked ||
                        neighbor.closed)
                    {
                        continue;
                    }

                    if (dx != 0 && dy != 0)
                    {
                        if (
                            grid[current.x + dx, current.y].blocked ||
                            grid[current.x, current.y + dy].blocked)
                        {
                            continue;
                        }
                    }

                    float moveCost =
                        (dx != 0 && dy != 0)
                        ? cellSize * 1.41421356f
                        : cellSize;

                    float newG =
                        current.g + moveCost;

                    if (newG >= neighbor.g)
                        continue;

                    neighbor.g = newG;
                    neighbor.h = Heuristic(
                        neighbor,
                        goal
                    );

                    neighbor.parent = current;

                    if (!open.Contains(neighbor))
                        open.Add(neighbor);
                }
            }
        }

        return null;
    }


    private Node GetLowestCost(List<Node> nodes)
    {
        Node best = nodes[0];

        for (int i = 1; i < nodes.Count; i++)
        {
            if (
                nodes[i].F < best.F ||
                (
                    Mathf.Approximately(
                        nodes[i].F,
                        best.F
                    )
                    &&
                    nodes[i].h < best.h
                ))
            {
                best = nodes[i];
            }
        }

        return best;
    }


    private float Heuristic(
        Node a,
        Node b)
    {
        return Vector2.Distance(
            a.position,
            b.position
        );
    }


    private List<Vector2> BuildPath(Node goal)
    {
        List<Vector2> path = new();

        Node current = goal;

        while (current != null)
        {
            path.Add(current.position);
            current = current.parent;
        }

        path.Reverse();

        return path;
    }


    private List<Vector2> SmoothPath(
        List<Vector2> path,
        IReadOnlyList<Vector2> dynamicObstacles)
    {
        if (path.Count <= 2)
            return path;

        List<Vector2> smooth = new();

        int current = 0;

        smooth.Add(path[0]);

        while (current < path.Count - 1)
        {
            int next = current + 1;

            for (
                int candidate = path.Count - 1;
                candidate > current;
                candidate--)
            {
                if (
                    HasClearPath(
                        path[current],
                        path[candidate],
                        dynamicObstacles))
                {
                    next = candidate;
                    break;
                }
            }

            smooth.Add(path[next]);
            current = next;
        }

        return smooth;
    }


    private bool HasClearPath(
        Vector2 start,
        Vector2 end,
        IReadOnlyList<Vector2> dynamicObstacles)
    {
        float distance =
            Vector2.Distance(start, end);

        int samples =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    distance /
                    (cellSize * 0.5f)
                )
            );

        for (int i = 0; i <= samples; i++)
        {
            Vector2 point =
                Vector2.Lerp(
                    start,
                    end,
                    (float)i / samples
                );

            if (
                IsBlocked(
                    point,
                    dynamicObstacles))
            {
                return false;
            }
        }

        return true;
    }


    private Node FindNearestFreeNode(
        Vector2 position)
    {
        Node best = null;

        float bestDistance =
            maxSnapDistance *
            maxSnapDistance;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Node node = grid[x, y];

                if (node.blocked)
                    continue;

                float distance =
                    (
                        node.position -
                        position
                    ).sqrMagnitude;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = node;
                }
            }
        }

        return best;
    }


    private bool Valid(int x, int y)
    {
        return
            x >= 0 &&
            y >= 0 &&
            x < width &&
            y < height;
    }


    private bool PointInPolygon(
        Vector2 point,
        IList<Vector2> polygon)
    {
        bool inside = false;

        for (
            int i = 0,
            j = polygon.Count - 1;
            i < polygon.Count;
            j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];

            bool intersects =
                ((a.y > point.y) !=
                 (b.y > point.y))
                &&
                (
                    point.x <
                    (b.x - a.x) *
                    (point.y - a.y) /
                    (b.y - a.y) +
                    a.x
                );

            if (intersects)
                inside = !inside;
        }

        return inside;
    }


    private float DistanceToPolygon(
        Vector2 point,
        IList<Vector2> polygon)
    {
        float closest =
            float.PositiveInfinity;

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];

            Vector2 b =
                polygon[
                    (i + 1) %
                    polygon.Count
                ];

            closest =
                Mathf.Min(
                    closest,
                    DistanceToSegment(
                        point,
                        a,
                        b
                    )
                );
        }

        return closest;
    }


    private float DistanceToSegment(
        Vector2 point,
        Vector2 a,
        Vector2 b)
    {
        Vector2 ab = b - a;

        if (ab.sqrMagnitude < 0.000001f)
            return Vector2.Distance(point, a);

        float t =
            Vector2.Dot(
                point - a,
                ab
            )
            /
            ab.sqrMagnitude;

        t = Mathf.Clamp01(t);

        return Vector2.Distance(
            point,
            a + ab * t
        );
    }


    [ContextMenu("Test Planner")]
    private void TestPlanner()
    {
        if (fieldMap == null)
            fieldMap = GetComponent<FieldMap2D>();

        Vector2 start = new Vector2(6.5f, 0.0f);
        Vector2 goal = new Vector2(2.2f, 0.0f);

        List<Vector2> path = Plan(start, goal);

        if (path == null)
        {
            Debug.LogError("GridPlanner test failed: no path found.");
            return;
        }

        Debug.Log($"GridPlanner found {path.Count} waypoints.");

        foreach (Vector2 point in path)
            Debug.Log($"Waypoint: {point}");
    }


    private void OnDrawGizmosSelected()
    {
        if (
            !drawPath ||
            lastPath == null ||
            lastPath.Count < 2)
        {
            return;
        }

        Gizmos.color = Color.green;

        for (int i = 0; i < lastPath.Count - 1; i++)
        {
            Vector3 a = new(
                lastPath[i].x,
                0.25f,
                lastPath[i].y
            );

            Vector3 b = new(
                lastPath[i + 1].x,
                0.25f,
                lastPath[i + 1].y
            );

            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(a, 0.06f);
        }
    }
}
