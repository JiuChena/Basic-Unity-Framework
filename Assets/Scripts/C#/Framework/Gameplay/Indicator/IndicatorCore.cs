using System.Collections.Generic;
using UnityEngine;

internal abstract class IndicatorCore
{
    private static readonly IndicatorCore SectorCore = new SectorIndicatorCore();
    private static readonly IndicatorCore DirectionalityCore = new DirectionalityIndicatorCore();
    private static readonly IndicatorCore ThrowableCore = new ThrowableIndicatorCore();

    public abstract bool UsesSecondaryMesh { get; }

    public void Excute(IndicatorModule module, IndicatorMeshBuffer buffer, Mesh primaryMesh, Mesh secondaryMesh,
        Mesh primaryEdgeMesh, Mesh secondaryEdgeMesh)
    {
        Execute(module, buffer, primaryMesh, secondaryMesh, primaryEdgeMesh, secondaryEdgeMesh);
    }

    public abstract void Execute(IndicatorModule module, IndicatorMeshBuffer buffer, Mesh primaryMesh, Mesh secondaryMesh,
        Mesh primaryEdgeMesh, Mesh secondaryEdgeMesh);

    public static IndicatorCore Resolve(IndicatorType type)
    {
        return type switch
        {
            IndicatorType.Directionality => DirectionalityCore,
            IndicatorType.Throwable => ThrowableCore,
            _ => SectorCore,
        };
    }

    protected static void ClearMesh(Mesh mesh)
    {
        if (mesh != null)
            mesh.Clear(false);
    }

    protected static void BuildFilledSector(IndicatorMeshBuffer buffer, float radius, float angle, int segments,
        Vector3 forward, float y, Color centerColor, Color outerColor)
    {
        if (radius <= 0f)
        {
            buffer.Begin(0, 0);
            return;
        }

        float clampedAngle = Mathf.Clamp(angle, 1f, 360f);
        if (clampedAngle >= 359.9f)
        {
            BuildFilledCircle(buffer, Vector3.zero, radius, segments, y, centerColor, outerColor);
            return;
        }

        int resolvedSegments = Mathf.Max(1, segments);
        buffer.Begin(resolvedSegments + 2, resolvedSegments * 3);
        int centerIndex = buffer.AddVertex(new Vector3(0f, y, 0f), centerColor);
        Vector3 planarForward = ResolvePlanarDirection(forward);
        float halfAngle = clampedAngle * 0.5f;
        for (int i = 0; i <= resolvedSegments; i++)
        {
            float t = resolvedSegments == 0 ? 0f : i / (float)resolvedSegments;
            float currentAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector3 direction = Quaternion.AngleAxis(currentAngle, Vector3.up) * planarForward;
            buffer.AddVertex(direction * radius + Vector3.up * y, outerColor);
        }

        for (int i = 1; i <= resolvedSegments; i++)
            buffer.AddTriangle(centerIndex, centerIndex + i, centerIndex + i + 1);
    }

    protected static void BuildFilledCircle(IndicatorMeshBuffer buffer, Vector3 center, float radius, int segments,
        float y, Color centerColor, Color outerColor)
    {
        if (radius <= 0f)
        {
            buffer.Begin(0, 0);
            return;
        }

        int resolvedSegments = Mathf.Max(3, segments);
        buffer.Begin(resolvedSegments + 1, resolvedSegments * 3);
        Vector3 resolvedCenter = new Vector3(center.x, y, center.z);
        int centerIndex = buffer.AddVertex(resolvedCenter, centerColor);
        for (int i = 0; i < resolvedSegments; i++)
        {
            float angle = i / (float)resolvedSegments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            buffer.AddVertex(resolvedCenter + new Vector3(x, 0f, z), outerColor);
        }

        for (int i = 0; i < resolvedSegments; i++)
        {
            int current = centerIndex + 1 + i;
            int next = centerIndex + 1 + ((i + 1) % resolvedSegments);
            buffer.AddTriangle(centerIndex, current, next);
        }
    }

    protected static void BuildDirectionRectangle(IndicatorMeshBuffer buffer, float length, float width, Vector3 forward,
        float y, Color backColor, Color frontColor)
    {
        if (length <= 0f || width <= 0f)
        {
            buffer.Begin(0, 0);
            return;
        }

        buffer.Begin(4, 6);
        BuildRectangleCorners(length, width, forward, y, out Vector3 backLeft, out Vector3 frontLeft,
            out Vector3 frontRight, out Vector3 backRight);

        int backLeftIndex = buffer.AddVertex(backLeft, backColor);
        int frontLeftIndex = buffer.AddVertex(frontLeft, frontColor);
        int frontRightIndex = buffer.AddVertex(frontRight, frontColor);
        int backRightIndex = buffer.AddVertex(backRight, backColor);

        buffer.AddTriangle(backLeftIndex, frontLeftIndex, frontRightIndex);
        buffer.AddTriangle(backLeftIndex, frontRightIndex, backRightIndex);
    }

