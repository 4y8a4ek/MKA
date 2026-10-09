using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Lab2
{
    public class MeshForm : Form
    {
        private MeshGenerator mesh;

        private double angleX = 0.55, angleY = 0.75, baseScale = 1.0, zoom = 1.0;
        private double centerX, centerY, centerZ, bboxDiag = 1.0;
        private Point lastMouse; private bool dragging = false;
        private Point mouseDownPos; private bool didDrag;
        private int mode = 0;
        private readonly SolidBrush[] palette = new SolidBrush[16];

        private const int SidebarWidth = 400;

        private Panel sidebar;
        private ComboBox cbScheme;
        private RadioButton rbNode, rbEdge, rbFace, rbTet;
        private TextBox infoBox;
        private TextBox tbNodeId, tbEdgeId, tbFaceId, tbTetId, tbBasisNum;
        private TextBox tbEdgeA, tbEdgeB, tbFaceA, tbFaceB, tbFaceC;
        private TextBox tbTetByEdge, tbTetByFace, tbTetByNode;

        private int pickMode = 0;
        private int selectedNode = -1, selectedEdge = -1, selectedFace = -1, selectedTet = -1;
        private PointF[] projXY; private double[] projDepth;

        public MeshForm(MeshGenerator mesh)
        {
            this.mesh = mesh;
            for (int i = 0; i < 16; i++)
            {
                double t = i / 15.0;
                int R = (int)(55 + 175 * t), G = (int)(85 + 145 * t), B = (int)(125 + 120 * t);
                palette[i] = new SolidBrush(Color.FromArgb(R, G, B));
            }

            Text = $"МКА — лаб. №2 — h = {mesh.h:0.###}";
            Width = 1450; Height = 820;
            DoubleBuffered = true; BackColor = Color.White;
            StartPosition = FormStartPosition.CenterScreen; KeyPreview = true;

            BuildSidebar();
            ComputeBounds();

            MouseDown += (s, e) =>
            {
                if (e.X < SidebarWidth) return;
                dragging = true; lastMouse = e.Location;
                mouseDownPos = e.Location; didDrag = false;
            };
            MouseMove += (s, e) =>
            {
                if (!dragging) return;
                if (Math.Abs(e.X - mouseDownPos.X) + Math.Abs(e.Y - mouseDownPos.Y) > 4) didDrag = true;
                angleY += (e.X - lastMouse.X) * 0.01;
                angleX += (e.Y - lastMouse.Y) * 0.01;
                lastMouse = e.Location; Invalidate();
            };
            MouseUp += (s, e) =>
            {
                bool was = dragging; dragging = false;
                if (was && !didDrag && e.X >= SidebarWidth && e.Y >= 0) HandlePick(e.Location);
            };
            MouseWheel += (s, e) =>
            {
                if (e.X < SidebarWidth) return;
                zoom *= e.Delta > 0 ? 1.1 : 1.0 / 1.1; Invalidate();
            };
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.D1) { mode = 1; Invalidate(); }
                if (e.KeyCode == Keys.D2) { mode = 0; Invalidate(); }
                if (e.KeyCode == Keys.D3) { mode = 2; Invalidate(); }
                if (e.KeyCode == Keys.R) { angleX = 0.55; angleY = 0.75; zoom = 1.0; Invalidate(); }
                if (e.KeyCode == Keys.Escape) Close();
                if (e.KeyCode == Keys.N) rbNode.Checked = true;
                if (e.KeyCode == Keys.E) rbEdge.Checked = true;
                if (e.KeyCode == Keys.F) rbFace.Checked = true;
                if (e.KeyCode == Keys.T) rbTet.Checked = true;
                if (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)
                { mesh.h = Math.Max(mesh.h / 2.0, 0.1); Rebuild(); }
                if (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)
                { mesh.h = Math.Min(mesh.h * 2.0, 8.0); Rebuild(); }
            };
            Resize += (s, e) => { UpdateScale(); Invalidate(); };
        }

        // =========================================================
        //  SIDEBAR
        // =========================================================
        private void BuildSidebar()
        {
            sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = SidebarWidth,
                BackColor = Color.FromArgb(245, 245, 250),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(sidebar);

            int L = 8;
            int GAP = 5;
            int gbW = SidebarWidth - 2 * L - 4;

            int colLabel = 95;
            int colInput = 55;
            int colBtn = 75;

            int y = 10;

            // ===== Схема =====
            sidebar.Controls.Add(new Label
            {
                Text = "Схема базисных функций:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Left = L,
                Top = y,
                Width = gbW,
                Height = 20
            });
            y += 22;

            cbScheme = new ComboBox
            {
                Left = L,
                Top = y,
                Width = gbW,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cbScheme.Items.AddRange(new object[]
            {
                "P1 Lagrange (4 функции на КЭ)",
                "P2 Lagrange (10 функций на КЭ)",
                "P3 Lagrange (20 функций на КЭ)",
                "P2 Hierarchical (10 функций на КЭ)",
                "P3 Hierarchical (21 функция на КЭ)"
            });
            cbScheme.SelectedIndex = 0;
            cbScheme.SelectedIndexChanged += (s, e) =>
            {
                mesh.Scheme = (BasisScheme)cbScheme.SelectedIndex;
                ClearSelection();
                Invalidate();
            };
            sidebar.Controls.Add(cbScheme);
            y += 32;

            // ===== Режим выбора =====
            sidebar.Controls.Add(new Label
            {
                Text = "Режим выбора мышью:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Left = L,
                Top = y,
                Width = gbW,
                Height = 20
            });
            y += 24;

            rbNode = new RadioButton { Text = "Узел (N)", Left = L, Top = y, Width = 90, Checked = true };
            rbEdge = new RadioButton { Text = "Ребро (E)", Left = L + 95, Top = y, Width = 90 };
            rbFace = new RadioButton { Text = "Грань (F)", Left = L + 190, Top = y, Width = 90 };
            rbTet = new RadioButton { Text = "КЭ (T)", Left = L + 285, Top = y, Width = 80 };
            sidebar.Controls.Add(rbNode);
            sidebar.Controls.Add(rbEdge);
            sidebar.Controls.Add(rbFace);
            sidebar.Controls.Add(rbTet);
            y += 30;

            rbNode.CheckedChanged += (s, e) => { if (rbNode.Checked) { pickMode = 0; ClearSelection(); } };
            rbEdge.CheckedChanged += (s, e) => { if (rbEdge.Checked) { pickMode = 1; ClearSelection(); } };
            rbFace.CheckedChanged += (s, e) => { if (rbFace.Checked) { pickMode = 2; ClearSelection(); } };
            rbTet.CheckedChanged += (s, e) => { if (rbTet.Checked) { pickMode = 3; ClearSelection(); } };

            // ===== Поиск по номеру =====
            var gbId = new GroupBox
            {
                Text = "Поиск по номеру",
                Left = L,
                Top = y,
                Width = gbW,
                Height = 178,
                Font = new Font("Segoe UI", 9f)
            };
            sidebar.Controls.Add(gbId);

            int rowY = 24, rowH = 28;
            int xLabel = 8;
            int xInput = xLabel + colLabel;
            int xBtn1 = xInput + colInput + GAP;
            int xBtn2 = xBtn1 + colBtn + GAP;

            gbId.Controls.Add(new Label { Text = "Узел №:", Left = xLabel, Top = rowY + 3, Width = colLabel, Height = 20 });
            tbNodeId = new TextBox { Left = xInput, Top = rowY, Width = colInput };
            gbId.Controls.Add(tbNodeId);
            gbId.Controls.Add(MakeButton("Найти", xBtn1, rowY, colBtn, (s, e) => LookupNode()));
            gbId.Controls.Add(MakeButton("Сброс", xBtn2, rowY, colBtn, (s, e) => ClearSelection()));
            rowY += rowH;

            gbId.Controls.Add(new Label { Text = "Ребро №:", Left = xLabel, Top = rowY + 3, Width = colLabel, Height = 20 });
            tbEdgeId = new TextBox { Left = xInput, Top = rowY, Width = colInput };
            gbId.Controls.Add(tbEdgeId);
            gbId.Controls.Add(MakeButton("Найти", xBtn1, rowY, colBtn, (s, e) => LookupEdge()));
            rowY += rowH;

            gbId.Controls.Add(new Label { Text = "Грань №:", Left = xLabel, Top = rowY + 3, Width = colLabel, Height = 20 });
            tbFaceId = new TextBox { Left = xInput, Top = rowY, Width = colInput };
            gbId.Controls.Add(tbFaceId);
            gbId.Controls.Add(MakeButton("Найти", xBtn1, rowY, colBtn, (s, e) => LookupFace()));
            rowY += rowH;

            gbId.Controls.Add(new Label { Text = "КЭ №:", Left = xLabel, Top = rowY + 3, Width = colLabel, Height = 20 });
            tbTetId = new TextBox { Left = xInput, Top = rowY, Width = colInput };
            gbId.Controls.Add(tbTetId);
            gbId.Controls.Add(MakeButton("Найти", xBtn1, rowY, colBtn, (s, e) => LookupTet()));
            rowY += rowH;

            gbId.Controls.Add(new Label { Text = "Баз.функция №:", Left = xLabel, Top = rowY + 3, Width = 115, Height = 20 });
            tbBasisNum = new TextBox { Left = 125, Top = rowY, Width = colInput };
            gbId.Controls.Add(tbBasisNum);
            gbId.Controls.Add(MakeButton("Найти", 125 + colInput + GAP, rowY, 150, (s, e) => LookupBasis()));

            y += gbId.Height + 8;

            // ===== Поиск по узлам =====
            var gb2 = new GroupBox
            {
                Text = "Поиск по узлам",
                Left = L,
                Top = y,
                Width = gbW,
                Height = 96,
                Font = new Font("Segoe UI", 9f)
            };
            sidebar.Controls.Add(gb2);

            gb2.Controls.Add(new Label { Text = "Ребро:", Left = 8, Top = 27, Width = 55, Height = 20 });
            tbEdgeA = new TextBox { Left = 70, Top = 24, Width = 45 };
            tbEdgeB = new TextBox { Left = 125, Top = 24, Width = 45 };
            gb2.Controls.Add(tbEdgeA);
            gb2.Controls.Add(tbEdgeB);
            gb2.Controls.Add(MakeButton("Найти ребро", 180, 24, 190, (s, e) => LookupEdgeByNodes()));

            gb2.Controls.Add(new Label { Text = "Грань:", Left = 8, Top = 63, Width = 55, Height = 20 });
            tbFaceA = new TextBox { Left = 70, Top = 60, Width = 40 };
            tbFaceB = new TextBox { Left = 115, Top = 60, Width = 40 };
            tbFaceC = new TextBox { Left = 160, Top = 60, Width = 40 };
            gb2.Controls.Add(tbFaceA);
            gb2.Controls.Add(tbFaceB);
            gb2.Controls.Add(tbFaceC);
            gb2.Controls.Add(MakeButton("Найти грань", 210, 60, 160, (s, e) => LookupFaceByNodes()));

            y += gb2.Height + 8;

            // ===== КЭ по объекту =====
            var gb3 = new GroupBox
            {
                Text = "КЭ по объекту",
                Left = L,
                Top = y,
                Width = gbW,
                Height = 122,
                Font = new Font("Segoe UI", 9f)
            };
            sidebar.Controls.Add(gb3);

            gb3.Controls.Add(new Label { Text = "По ребру:", Left = 8, Top = 27, Width = 85, Height = 20 });
            tbTetByEdge = new TextBox { Left = 100, Top = 24, Width = 60 };
            gb3.Controls.Add(tbTetByEdge);
            gb3.Controls.Add(MakeButton("Найти", 170, 24, 100, (s, e) => LookupTetsByEdge()));

            gb3.Controls.Add(new Label { Text = "По грани:", Left = 8, Top = 57, Width = 85, Height = 20 });
            tbTetByFace = new TextBox { Left = 100, Top = 54, Width = 60 };
            gb3.Controls.Add(tbTetByFace);
            gb3.Controls.Add(MakeButton("Найти", 170, 54, 100, (s, e) => LookupTetsByFace()));

            gb3.Controls.Add(new Label { Text = "По узлу:", Left = 8, Top = 87, Width = 85, Height = 20 });
            tbTetByNode = new TextBox { Left = 100, Top = 84, Width = 60 };
            gb3.Controls.Add(tbTetByNode);
            gb3.Controls.Add(MakeButton("Найти", 170, 84, 100, (s, e) => LookupTetsByNode()));

            y += gb3.Height + 8;

            // ===== Информация =====
            sidebar.Controls.Add(new Label
            {
                Text = "Информация об объекте:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Left = L,
                Top = y,
                Width = gbW,
                Height = 20
            });
            y += 22;

            infoBox = new TextBox
            {
                Left = L,
                Top = y,
                Width = gbW,
                Height = Math.Max(200, ClientSize.Height - y - 20),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8.5f),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            sidebar.Controls.Add(infoBox);
        }

        private Button MakeButton(string text, int x, int y, int w, EventHandler onClick)
        {
            var b = new Button { Text = text, Left = x, Top = y, Width = w, Height = 24 };
            b.Click += onClick;
            return b;
        }

        // =========================================================
        //  LOOKUP
        // =========================================================
        private void LookupNode()
        {
            if (!int.TryParse(tbNodeId.Text, out int id)) return; id -= 1;
            if (id < 0 || id >= mesh.Nodes.Count) { ShowMsg("Узел не найден"); return; }
            selectedNode = id; selectedEdge = selectedFace = selectedTet = -1;
            ShowNodeInfo(id); Invalidate();
        }
        private void LookupEdge()
        {
            if (!int.TryParse(tbEdgeId.Text, out int id)) return; id -= 1;
            if (id < 0 || id >= mesh.Edges.Count) { ShowMsg("Ребро не найдено"); return; }
            selectedEdge = id; selectedNode = selectedFace = selectedTet = -1;
            ShowEdgeInfo(id); Invalidate();
        }
        private void LookupFace()
        {
            if (!int.TryParse(tbFaceId.Text, out int id)) return; id -= 1;
            if (id < 0 || id >= mesh.Faces.Count) { ShowMsg("Грань не найдена"); return; }
            selectedFace = id; selectedNode = selectedEdge = selectedTet = -1;
            ShowFaceInfo(id); Invalidate();
        }
        private void LookupTet()
        {
            if (!int.TryParse(tbTetId.Text, out int id)) return; id -= 1;
            if (id < 0 || id >= mesh.Tets.Count) { ShowMsg("КЭ не найден"); return; }
            selectedTet = id; selectedNode = selectedEdge = selectedFace = -1;
            ShowTetInfo(id); Invalidate();
        }
        private void LookupBasis()
        {
            if (!int.TryParse(tbBasisNum.Text, out int g)) return;
            var obj = mesh.FindBasisObject(g);
            if (obj.type == "none") { ShowMsg("Базисная функция не найдена"); return; }
            ClearSelection();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"БАЗИСНАЯ ФУНКЦИЯ № {g}");
            sb.AppendLine($"Схема: {mesh.Scheme}");
            sb.AppendLine($"Тип носителя: {obj.type}");
            sb.AppendLine($"Носитель: id {obj.id + 1}, подындекс {obj.sub + 1}");
            sb.AppendLine();
            switch (obj.type)
            {
                case "vertex":
                    sb.AppendLine($"Узел {obj.id + 1}: ({mesh.Nodes[obj.id].X:0.###}, {mesh.Nodes[obj.id].Y:0.###}, {mesh.Nodes[obj.id].Z:0.###})");
                    sb.AppendLine($"Инцидентные КЭ: {Join(mesh.NodeToTets[obj.id])}");
                    selectedNode = obj.id; break;
                case "edge":
                    var ed = mesh.Edges[obj.id];
                    sb.AppendLine($"Ребро {obj.id + 1}: узлы ({ed.A + 1}, {ed.B + 1})");
                    sb.AppendLine($"Инцидентные КЭ: {Join(mesh.EdgeToTets[obj.id])}");
                    sb.AppendLine($"Инцидентные грани: {Join(mesh.EdgeToFaces[obj.id])}");
                    selectedEdge = obj.id; break;
                case "face":
                    var fc = mesh.Faces[obj.id];
                    sb.AppendLine($"Грань {obj.id + 1}: узлы ({fc.A + 1}, {fc.B + 1}, {fc.C + 1})");
                    sb.AppendLine($"Инцидентные КЭ: {Join(mesh.FaceToTets[obj.id])}");
                    selectedFace = obj.id; break;
                case "volume":
                    sb.AppendLine($"Объёмная функция КЭ {obj.id + 1} (bubble)");
                    selectedTet = obj.id; break;
            }
            ShowMsg(sb.ToString()); Invalidate();
        }
        private void LookupEdgeByNodes()
        {
            if (!int.TryParse(tbEdgeA.Text, out int a) || !int.TryParse(tbEdgeB.Text, out int b)) return;
            a -= 1; b -= 1;
            int e = mesh.FindEdge(a, b);
            if (e < 0) { ShowMsg($"Ребро ({a + 1},{b + 1}) не найдено"); return; }
            selectedEdge = e; selectedNode = selectedFace = selectedTet = -1;
            ShowEdgeInfo(e); Invalidate();
        }
        private void LookupFaceByNodes()
        {
            if (!int.TryParse(tbFaceA.Text, out int a) ||
                !int.TryParse(tbFaceB.Text, out int b) ||
                !int.TryParse(tbFaceC.Text, out int c)) return;
            a -= 1; b -= 1; c -= 1;
            int f = mesh.FindFace(a, b, c);
            if (f < 0) { ShowMsg($"Грань ({a + 1},{b + 1},{c + 1}) не найдена"); return; }
            selectedFace = f; selectedNode = selectedEdge = selectedTet = -1;
            ShowFaceInfo(f); Invalidate();
        }
        private void LookupTetsByEdge()
        {
            if (!int.TryParse(tbTetByEdge.Text, out int e)) return; e -= 1;
            if (e < 0 || e >= mesh.Edges.Count) { ShowMsg("Ребро не найдено"); return; }
            ShowMsg($"Ребро {e + 1}: инцидентные КЭ:\n" + Join(mesh.EdgeToTets[e]));
        }
        private void LookupTetsByFace()
        {
            if (!int.TryParse(tbTetByFace.Text, out int f)) return; f -= 1;
            if (f < 0 || f >= mesh.Faces.Count) { ShowMsg("Грань не найдена"); return; }
            ShowMsg($"Грань {f + 1}: инцидентные КЭ:\n" + Join(mesh.FaceToTets[f]));
        }
        private void LookupTetsByNode()
        {
            if (!int.TryParse(tbTetByNode.Text, out int n)) return; n -= 1;
            if (n < 0 || n >= mesh.Nodes.Count) { ShowMsg("Узел не найден"); return; }
            ShowMsg($"Узел {n + 1}: инцидентные КЭ:\n" + Join(mesh.NodeToTets[n]));
        }

        private static string Join(List<int> list)
        {
            if (list.Count == 0) return "(пусто)";
            var parts = new string[list.Count];
            for (int i = 0; i < list.Count; i++) parts[i] = (list[i] + 1).ToString();
            return string.Join(", ", parts);
        }
        private static string Join(int[] arr)
        {
            if (arr == null || arr.Length == 0) return "(пусто)";
            var parts = new string[arr.Length];
            for (int i = 0; i < arr.Length; i++) parts[i] = (arr[i] + 1).ToString();
            return string.Join(", ", parts);
        }
        private void ShowMsg(string s) { infoBox.Text = s; }

        // =========================================================
        //  ИНФОРМАЦИЯ
        // =========================================================
        private void ShowNodeInfo(int n)
        {
            var p = mesh.Nodes[n];
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"УЗЕЛ № {n + 1}");
            sb.AppendLine($"Координаты: ({p.X:0.####}, {p.Y:0.####}, {p.Z:0.####})");
            sb.AppendLine($"Схема: {mesh.Scheme}");
            sb.AppendLine($"Вершинная базисная функция: № {mesh.VertexBasisNumber(n)}");
            sb.AppendLine();
            sb.AppendLine($"Инцидентных КЭ: {mesh.NodeToTets[n].Count}");
            sb.AppendLine($"  КЭ: {Join(mesh.NodeToTets[n])}");
            sb.AppendLine($"Инцидентных рёбер: {mesh.NodeToEdges[n].Count}");
            sb.AppendLine($"  Рёбра: {Join(mesh.NodeToEdges[n])}");
            sb.AppendLine($"Инцидентных граней: {mesh.NodeToFaces[n].Count}");
            if (mesh.NodeToFaces[n].Count <= 30)
                sb.AppendLine($"  Грани: {Join(mesh.NodeToFaces[n])}");
            else
                sb.AppendLine($"  Грани: {mesh.NodeToFaces[n].Count} шт. (слишком много для вывода)");
            ShowMsg(sb.ToString());
        }

        private void ShowEdgeInfo(int e)
        {
            var ed = mesh.Edges[e];
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"РЕБРО № {e + 1}");
            sb.AppendLine($"Узлы: {ed.A + 1}, {ed.B + 1}");
            var pA = mesh.Nodes[ed.A]; var pB = mesh.Nodes[ed.B];
            double len = Math.Sqrt(
                (pA.X - pB.X) * (pA.X - pB.X) +
                (pA.Y - pB.Y) * (pA.Y - pB.Y) +
                (pA.Z - pB.Z) * (pA.Z - pB.Z));
            sb.AppendLine($"Длина: {len:0.#####}");
            sb.AppendLine($"Схема: {mesh.Scheme}");
            int ep = mesh.EdgeFunctionsPerEdge();
            if (ep == 0) sb.AppendLine("Рёберных базисных функций в этой схеме нет");
            else
            {
                int gbase = mesh.EdgeBasisBase(e);
                sb.AppendLine($"Рёберные базисные функции ({ep} шт.):");
                for (int s = 0; s < ep; s++) sb.AppendLine($"  № {gbase + s} (подындекс {s + 1})");
            }
            sb.AppendLine();
            sb.AppendLine($"Инцидентных КЭ: {mesh.EdgeToTets[e].Count}");
            sb.AppendLine($"  КЭ: {Join(mesh.EdgeToTets[e])}");
            sb.AppendLine($"Инцидентных граней: {mesh.EdgeToFaces[e].Count}");
            sb.AppendLine($"  Грани: {Join(mesh.EdgeToFaces[e])}");
            ShowMsg(sb.ToString());
        }

        private void ShowFaceInfo(int f)
        {
            var fc = mesh.Faces[f];
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"ГРАНЬ № {f + 1}");
            sb.AppendLine($"Узлы: {fc.A + 1}, {fc.B + 1}, {fc.C + 1}");
            var pA = mesh.Nodes[fc.A]; var pB = mesh.Nodes[fc.B]; var pC = mesh.Nodes[fc.C];
            double e1x = pB.X - pA.X, e1y = pB.Y - pA.Y, e1z = pB.Z - pA.Z;
            double e2x = pC.X - pA.X, e2y = pC.Y - pA.Y, e2z = pC.Z - pA.Z;
            double nx = e1y * e2z - e1z * e2y, ny = e1z * e2x - e1x * e2z, nz = e1x * e2y - e1y * e2x;
            sb.AppendLine($"Площадь: {0.5 * Math.Sqrt(nx * nx + ny * ny + nz * nz):0.#####}");
            sb.AppendLine($"Схема: {mesh.Scheme}");
            if (mesh.HasFaceFunctions())
                sb.AppendLine($"Граневая базисная функция: № {mesh.FaceBasisNumber(f)}");
            else
                sb.AppendLine("Граневых базисных функций в этой схеме нет");
            sb.AppendLine();
            sb.AppendLine($"Инцидентных КЭ: {mesh.FaceToTets[f].Count}");
            sb.AppendLine($"  КЭ: {Join(mesh.FaceToTets[f])}");
            sb.AppendLine($"  Тип: {(mesh.FaceToTets[f].Count == 1 ? "граничная" : "внутренняя")}");
            ShowMsg(sb.ToString());
        }

        private void ShowTetInfo(int t)
        {
            var tet = mesh.Tets[t];
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"КОНЕЧНЫЙ ЭЛЕМЕНТ № {t + 1}");
            sb.AppendLine($"Схема: {mesh.Scheme}");
            sb.AppendLine($"Узлы: {tet.V[0] + 1}, {tet.V[1] + 1}, {tet.V[2] + 1}, {tet.V[3] + 1}");
            sb.AppendLine($"Всего базисных функций на КЭ: {mesh.GetTetBasis(t).Count}");
            sb.AppendLine();
            sb.AppendLine("лок. → глоб. | тип     | носитель");
            sb.AppendLine("-----------------------------------");
            foreach (var b in mesh.GetTetBasis(t))
                sb.AppendLine($" {b.local,3} → {b.global,5} | {b.type,-7} | {b.info}");
            sb.AppendLine();
            sb.AppendLine($"Рёбра КЭ (6): {Join(mesh.TetToEdges[t])}");
            sb.AppendLine($"Грани КЭ (4): {Join(mesh.TetToFaces[t])}");
            ShowMsg(sb.ToString());
        }

        // =========================================================
        //  ВЫБОР МЫШЬЮ
        // =========================================================
        private void HandlePick(Point screen)
        {
            if (projXY == null || projXY.Length == 0) return;
            switch (pickMode)
            {
                case 0: PickNode(screen); break;
                case 1: PickEdge(screen); break;
                case 2: PickFace(screen); break;
                case 3: PickTet(screen); break;
            }
            Invalidate();
        }

        private void PickNode(Point screen)
        {
            double best = 15 * 15; int bestI = -1;
            for (int i = 0; i < projXY.Length; i++)
            {
                double dx = projXY[i].X - screen.X, dy = projXY[i].Y - screen.Y;
                double d2 = dx * dx + dy * dy;
                if (d2 < best) { best = d2; bestI = i; }
            }
            if (bestI < 0) { ClearSelection(); ShowMsg("Узел не выбран"); return; }
            selectedNode = bestI; selectedEdge = selectedFace = selectedTet = -1;
            ShowNodeInfo(bestI);
        }

        private void PickEdge(Point screen)
        {
            double best = 8.0; int bestI = -1;
            for (int e = 0; e < mesh.Edges.Count; e++)
            {
                var ed = mesh.Edges[e];
                double d = DistPointSeg(screen, projXY[ed.A], projXY[ed.B]);
                if (d < best) { best = d; bestI = e; }
            }
            if (bestI < 0) { ClearSelection(); ShowMsg("Ребро не выбрано"); return; }
            selectedEdge = bestI; selectedNode = selectedFace = selectedTet = -1;
            ShowEdgeInfo(bestI);
        }

        private void PickFace(Point screen)
        {
            double bestDepth = double.MaxValue; int bestI = -1;
            for (int f = 0; f < mesh.Faces.Count; f++)
            {
                var fc = mesh.Faces[f];
                if (!PointInTriangle(screen, projXY[fc.A], projXY[fc.B], projXY[fc.C])) continue;
                double depth = (projDepth[fc.A] + projDepth[fc.B] + projDepth[fc.C]) / 3.0;
                if (depth < bestDepth) { bestDepth = depth; bestI = f; }
            }
            if (bestI < 0) { ClearSelection(); ShowMsg("Грань не выбрана"); return; }
            selectedFace = bestI; selectedNode = selectedEdge = selectedTet = -1;
            ShowFaceInfo(bestI);
        }

        private void PickTet(Point screen)
        {
            double bestDepth = double.MaxValue; int bestI = -1;
            for (int t = 0; t < mesh.Tets.Count; t++)
            {
                var tet = mesh.Tets[t];
                var p0 = projXY[tet.V[0]]; var p1 = projXY[tet.V[1]];
                var p2 = projXY[tet.V[2]]; var p3 = projXY[tet.V[3]];
                bool inside = PointInTriangle(screen, p0, p1, p2) || PointInTriangle(screen, p0, p1, p3) ||
                              PointInTriangle(screen, p0, p2, p3) || PointInTriangle(screen, p1, p2, p3);
                if (!inside) continue;
                double depth = (projDepth[tet.V[0]] + projDepth[tet.V[1]] +
                                projDepth[tet.V[2]] + projDepth[tet.V[3]]) / 4.0;
                if (depth < bestDepth) { bestDepth = depth; bestI = t; }
            }
            if (bestI < 0) { ClearSelection(); ShowMsg("КЭ не выбран"); return; }
            selectedTet = bestI; selectedNode = selectedEdge = selectedFace = -1;
            ShowTetInfo(bestI);
        }

        private static double DistPointSeg(Point p, PointF a, PointF b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
            if (l2 < 1e-12) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
            double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2;
            if (t < 0) t = 0; if (t > 1) t = 1;
            double px = a.X + t * dx, py = a.Y + t * dy;
            return Math.Sqrt((p.X - px) * (p.X - px) + (p.Y - py) * (p.Y - py));
        }
        private static bool PointInTriangle(Point p, PointF a, PointF b, PointF c)
        {
            double d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(hasNeg && hasPos);
        }
        private static double Sign(Point p, PointF a, PointF b) =>
            (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);

        private void ClearSelection()
        {
            selectedNode = selectedEdge = selectedFace = selectedTet = -1;
            if (infoBox != null) infoBox.Text = "";
        }

        // =========================================================
        //  ВИД
        // =========================================================
        private void Rebuild()
        {
            mesh.BuildFromDomain();
            ComputeBounds();
            Text = $"МКА — лаб. №2 — h = {mesh.h:0.###}";
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
            int vw = ClientSize.Width - SidebarWidth;
            int vh = ClientSize.Height;
            if (vw <= 0 || vh <= 0) return;
            baseScale = Math.Min(vw, vh) * 0.42 / bboxDiag;
        }

        private void RotateAll(double cosX, double sinX, double cosY, double sinY,
                               out double[] rx, out double[] ry, out double[] rz)
        {
            int n = mesh.Nodes.Count;
            rx = new double[n]; ry = new double[n]; rz = new double[n];
            for (int i = 0; i < n; i++)
            {
                var p = mesh.Nodes[i];
                double px = p.X - centerX, py = p.Y - centerY, pz = p.Z - centerZ;
                double x1 = px * cosY + pz * sinY, z1 = -px * sinY + pz * cosY;
                double y2 = py * cosX - z1 * sinX, z2 = py * sinX + z1 * cosX;
                rx[i] = x1; ry[i] = y2; rz[i] = z2;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int vw = ClientSize.Width - SidebarWidth;
            int cx = SidebarWidth + vw / 2;
            int cy = ClientSize.Height / 2;
            double s = baseScale * zoom;

            double cosY = Math.Cos(angleY), sinY = Math.Sin(angleY);
            double cosX = Math.Cos(angleX), sinX = Math.Sin(angleX);

            RotateAll(cosX, sinX, cosY, sinY, out var rx, out var ry, out var rz);

            int nn = mesh.Nodes.Count;
            projXY = new PointF[nn]; projDepth = new double[nn];
            for (int i = 0; i < nn; i++)
            {
                projXY[i] = new PointF((float)(cx + rx[i] * s), (float)(cy - ry[i] * s));
                projDepth[i] = rz[i];
            }

            if (mode == 0 || mode == 2) DrawSurface(g, cx, cy, s, rx, ry, rz);
            if (mode == 1 || mode == 2) DrawTetEdges(g, cx, cy, s, rx, ry, rz);
            DrawHighlight(g, cx, cy, s, rx, ry, rz);
            DrawInfo(g);
        }

        private void DrawHighlight(Graphics g, int cx, int cy, double s,
                                   double[] rx, double[] ry, double[] rz)
        {
            if (selectedNode >= 0)
            {
                float x = (float)(cx + rx[selectedNode] * s), y = (float)(cy - ry[selectedNode] * s);
                using (var br = new SolidBrush(Color.Red))
                using (var pen = new Pen(Color.DarkRed, 2))
                { g.FillEllipse(br, x - 5, y - 5, 10, 10); g.DrawEllipse(pen, x - 5, y - 5, 10, 10); }
            }
            if (selectedEdge >= 0)
            {
                var ed = mesh.Edges[selectedEdge];
                using (var pen = new Pen(Color.Red, 3))
                    g.DrawLine(pen,
                        (float)(cx + rx[ed.A] * s), (float)(cy - ry[ed.A] * s),
                        (float)(cx + rx[ed.B] * s), (float)(cy - ry[ed.B] * s));
            }
            if (selectedFace >= 0)
            {
                var fc = mesh.Faces[selectedFace];
                using (var br = new SolidBrush(Color.FromArgb(160, 255, 60, 60)))
                using (var pen = new Pen(Color.Red, 2))
                {
                    var pts = new[]
                    {
                        new PointF((float)(cx + rx[fc.A] * s), (float)(cy - ry[fc.A] * s)),
                        new PointF((float)(cx + rx[fc.B] * s), (float)(cy - ry[fc.B] * s)),
                        new PointF((float)(cx + rx[fc.C] * s), (float)(cy - ry[fc.C] * s))
                    };
                    g.FillPolygon(br, pts); g.DrawPolygon(pen, pts);
                }
            }
            if (selectedTet >= 0)
            {
                var tet = mesh.Tets[selectedTet];
                using (var pen = new Pen(Color.Orange, 2))
                {
                    int[][] es =
                    {
                        new[]{tet.V[0],tet.V[1]}, new[]{tet.V[0],tet.V[2]},
                        new[]{tet.V[0],tet.V[3]}, new[]{tet.V[1],tet.V[2]},
                        new[]{tet.V[1],tet.V[3]}, new[]{tet.V[2],tet.V[3]}
                    };
                    foreach (var ed in es)
                        g.DrawLine(pen,
                            (float)(cx + rx[ed[0]] * s), (float)(cy - ry[ed[0]] * s),
                            (float)(cx + rx[ed[1]] * s), (float)(cy - ry[ed[1]] * s));
                }
            }
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
                double e1x = rx[f.B] - rx[f.A], e1y = ry[f.B] - ry[f.A], e1z = rz[f.B] - rz[f.A];
                double e2x = rx[f.C] - rx[f.A], e2y = ry[f.C] - ry[f.A], e2z = rz[f.C] - rz[f.A];
                double nx = e1y * e2z - e1z * e2y, ny = e1z * e2x - e1x * e2z, nz = e1x * e2y - e1y * e2x;
                double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                int shade = 4;
                if (nl > 1e-12)
                {
                    nx /= nl; ny /= nl; nz /= nl;
                    double dot = nx * lx + ny * ly + nz * lz;
                    double intensity = 0.25 + 0.75 * Math.Max(0, dot);
                    shade = (int)(intensity * 15);
                    if (shade < 0) shade = 0; if (shade > 15) shade = 15;
                }
                items[k] = (f.A, f.B, f.C, depth, shade);
            }

            Array.Sort(items, (p, q) => p.depth.CompareTo(q.depth));

            using (var edgePen = new Pen(Color.FromArgb(90, 25, 40, 70), 0.5f))
            {
                foreach (var it in items)
                {
                    float x1 = (float)(cx + rx[it.a] * s), y1 = (float)(cy - ry[it.a] * s);
                    float x2 = (float)(cx + rx[it.b] * s), y2 = (float)(cy - ry[it.b] * s);
                    float x3 = (float)(cx + rx[it.c] * s), y3 = (float)(cy - ry[it.c] * s);
                    var pts = new[] { new PointF(x1, y1), new PointF(x2, y2), new PointF(x3, y3) };
                    g.FillPolygon(palette[it.shade], pts);
                    g.DrawPolygon(edgePen, pts);
                }
            }
        }

        private void DrawTetEdges(Graphics g, int cx, int cy, double s,
                                  double[] rx, double[] ry, double[] rz)
        {
            using (var pen = new Pen(Color.FromArgb(120, 130, 30, 30), 0.5f))
            {
                foreach (var e in mesh.TetEdges)
                    g.DrawLine(pen,
                        (float)(cx + rx[e[0]] * s), (float)(cy - ry[e[0]] * s),
                        (float)(cx + rx[e[1]] * s), (float)(cy - ry[e[1]] * s));
            }
        }

        private void DrawInfo(Graphics g)
        {
            string modeName = mode == 0 ? "поверхность"
                            : mode == 1 ? "рёбра тетраэдров"
                                        : "поверхность + рёбра";
            string pickName = pickMode == 0 ? "узел"
                            : pickMode == 1 ? "ребро"
                            : pickMode == 2 ? "грань" : "КЭ";
            string schemeName = mesh.Scheme.ToString().Replace('_', ' ');

            string text =
                $"Схема: {schemeName}\n" +
                $"Режим отображения: {modeName} (1/2/3)\n" +
                $"Режим выбора: {pickName} (N/E/F/T)\n" +
                $"h = {mesh.h:0.###}\n" +
                $"Узлов: {mesh.Nodes.Count}\n" +
                $"Рёбер: {mesh.Edges.Count}\n" +
                $"Граней: {mesh.Faces.Count}\n" +
                $"Тетраэдров: {mesh.Tets.Count}\n" +
                $"Базисных функций всего: {mesh.TotalBasisFunctions()}\n" +
                $"  вершинных: {mesh.NumVertices}\n" +
                $"  рёберных: {mesh.EdgeFunctionsPerEdge() * mesh.NumEdges}\n" +
                $"  граневых: {(mesh.HasFaceFunctions() ? mesh.NumFaces : 0)}\n" +
                $"  объёмных: {(mesh.HasVolumeFunctions() ? mesh.NumTets : 0)}";

            using (var font = new Font("Segoe UI", 9.5f))
            using (var brush = new SolidBrush(Color.FromArgb(230, 20, 20, 20)))
            using (var bg = new SolidBrush(Color.FromArgb(215, 255, 255, 255)))
            {
                var size = g.MeasureString(text, font);
                g.FillRectangle(bg, SidebarWidth + 8, 8, size.Width + 10, size.Height + 10);
                g.DrawString(text, font, brush, SidebarWidth + 13, 13);
            }
        }
    }
}