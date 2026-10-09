using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

public class Vec3
{
    public double X, Y, Z;
    public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }
}

public class Prism
{
    public int[] V = new int[6];
    public Prism(int[] v) { Array.Copy(v, V, 6); }
}

public static class MeshLoader
{
    public static List<Vec3> LoadNodes(string path)
    {
        var list = new List<Vec3>();
        var lines = File.ReadAllLines(path);
        int n = int.Parse(lines[0]);
        for (int i = 1; i <= n; i++)
        {
            var p = lines[i].Split(new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries);
            list.Add(new Vec3(
                double.Parse(p[1], CultureInfo.InvariantCulture),
                double.Parse(p[2], CultureInfo.InvariantCulture),
                double.Parse(p[3], CultureInfo.InvariantCulture)));
        }
        return list;
    }

    public static List<Prism> LoadPrisms(string path)
    {
        var list = new List<Prism>();
        var lines = File.ReadAllLines(path);
        int n = int.Parse(lines[0]);
        for (int i = 1; i <= n; i++)
        {
            var p = lines[i].Split(new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries);
            int[] v = new int[6];
            for (int j = 0; j < 6; j++) v[j] = int.Parse(p[j]);
            list.Add(new Prism(v));
        }
        return list;
    }
}