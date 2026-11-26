using System;

namespace Lab2
{
    public class SparseMatrix
    {
        public int Size;
        public double[] Values;
        public int[] ColIndices;
        public int[] RowPointers;

        public SparseMatrix(int size, int[] rowPointers, int[] colIndices)
        {
            Size = size;
            RowPointers = rowPointers;
            ColIndices = colIndices;
            Values = new double[colIndices.Length];
        }

        public void AddValue(int row, int col, double value)
        {
            for (int i = RowPointers[row]; i < RowPointers[row + 1]; i++)
            {
                if (ColIndices[i] == col)
                {
                    Values[i] += value;
                    return;
                }
            }
            throw new Exception($"Элемент ({row}, {col}) отсутствует в портрете!");
        }

        public double GetValue(int row, int col)
        {
            if (col > row) return 0.0;
            for (int i = RowPointers[row]; i < RowPointers[row + 1]; i++)
                if (ColIndices[i] == col) return Values[i];
            return 0.0;
        }

        public void SetValue(int row, int col, double value)
        {
            for (int i = RowPointers[row]; i < RowPointers[row + 1]; i++)
            {
                if (ColIndices[i] == col)
                {
                    Values[i] = value;
                    return;
                }
            }
        }
    }
}