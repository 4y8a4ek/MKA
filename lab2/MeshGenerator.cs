using System;
using System.Collections.Generic;
using System.Globalization;

namespace Lab2
{
    public class MeshGenerator
    {
        // ----- Геометрия и элементы -----
        public List<Node> Nodes = new List<Node>();
        public List<Tet> Tets = new List<Tet>();
        public List<BFace> BoundaryFaces = new List<BFace>();
        public List<int[]> TetEdges = new List<int[]>();   // все рёбра (для отрисовки, без дублей)

        // ----- Топология с глобальной нумерацией -----
        public List<Edge> Edges = new List<Edge>();
        public List<Face> Faces = new List<Face>();
        private Dictionary<long, int> edgeMap = new Dictionary<long, int>();
        private Dictionary<long, int> faceMap = new Dictionary<long, int>();

        // ----- Обратные связи -----
        public List<List<int>> NodeToTets = new List<List<int>>();
        public List<List<int>> NodeToEdges = new List<List<int>>();
        public List<List<int>> NodeToFaces = new List<List<int>>();
        public List<List<int>> EdgeToTets = new List<List<int>>();
        public List<List<int>> EdgeToFaces = new List<List<int>>();
        public List<List<int>> FaceToTets = new List<List<int>>();
        public List<int[]> TetToEdges = new List<int[]>();   // 6 локальных рёбер
        public List<int[]> TetToFaces = new List<int[]>();   // 4 локальные грани

        private List<Hex> hexes = new List<Hex>();
        private Dictionary<string, int> nodeMap = new Dictionary<string, int>();

        public double h = 1.0;
        public DomainDescription Domain;
        public BasisScheme Scheme = BasisScheme.P1_Lagrange;

        public void Reset()
        {
            Nodes.Clear(); Tets.Clear(); BoundaryFaces.Clear(); TetEdges.Clear();
            Edges.Clear(); Faces.Clear(); edgeMap.Clear(); faceMap.Clear();
            NodeToTets.Clear(); NodeToEdges.Clear(); NodeToFaces.Clear();
            EdgeToTets.Clear(); EdgeToFaces.Clear(); FaceToTets.Clear();
            TetToEdges.Clear(); TetToFaces.Clear();
            hexes.Clear(); nodeMap.Clear();
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

        public void LoadDomain(string path)
        {
            Domain = DomainFileReader.Read(path);
        }

        // =========================================================
        //  ПОСТРОЕНИЕ: ТИРАЖИРОВАНИЕ БАЗОВОЙ ЯЧЕЙКИ
        // =========================================================
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

            // Общие деления по каждому интервалу
            int[] nx = new int[cx];
            for (int ii = 0; ii < cx; ii++)
            {
                double w = 0;
                for (int j = 0; j < d.Ky; j++)
                    w += Math.Abs(d.X[j, ii + 1] - d.X[j, ii]);
                w /= d.Ky;
                nx[ii] = Math.Max(1, (int)Math.Round(w / h));
            }
            int[] ny = new int[cy];
            for (int jj = 0; jj < cy; jj++)
            {
                double w = Math.Abs(d.Y[jj + 1, 0] - d.Y[jj, 0]);
                ny[jj] = Math.Max(1, (int)Math.Round(w / h));
            }
            int[] nz = new int[cz];
            for (int kk = 0; kk < cz; kk++)
            {
                double w = Math.Abs(d.Z[kk + 1] - d.Z[kk]);
                nz[kk] = Math.Max(1, (int)Math.Round(w / h));
            }

            for (int i = 0; i < cx; i++)
                for (int j = 0; j < cy; j++)
                    for (int k = 0; k < cz; k++)
                    {
                        if (!active[i, j, k]) continue;
                        TileCell(d, i, j, k, nx[i], ny[j], nz[k]);
                    }

            SplitToTets();
            ExtractBoundaryFaces();
            ExtractTetEdges();
            BuildTopology();
        }

