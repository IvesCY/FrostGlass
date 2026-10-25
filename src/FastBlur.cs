using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FrostGlass
{
    /// <summary>
    /// Fast separable box blur for a 32bpp ARGB bitmap. Good enough for a soft
    /// frosted preview and cheap enough to refresh several times per second.
    /// </summary>
    internal sealed class FastBlur : IDisposable
    {
        private int[] _pixels;
        private int[] _temp;
        private int _width;
        private int _height;
        private int _strideInts;

        public void Blur(Bitmap bitmap, int radius)
        {
            if (bitmap == null || radius <= 0 || bitmap.Width < 2 || bitmap.Height < 2)
                return;

            int maxRadius = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 4);
            radius = Math.Min(radius, maxRadius);

            BitmapData data = null;
            try
            {
                data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                int strideInts = Math.Abs(data.Stride) / 4;
                int needed = strideInts * bitmap.Height;

                if (_pixels == null || _pixels.Length < needed ||
                    _width != bitmap.Width || _height != bitmap.Height || _strideInts != strideInts)
                {
                    _pixels = new int[needed];
                    _temp = new int[needed];
                    _width = bitmap.Width;
                    _height = bitmap.Height;
                    _strideInts = strideInts;
                }

                Marshal.Copy(data.Scan0, _pixels, 0, needed);
                BlurHorizontal(_pixels, _temp, bitmap.Width, bitmap.Height, strideInts, radius);
                BlurVertical(_temp, _pixels, bitmap.Width, bitmap.Height, strideInts, radius);
                Marshal.Copy(_pixels, 0, data.Scan0, needed);
            }
            finally
            {
                if (data != null)
                    bitmap.UnlockBits(data);
            }
        }

        private static void BlurHorizontal(int[] source, int[] target, int width, int height,
            int strideInts, int radius)
        {
            int window = radius * 2 + 1;
            for (int y = 0; y < height; y++)
            {
                int row = y * strideInts;
                long sumR = 0, sumG = 0, sumB = 0;

                for (int i = -radius; i <= radius; i++)
                {
                    int x = i;
                    if (x < 0) x = 0;
                    else if (x >= width) x = width - 1;
                    int color = source[row + x];
                    sumR += (color >> 16) & 0xFF;
                    sumG += (color >> 8) & 0xFF;
                    sumB += color & 0xFF;
                }

                for (int x = 0; x < width; x++)
                {
                    int r = (int)(sumR / window);
                    int g = (int)(sumG / window);
                    int b = (int)(sumB / window);
                    target[row + x] = unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;

                    int removeX = x - radius;
                    if (removeX < 0) removeX = 0;
                    int addX = x + radius + 1;
                    if (addX >= width) addX = width - 1;

                    int oldColor = source[row + removeX];
                    int newColor = source[row + addX];
                    sumR += ((newColor >> 16) & 0xFF) - ((oldColor >> 16) & 0xFF);
                    sumG += ((newColor >> 8) & 0xFF) - ((oldColor >> 8) & 0xFF);
                    sumB += (newColor & 0xFF) - (oldColor & 0xFF);
                }
            }
        }

        private static void BlurVertical(int[] source, int[] target, int width, int height,
            int strideInts, int radius)
        {
            int window = radius * 2 + 1;
            for (int x = 0; x < width; x++)
            {
                long sumR = 0, sumG = 0, sumB = 0;

                for (int i = -radius; i <= radius; i++)
                {
                    int y = i;
                    if (y < 0) y = 0;
                    else if (y >= height) y = height - 1;
                    int color = source[y * strideInts + x];
                    sumR += (color >> 16) & 0xFF;
                    sumG += (color >> 8) & 0xFF;
                    sumB += color & 0xFF;
                }

                for (int y = 0; y < height; y++)
                {
                    int r = (int)(sumR / window);
                    int g = (int)(sumG / window);
                    int b = (int)(sumB / window);
                    target[y * strideInts + x] = unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;

                    int removeY = y - radius;
                    if (removeY < 0) removeY = 0;
                    int addY = y + radius + 1;
                    if (addY >= height) addY = height - 1;

                    int oldColor = source[removeY * strideInts + x];
                    int newColor = source[addY * strideInts + x];
                    sumR += ((newColor >> 16) & 0xFF) - ((oldColor >> 16) & 0xFF);
                    sumG += ((newColor >> 8) & 0xFF) - ((oldColor >> 8) & 0xFF);
                    sumB += (newColor & 0xFF) - (oldColor & 0xFF);
                }
            }
        }

        public void Dispose()
        {
            _pixels = null;
            _temp = null;
        }
    }
}
