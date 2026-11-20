using System;
using System.Collections.Generic;
namespace Pwaat.Accessibility.Ui
{
    // Pure editor: source identity does not carry answer information.
    internal sealed class DyingMessageState
    {
        internal const int MaximumCells = 8;
        private readonly List<int[]> _cells = new List<int[]>();
        private readonly List<int> _undo = new List<int>();
        internal readonly int SourceCount;
        internal int Held = -1;
        internal int CellCount { get { return _cells.Count; } }
        internal DyingMessageState(int sourceCount) { if (sourceCount < 1) throw new ArgumentOutOfRangeException("sourceCount"); SourceCount = sourceCount; AddCell(); }
        internal int At(int cell, int dot) { return _cells[cell][dot]; }
        internal bool Used(int token) { foreach (int[] cell in _cells) foreach (int item in cell) if (item == token) return true; return false; }
        internal bool Pick(int token) { if (token < 0 || token >= SourceCount || Held >= 0 || Used(token)) return false; Held = token; return true; }
        internal bool Place(int cell, int dot)
        { if (cell < 0 || cell >= CellCount || dot < 0 || dot >= 6 || Held < 0 || At(cell, dot) >= 0) return false; _cells[cell][dot] = Held; Held = -1; _undo.Add(cell * 6 + dot); return true; }
        internal bool Remove(int cell, int dot)
        { if (Held >= 0 || At(cell, dot) < 0) return false; _cells[cell][dot] = -1; _undo.Remove(cell * 6 + dot); return true; }
        internal bool Back()
        { if (Held >= 0) { Held = -1; return true; } if (_undo.Count == 0) return false; int address = _undo[_undo.Count - 1]; _undo.RemoveAt(_undo.Count - 1); _cells[address / 6][address % 6] = -1; return true; }
        internal bool AddCell() { if (CellCount >= MaximumCells) return false; _cells.Add(new int[] { -1, -1, -1, -1, -1, -1 }); return true; }
        internal bool RemoveLastCell()
        { if (CellCount <= 1) return false; int last = CellCount - 1; _cells.RemoveAt(last); _undo.RemoveAll(delegate (int address) { return address / 6 == last; }); return true; }
        internal int[] Masks()
        { int[] masks = new int[CellCount]; for (int c = 0; c < CellCount; c++) for (int d = 0; d < 6; d++) if (At(c, d) >= 0) masks[c] |= 1 << d; return masks; }
    }
    internal static class DyingMessageAnswers
    {
        private static int Mask(string dots) { int result = 0; foreach (char dot in dots) result |= 1 << (dot - '1'); return result; }
        private static bool Equals(int[] actual, params string[] expected)
        { if (actual.Length != expected.Length) return false; for (int i = 0; i < actual.Length; i++) if (actual[i] != Mask(expected[i])) return false; return true; }
        internal static bool BaseAccept(Language language, int[] cells)
        {
            if (cells == null) return false;
            if (language == Language.CHINA_S || language == Language.CHINA_T)
            {
                if (Equals(cells, "13", "146") || Equals(cells, "125", "24")) return true;
                if (language == Language.CHINA_T && (Equals(cells, "245", "2345", "5") || Equals(cells, "15", "16", "3"))) return true;
                return false;
            }
            if (language == Language.JAPAN) return Equals(cells, "1", "16", "1234");
            if (language == Language.KOREA) return Equals(cells, "126", "124", "14", "1345");
            return Equals(cells, "15", "134", "1");
        }
    }
}
