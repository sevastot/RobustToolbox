using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Robust.Shared.Graphics;
using Robust.Shared.Maths;

namespace Robust.Client.Graphics;

/// <summary>
/// Публичный API экспериментального 3D-прохода. Content заполняет сцену (треугольники + камера),
/// а Clyde рисует её вместо обычного 2D-мира, когда <see cref="Enabled"/> = true.
/// Система координат: X — вправо, Y — вверх, Z — «на зрителя». Мировая 2D-точка (x, y) = 3D (x, h, -y),
/// поэтому yaw = 0 смотрит на «север» (мировой +Y).
/// </summary>
public static class Scene3D
{
    public static bool Enabled;

    /// <summary>
    /// Глаз, для которого включён 3D (обычно IEyeManager.CurrentEye). Остальные viewport'ы рисуются как обычно.
    /// </summary>
    public static IEye? TargetEye;

    public static Vector3 CameraPosition = new(0f, 1.6f, 0f);

    /// <summary>Поворот камеры вокруг вертикали, радианы. Положительный — вправо.</summary>
    public static float Yaw;

    /// <summary>Наклон камеры, радианы. Положительный — вверх.</summary>
    public static float Pitch;

    public static float FovDegrees = 75f;

    /// <summary>Цвет фона (в sRGB, как обычные цвета).</summary>
    public static Color SkyColor = new(0.05f, 0.06f, 0.09f, 1f);

    internal static readonly List<Vertex3D> Vertices = new();

    public static void Clear()
    {
        Vertices.Clear();
    }

    public static void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        var linear = Color.FromSrgb(color);
        Vertices.Add(new Vertex3D(a, linear));
        Vertices.Add(new Vertex3D(b, linear));
        Vertices.Add(new Vertex3D(c, linear));
    }

    public static void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        var linear = Color.FromSrgb(color);
        Vertices.Add(new Vertex3D(a, linear));
        Vertices.Add(new Vertex3D(b, linear));
        Vertices.Add(new Vertex3D(c, linear));
        Vertices.Add(new Vertex3D(a, linear));
        Vertices.Add(new Vertex3D(c, linear));
        Vertices.Add(new Vertex3D(d, linear));
    }

    /// <summary>Коробка без дна (дно всё равно не видно). Грани слегка затемнены для объёма.</summary>
    public static void AddBox(Vector3 min, Vector3 max, Color color)
    {
        float x0 = min.X, y0 = min.Y, z0 = min.Z;
        float x1 = max.X, y1 = max.Y, z1 = max.Z;

        // верх
        AddQuad(new(x0, y1, z0), new(x1, y1, z0), new(x1, y1, z1), new(x0, y1, z1), Shade(color, 1.0f));
        // +Z
        AddQuad(new(x0, y0, z1), new(x1, y0, z1), new(x1, y1, z1), new(x0, y1, z1), Shade(color, 0.8f));
        // -Z
        AddQuad(new(x1, y0, z0), new(x0, y0, z0), new(x0, y1, z0), new(x1, y1, z0), Shade(color, 0.6f));
        // +X
        AddQuad(new(x1, y0, z1), new(x1, y0, z0), new(x1, y1, z0), new(x1, y1, z1), Shade(color, 0.7f));
        // -X
        AddQuad(new(x0, y0, z0), new(x0, y0, z1), new(x0, y1, z1), new(x0, y1, z0), Shade(color, 0.9f));
    }

    private static Color Shade(Color c, float k)
    {
        return new Color(c.R * k, c.G * k, c.B * k, c.A);
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct Vertex3D
{
    public readonly Vector3 Position;

    // Цвет уже в линейном пространстве (как у Vertex2D).
    public readonly Color Color;

    public Vertex3D(Vector3 position, Color color)
    {
        Position = position;
        Color = color;
    }
}
