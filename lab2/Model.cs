using System;

namespace Lab2
{
    // Узел сетки
    public class Node
    {
        public double X, Y, Z;
        public Node(double x, double y, double z) { X = x; Y = y; Z = z; }
    }

    // Гексаэдр (используется только на этапе построения)
    public class Hex
    {
        public int[] V = new int[8];
        public Hex(int[] v) { Array.Copy(v, V, 8); }
    }

    // Тетраэдр — конечный элемент
    public class Tet
    {
        public int[] V = new int[4];
        public Tet(int a, int b, int c, int d)
        { V[0] = a; V[1] = b; V[2] = c; V[3] = d; }
    }

    // Треугольная грань для отрисовки
    public class BFace
    {
        public int A, B, C;
        public BFace(int a, int b, int c) { A = a; B = b; C = c; }
    }

    // Уникальное ребро (A < B)
    public class Edge
    {
        public int A, B;
        public Edge(int a, int b)
        { if (a < b) { A = a; B = b; } else { A = b; B = a; } }
    }

    // Уникальная треугольная грань (A < B < C)
    public class Face
    {
        public int A, B, C;
        public Face(int a, int b, int c)
        {
            int t;
            if (a > b) { t = a; a = b; b = t; }
            if (b > c) { t = b; b = c; c = t; }
            if (a > b) { t = a; a = b; b = t; }
            A = a; B = b; C = c;
        }
    }

    // Схемы базисных функций
    public enum BasisScheme
    {
        P1_Lagrange,       //  4 функции на КЭ
        P2_Lagrange,       // 10 функций на КЭ
        P3_Lagrange,       // 20 функций на КЭ
        P2_Hierarchical,   // 10 функций на КЭ
        P3_Hierarchical    // 21 функция на КЭ
    }
}