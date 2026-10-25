using UnityEngine;

public enum GlobalDeformationMode { Taper, Twist, Bend, FFD }
public enum DeformationAxis { X, Y, Z }

/// <summary>All mappings read the rest pose; switching modes never accumulates error.</summary>
public static class DeformationMath
{
    public static Vector3 ToAxis(Vector3 p, DeformationAxis axis)
    {
        if (axis == DeformationAxis.X) return new Vector3(p.z, p.x, p.y);
        if (axis == DeformationAxis.Z) return new Vector3(p.y, p.z, p.x);
        return p;
    }

    public static Vector3 FromAxis(Vector3 p, DeformationAxis axis)
    {
        if (axis == DeformationAxis.X) return new Vector3(p.y, p.z, p.x);
        if (axis == DeformationAxis.Z) return new Vector3(p.z, p.x, p.y);
        return p;
    }

    public static Vector3 Evaluate(Vector3 rest, Bounds bounds, GlobalDeformationMode mode,
        DeformationAxis axis, float amount)
    {
        if (Mathf.Abs(amount) < 0.00001f || mode == GlobalDeformationMode.FFD) return rest;
        Vector3 p = ToAxis(rest - bounds.center, axis);
        Vector3 size = ToAxis(bounds.size, axis);
        float height = Mathf.Max(size.y, 0.00001f);
        float bottom = -height * 0.5f;
        float t = Mathf.Clamp01((p.y - bottom) / height);
        if (mode == GlobalDeformationMode.Taper)
        {
            float scale = 1f + amount * t;
            p.x *= scale;
            p.z *= scale;
        }
        else if (mode == GlobalDeformationMode.Twist)
        {
            float angle = amount * Mathf.Deg2Rad * t;
            float x = p.x * Mathf.Cos(angle) - p.z * Mathf.Sin(angle);
            p.z = p.x * Mathf.Sin(angle) + p.z * Mathf.Cos(angle);
            p.x = x;
        }
        else if (mode == GlobalDeformationMode.Bend)
        {
            // Circular bend, anchored at the lower bounding-box plane.
            float angle = amount * Mathf.Deg2Rad;
            float radius = height / angle;
            float theta = angle * t;
            float radial = radius - p.x;
            // Stable 1-cos(theta) form for very small angles.
            float halfSin = Mathf.Sin(theta * 0.5f);
            p.x = radius * 2f * halfSin * halfSin + p.x * Mathf.Cos(theta);
            p.y = bottom + radial * Mathf.Sin(theta);
        }
        return FromAxis(p, axis) + bounds.center;
    }
}
