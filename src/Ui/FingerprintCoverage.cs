using System;

namespace Pwaat.Accessibility.Ui
{
    // The native Blow routine indexes mask pixels against the lower-left readback of its render target.
    // This cache is changed only by real powder draws and completed native blows, never by a timer.
    internal sealed class FingerprintCoverage
    {
        private readonly byte[] _mask, _alpha;
        private readonly int _maskWidth, _maskHeight, _screenWidth, _screenHeight;
        internal readonly int Width, Height;
        internal readonly bool FlipX, FlipY;
        private bool _noTarget;
        private int _stepX, _stepY, _bucketColumns, _bucketRows, _brushWidth, _brushHeight;
        private int[] _remainingBuckets;
        private bool _stampStarted;
        private readonly float[] _emptyBounds = new float[6];
        internal FingerprintCoverage(byte[] mask, int maskWidth, int maskHeight, int width, int height,
            int screenWidth, int screenHeight, bool flipX, bool flipY)
        {
            if (mask == null || maskWidth <= 0 || maskHeight <= 0 || mask.Length != maskWidth * maskHeight
                || width <= 0 || height <= 0 || screenWidth <= 0 || screenHeight <= 0)
                throw new ArgumentException("Invalid native fingerprint coverage dimensions.");
            _mask = mask; _maskWidth = maskWidth; _maskHeight = maskHeight;
            Width = width; Height = height; _screenWidth = screenWidth; _screenHeight = screenHeight;
            FlipX = flipX; FlipY = flipY; _alpha = new byte[width * height];
        }
        internal int RawX(int cursorX) { return FlipX ? Width - 1 - cursorX : cursorX; }
        internal int RawY(int cursorY) { return FlipY ? Height - 1 - cursorY : cursorY; }
        internal int CursorX(int rawX) { return RawX(rawX); }
        internal int CursorY(int rawY) { return RawY(rawY); }
        private int Required(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _maskWidth || y >= _maskHeight || x >= _screenWidth || y >= _screenHeight
                || x >= Width || y >= Height) return 0;
            return _mask[y * _maskWidth + x];
        }
        internal bool Incomplete(int x, int y)
        { int required = Required(x, y); return required != 0 && _alpha[y * Width + x] < required; }
        internal bool Commit(int x, int y, byte alpha)
        {
            int required = Required(x, y), index = y * Width + x;
            bool added = required != 0 && _alpha[index] < required && alpha > _alpha[index];
            if (_alpha[index] != alpha) _noTarget = false;
            bool beforeIncomplete = required != 0 && _alpha[index] < required;
            bool afterIncomplete = required != 0 && alpha < required;
            if (_remainingBuckets != null && beforeIncomplete != afterIncomplete)
                _remainingBuckets[(y / _stepY) * _bucketColumns + x / _stepX] += afterIncomplete ? 1 : -1;
            _alpha[index] = alpha; return added;
        }
        internal bool HasIncomplete(int x, int y, int width, int height)
        {
            for (int row = y; row < y + height; row++)
                for (int col = x; col < x + width; col++) if (Incomplete(col, row)) return true;
            return false;
        }
        internal bool Nearest(float cursorX, float cursorY, float minX, float minY, float maxX, float maxY,
            float guideX, float guideY, out int targetX, out int targetY)
        {
            targetX = targetY = -1; double closest = double.MaxValue;
            if (_noTarget && _emptyBounds[0] == minX && _emptyBounds[1] == minY && _emptyBounds[2] == maxX
                && _emptyBounds[3] == maxY && _emptyBounds[4] == guideX && _emptyBounds[5] == guideY) return false;
            // A held key normally remains over the current incomplete pixel until a real draw fills it.
            int hereX = (int)cursorX, hereY = (int)cursorY;
            if (cursorX == hereX && cursorY == hereY && hereX >= minX && hereX <= maxX && hereY >= minY && hereY <= maxY
                && !(hereX > guideX && hereY > guideY) && Incomplete(RawX(hereX), RawY(hereY)))
            { targetX = hereX; targetY = hereY; return true; }
            int endX = Math.Min(Math.Min(_maskWidth, _screenWidth), Width);
            int endY = Math.Min(Math.Min(_maskHeight, _screenHeight), Height);
            for (int y = 0; y < endY; y++) for (int x = 0; x < endX; x++)
            {
                if (!Incomplete(x, y)) continue;
                int cx = CursorX(x), cy = CursorY(y);
                if (cx < minX || cx > maxX || cy < minY || cy > maxY || cx > guideX && cy > guideY) continue;
                double dx = cx - cursorX, dy = cy - cursorY, distance = dx * dx + dy * dy;
                if (distance >= closest) continue;
                closest = distance; targetX = cx; targetY = cy;
            }
            if (targetX < 0)
            {
                _noTarget = true; _emptyBounds[0] = minX; _emptyBounds[1] = minY; _emptyBounds[2] = maxX;
                _emptyBounds[3] = maxY; _emptyBounds[4] = guideX; _emptyBounds[5] = guideY;
            }
            return targetX >= 0;
        }
        internal void ConfigureBrush(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Invalid native powder brush dimensions.");
            _brushWidth = width; _brushHeight = height;
            // Overlapping stamp centres cover the sparse native patterns without chasing adjacent mask pixels.
            _stepX = Math.Max(1, width / 4); _stepY = Math.Max(1, height / 4);
            _bucketColumns = (Width + _stepX - 1) / _stepX; _bucketRows = (Height + _stepY - 1) / _stepY;
            _remainingBuckets = new int[_bucketColumns * _bucketRows]; _stampStarted = false;
            int endX = Math.Min(Math.Min(_maskWidth, _screenWidth), Width), endY = Math.Min(Math.Min(_maskHeight, _screenHeight), Height);
            for (int y = 0; y < endY; y++) for (int x = 0; x < endX; x++)
                if (Incomplete(x, y)) _remainingBuckets[(y / _stepY) * _bucketColumns + x / _stepX]++;
        }
        private bool StampHasIncomplete(int cursorX, int cursorY)
        {
            int left = Math.Max(0, cursorX - _brushWidth / 2), top = Math.Max(0, cursorY - _brushHeight / 2);
            int right = Math.Min(Width, cursorX + _brushWidth / 2), bottom = Math.Min(Height, cursorY + _brushHeight / 2);
            if (right <= left || bottom <= top) return false;
            int rx = FlipX ? Width - right : left, ry = FlipY ? Height - bottom : top;
            int endX = rx + right - left, endY = ry + bottom - top;
            for (int by = ry / _stepY; by <= (endY - 1) / _stepY; by++)
                for (int bx = rx / _stepX; bx <= (endX - 1) / _stepX; bx++)
                {
                    if (_remainingBuckets[by * _bucketColumns + bx] == 0) continue;
                    int x0 = bx * _stepX, y0 = by * _stepY;
                    int x1 = Math.Min(Width, x0 + _stepX), y1 = Math.Min(Height, y0 + _stepY);
                    if (x0 >= rx && y0 >= ry && x1 <= endX && y1 <= endY) return true;
                    if (HasIncomplete(Math.Max(rx, x0), Math.Max(ry, y0),
                        Math.Min(endX, x1) - Math.Max(rx, x0), Math.Min(endY, y1) - Math.Max(ry, y0))) return true;
                }
            return false;
        }
        internal bool NextStamp(float cursorX, float cursorY, float minX, float minY, float maxX, float maxY,
            float guideX, float guideY, int direction, out int targetX, out int targetY)
        {
            if (_remainingBuckets == null || direction != -1 && direction != 1) throw new InvalidOperationException("Powder sweep not configured.");
            targetX = targetY = -1;
            if (!_stampStarted)
            {
                _stampStarted = true;
                if (Nearest(cursorX, cursorY, minX, minY, maxX, maxY, guideX, guideY, out targetX, out targetY)) return true;
            }
            int columns = (int)Math.Ceiling((maxX - minX) / _stepX) + 1;
            int rows = (int)Math.Ceiling((maxY - minY) / _stepY) + 1;
            if (columns <= 0 || rows <= 0) return false;
            int column = Math.Max(0, Math.Min(columns - 1, (int)Math.Round((cursorX - minX) / _stepX)));
            int row = Math.Max(0, Math.Min(rows - 1, (int)Math.Round((cursorY - minY) / _stepY)));
            int start = row * columns + column, total = columns * rows;
            for (int step = 1; step <= total; step++)
            {
                int index = ((start + direction * step) % total + total) % total;
                int x = (int)Math.Min(maxX, minX + (index % columns) * _stepX);
                int y = (int)Math.Min(maxY, minY + (index / columns) * _stepY);
                if (x < 0 || y < 0 || x >= Width || y >= Height || x > guideX && y > guideY) continue;
                // Do not return the same or an adjacent pixel merely because its mask alpha is still unmet.
                if (Math.Abs(x - cursorX) < _stepX && Math.Abs(y - cursorY) < _stepY) continue;
                if (!StampHasIncomplete(x, y)) continue;
                targetX = x; targetY = y; return true;
            }
            // A narrow permitted area may have no distinct grid site; retain a truthful native-mask fallback.
            return Nearest(cursorX, cursorY, minX, minY, maxX, maxY, guideX, guideY, out targetX, out targetY);
        }
        internal static int Percentage(int score, int threshold, bool complete)
        {
            if (threshold <= 0 || score < 0) throw new ArgumentOutOfRangeException("threshold");
            return complete ? 100 : (int)Math.Min(99L, (long)score * 100L / threshold);
        }
    }
}
