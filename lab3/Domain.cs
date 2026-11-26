using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Lab2
{
    public class Subdomain
    {
        public int Material;
        public int Nxb, Nxe, Nyb, Nye, Nzb, Nze;
    }

    public class DomainDescription
    {
        public int Kx, Ky, Kz;
        public double[,] X, Y;
        public double[] Z;
        public List<Subdomain> Subdomains = new List<Subdomain>();
    }

    public static class DomainFileReader
    {
        public static DomainDescription Read(string path)
        {
            var lines = File.ReadAllLines(path);
            int p = 0;
            var d = new DomainDescription();

            var head = Split(lines[p++]);
            d.Kx = int.Parse(head[0], CultureInfo.InvariantCulture);
            d.Ky = int.Parse(head[1], CultureInfo.InvariantCulture);

            d.X = new double[d.Ky, d.Kx];
            d.Y = new double[d.Ky, d.Kx];
            for (int j = 0; j < d.Ky; j++)
            {
                var t = Split(lines[p++]);
                for (int i = 0; i < d.Kx; i++)
                {
                    d.X[j, i] = double.Parse(t[2 * i], CultureInfo.InvariantCulture);
                    d.Y[j, i] = double.Parse(t[2 * i + 1], CultureInfo.InvariantCulture);
                }
            }

            d.Kz = int.Parse(lines[p++].Trim(), CultureInfo.InvariantCulture);
            var zt = Split(lines[p++]);
            d.Z = new double[d.Kz];
            for (int k = 0; k < d.Kz; k++)
                d.Z[k] = double.Parse(zt[k], CultureInfo.InvariantCulture);

            int No = int.Parse(lines[p++].Trim(), CultureInfo.InvariantCulture);
            for (int i = 0; i < No; i++)
            {
                var t = Split(lines[p++]);
                d.Subdomains.Add(new Subdomain
                {
                    Material = int.Parse(t[0], CultureInfo.InvariantCulture),
                    Nxb = int.Parse(t[1], CultureInfo.InvariantCulture),
                    Nxe = int.Parse(t[2], CultureInfo.InvariantCulture),
                    Nyb = int.Parse(t[3], CultureInfo.InvariantCulture),
                    Nye = int.Parse(t[4], CultureInfo.InvariantCulture),
                    Nzb = int.Parse(t[5], CultureInfo.InvariantCulture),
                    Nze = int.Parse(t[6], CultureInfo.InvariantCulture)
                });
            }
            return d;
        }

        private static string[] Split(string s) =>
            s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    }
}