        private void TileCell(DomainDescription d, int i, int j, int k,
                              int nu, int nv, int nw)
        {
            double[] xs = { d.X[j, i], d.X[j, i + 1], d.X[j + 1, i + 1], d.X[j + 1, i],
                            d.X[j, i], d.X[j, i + 1], d.X[j + 1, i + 1], d.X[j + 1, i] };
            double[] ys = { d.Z[k], d.Z[k], d.Z[k], d.Z[k],
                            d.Z[k + 1], d.Z[k + 1], d.Z[k + 1], d.Z[k + 1] };
            double[] zs = { d.Y[j, i], d.Y[j, i + 1], d.Y[j + 1, i + 1], d.Y[j + 1, i],
                            d.Y[j, i], d.Y[j, i + 1], d.Y[j + 1, i + 1], d.Y[j + 1, i] };

            int[,,] ids = new int[nu + 1, nv + 1, nw + 1];
            for (int a = 0; a <= nu; a++)
                for (int b = 0; b <= nv; b++)
                    for (int c = 0; c <= nw; c++)
                    {
                        double u = (double)a / nu, v = (double)b / nv, w = (double)c / nw;
                        ids[a, b, c] = GetOrAddNode(
                            Trilinear(u, v, w, xs),
                            Trilinear(u, v, w, ys),
                            Trilinear(u, v, w, zs));
                    }

            for (int a = 0; a < nu; a++)
                for (int b = 0; b < nv; b++)
                    for (int c = 0; c < nw; c++)
                    {
                        int v0 = ids[a, b, c], v1 = ids[a + 1, b, c],
                            v2 = ids[a + 1, b + 1, c], v3 = ids[a, b + 1, c],
                            v4 = ids[a, b, c + 1], v5 = ids[a + 1, b, c + 1],
                            v6 = ids[a + 1, b + 1, c + 1], v7 = ids[a, b + 1, c + 1];
                        hexes.Add(new Hex(new[] { v0, v1, v2, v3, v4, v5, v6, v7 }));
                    }
        }

        private static readonly int[][] HexToTets = new int[][]
        {
            new[] {0,1,2,6}, new[] {0,2,3,6}, new[] {0,3,7,6},
            new[] {0,7,4,6}, new[] {0,4,5,6}, new[] {0,5,1,6}
        };

        public void SplitToTets()
        {
            Tets.Clear();
            foreach (var hx in hexes)
                foreach (var p in HexToTets)
                    Tets.Add(new Tet(hx.V[p[0]], hx.V[p[1]], hx.V[p[2]], hx.V[p[3]]));
            hexes.Clear();
        }

