using System;
using System.Collections.Generic;
using UnityEngine;

public enum ParametricCurveType
{
    Bezier,
    CatmullRom,
    UniformBSpline,
    Lagrange
}

/// <summary>
/// Pure curve mathematics. It does not depend on scene objects, so it can be
/// reused later for editing, measurement, exporting, or unit tests.
/// </summary>
public static class ParametricCurveMath
{
    public static Vector3 Evaluate(ParametricCurveType type, IReadOnlyList<Vector3> points, float t)
    {
        if (points == null || points.Count == 0)
        {
            return Vector3.zero;
        }

        if (points.Count == 1)
        {
            return points[0];
        }

        t = Mathf.Clamp01(t);
        switch (type)
        {
            case ParametricCurveType.CatmullRom:
                return EvaluateCatmullRom(points, t);
            case ParametricCurveType.UniformBSpline:
                return EvaluateUniformBSpline(points, t);
            case ParametricCurveType.Lagrange:
                return EvaluateLagrange(points, t);
            default:
                return EvaluateBezier(points, t);
        }
    }

    public static List<Vector3> Sample(ParametricCurveType type, IReadOnlyList<Vector3> points, int segments)
    {
        segments = Mathf.Max(2, segments);
        List<Vector3> result = new List<Vector3>(segments + 1);
        for (int i = 0; i <= segments; i++)
        {
            result.Add(Evaluate(type, points, i / (float)segments));
        }

        return result;
    }

    /// <summary>
    /// Approximate conversion between representations. Bezier uses four
    /// control points; the other representations use a resampled control net.
    /// The original curve is sampled first, so conversion is predictable even
    /// when the source type has a different number of control points.
    /// </summary>
    public static List<Vector3> Convert(
        ParametricCurveType sourceType,
        IReadOnlyList<Vector3> sourcePoints,
        ParametricCurveType targetType,
        int targetControlPointCount)
    {
        if (sourcePoints == null || sourcePoints.Count == 0)
        {
            return new List<Vector3>();
        }

        if (sourceType == targetType)
        {
            return Copy(sourcePoints);
        }

        List<Vector3> samples = Sample(sourceType, sourcePoints, 128);
        if (targetType == ParametricCurveType.Bezier)
        {
            return new List<Vector3>
            {
                samples[0],
                SampleAt(samples, 1f / 3f),
                SampleAt(samples, 2f / 3f),
                samples[samples.Count - 1]
            };
        }

        int count = Mathf.Max(4, targetControlPointCount);
        List<Vector3> converted = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            converted.Add(SampleAt(samples, i / (float)(count - 1)));
        }

        return converted;
    }

    private static Vector3 EvaluateBezier(IReadOnlyList<Vector3> points, float t)
    {
        Vector3[] work = new Vector3[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            work[i] = points[i];
        }

        for (int level = points.Count - 1; level > 0; level--)
        {
            for (int i = 0; i < level; i++)
            {
                work[i] = Vector3.LerpUnclamped(work[i], work[i + 1], t);
            }
        }

        return work[0];
    }

    private static Vector3 EvaluateCatmullRom(IReadOnlyList<Vector3> points, float t)
    {
        if (points.Count < 4)
        {
            return EvaluateBezier(points, t);
        }

        float scaled = t * (points.Count - 1);
        int segment = Mathf.Min(Mathf.FloorToInt(scaled), points.Count - 2);
        float localT = scaled - segment;
        Vector3 p0 = points[Mathf.Max(0, segment - 1)];
        Vector3 p1 = points[segment];
        Vector3 p2 = points[segment + 1];
        Vector3 p3 = points[Mathf.Min(points.Count - 1, segment + 2)];

        float t2 = localT * localT;
        float t3 = t2 * localT;
        return 0.5f * ((2f * p1)
                       + (-p0 + p2) * localT
                       + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                       + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static Vector3 EvaluateUniformBSpline(IReadOnlyList<Vector3> points, float t)
    {
        const int degree = 3;
        if (points.Count <= degree)
        {
            return EvaluateBezier(points, t);
        }

        int lastControlIndex = points.Count - 1;
        int knotCount = points.Count + degree + 1;
        float[] knots = new float[knotCount];
        for (int i = 0; i < knotCount; i++)
        {
            if (i <= degree)
            {
                knots[i] = 0f;
            }
            else if (i >= points.Count)
            {
                knots[i] = 1f;
            }
            else
            {
                knots[i] = (i - degree) / (float)(points.Count - degree);
            }
        }

        int span = lastControlIndex;
        if (t < 1f)
        {
            for (int i = degree; i < lastControlIndex + 1; i++)
            {
                if (t >= knots[i] && t < knots[i + 1])
                {
                    span = i;
                    break;
                }
            }
        }

        Vector3[] work = new Vector3[degree + 1];
        for (int j = 0; j <= degree; j++)
        {
            work[j] = points[span - degree + j];
        }

        for (int r = 1; r <= degree; r++)
        {
            for (int j = degree; j >= r; j--)
            {
                int knotIndex = span - degree + j;
                float denominator = knots[knotIndex + degree - r + 1] - knots[knotIndex];
                float alpha = denominator <= Mathf.Epsilon ? 0f : (t - knots[knotIndex]) / denominator;
                work[j] = Vector3.LerpUnclamped(work[j - 1], work[j], alpha);
            }
        }

        return work[degree];
    }

    private static Vector3 EvaluateLagrange(IReadOnlyList<Vector3> points, float t)
    {
        Vector3 result = Vector3.zero;
        int last = points.Count - 1;
        for (int i = 0; i <= last; i++)
        {
            float ti = i / (float)last;
            float basis = 1f;
            for (int j = 0; j <= last; j++)
            {
                if (i == j)
                {
                    continue;
                }

                float tj = j / (float)last;
                float denominator = ti - tj;
                if (Mathf.Abs(denominator) > Mathf.Epsilon)
                {
                    basis *= (t - tj) / denominator;
                }
            }

            result += points[i] * basis;
        }

        return result;
    }

    private static Vector3 SampleAt(IReadOnlyList<Vector3> samples, float t)
    {
        float scaled = Mathf.Clamp01(t) * (samples.Count - 1);
        int index = Mathf.Min(Mathf.FloorToInt(scaled), samples.Count - 2);
        float localT = scaled - index;
        return Vector3.LerpUnclamped(samples[index], samples[index + 1], localT);
    }

    private static List<Vector3> Copy(IReadOnlyList<Vector3> source)
    {
        List<Vector3> copy = new List<Vector3>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            copy.Add(source[i]);
        }

        return copy;
    }
}