    protected static void BuildCircleEdge(IndicatorMeshBuffer buffer, Vector3 center, float radius, float thickness,
        int segments, float y, Color color)
    {
        if (radius <= 0f || thickness <= 0f)
        {
            buffer.Begin(0, 0);
            return;
        }

        int resolvedSegments = Mathf.Max(3, segments);
        float innerRadius = Mathf.Max(0f, radius - thickness);
        buffer.Begin((resolvedSegments + 1) * 2, resolvedSegments * 6);
        Vector3 resolvedCenter = new Vector3(center.x, y, center.z);
        for (int i = 0; i <= resolvedSegments; i++)
        {
            float angle = i / (float)resolvedSegments * Mathf.PI * 2f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            buffer.AddVertex(resolvedCenter + direction * radius, color);
            buffer.AddVertex(resolvedCenter + direction * innerRadius, color);
        }

        for (int i = 0; i < resolvedSegments; i++)
        {
            int startIndex = i * 2;
            int nextIndex = startIndex + 2;
            buffer.AddTriangle(startIndex, startIndex + 1, nextIndex + 1);
            buffer.AddTriangle(startIndex, nextIndex + 1, nextIndex);
        }
    }

    protected static void BuildSectorEdge(IndicatorMeshBuffer buffer, float radius, float angle, float thickness,
        int segments, Vector3 forward, float y, Color color)
    {
        if (radius <= 0f || thickness <= 0f)
        {
            buffer.Begin(0, 0);
            return;
        }

        float clampedAngle = Mathf.Clamp(angle, 1f, 360f);
        if (clampedAngle >= 359.9f)
        {
            BuildCircleEdge(buffer, Vector3.zero, radius, thickness, segments, y, color);
            return;
        }

        int resolvedSegments = Mathf.Max(1, segments);
        float innerRadius = Mathf.Max(0f, radius - thickness);
        buffer.Begin((resolvedSegments + 1) * 2 + 8, resolvedSegments * 6 + 12);

        Vector3 planarForward = ResolvePlanarDirection(forward);
        float halfAngle = clampedAngle * 0.5f;
        for (int i = 0; i <= resolvedSegments; i++)
        {
            float t = resolvedSegments == 0 ? 0f : i / (float)resolvedSegments;
            float currentAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector3 direction = Quaternion.AngleAxis(currentAngle, Vector3.up) * planarForward;
            buffer.AddVertex(direction * radius + Vector3.up * y, color);
            buffer.AddVertex(direction * innerRadius + Vector3.up * y, color);
        }

        for (int i = 0; i < resolvedSegments; i++)
        {
            int startIndex = i * 2;
            int nextIndex = startIndex + 2;
            buffer.AddTriangle(startIndex, startIndex + 1, nextIndex + 1);
            buffer.AddTriangle(startIndex, nextIndex + 1, nextIndex);
        }

        Vector3 startDirection = Quaternion.AngleAxis(-halfAngle, Vector3.up) * planarForward;
        Vector3 endDirection = Quaternion.AngleAxis(halfAngle, Vector3.up) * planarForward;
        BuildRayEdgeStrip(buffer, Vector3.zero, startDirection, radius, Vector3.Cross(startDirection, Vector3.up),
            thickness, y, color);
        BuildRayEdgeStrip(buffer, Vector3.zero, endDirection, radius, Vector3.Cross(Vector3.up, endDirection),
            thickness, y, color);
    }

