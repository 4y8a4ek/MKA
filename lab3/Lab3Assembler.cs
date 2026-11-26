using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lab2
{
    public class Lab3Assembler
    {
        private MeshGenerator mesh;
        public SparseMatrix A { get; private set; }
        public double[] RHS { get; private set; }
        public double[] Solution { get; private set; }
        public int CgIterations { get; private set; }
        public double CgResidual { get; private set; }
        public long AssembleMs { get; private set; }
        public long SolveMs { get; private set; }

        public Lab3Assembler(MeshGenerator meshGen) { mesh = meshGen; }

        // =========================================================
        // ДИСПЕТЧЕР ПО СХЕМЕ
        // =========================================================
        private double[,] ComputeLocalStiffness(Tet tet, int nLocal)
        {
            switch (mesh.Scheme)
            {
                case BasisScheme.P1_Lagrange: return ComputeP1(tet);
                case BasisScheme.P2_Lagrange: return ComputeP2Lagrange(tet);
                case BasisScheme.P3_Lagrange: return ComputeP3Lagrange(tet);
                case BasisScheme.P2_Hierarchical: return ComputeP2Hierarchical(tet);
                case BasisScheme.P3_Hierarchical: return ComputeP3Hierarchical(tet);
                case BasisScheme.Hermite_3:
                    throw new NotImplementedException(
                        "Hermite 3-го порядка требует 4 DOF на вершину (u, du/dx, du/dy, du/dz). " +
                        "Измените MeshGenerator: TotalBasisFunctions/GetTetBasis/FindBasisObject " +
                        "должны возвращать 4*NumVertices глоб. функций и корректные локальные соответствия.");
                default:
                    throw new NotImplementedException($"{mesh.Scheme}");
            }
        }

        // =========================================================
        // P1 (аналитически, как было)
        // =========================================================
        private double[,] ComputeP1(Tet tet)
        {
            Node p0 = mesh.Nodes[tet.V[0]], p1 = mesh.Nodes[tet.V[1]], p2 = mesh.Nodes[tet.V[2]], p3 = mesh.Nodes[tet.V[3]];
            double[,] J = { { p1.X - p0.X, p2.X - p0.X, p3.X - p0.X }, { p1.Y - p0.Y, p2.Y - p0.Y, p3.Y - p0.Y }, { p1.Z - p0.Z, p2.Z - p0.Z, p3.Z - p0.Z } };
            double detJ = Det3(J);
            double V = Math.Abs(detJ) / 6.0;
            double[,] invJ = Inv3(J, detJ);
            double[,] g = new double[4, 3];
            g[0, 0] = -invJ[0, 0] - invJ[1, 0] - invJ[2, 0]; g[0, 1] = -invJ[0, 1] - invJ[1, 1] - invJ[2, 1]; g[0, 2] = -invJ[0, 2] - invJ[1, 2] - invJ[2, 2];
            g[1, 0] = invJ[0, 0]; g[1, 1] = invJ[0, 1]; g[1, 2] = invJ[0, 2];
            g[2, 0] = invJ[1, 0]; g[2, 1] = invJ[1, 1]; g[2, 2] = invJ[1, 2];
            g[3, 0] = invJ[2, 0]; g[3, 1] = invJ[2, 1]; g[3, 2] = invJ[2, 2];
            double[,] Ke = new double[4, 4];
            for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++)
                Ke[i, j] = V * (g[i, 0] * g[j, 0] + g[i, 1] * g[j, 1] + g[i, 2] * g[j, 2]);
            return Ke;
        }

        // =========================================================
        // P2 Lagrange (10x10, 4-точечная квадратура)
        // =========================================================
        private double[,] ComputeP2Lagrange(Tet tet)
        {
            return ComputeGeneric(tet, 10, Quad4, EvalP2Lagrange);
        }

        private void EvalP2Lagrange(Tet tet, double L0, double L1, double L2, double L3, double[] N, double[,] dNL)
        {
            double[] L = { L0, L1, L2, L3 };
            // 4 вершины: N_i = L_i (2L_i - 1)
            for (int i = 0; i < 4; i++)
            {
                N[i] = L[i] * (2 * L[i] - 1);
                double dN = 4 * L[i] - 1;
                SetBaryGrad(dNL, i, i, dN);
            }
            // 6 рёбер: N = 4 L_i L_j
            int[][] edgeV = { new[] { 0, 1 }, new[] { 0, 2 }, new[] { 0, 3 }, new[] { 1, 2 }, new[] { 1, 3 }, new[] { 2, 3 } };
            for (int e = 0; e < 6; e++)
            {
                int ii = edgeV[e][0], jj = edgeV[e][1];
                N[4 + e] = 4 * L[ii] * L[jj];
                // d/dLii = 4 Ljj, d/dLjj = 4 Lii
                AddBaryGrad(dNL, 4 + e, ii, 4 * L[jj]);
                AddBaryGrad(dNL, 4 + e, jj, 4 * L[ii]);
            }
        }

        // =========================================================
        // P3 Lagrange (20x20, 36-точечная квадратура)
        // =========================================================
        private double[,] ComputeP3Lagrange(Tet tet)
        {
            return ComputeGeneric(tet, 20, Quad36, EvalP3Lagrange);
        }

        private void EvalP3Lagrange(Tet tet, double L0, double L1, double L2, double L3, double[] N, double[,] dNL)
        {
            double[] L = { L0, L1, L2, L3 };
            // 4 вершины: N_i = 0.5 L_i (3L_i - 1)(3L_i - 2)
            for (int i = 0; i < 4; i++)
            {
                N[i] = 0.5 * L[i] * (3 * L[i] - 1) * (3 * L[i] - 2);
                double dN = 0.5 * (27 * L[i] * L[i] - 18 * L[i] + 2);
                SetBaryGrad(dNL, i, i, dN);
            }
            // 12 рёберных (2 на ребро)
            int[][] edgeV = { new[] { 0, 1 }, new[] { 0, 2 }, new[] { 0, 3 }, new[] { 1, 2 }, new[] { 1, 3 }, new[] { 2, 3 } };
            for (int e = 0; e < 6; e++)
            {
                int ii = edgeV[e][0], jj = edgeV[e][1];
                int gE = mesh.TetToEdges[mesh.Tets.IndexOf(tet)][e];
                bool flip = tet.V[ii] != mesh.Edges[gE].A;
                // Канонически: sub0 = peak ближе к A(=локальный ii), sub1 = peak ближе к B(=локальный jj)
                int li = flip ? 5 + 2 * e + 1 : 5 + 2 * e;   // в какой локальный слот пойдёт sub0
                int lj = flip ? 5 + 2 * e : 5 + 2 * e + 1; // sub1

                double Nij = L[ii] * L[jj];
                // sub0: (9/2) L_ii L_jj (3L_ii - 1)  — peak near ii
                N[li] = 4.5 * Nij * (3 * L[ii] - 1);
                AddBaryGrad(dNL, li, ii, 4.5 * L[jj] * (6 * L[ii] - 1));
                AddBaryGrad(dNL, li, jj, 4.5 * L[ii] * (3 * L[ii] - 1));
                // sub1: (9/2) L_ii L_jj (3L_jj - 1) — peak near jj
                N[lj] = 4.5 * Nij * (3 * L[jj] - 1);
                AddBaryGrad(dNL, lj, ii, 4.5 * L[jj] * (3 * L[jj] - 1));
                AddBaryGrad(dNL, lj, jj, 4.5 * L[ii] * (6 * L[jj] - 1));
            }
            // 4 граневых: 27 L_i L_j L_k (симметрично → ориентация не важна)
            int[][] faceV = { new[] { 0, 1, 2 }, new[] { 0, 1, 3 }, new[] { 0, 2, 3 }, new[] { 1, 2, 3 } };
            for (int f = 0; f < 4; f++)
            {
                int a = faceV[f][0], b = faceV[f][1], c = faceV[f][2];
                int loc = 17 + f;
                N[loc] = 27 * L[a] * L[b] * L[c];
                AddBaryGrad(dNL, loc, a, 27 * L[b] * L[c]);
                AddBaryGrad(dNL, loc, b, 27 * L[a] * L[c]);
                AddBaryGrad(dNL, loc, c, 27 * L[a] * L[b]);
            }
        }

        // =========================================================
        // P2 Hierarchical (10x10): L_i + L_i*L_j
        // =========================================================
        private double[,] ComputeP2Hierarchical(Tet tet)
        {
            return ComputeGeneric(tet, 10, Quad12, EvalP2Hier);
        }

        private void EvalP2Hier(Tet tet, double L0, double L1, double L2, double L3, double[] N, double[,] dNL)
        {
            double[] L = { L0, L1, L2, L3 };
            for (int i = 0; i < 4; i++)
            {
                N[i] = L[i];
                SetBaryGrad(dNL, i, i, 1.0);
            }
            int[][] edgeV = { new[] { 0, 1 }, new[] { 0, 2 }, new[] { 0, 3 }, new[] { 1, 2 }, new[] { 1, 3 }, new[] { 2, 3 } };
            for (int e = 0; e < 6; e++)
            {
                int ii = edgeV[e][0], jj = edgeV[e][1];
                N[4 + e] = L[ii] * L[jj];
                AddBaryGrad(dNL, 4 + e, ii, L[jj]);
                AddBaryGrad(dNL, 4 + e, jj, L[ii]);
            }
        }

        // =========================================================
        // P3 Hierarchical (21x21): L_i, L_i*L_j, L_i*L_j*(L_i-L_j), 27L_iL_jL_k, bubble
        // =========================================================
        private double[,] ComputeP3Hierarchical(Tet tet)
        {
            return ComputeGeneric(tet, 21, Quad36, EvalP3Hier);
        }

        private void EvalP3Hier(Tet tet, double L0, double L1, double L2, double L3, double[] N, double[,] dNL)
        {
            double[] L = { L0, L1, L2, L3 };
            // 4 вершины (level 0)
            for (int i = 0; i < 4; i++)
            {
                N[i] = L[i];
                SetBaryGrad(dNL, i, i, 1.0);
            }
            // 12 рёберных (2 на ребро: level 1 + level 2)
            int[][] edgeV = { new[] { 0, 1 }, new[] { 0, 2 }, new[] { 0, 3 }, new[] { 1, 2 }, new[] { 1, 3 }, new[] { 2, 3 } };
            int tetIdx = mesh.Tets.IndexOf(tet);
            for (int e = 0; e < 6; e++)
            {
                int ii = edgeV[e][0], jj = edgeV[e][1];
                int gE = mesh.TetToEdges[tetIdx][e];
                bool flip = tet.V[ii] != mesh.Edges[gE].A;

                int s0 = 5 + 2 * e;
                int s1 = 5 + 2 * e + 1;

                // Level 1: L_ii L_jj (симметрично)
                N[s0] = L[ii] * L[jj];
                AddBaryGrad(dNL, s0, ii, L[jj]);
                AddBaryGrad(dNL, s0, jj, L[ii]);

                // Level 2: L_ii L_jj (L_ii - L_jj)  — антисимметрично
                // При flip знак меняется
                double sign = flip ? -1.0 : 1.0;
                double D = L[ii] - L[jj];
                N[s1] = sign * L[ii] * L[jj] * D;
                // d/dLii = sign * [L_jj*D + L_ii*L_jj]
                //        = sign * L_jj * (L_ii - L_jj + L_ii) = sign * L_jj * (2L_ii - L_jj)
                AddBaryGrad(dNL, s1, ii, sign * L[jj] * (2 * L[ii] - L[jj]));
                // d/dLjj = sign * [L_ii*D + L_ii*L_jj*(-1)] = sign * L_ii * (L_ii - L_jj - L_jj) = sign * L_ii * (L_ii - 2L_jj)
                AddBaryGrad(dNL, s1, jj, sign * L[ii] * (L[ii] - 2 * L[jj]));
            }
            // 4 граневых (level 2): 27 L_i L_j L_k
            int[][] faceV = { new[] { 0, 1, 2 }, new[] { 0, 1, 3 }, new[] { 0, 2, 3 }, new[] { 1, 2, 3 } };
            for (int f = 0; f < 4; f++)
            {
                int a = faceV[f][0], b = faceV[f][1], c = faceV[f][2];
                int loc = 17 + f;
                N[loc] = 27 * L[a] * L[b] * L[c];
                AddBaryGrad(dNL, loc, a, 27 * L[b] * L[c]);
                AddBaryGrad(dNL, loc, b, 27 * L[a] * L[c]);
                AddBaryGrad(dNL, loc, c, 27 * L[a] * L[b]);
            }
            // Bubble (level 3): L0 L1 L2 L3
            N[20] = L[0] * L[1] * L[2] * L[3];
            AddBaryGrad(dNL, 20, 0, L[1] * L[2] * L[3]);
            AddBaryGrad(dNL, 20, 1, L[0] * L[2] * L[3]);
            AddBaryGrad(dNL, 20, 2, L[0] * L[1] * L[3]);
            AddBaryGrad(dNL, 20, 3, L[0] * L[1] * L[2]);
        }

        // =========================================================
        // ГЕНЕРИЧЕСКАЯ СБОРКА ЛОКАЛЬНОЙ МАТРИЦЫ
        // =========================================================
        private delegate void ShapeEval(Tet tet, double L0, double L1, double L2, double L3, double[] N, double[,] dNL);
        private delegate List<(double L1, double L2, double L3, double w)> QuadGen(double V);

        private double[,] ComputeGeneric(Tet tet, int nLocal, QuadGen quad, ShapeEval eval)
        {
            Node p0 = mesh.Nodes[tet.V[0]], p1 = mesh.Nodes[tet.V[1]], p2 = mesh.Nodes[tet.V[2]], p3 = mesh.Nodes[tet.V[3]];
            double[,] J = { { p1.X - p0.X, p2.X - p0.X, p3.X - p0.X }, { p1.Y - p0.Y, p2.Y - p0.Y, p3.Y - p0.Y }, { p1.Z - p0.Z, p2.Z - p0.Z, p3.Z - p0.Z } };
            double detJ = Det3(J);
            double V = Math.Abs(detJ) / 6.0;
            double[,] invJ = Inv3(J, detJ);

            double[,] Ke = new double[nLocal, nLocal];
            var pts = quad(V);
            double[] N = new double[nLocal];
            double[,] dNL = new double[nLocal, 3];
            double[,] grad = new double[nLocal, 3];

            foreach (var (L1, L2, L3, w) in pts)
            {
                double L0 = 1.0 - L1 - L2 - L3;
                Array.Clear(N, 0, nLocal);
                Array.Clear(dNL, 0, nLocal * 3);
                eval(tet, L0, L1, L2, L3, N, dNL);

                for (int i = 0; i < nLocal; i++)
                    for (int d = 0; d < 3; d++)
                    {
                        double s = 0;
                        for (int k = 0; k < 3; k++) s += dNL[i, k] * invJ[k, d];
                        grad[i, d] = s;
                    }

                for (int i = 0; i < nLocal; i++)
                    for (int j = 0; j < nLocal; j++)
                        Ke[i, j] += w * (grad[i, 0] * grad[j, 0] + grad[i, 1] * grad[j, 1] + grad[i, 2] * grad[j, 2]);
            }
            return Ke;
        }

        // =========================================================
        // КВАДРАТУРЫ
        // =========================================================
        private List<(double L1, double L2, double L3, double w)> Quad4(double V)
        {
            // 4-точечная (a,b,b,b), точна до степени 2
            double a = (5.0 + 3.0 * Math.Sqrt(5.0)) / 20.0;
            double b = (5.0 - Math.Sqrt(5.0)) / 20.0;
            double w0 = V / 4.0;
            return new List<(double, double, double, double)>
            {
                (a, b, b, w0), (b, a, b, w0), (b, b, a, w0), (b, b, b, w0)
            };
        }

        private List<(double L1, double L2, double L3, double w)> Quad12(double V)
        {
            // 4*3*2 = 24 точки через подстановку (точность ~ степень 3)
            // Но оставим 12 = 4*3*1? Хм... используем 4*3 = 12 в подстановке с nu=4, nv=3, nw=2 = 24.
            return QuadSub(V, 4, 3, 2);
        }

        private List<(double L1, double L2, double L3, double w)> Quad36(double V)
        {
            // 4*3*3 = 36 точек (достаточно для степени 4)
            return QuadSub(V, 4, 3, 3);
        }

        // Подстановка: (u,v,w) ∈ [0,1]³ → (L1,L2,L3) = (u, v(1-u), w(1-u)(1-v))
        private List<(double L1, double L2, double L3, double w)> QuadSub(double V, int nu, int nv, int nw)
        {
            var pu = Gauss1D(nu); var pv = Gauss1D(nv); var pw = Gauss1D(nw);
            var result = new List<(double, double, double, double)>();
            foreach (var (u, wu) in pu)
                foreach (var (v, wv) in pv)
                    foreach (var (w, ww) in pw)
                    {
                        double L1 = u;
                        double L2 = v * (1 - u);
                        double L3 = w * (1 - u) * (1 - v);
                        double jac = (1 - u) * (1 - u) * (1 - v);
                        double wt = 6.0 * V * wu * wv * ww * jac;
                        result.Add((L1, L2, L3, wt));
                    }
            return result;
        }

        private List<(double x, double w)> Gauss1D(int n)
        {
            if (n == 1) return new List<(double, double)> { (0.5, 1.0) };
            if (n == 2) return new List<(double, double)> { (0.5 - 0.5 / Math.Sqrt(3.0), 0.5), (0.5 + 0.5 / Math.Sqrt(3.0), 0.5) };
            if (n == 3) return new List<(double, double)> { (0.5, 4.0 / 9.0), (0.5 * (1 - Math.Sqrt(0.6)), 5.0 / 18.0), (0.5 * (1 + Math.Sqrt(0.6)), 5.0 / 18.0) };
            if (n == 4)
            {
                double a = Math.Sqrt((3.0 - 2 * Math.Sqrt(1.2)) / 7.0);
                double b = Math.Sqrt((3.0 + 2 * Math.Sqrt(1.2)) / 7.0);
                return new List<(double, double)> {
                    (0.5*(1-b), (18+Math.Sqrt(30.0))/72.0),
                    (0.5*(1-a), (18-Math.Sqrt(30.0))/72.0),
                    (0.5*(1+a), (18-Math.Sqrt(30.0))/72.0),
                    (0.5*(1+b), (18+Math.Sqrt(30.0))/72.0)
                };
            }
            throw new NotImplementedException($"Gauss1D n={n}");
        }

        // =========================================================
        // УТИЛИТЫ БАРИЦЕНТРИЧЕСКИХ ПРОИЗВОДНЫХ
        // =========================================================
        // Устанавливает dN_dL[loc, k] = val * dL_i/dL_k, где k = 0,1,2 → L1,L2,L3
        private static void SetBaryGrad(double[,] dNL, int loc, int i, double val)
        {
            // dL_0/dL_k = -1 для k=0,1,2
            if (i == 0) { dNL[loc, 0] = -val; dNL[loc, 1] = -val; dNL[loc, 2] = -val; }
            else if (i == 1) dNL[loc, 0] = val;
            else if (i == 2) dNL[loc, 1] = val;
            else dNL[loc, 2] = val;
        }
        private static void AddBaryGrad(double[,] dNL, int loc, int i, double val)
        {
            if (i == 0) { dNL[loc, 0] -= val; dNL[loc, 1] -= val; dNL[loc, 2] -= val; }
            else if (i == 1) dNL[loc, 0] += val;
            else if (i == 2) dNL[loc, 1] += val;
            else dNL[loc, 2] += val;
        }

        private static double Det3(double[,] M)
        {
            return M[0, 0] * (M[1, 1] * M[2, 2] - M[1, 2] * M[2, 1])
                 - M[0, 1] * (M[1, 0] * M[2, 2] - M[1, 2] * M[2, 0])
                 + M[0, 2] * (M[1, 0] * M[2, 1] - M[1, 1] * M[2, 0]);
        }

        private static double[,] Inv3(double[,] M, double det)
        {
            double[,] r = new double[3, 3];
            r[0, 0] = (M[1, 1] * M[2, 2] - M[1, 2] * M[2, 1]) / det; r[0, 1] = -(M[0, 1] * M[2, 2] - M[0, 2] * M[2, 1]) / det; r[0, 2] = (M[0, 1] * M[1, 2] - M[0, 2] * M[1, 1]) / det;
            r[1, 0] = -(M[1, 0] * M[2, 2] - M[1, 2] * M[2, 0]) / det; r[1, 1] = (M[0, 0] * M[2, 2] - M[0, 2] * M[2, 0]) / det; r[1, 2] = -(M[0, 0] * M[1, 2] - M[0, 2] * M[1, 0]) / det;
            r[2, 0] = (M[1, 0] * M[2, 1] - M[1, 1] * M[2, 0]) / det; r[2, 1] = -(M[0, 0] * M[2, 1] - M[0, 1] * M[2, 0]) / det; r[2, 2] = (M[0, 0] * M[1, 1] - M[0, 1] * M[1, 0]) / det;
            return r;
        }

        // =========================================================
        // СБОРКА
        // =========================================================
        public void AssembleSystem()
        {
            var sw = Stopwatch.StartNew();
            int nTets = mesh.Tets.Count;
            A = mesh.BuildProfile();
            RHS = new double[mesh.TotalBasisFunctions()];

            var basisLists = new List<(int local, int global, string type, string info)>[nTets];
            var localMats = new double[nTets][,];

            Parallel.For(0, nTets, t =>
            {
                var bl = mesh.GetTetBasis(t);
                basisLists[t] = bl;
                localMats[t] = ComputeLocalStiffness(mesh.Tets[t], bl.Count);
            });

            for (int t = 0; t < nTets; t++)
            {
                var bl = basisLists[t]; var Ke = localMats[t]; int n = bl.Count;
                for (int i = 0; i < n; i++)
                {
                    int gi = bl[i].global - 1;
                    for (int j = 0; j < n; j++)
                    {
                        int gj = bl[j].global - 1;
                        if (gi >= gj) A.AddValue(gi, gj, Ke[i, j]);
                    }
                }
            }
            AssembleMs = sw.ElapsedMilliseconds;
        }

        // =========================================================
        // КРАЕВЫЕ УСЛОВИЯ (как было — по физической вертикали Node.Y)
        // =========================================================
        public void ApplyBoundaryConditions(double c)
        {
            if (A == null) throw new Exception("Матрица не собрана!");
            var bcNodes = new HashSet<int>();
            foreach (var bf in mesh.BoundaryFaces)
            {
                var p0 = mesh.Nodes[bf.A]; var p1 = mesh.Nodes[bf.B]; var p2 = mesh.Nodes[bf.C];
                double e1x = p1.X - p0.X, e1y = p1.Y - p0.Y, e1z = p1.Z - p0.Z;
                double e2x = p2.X - p0.X, e2y = p2.Y - p0.Y, e2z = p2.Z - p0.Z;
                double nx = e1y * e2z - e1z * e2y, ny = e1z * e2x - e1x * e2z, nz = e1x * e2y - e1y * e2x;
                double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (nl < 1e-12) continue;
                if (Math.Abs(ny) / nl > 0.5) { bcNodes.Add(bf.A); bcNodes.Add(bf.B); bcNodes.Add(bf.C); }
            }
            for (int i = 0; i < mesh.TotalBasisFunctions(); i++)
            {
                var obj = mesh.FindBasisObject(i + 1);
                if (obj.type == "none") continue;
                double fy = 0; bool isBc = false;
                if (obj.type == "vertex") { fy = mesh.Nodes[obj.id].Y; isBc = bcNodes.Contains(obj.id); }
                else if (obj.type == "edge") { var e = mesh.Edges[obj.id]; fy = (mesh.Nodes[e.A].Y + mesh.Nodes[e.B].Y) / 2; isBc = bcNodes.Contains(e.A) && bcNodes.Contains(e.B); }
                else if (obj.type == "face") { var f = mesh.Faces[obj.id]; fy = (mesh.Nodes[f.A].Y + mesh.Nodes[f.B].Y + mesh.Nodes[f.C].Y) / 3; isBc = bcNodes.Contains(f.A) && bcNodes.Contains(f.B) && bcNodes.Contains(f.C); }
                if (!isBc) continue;
                double exactValue = c * fy;
                for (int k = 0; k < i; k++) { double v = A.GetValue(i, k); if (Math.Abs(v) > 1e-16) { RHS[k] -= v * exactValue; A.SetValue(i, k, 0); } }
                for (int k = i + 1; k < A.Size; k++) { double v = A.GetValue(k, i); if (Math.Abs(v) > 1e-16) { RHS[k] -= v * exactValue; A.SetValue(k, i, 0); } }
                for (int j = A.RowPointers[i]; j < A.RowPointers[i + 1]; j++) A.Values[j] = 0;
                A.SetValue(i, i, 1.0);
                RHS[i] = exactValue;
            }
        }

        // =========================================================
        // CG-РЕШАТЕЛЬ (без изменений)
        // =========================================================
        public void Solve()
        {
            if (A == null) throw new Exception("Матрица не собрана!");
            var sw = Stopwatch.StartNew();
            Solution = ConjugateGradient(A, RHS, 1e-12, 50000);
            SolveMs = sw.ElapsedMilliseconds;
        }

        private double[] ConjugateGradient(SparseMatrix A, double[] b, double tol, int maxIter)
        {
            int n = A.Size;
            double[] x = new double[n];
            double[] r = (double[])b.Clone();
            double[] p = (double[])r.Clone();
            double[] Ap = new double[n];
            double rr = Dot(r, r); double bnorm = Math.Sqrt(rr);
            CgIterations = 0; CgResidual = bnorm;
            if (bnorm < 1e-15) return x;
            for (int iter = 0; iter < maxIter; iter++)
            {
                MultiplyLowerSymmetric(A, p, Ap);
                double pAp = Dot(p, Ap);
                if (Math.Abs(pAp) < 1e-30) break;
                double alpha = rr / pAp;
                Parallel.For(0, n, i => { x[i] += alpha * p[i]; r[i] -= alpha * Ap[i]; });
                double rrNew = Dot(r, r);
                CgIterations = iter + 1; CgResidual = Math.Sqrt(rrNew);
                if (CgResidual < tol * bnorm) break;
                double beta = rrNew / rr;
                Parallel.For(0, n, i => p[i] = r[i] + beta * p[i]);
                rr = rrNew;
            }
            return x;
        }

        private void MultiplyLowerSymmetric(SparseMatrix A, double[] p, double[] result)
        {
            int n = A.Size;
            int nBlocks = Environment.ProcessorCount;
            var local = new double[nBlocks][];
            for (int b = 0; b < nBlocks; b++) local[b] = new double[n];
            Parallel.For(0, nBlocks, b =>
            {
                int start = b * n / nBlocks, end = (b + 1) * n / nBlocks;
                var loc = local[b];
                for (int i = start; i < end; i++)
                {
                    double sum = 0;
                    for (int j = A.RowPointers[i]; j < A.RowPointers[i + 1]; j++)
                    {
                        int col = A.ColIndices[j]; double v = A.Values[j];
                        sum += v * p[col];
                        if (col != i) loc[col] += v * p[i];
                    }
                    loc[i] += sum;
                }
            });
            Parallel.For(0, n, i => { double s = 0; for (int b = 0; b < nBlocks; b++) s += local[b][i]; result[i] = s; });
        }

        private static double Dot(double[] a, double[] b)
        {
            double sum = 0; object lk = new object();
            Parallel.For(0, a.Length, () => 0.0, (i, st, loc) => loc + a[i] * b[i], loc => { lock (lk) { sum += loc; } });
            return sum;
        }

        // =========================================================
        // ТЕСТ (как было)
        // =========================================================
        public string CheckTest(double c)
        {
            if (Solution == null) return "Решение не найдено!";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"CG: {CgIterations} итераций, residual = {CgResidual:E3}");
            sb.AppendLine($"Сборка: {AssembleMs} мс, Решение: {SolveMs} мс");
            sb.AppendLine();
            sb.AppendLine("  №  |   Тип    |  Y(физ.Z)|  Точное  | Получено |   Ошибка");
            sb.AppendLine("-----+----------+----------+----------+----------+-----------");
            double maxError = 0; int worstIdx = -1; bool hasNaN = false;
            int total = mesh.TotalBasisFunctions(), shown = 0, maxShow = 50;
            for (int i = 0; i < total; i++)
            {
                var obj = mesh.FindBasisObject(i + 1);
                if (obj.type == "none") continue;
                double ez = 0;
                if (obj.type == "vertex") ez = mesh.Nodes[obj.id].Y;
                else if (obj.type == "edge") { var e = mesh.Edges[obj.id]; ez = (mesh.Nodes[e.A].Y + mesh.Nodes[e.B].Y) / 2; }
                else if (obj.type == "face") { var f = mesh.Faces[obj.id]; ez = (mesh.Nodes[f.A].Y + mesh.Nodes[f.B].Y + mesh.Nodes[f.C].Y) / 3; }
                else if (obj.type == "volume") { var t = mesh.Tets[obj.id]; ez = (mesh.Nodes[t.V[0]].Y + mesh.Nodes[t.V[1]].Y + mesh.Nodes[t.V[2]].Y + mesh.Nodes[t.V[3]].Y) / 4; }
                else continue;
                double exact = c * ez, got = Solution[i], err = Math.Abs(got - exact);
                bool bad = double.IsNaN(got) || double.IsInfinity(got);
                if (bad) { hasNaN = true; if (worstIdx < 0) worstIdx = i; }
                else if (err > maxError) { maxError = err; worstIdx = i; }
                if (shown < maxShow || i == worstIdx)
                {
                    string gs = bad ? "  NaN/Inf" : got.ToString("F4");
                    string es = bad ? "    ---   " : err.ToString("E3");
                    sb.AppendLine($"{i + 1,4} | {obj.type,-8} | {ez,8:F4} | {exact,8:F4} | {gs,8} | {es}");
                    shown++;
                }
            }
            if (total > maxShow) sb.AppendLine($"... (показано {maxShow} из {total})");
            sb.AppendLine();
            if (hasNaN) { sb.AppendLine("⚠️ NaN/Inf!"); return sb.ToString(); }
            sb.AppendLine($"Худший узел: №{worstIdx + 1}");
            sb.AppendLine($"Максимальная ошибка: {maxError:E6}");
            sb.AppendLine(maxError < 1e-6 ? "✅ ТЕСТ ПРОЙДЕН!" : "❌ ОШИБКА!");
            return sb.ToString();
        }
    }
}