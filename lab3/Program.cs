using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace Lab2{
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