    protected static void BuildDirectionRectangleEdge(IndicatorMeshBuffer buffer, float length, float width,
        float thickness, Vector3 forward, float y, Color color)
    {
        if (length <= 0f || width <= 0f || thickness <= 0f)
        {
            buffer.Begin(0, 0);
            return;
        }

        float maxInset = Mathf.Min(length * 0.5f, width * 0.5f);
        float inset = Mathf.Min(thickness, maxInset);
        if (inset <= 0.001f || length <= inset * 2f || width <= inset * 2f)
        {
            BuildDirectionRectangle(buffer, length, width, forward, y, color, color);
            return;
        }

        buffer.Begin(16, 24);
        BuildRectangleCorners(length, width, forward, y, out Vector3 outerBackLeft, out Vector3 outerFrontLeft,
            out Vector3 outerFrontRight, out Vector3 outerBackRight);

        Vector3 planarForward = ResolvePlanarDirection(forward);
        Vector3 side = new Vector3(planarForward.z, 0f, -planarForward.x);
        float innerHalfWidth = Mathf.Max(0f, width * 0.5f - inset);
        float innerFrontDistance = Mathf.Max(inset, length - inset);
        Vector3 heightOffset = Vector3.up * y;
        Vector3 innerBackLeft = planarForward * inset + side * innerHalfWidth + heightOffset;
        Vector3 innerFrontLeft = planarForward * innerFrontDistance + side * innerHalfWidth + heightOffset;
        Vector3 innerFrontRight = planarForward * innerFrontDistance - side * innerHalfWidth + heightOffset;
        Vector3 innerBackRight = planarForward * inset - side * innerHalfWidth + heightOffset;

        BuildQuad(buffer, outerBackLeft, innerBackLeft, innerFrontLeft, outerFrontLeft, color);
        BuildQuad(buffer, outerFrontLeft, innerFrontLeft, innerFrontRight, outerFrontRight, color);
        BuildQuad(buffer, outerFrontRight, innerFrontRight, innerBackRight, outerBackRight, color);
        BuildQuad(buffer, outerBackRight, innerBackRight, innerBackLeft, outerBackLeft, color);
    }

    protected static void BuildQuad(IndicatorMeshBuffer buffer, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        int startIndex = buffer.Count;
        buffer.AddVertex(a, color);
        buffer.AddVertex(b, color);
        buffer.AddVertex(c, color);
        buffer.AddVertex(d, color);
        buffer.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        buffer.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }

    private static void BuildRayEdgeStrip(IndicatorMeshBuffer buffer, Vector3 origin, Vector3 direction,
        float length, Vector3 inwardDirection, float thickness, float y, Color color)
    {
        Vector3 planarDirection = ResolvePlanarDirection(direction);
        Vector3 inward = ResolvePlanarDirection(inwardDirection);
        if (planarDirection.sqrMagnitude <= 0.0001f || inward.sqrMagnitude <= 0.0001f)
            return;

        Vector3 heightOffset = Vector3.up * y;
        Vector3 outerStart = origin + heightOffset;
        Vector3 outerEnd = origin + planarDirection * length + heightOffset;
        Vector3 innerStart = outerStart + inward * thickness;
        Vector3 innerEnd = outerEnd + inward * thickness;
        BuildQuad(buffer, outerStart, innerStart, innerEnd, outerEnd, color);
    }

    private static void BuildRectangleCorners(float length, float width, Vector3 forward, float y,
        out Vector3 backLeft, out Vector3 frontLeft, out Vector3 frontRight, out Vector3 backRight)
    {
        Vector3 planarForward = ResolvePlanarDirection(forward);
        Vector3 side = new Vector3(planarForward.z, 0f, -planarForward.x);
        float halfWidth = width * 0.5f;
        Vector3 heightOffset = Vector3.up * y;
        backLeft = side * halfWidth + heightOffset;
        frontLeft = planarForward * length + side * halfWidth + heightOffset;
        frontRight = planarForward * length - side * halfWidth + heightOffset;
        backRight = -side * halfWidth + heightOffset;
    }

    private static Vector3 ResolvePlanarDirection(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            return Vector3.forward;

        return forward.normalized;
    }
}

internal sealed class SectorIndicatorCore : IndicatorCore
{
    public override bool UsesSecondaryMesh => false;

    public override void Execute(IndicatorModule module, IndicatorMeshBuffer buffer, Mesh primaryMesh, Mesh secondaryMesh,
        Mesh primaryEdgeMesh, Mesh secondaryEdgeMesh)
    {
        ClearMesh(secondaryMesh);
        ClearMesh(secondaryEdgeMesh);
        BuildFilledSector(buffer, module.SectorRadius, module.SectorAngle, module.ArcSegments,
            module.ResolveLocalAimDirection(), module.SurfaceOffset, module.PrimaryFillCenterColor,
            module.PrimaryFillOuterColor);
        buffer.Apply(primaryMesh);
        BuildSectorEdge(buffer, module.SectorRadius, module.SectorAngle, module.PrimaryEdgeWidth, module.ArcSegments,
            module.ResolveLocalAimDirection(), module.SurfaceOffset, module.PrimaryEdgeColor);
        buffer.Apply(primaryEdgeMesh);
    }
}

internal sealed class DirectionalityIndicatorCore : IndicatorCore
{
    public override bool UsesSecondaryMesh => false;

