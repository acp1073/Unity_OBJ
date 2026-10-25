using System;
using UnityEngine;

/// <summary>Tensor-product Bernstein FFD of degree n-1 in each dimension.</summary>
public sealed class FfdLattice
{
    public int Resolution { get; private set; }
    public Bounds Bounds { get; private set; }
    public Vector3[] RestPoints { get; private set; }
    public Vector3[] Points { get; private set; }
    private readonly float[] basis;

    public FfdLattice(Vector3[] vertices, Bounds bounds, int resolution)
    {
        Resolution = Mathf.Clamp(resolution, 2, 6);
        // Flat meshes still need a nonzero volume for the parameter coordinates.
        float epsilon = Mathf.Max(bounds.size.magnitude * 0.001f, 0.0001f);
        Vector3 size = bounds.size;
        bounds.size = new Vector3(Mathf.Max(size.x, epsilon), Mathf.Max(size.y, epsilon), Mathf.Max(size.z, epsilon));
        Bounds = bounds;
        int n = Resolution;
        RestPoints = new Vector3[n * n * n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                for (int k = 0; k < n; k++)
                    RestPoints[Index(i, j, k)] = bounds.min + Vector3.Scale(bounds.size,
                        new Vector3(i, j, k) / (n - 1f));
        Points = (Vector3[])RestPoints.Clone();
        basis = new float[vertices.Length * n * 3];
        for (int v = 0; v < vertices.Length; v++)
        {
            Vector3 p = vertices[v] - bounds.min;
            p = new Vector3(p.x / bounds.size.x, p.y / bounds.size.y, p.z / bounds.size.z);
            for (int i = 0; i < n; i++)
            {
                basis[(v * 3) * n + i] = Bernstein(n - 1, i, Mathf.Clamp01(p.x));
                basis[(v * 3 + 1) * n + i] = Bernstein(n - 1, i, Mathf.Clamp01(p.y));
                basis[(v * 3 + 2) * n + i] = Bernstein(n - 1, i, Mathf.Clamp01(p.z));
            }
        }
    }

    public int Index(int i, int j, int k) { return (i * Resolution + j) * Resolution + k; }

    public void Reset() { Array.Copy(RestPoints, Points, Points.Length); }

    public void Evaluate(Vector3[] destination)
    {
        int n = Resolution;
        if (destination.Length * n * 3 != basis.Length) throw new ArgumentException("Vertex count changed.");
        for (int v = 0; v < destination.Length; v++)
        {
            Vector3 p = Vector3.zero;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float xy = basis[v * 3 * n + i] * basis[(v * 3 + 1) * n + j];
                    for (int k = 0; k < n; k++)
                        p += Points[Index(i, j, k)] * (xy * basis[(v * 3 + 2) * n + k]);
                }
            destination[v] = p;
        }
    }

    private static float Bernstein(int degree, int i, float t)
    {
        float coefficient = 1f;
        for (int j = 1; j <= i; j++) coefficient *= (degree - j + 1f) / j;
        return coefficient * Mathf.Pow(t, i) * Mathf.Pow(1f - t, degree - i);
    }
}
