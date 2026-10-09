using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace Lab1
{
    public class Node
    {
        public double X, Y, Z;
        public Node(double x, double y, double z) { X = x; Y = y; Z = z; }
    }

    public class Hex
    {
        public int[] V = new int[8];
        public Hex(int[] v) { Array.Copy(v, V, 8); }
    }

    public class Tet
    {
        public int[] V = new int[4];
        public Tet(int a, int b, int c, int d)
        { V[0] = a; V[1] = b; V[2] = c; V[3] = d; }
    }

    public class BFace
    {
        public int A, B, C;
        public BFace(int a, int b, int c) { A = a; B = b; C = c; }
    }

    public class Subdomain
    {
        public int Material;
        public int Nxb, Nxe, Nyb, Nye, Nzb, Nze;
    }

    public class DomainDescription
    {
        public int Kx, Ky, Kz;
        public double[,] X;
        public double[,] Y;
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

    public class Tetra
    {
        public int A, B, C, D;
        public double Cx, Cy, Cz, R2;
        public bool Alive = true;

        public Tetra(int a, int b, int c, int d,
                     double cx, double cy, double cz, double r2)
        { A = a; B = b; C = c; D = d; Cx = cx; Cy = cy; Cz = cz; R2 = r2; }
    }

    
    public class MeshGenerator
    {
        public List<Node> Nodes = new List<Node>();
        public List<Tet> Tets = new List<Tet>();
        public List<BFace> BoundaryFaces = new List<BFace>();
        public List<int[]> TetEdges = new List<int[]>();

        private List<Hex> hexes = new List<Hex>();
        private Dictionary<string, int> nodeMap = new Dictionary<string, int>();

        public double h = 1.0;
        public DomainDescription Domain;

        public void Reset()
        {
            Nodes.Clear();
            Tets.Clear();
            BoundaryFaces.Clear();
            TetEdges.Clear();
            hexes.Clear();
            nodeMap.Clear();
        }

        private int GetOrAddNode(double x, double y, double z)
        {
            string key = string.Format(CultureInfo.InvariantCulture,
                "{0:F5}|{1:F5}|{2:F5}", x, y, z);
            if (nodeMap.TryGetValue(key, out int id)) return id;
            id = Nodes.Count;
            Nodes.Add(new Node(x, y, z));
            nodeMap[key] = id;
            return id;
        }

        private static double Trilinear(double u, double v, double w, double[] c)
        {
            double N0 = (1 - u) * (1 - v) * (1 - w);
            double N1 = u * (1 - v) * (1 - w);
            double N2 = u * v * (1 - w);
            double N3 = (1 - u) * v * (1 - w);
            double N4 = (1 - u) * (1 - v) * w;
            double N5 = u * (1 - v) * w;
            double N6 = u * v * w;
            double N7 = (1 - u) * v * w;
            return N0 * c[0] + N1 * c[1] + N2 * c[2] + N3 * c[3]
                 + N4 * c[4] + N5 * c[5] + N6 * c[6] + N7 * c[7];
        }

        private static double Dist(double x1, double y1, double z1,
                                   double x2, double y2, double z2)
        {
            double dx = x2 - x1, dy = y2 - y1, dz = z2 - z1;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public void AddBlock(
            double x0, double y0, double z0,
            double x1, double y1, double z1,
            double x2, double y2, double z2,
            double x3, double y3, double z3,
            double x4, double y4, double z4,
            double x5, double y5, double z5,
            double x6, double y6, double z6,
            double x7, double y7, double z7,
            int nx, int ny, int nz)
        {
            double[] xs = { x0, x1, x2, x3, x4, x5, x6, x7 };
            double[] ys = { y0, y1, y2, y3, y4, y5, y6, y7 };
            double[] zs = { z0, z1, z2, z3, z4, z5, z6, z7 };

            int[,,] ids = new int[nx + 1, ny + 1, nz + 1];
            for (int k = 0; k <= nz; k++)
            {
                double w = (double)k / nz;
                for (int j = 0; j <= ny; j++)
                {
                    double v = (double)j / ny;
                    for (int i = 0; i <= nx; i++)
                    {
                        double u = (double)i / nx;
                        ids[i, j, k] = GetOrAddNode(
                            Trilinear(u, v, w, xs),
                            Trilinear(u, v, w, ys),
                            Trilinear(u, v, w, zs));
                    }
                }
            }
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    for (int k = 0; k < nz; k++)
                    {
                        int v0 = ids[i, j, k];
                        int v1 = ids[i + 1, j, k];
                        int v2 = ids[i + 1, j + 1, k];
                        int v3 = ids[i, j + 1, k];
                        int v4 = ids[i, j, k + 1];
                        int v5 = ids[i + 1, j, k + 1];
                        int v6 = ids[i + 1, j + 1, k + 1];
                        int v7 = ids[i, j + 1, k + 1];
                        hexes.Add(new Hex(new[] { v0, v1, v2, v3, v4, v5, v6, v7 }));
                    }
        }

        public void AddBlockAuto(
            double x0, double y0, double z0,
            double x1, double y1, double z1,
            double x2, double y2, double z2,
            double x3, double y3, double z3,
            double x4, double y4, double z4,
            double x5, double y5, double z5,
            double x6, double y6, double z6,
            double x7, double y7, double z7)
        {
            double Lu = Dist(x0, y0, z0, x1, y1, z1);
            double Lv = Dist(x0, y0, z0, x3, y3, z3);
            double Lw = Dist(x0, y0, z0, x4, y4, z4);

            int nx = Math.Max(1, (int)Math.Round(Lu / h));
            int ny = Math.Max(1, (int)Math.Round(Lv / h));
            int nz = Math.Max(1, (int)Math.Round(Lw / h));

            AddBlock(
                x0, y0, z0, x1, y1, z1, x2, y2, z2, x3, y3, z3,
                x4, y4, z4, x5, y5, z5, x6, y6, z6, x7, y7, z7,
                nx, ny, nz);
        }
        public void LoadDomain(string path)
        {
            Domain = DomainFileReader.Read(path);
        }

       public void BuildFromDomain()
{
    Reset();
    if (Domain == null) return;
    var d = Domain;

    int cx = d.Kx - 1, cy = d.Ky - 1, cz = d.Kz - 1;
    var active = new bool[cx, cy, cz];

    foreach (var s in d.Subdomains)
    {
        int i0 = Math.Max(0, s.Nxb - 1), i1 = Math.Min(cx, s.Nxe);
        int j0 = Math.Max(0, s.Nyb - 1), j1 = Math.Min(cy, s.Nye);
        int k0 = Math.Max(0, s.Nzb - 1), k1 = Math.Min(cz, s.Nze);
        for (int k = k0; k < k1; k++)
            for (int j = j0; j < j1; j++)
                for (int i = i0; i < i1; i++)
                    active[i, j, k] = true;
    }
    int[] nx = new int[d.Kx - 1];
    for (int ii = 0; ii < d.Kx - 1; ii++)
    {
        double w = 0;
        for (int j = 0; j < d.Ky; j++)
            w += Math.Abs(d.X[j, ii + 1] - d.X[j, ii]);
        w /= d.Ky;
        nx[ii] = Math.Max(1, (int)Math.Round(w / h));
    }
    int[] ny = new int[d.Ky - 1];
    for (int jj = 0; jj < d.Ky - 1; jj++)
    {
        double w = Math.Abs(d.Y[jj + 1, 0] - d.Y[jj, 0]);
        ny[jj] = Math.Max(1, (int)Math.Round(w / h));
    }
    int[] nz = new int[d.Kz - 1];
    for (int kk = 0; kk < d.Kz - 1; kk++)
    {
        double w = Math.Abs(d.Z[kk + 1] - d.Z[kk]);
        nz[kk] = Math.Max(1, (int)Math.Round(w / h));
    }

    for (int i = 0; i < cx; i++)
    for (int j = 0; j < cy; j++)
    for (int k = 0; k < cz; k++)
    {
        if (!active[i, j, k]) continue;

        double[] xs = { d.X[j, i],   d.X[j, i + 1], d.X[j + 1, i + 1], d.X[j + 1, i],
                        d.X[j, i],   d.X[j, i + 1], d.X[j + 1, i + 1], d.X[j + 1, i] };
        double[] ys = { d.Z[k],     d.Z[k],         d.Z[k],           d.Z[k],
                        d.Z[k + 1], d.Z[k + 1],     d.Z[k + 1],       d.Z[k + 1] };
        double[] zs = { d.Y[j, i],   d.Y[j, i + 1], d.Y[j + 1, i + 1], d.Y[j + 1, i],
                        d.Y[j, i],   d.Y[j, i + 1], d.Y[j + 1, i + 1], d.Y[j + 1, i] };

        int nu = nx[i], nv = ny[j], nw = nz[k];
        int[,,] ids = new int[nu + 1, nv + 1, nw + 1];

        for (int a = 0; a <= nu; a++)
        for (int b = 0; b <= nv; b++)
        for (int c = 0; c <= nw; c++)
        {
            double u = (double)a / nu;
            double v = (double)b / nv;
            double w = (double)c / nw;
            ids[a, b, c] = GetOrAddNode(
                Trilinear(u, v, w, xs),
                Trilinear(u, v, w, ys),
                Trilinear(u, v, w, zs));
        }

        for (int a = 0; a < nu; a++)
        for (int b = 0; b < nv; b++)
        for (int c = 0; c < nw; c++)
        {
            int v0 = ids[a, b, c];
            int v1 = ids[a + 1, b, c];
            int v2 = ids[a + 1, b + 1, c];
            int v3 = ids[a, b + 1, c];
            int v4 = ids[a, b, c + 1];
            int v5 = ids[a + 1, b, c + 1];
            int v6 = ids[a + 1, b + 1, c + 1];
            int v7 = ids[a, b + 1, c + 1];
            hexes.Add(new Hex(new[] { v0, v1, v2, v3, v4, v5, v6, v7 }));
        }
    }

    SplitToTets();
    ExtractBoundaryFaces();
    ExtractTetEdges();
}

       

        private static readonly int[][] HexToTets = new int[][]
        {
            new[] {0,1,2,6},
            new[] {0,2,3,6},
            new[] {0,3,7,6},
            new[] {0,7,4,6},
            new[] {0,4,5,6},
            new[] {0,5,1,6}
        };

        public void SplitToTets()
        {
            Tets.Clear();
            foreach (var hx in hexes)
                foreach (var p in HexToTets)
                    Tets.Add(new Tet(hx.V[p[0]], hx.V[p[1]],
                                     hx.V[p[2]], hx.V[p[3]]));
            hexes.Clear();
        }

        public void ExtractBoundaryFaces()
        {
            var map = new Dictionary<string, List<(int[] face, int tet)>>();
            for (int t = 0; t < Tets.Count; t++)
            {
                int a = Tets[t].V[0], b = Tets[t].V[1];
                int c = Tets[t].V[2], d = Tets[t].V[3];
                int[][] fs =
                {
                    new[]{a,b,c}, new[]{a,b,d}, new[]{a,c,d}, new[]{b,c,d}
                };
                foreach (var f in fs)
                {
                    var s = new[] { f[0], f[1], f[2] };
                    Array.Sort(s);
                    string key = s[0] + "_" + s[1] + "_" + s[2];
                    if (!map.TryGetValue(key, out var list))
                    {
                        list = new List<(int[], int)>();
                        map[key] = list;
                    }
                    list.Add((f, t));
                }
            }

            BoundaryFaces.Clear();
            foreach (var kv in map)
            {
                if (kv.Value.Count != 1) continue;
                var (f, t) = kv.Value[0];
                var tet = Tets[t];

                double cx = 0, cy = 0, cz = 0;
                for (int i = 0; i < 4; i++)
                {
                    var n = Nodes[tet.V[i]];
                    cx += n.X; cy += n.Y; cz += n.Z;
                }
                cx /= 4; cy /= 4; cz /= 4;

                var p0 = Nodes[f[0]]; var p1 = Nodes[f[1]]; var p2 = Nodes[f[2]];
                double e1x = p1.X - p0.X, e1y = p1.Y - p0.Y, e1z = p1.Z - p0.Z;
                double e2x = p2.X - p0.X, e2y = p2.Y - p0.Y, e2z = p2.Z - p0.Z;
                double nxv = e1y * e2z - e1z * e2y;
                double nyv = e1z * e2x - e1x * e2z;
                double nzv = e1x * e2y - e1y * e2x;
                double dx = p0.X - cx, dy = p0.Y - cy, dz = p0.Z - cz;
                double dot = nxv * dx + nyv * dy + nzv * dz;

                if (dot > 0) BoundaryFaces.Add(new BFace(f[0], f[1], f[2]));
                else BoundaryFaces.Add(new BFace(f[0], f[2], f[1]));
            }
        }

        public void ExtractTetEdges()
        {
            var set = new HashSet<long>();
            TetEdges.Clear();
            foreach (var tet in Tets)
            {
                int a = tet.V[0], b = tet.V[1], c = tet.V[2], d = tet.V[3];
                int[][] edges =
                {
                    new[]{a,b}, new[]{a,c}, new[]{a,d},
                    new[]{b,c}, new[]{b,d}, new[]{c,d}
                };
                foreach (var e in edges)
                {
                    int x = Math.Min(e[0], e[1]);
                    int y = Math.Max(e[0], e[1]);
                    long key = ((long)x << 32) | (uint)y;
                    if (set.Add(key))
                        TetEdges.Add(new[] { x, y });
                }
            }
        }
    }
    public class MeshForm : Form
    {
        private MeshGenerator mesh;

        private double angleX = 0.55;
        private double angleY = 0.75;
        private double baseScale = 1.0;
        private double zoom = 1.0;

        private double centerX, centerY, centerZ;
        private double bboxDiag = 1.0;

        private Point lastMouse;
        private bool dragging = false;

        private int mode = 0;

        private readonly SolidBrush[] palette = new SolidBrush[16];

        public MeshForm(MeshGenerator mesh)
        {
            this.mesh = mesh;

            for (int i = 0; i < 16; i++)
            {
                double t = i / 15.0;
                int R = (int)(55 + 175 * t);
                int G = (int)(85 + 145 * t);
                int B = (int)(125 + 120 * t);
                palette[i] = new SolidBrush(Color.FromArgb(R, G, B));
            }

            Text = $"МКА — лаб. №1 — h = {mesh.h:0.###}";
            Width = 1100;
            Height = 750;
            DoubleBuffered = true;
            BackColor = Color.White;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            ComputeBounds();

            MouseDown += (s, e) => { dragging = true; lastMouse = e.Location; };
            MouseUp += (s, e) => { dragging = false; };
            MouseMove += (s, e) =>
            {
                if (!dragging) return;
                angleY += (e.X - lastMouse.X) * 0.01;
                angleX += (e.Y - lastMouse.Y) * 0.01;
                lastMouse = e.Location;
                Invalidate();
            };
            MouseWheel += (s, e) =>
            {
                zoom *= e.Delta > 0 ? 1.1 : 1.0 / 1.1;
                Invalidate();
            };
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.D1) { mode = 1; Invalidate(); }
                if (e.KeyCode == Keys.D2) { mode = 0; Invalidate(); }
                if (e.KeyCode == Keys.D3) { mode = 2; Invalidate(); }
                if (e.KeyCode == Keys.R)
                { angleX = 0.55; angleY = 0.75; zoom = 1.0; Invalidate(); }
                if (e.KeyCode == Keys.Escape) Close();

                if (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)
                {
                    mesh.h = Math.Max(mesh.h / 2.0, 0.1);
                    Rebuild();
                }
                if (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)
                {
                    mesh.h = Math.Min(mesh.h * 2.0, 8.0);
                    Rebuild();
                }
            };
            Resize += (s, e) => { UpdateScale(); Invalidate(); };
        }

        private void Rebuild()
        {
            mesh.BuildFromDomain();
            ComputeBounds();
            Text = $"МКА — лаб. №1 — h = {mesh.h:0.###}";
            Invalidate();
        }

        private void ComputeBounds()
        {
            if (mesh.Nodes.Count == 0) return;

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            double minZ = double.MaxValue, maxZ = double.MinValue;
            foreach (var n in mesh.Nodes)
            {
                if (n.X < minX) minX = n.X; if (n.X > maxX) maxX = n.X;
                if (n.Y < minY) minY = n.Y; if (n.Y > maxY) maxY = n.Y;
                if (n.Z < minZ) minZ = n.Z; if (n.Z > maxZ) maxZ = n.Z;
            }
            centerX = (minX + maxX) / 2;
            centerY = (minY + maxY) / 2;
            centerZ = (minZ + maxZ) / 2;

            double dx = maxX - minX, dy = maxY - minY, dz = maxZ - minZ;
            bboxDiag = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (bboxDiag < 1e-9) bboxDiag = 1.0;

            UpdateScale();
        }

        private void UpdateScale()
        {
            int w = ClientSize.Width, hh = ClientSize.Height;
            if (w <= 0 || hh <= 0) return;
            baseScale = Math.Min(w, hh) * 0.42 / bboxDiag;
        }

        private void RotateAll(double cosX, double sinX, double cosY, double sinY,
                               out double[] rx, out double[] ry, out double[] rz)
        {
            int n = mesh.Nodes.Count;
            rx = new double[n];
            ry = new double[n];
            rz = new double[n];
            for (int i = 0; i < n; i++)
            {
                var p = mesh.Nodes[i];
                double px = p.X - centerX;
                double py = p.Y - centerY;
                double pz = p.Z - centerZ;

                double x1 = px * cosY + pz * sinY;
                double z1 = -px * sinY + pz * cosY;

                double y2 = py * cosX - z1 * sinX;
                double z2 = py * sinX + z1 * cosX;

                rx[i] = x1; ry[i] = y2; rz[i] = z2;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = ClientSize.Width / 2;
            int cy = ClientSize.Height / 2;
            double s = baseScale * zoom;

            double cosY = Math.Cos(angleY), sinY = Math.Sin(angleY);
            double cosX = Math.Cos(angleX), sinX = Math.Sin(angleX);

            RotateAll(cosX, sinX, cosY, sinY,
                      out var rx, out var ry, out var rz);

            if (mode == 0 || mode == 2) DrawSurface(g, cx, cy, s, rx, ry, rz);
            if (mode == 1 || mode == 2) DrawTetEdges(g, cx, cy, s, rx, ry, rz);

            DrawInfo(g);
        }

        private void DrawSurface(Graphics g, int cx, int cy, double s,
                                 double[] rx, double[] ry, double[] rz)
        {
            double lx = 0.35, ly = 0.55, lz = 0.75;
            double ll = Math.Sqrt(lx * lx + ly * ly + lz * lz);
            lx /= ll; ly /= ll; lz /= ll;

            var faces = mesh.BoundaryFaces;
            var items = new (int a, int b, int c, double depth, int shade)[faces.Count];

            for (int k = 0; k < faces.Count; k++)
            {
                var f = faces[k];
                double depth = (rz[f.A] + rz[f.B] + rz[f.C]) / 3.0;

                double e1x = rx[f.B] - rx[f.A];
                double e1y = ry[f.B] - ry[f.A];
                double e1z = rz[f.B] - rz[f.A];
                double e2x = rx[f.C] - rx[f.A];
                double e2y = ry[f.C] - ry[f.A];
                double e2z = rz[f.C] - rz[f.A];

                double nx = e1y * e2z - e1z * e2y;
                double ny = e1z * e2x - e1x * e2z;
                double nz = e1x * e2y - e1y * e2x;
                double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);

                int shade = 4;
                if (nl > 1e-12)
                {
                    nx /= nl; ny /= nl; nz /= nl;
                    double dot = nx * lx + ny * ly + nz * lz;
                    double intensity = 0.25 + 0.75 * Math.Max(0, dot);
                    shade = (int)(intensity * 15);
                    if (shade < 0) shade = 0;
                    if (shade > 15) shade = 15;
                }
                items[k] = (f.A, f.B, f.C, depth, shade);
            }

            Array.Sort(items, (p, q) => p.depth.CompareTo(q.depth));

            using (var edgePen = new Pen(Color.FromArgb(90, 25, 40, 70), 0.5f))
            {
                foreach (var it in items)
                {
                    float x1 = (float)(cx + rx[it.a] * s);
                    float y1 = (float)(cy - ry[it.a] * s);
                    float x2 = (float)(cx + rx[it.b] * s);
                    float y2 = (float)(cy - ry[it.b] * s);
                    float x3 = (float)(cx + rx[it.c] * s);
                    float y3 = (float)(cy - ry[it.c] * s);

                    var pts = new[]
                    {
                        new PointF(x1, y1),
                        new PointF(x2, y2),
                        new PointF(x3, y3)
                    };
                    g.FillPolygon(palette[it.shade], pts);
                    g.DrawPolygon(edgePen, pts);
                }
            }
        }

        private void DrawTetEdges(Graphics g, int cx, int cy, double s,
                                  double[] rx, double[] ry, double[] rz)
        {
            using (var pen = new Pen(Color.FromArgb(160, 130, 30, 30), 0.5f))
            {
                foreach (var e in mesh.TetEdges)
                {
                    float x1 = (float)(cx + rx[e[0]] * s);
                    float y1 = (float)(cy - ry[e[0]] * s);
                    float x2 = (float)(cx + rx[e[1]] * s);
                    float y2 = (float)(cy - ry[e[1]] * s);
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
            }
        }

        private void DrawInfo(Graphics g)
        {
            string modeName = mode == 0 ? "поверхность"
                            : mode == 1 ? "рёбра тетраэдров"
                                        : "поверхность + рёбра";

            string text =
                $"Режим: {modeName}\n" +
                $"h (целевой размер элемента): {mesh.h:0.###}\n" +
                $"Узлов: {mesh.Nodes.Count}\n" +
                $"Тетраэдров: {mesh.Tets.Count}\n" +
                $"Граничных треугольников: {mesh.BoundaryFaces.Count}\n" +
                $"Рёбер тетраэдров: {mesh.TetEdges.Count}\n\n" +
                "ЛКМ — вращение\n" +
                "Колесо — масштаб\n" +
                "1 / 2 / 3 — режимы отображения\n" +
                "+ / − — плотнее / грубее сетка\n" +
                "R — сброс вида\n" +
                "Esc — выход";

            using (var font = new Font("Segoe UI", 9.5f))
            using (var brush = new SolidBrush(Color.FromArgb(230, 20, 20, 20)))
            using (var bg = new SolidBrush(Color.FromArgb(215, 255, 255, 255)))
            {
                var size = g.MeasureString(text, font);
                g.FillRectangle(bg, 8, 8, size.Width + 10, size.Height + 10);
                g.DrawString(text, font, brush, 13, 13);
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string path = "domain.txt";
            if (!File.Exists(path)) File.WriteAllText(path, DefaultDomain);

            var mesh = new MeshGenerator();
            mesh.h = 1.0;
            mesh.LoadDomain(path);
            mesh.BuildFromDomain();

            Application.Run(new MeshForm(mesh));
        }

        private const string DefaultDomain =
@"7 7
-10 0  -5 0  -3 0  0 0  3 0  5 0  10 0
-9.5 1  -5 1  -3 1  0 1  3 1  5 1  9.5 1
-9 2  -5 2  -3 2  0 2  3 2  5 2  9 2
-8.5 3  -5 3  -3 3  0 3  3 3  5 3  8.5 3
-8 4  -5 4  -3 4  0 4  3 4  5 4  8 4
-7.5 5  -5 5  -3 5  0 5  3 5  5 5  7.5 5
-7 6  -5 6  -3 6  0 6  3 6  5 6  7 6
4
0 1 3 6
7
1 1 6 1 6 1 1
1 1 2 1 6 2 2
1 3 4 1 2 2 2
1 5 6 1 6 2 2
1 1 1 1 6 3 3
1 2 5 1 2 3 3
1 6 6 1 6 3 3
";
    }
}