        public void ExtractBoundaryFaces()
        {
            var map = new Dictionary<string, List<(int[] face, int tet)>>();
            for (int t = 0; t < Tets.Count; t++)
            {
                int a = Tets[t].V[0], b = Tets[t].V[1], c = Tets[t].V[2], d = Tets[t].V[3];
                int[][] fs = { new[] { a, b, c }, new[] { a, b, d }, new[] { a, c, d }, new[] { b, c, d } };
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

                double ccx = 0, ccy = 0, ccz = 0;
                for (int i = 0; i < 4; i++)
                {
                    var n = Nodes[tet.V[i]];
                    ccx += n.X; ccy += n.Y; ccz += n.Z;
                }
                ccx /= 4; ccy /= 4; ccz /= 4;

                var p0 = Nodes[f[0]]; var p1 = Nodes[f[1]]; var p2 = Nodes[f[2]];
                double e1x = p1.X - p0.X, e1y = p1.Y - p0.Y, e1z = p1.Z - p0.Z;
                double e2x = p2.X - p0.X, e2y = p2.Y - p0.Y, e2z = p2.Z - p0.Z;
                double nxv = e1y * e2z - e1z * e2y;
                double nyv = e1z * e2x - e1x * e2z;
                double nzv = e1x * e2y - e1y * e2x;
                double dx = p0.X - ccx, dy = p0.Y - ccy, dz = p0.Z - ccz;
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

        public void BuildTopology()
        {
            Edges.Clear(); Faces.Clear(); edgeMap.Clear(); faceMap.Clear();
            TetToEdges.Clear(); TetToFaces.Clear();

            for (int t = 0; t < Tets.Count; t++)
            {
                var tet = Tets[t];
                int a = tet.V[0], b = tet.V[1], c = tet.V[2], d = tet.V[3];

                var es = new int[6];
                int k = 0;
                for (int i = 0; i < 4; i++)
                    for (int j = i + 1; j < 4; j++)
                        es[k++] = GetOrAddEdge(tet.V[i], tet.V[j]);
                TetToEdges.Add(es);

                var fs = new int[4];
                fs[0] = GetOrAddFace(a, b, c);
                fs[1] = GetOrAddFace(a, b, d);
                fs[2] = GetOrAddFace(a, c, d);
                fs[3] = GetOrAddFace(b, c, d);
                TetToFaces.Add(fs);
            }

            int nn = Nodes.Count;
            NodeToTets = new List<List<int>>(nn);
            NodeToEdges = new List<List<int>>(nn);
            NodeToFaces = new List<List<int>>(nn);
            for (int i = 0; i < nn; i++)
            {
                NodeToTets.Add(new List<int>());
                NodeToEdges.Add(new List<int>());
                NodeToFaces.Add(new List<int>());
            }

            EdgeToTets = new List<List<int>>(Edges.Count);
            EdgeToFaces = new List<List<int>>(Edges.Count);
            for (int i = 0; i < Edges.Count; i++)
            {
                EdgeToTets.Add(new List<int>());
                EdgeToFaces.Add(new List<int>());
            }

            FaceToTets = new List<List<int>>(Faces.Count);
            for (int i = 0; i < Faces.Count; i++)
                FaceToTets.Add(new List<int>());

            for (int t = 0; t < Tets.Count; t++)
            {
                foreach (var v in Tets[t].V) NodeToTets[v].Add(t);
                foreach (var e in TetToEdges[t]) EdgeToTets[e].Add(t);
                foreach (var f in TetToFaces[t]) FaceToTets[f].Add(t);
            }

            for (int e = 0; e < Edges.Count; e++)
            {
                NodeToEdges[Edges[e].A].Add(e);
                NodeToEdges[Edges[e].B].Add(e);
            }

            for (int f = 0; f < Faces.Count; f++)
            {
                var fc = Faces[f];
                NodeToFaces[fc.A].Add(f);
                NodeToFaces[fc.B].Add(f);
                NodeToFaces[fc.C].Add(f);
                TryAddEdgeFace(fc.A, fc.B, f);
                TryAddEdgeFace(fc.A, fc.C, f);
                TryAddEdgeFace(fc.B, fc.C, f);
            }
        }

        private void TryAddEdgeFace(int a, int b, int f)
        {
            long key = MakeEdgeKey(a, b);
            if (edgeMap.TryGetValue(key, out int e))
                if (!EdgeToFaces[e].Contains(f))
                    EdgeToFaces[e].Add(f);
        }

        private int GetOrAddEdge(int a, int b)
        {
            long key = MakeEdgeKey(a, b);
            if (edgeMap.TryGetValue(key, out int id)) return id;
            id = Edges.Count;
            Edges.Add(new Edge(a, b));
            edgeMap[key] = id;
            return id;
        }

        private static long MakeEdgeKey(int a, int b)
        {
            int mn = a < b ? a : b;
            int mx = a < b ? b : a;
            return ((long)mn << 32) | (uint)mx;
        }

        private int GetOrAddFace(int a, int b, int c)
        {
            long key = MakeFaceKey(a, b, c);
            if (faceMap.TryGetValue(key, out int id)) return id;
            id = Faces.Count;
            Faces.Add(new Face(a, b, c));
            faceMap[key] = id;
            return id;
        }

        private static long MakeFaceKey(int a, int b, int c)
        {
            int t;
            if (a > b) { t = a; a = b; b = t; }
            if (b > c) { t = b; b = c; c = t; }
            if (a > b) { t = a; a = b; b = t; }
            return ((long)a << 42) | ((long)b << 21) | (long)c;
        }

        public int FindEdge(int a, int b)
        {
            long key = MakeEdgeKey(a, b);
            return edgeMap.TryGetValue(key, out int id) ? id : -1;
        }

        public int FindFace(int a, int b, int c)
        {
            long key = MakeFaceKey(a, b, c);
            return faceMap.TryGetValue(key, out int id) ? id : -1;
        }

        // =========================================================
        //  БАЗИСНЫЕ ФУНКЦИИ — НУМЕРАЦИЯ
        // =========================================================
        public int NumVertices => Nodes.Count;
        public int NumEdges => Edges.Count;
        public int NumFaces => Faces.Count;
        public int NumTets => Tets.Count;

        public int EdgeFunctionsPerEdge()
        {
            switch (Scheme)
            {
                case BasisScheme.P1_Lagrange: return 0;
                case BasisScheme.P2_Lagrange:
                case BasisScheme.P2_Hierarchical: return 1;
                case BasisScheme.P3_Lagrange:
                case BasisScheme.P3_Hierarchical: return 2;
            }
            return 0;
        }

        public bool HasFaceFunctions() =>
            Scheme == BasisScheme.P3_Lagrange || Scheme == BasisScheme.P3_Hierarchical;

        public bool HasVolumeFunctions() => Scheme == BasisScheme.P3_Hierarchical;

        public int TotalBasisFunctions()
        {
            int ep = EdgeFunctionsPerEdge();
            int total = NumVertices + ep * NumEdges;
            if (HasFaceFunctions()) total += NumFaces;
            if (HasVolumeFunctions()) total += NumTets;
            return total;
        }

        public int VertexBasisNumber(int nodeId) => nodeId + 1;

        public int EdgeBasisBase(int edgeId)
        {
            int ep = EdgeFunctionsPerEdge();
            if (ep == 0) return -1;
            return NumVertices + 1 + edgeId * ep;
        }

        public int FaceBasisNumber(int faceId) =>
            HasFaceFunctions() ? (NumVertices + EdgeFunctionsPerEdge() * NumEdges + 1 + faceId) : -1;

        public int VolumeBasisNumber(int tetId) =>
            HasVolumeFunctions()
                ? (NumVertices + EdgeFunctionsPerEdge() * NumEdges + NumFaces + 1 + tetId)
                : -1;

        public (string type, int id, int sub) FindBasisObject(int globalNumber)
        {
            int n = globalNumber - 1;
            if (n < 0 || n >= TotalBasisFunctions()) return ("none", -1, 0);
            int NN = NumVertices, NE = NumEdges, NF = NumFaces, NT = NumTets;
            if (n < NN) return ("vertex", n, 0);
            n -= NN;
            int ep = EdgeFunctionsPerEdge();
            if (ep > 0 && n < ep * NE) return ("edge", n / ep, n % ep);
            n -= ep * NE;
            if (HasFaceFunctions() && n < NF) return ("face", n, 0);
            n -= NF;
            if (HasVolumeFunctions() && n < NT) return ("volume", n, 0);
            return ("none", -1, 0);
        }

        public List<(int local, int global, string type, string info)> GetTetBasis(int t)
        {
            var list = new List<(int, int, string, string)>();
            var tet = Tets[t];
            for (int i = 0; i < 4; i++)
                list.Add((i + 1, VertexBasisNumber(tet.V[i]), "vertex", $"узел {tet.V[i] + 1}"));

            int ep = EdgeFunctionsPerEdge();
            if (ep > 0)
            {
                var edges = TetToEdges[t];
                for (int i = 0; i < 6; i++)
                {
                    int gbase = EdgeBasisBase(edges[i]);
                    for (int s = 0; s < ep; s++)
                        list.Add((5 + i * ep + s, gbase + s, "edge",
                                  $"ребро {edges[i] + 1} (подф.{s + 1})"));
                }
            }

            if (HasFaceFunctions())
            {
                var faces = TetToFaces[t];
                int loc0 = 5 + 6 * ep;
                for (int i = 0; i < 4; i++)
                    list.Add((loc0 + i, FaceBasisNumber(faces[i]), "face", $"грань {faces[i] + 1}"));
            }

            if (HasVolumeFunctions())
                list.Add((5 + 6 * ep + 4, VolumeBasisNumber(t), "volume", $"КЭ {t + 1} (bubble)"));

            return list;
        }
    }
}