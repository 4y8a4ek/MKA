using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class MainForm : Form
{
    private List<Vec3> nodes;
    private List<Prism> prisms;


    private double angleX = 0.6;
    private double angleY = 0.8;
    private double scale = 1.0;
    private Point lastMouse;
    private bool dragging = false;
    private static readonly int[,] PrismEdges = new int[,]
    {
        {0,1},{1,2},{2,0},
        {3,4},{4,5},{5,3},
        {0,3},{1,4},{2,5}
    };

    public MainForm(string nodesFile, string prismsFile)
    {
        Text = "Просмотр конечно-элементной сетки (вариант 28)";
        Width = 1000;
        Height = 700;
        DoubleBuffered = true;
        BackColor = Color.White;

        nodes = MeshLoader.LoadNodes(nodesFile);
        prisms = MeshLoader.LoadPrisms(prismsFile);

        // Автомасштаб
        AutoFit();

        MouseDown += (s, e) => { dragging = true; lastMouse = e.Location; };
        MouseUp += (s, e) => { dragging = false; };
        MouseMove += OnMouseMove;
        MouseWheel += OnMouseWheel;
        KeyDown += OnKeyDown;
    }

    private void AutoFit()
    {
        if (nodes.Count == 0) return;
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        double minZ = double.MaxValue, maxZ = double.MinValue;
        foreach (var n in nodes)
        {
            minX = Math.Min(minX, n.X); maxX = Math.Max(maxX, n.X);
            minY = Math.Min(minY, n.Y); maxY = Math.Max(maxY, n.Y);
            minZ = Math.Min(minZ, n.Z); maxZ = Math.Max(maxZ, n.Z);
        }
        double dx = maxX - minX, dy = maxY - minY, dz = maxZ - minZ;
        double maxDim = Math.Max(dx, Math.Max(dy, dz));
        if (maxDim < 1e-9) maxDim = 1.0;

        scale = 400.0 / maxDim;

        double cx = (minX + maxX) / 2;
        double cy = (minY + maxY) / 2;
        double cz = (minZ + maxZ) / 2;
        for (int i = 0; i < nodes.Count; i++)
            nodes[i] = new Vec3(nodes[i].X - cx, nodes[i].Y - cy, nodes[i].Z - cz);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!dragging) return;
        double dx = e.X - lastMouse.X;
        double dy = e.Y - lastMouse.Y;
        angleY += dx * 0.01;
        angleX += dy * 0.01;
        lastMouse = e.Location;
        Invalidate();
    }

    private void OnMouseWheel(object sender, MouseEventArgs e)
    {
        double factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        scale *= factor;
        Invalidate();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.R) { angleX = 0.6; angleY = 0.8; AutoFit(); Invalidate(); }
        if (e.KeyCode == Keys.Escape) Close();
    }

    private PointF Project(Vec3 p, int cx, int cy)
    {
        double cosY = Math.Cos(angleY), sinY = Math.Sin(angleY);
        double x1 = p.X * cosY + p.Z * sinY;
        double z1 = -p.X * sinY + p.Z * cosY;

        double cosX = Math.Cos(angleX), sinX = Math.Sin(angleX);
        double y2 = p.Y * cosX - z1 * sinX;
        double z2 = p.Y * sinX + z1 * cosX;

        float sx = (float)(cx + x1 * scale);
        float sy = (float)(cy - y2 * scale);
        return new PointF(sx, sy);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int cx = ClientSize.Width / 2;
        int cy = ClientSize.Height / 2;

        var proj = new PointF[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
            proj[i] = Project(nodes[i], cx, cy);

        using (var pen = new Pen(Color.FromArgb(180, 30, 60, 120), 0.8f))
        {
            foreach (var prism in prisms)
            {
                for (int k = 0; k < PrismEdges.GetLength(0); k++)
                {
                    int a = prism.V[PrismEdges[k, 0]];
                    int b = prism.V[PrismEdges[k, 1]];
                    g.DrawLine(pen, proj[a], proj[b]);
                }
            }
        }
        using (var font = new Font("Segoe UI", 10))
        using (var brush = new SolidBrush(Color.Black))
        {
            g.DrawString(
                $"Узлов: {nodes.Count}   Призм: {prisms.Count}\n" +
                "ЛКМ — вращение, колесо — масштаб, R — сброс",
                font, brush, 10, 10);
        }
    }
}