    public override void Execute(IndicatorModule module, IndicatorMeshBuffer buffer, Mesh primaryMesh, Mesh secondaryMesh,
        Mesh primaryEdgeMesh, Mesh secondaryEdgeMesh)
    {
        ClearMesh(secondaryMesh);
        ClearMesh(secondaryEdgeMesh);
        BuildDirectionRectangle(buffer, module.DirectionLength, module.DirectionWidth, module.ResolveLocalAimDirection(),
            module.SurfaceOffset, module.PrimaryFillCenterColor, module.PrimaryFillOuterColor);
        buffer.Apply(primaryMesh);
        BuildDirectionRectangleEdge(buffer, module.DirectionLength, module.DirectionWidth, module.PrimaryEdgeWidth,
            module.ResolveLocalAimDirection(), module.SurfaceOffset, module.PrimaryEdgeColor);
        buffer.Apply(primaryEdgeMesh);
    }
}

internal sealed class ThrowableIndicatorCore : IndicatorCore
{
    public override bool UsesSecondaryMesh => true;

    public override void Execute(IndicatorModule module, IndicatorMeshBuffer buffer, Mesh primaryMesh, Mesh secondaryMesh,
        Mesh primaryEdgeMesh, Mesh secondaryEdgeMesh)
    {
        Vector3 primaryCenter = module.ResolveThrowableLocalCenter();
        BuildFilledCircle(buffer, primaryCenter, module.ThrowableAreaRadius, module.ArcSegments, module.SurfaceOffset,
            module.PrimaryFillCenterColor, module.PrimaryFillOuterColor);
        buffer.Apply(primaryMesh);
        BuildCircleEdge(buffer, primaryCenter, module.ThrowableAreaRadius, module.PrimaryEdgeWidth, module.ArcSegments,
            module.SurfaceOffset, module.PrimaryEdgeColor);
        buffer.Apply(primaryEdgeMesh);

        if (secondaryMesh == null)
            return;

        BuildFilledCircle(buffer, Vector3.zero, module.ThrowableMaxDistance, module.ArcSegments,
            module.SurfaceOffset + module.SecondarySurfaceOffset, module.SecondaryFillCenterColor,
            module.SecondaryFillOuterColor);
        buffer.Apply(secondaryMesh);
        BuildCircleEdge(buffer, Vector3.zero, module.ThrowableMaxDistance, module.SecondaryEdgeWidth,
            module.ArcSegments, module.SurfaceOffset + module.SecondarySurfaceOffset, module.SecondaryEdgeColor);
        buffer.Apply(secondaryEdgeMesh);
    }
}

internal sealed class IndicatorMeshBuffer
{
    private readonly List<Vector3> _vertices = new List<Vector3>(128);
    private readonly List<int> _triangles = new List<int>(256);
    private readonly List<Vector3> _normals = new List<Vector3>(128);
    private readonly List<Vector2> _uvs = new List<Vector2>(128);
    private readonly List<Color> _colors = new List<Color>(128);

    public int Count => _vertices.Count;

    public void Begin(int vertexCapacity, int triangleIndexCapacity)
    {
        _vertices.Clear();
        _triangles.Clear();
        _normals.Clear();
        _uvs.Clear();
        _colors.Clear();
        if (_vertices.Capacity < vertexCapacity)
            _vertices.Capacity = vertexCapacity;
        if (_triangles.Capacity < triangleIndexCapacity)
            _triangles.Capacity = triangleIndexCapacity;
        if (_normals.Capacity < vertexCapacity)
            _normals.Capacity = vertexCapacity;
        if (_uvs.Capacity < vertexCapacity)
            _uvs.Capacity = vertexCapacity;
        if (_colors.Capacity < vertexCapacity)
            _colors.Capacity = vertexCapacity;
    }

    public int AddVertex(Vector3 vertex, Color color)
    {
        int index = _vertices.Count;
        _vertices.Add(vertex);
        _normals.Add(Vector3.up);
        _uvs.Add(new Vector2(vertex.x, vertex.z));
        _colors.Add(color);
        return index;
    }

    public void AddTriangle(int a, int b, int c)
    {
        _triangles.Add(a);
        _triangles.Add(b);
        _triangles.Add(c);
    }

    public void Apply(Mesh mesh)
    {
        if (mesh == null)
            return;

        mesh.Clear(false);
        if (_vertices.Count == 0)
            return;

        mesh.SetVertices(_vertices);
        mesh.SetNormals(_normals);
        mesh.SetUVs(0, _uvs);
        mesh.SetColors(_colors);
        mesh.SetTriangles(_triangles, 0, true);
        mesh.RecalculateBounds();
    }
}
