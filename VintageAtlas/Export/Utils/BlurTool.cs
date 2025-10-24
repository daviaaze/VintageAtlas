using System;

namespace VintageAtlas.Export.Utils;

public static class BlurTool
{
    private readonly struct BlurContext
    {
        public readonly int HalfRange;
        public readonly int XStart;
        public readonly int YStart;
        public readonly int XEnd;
        public readonly int YEnd;

        public BlurContext(int halfRange, int xStart, int yStart, int xEnd, int yEnd)
        {
            HalfRange = halfRange;
            XStart = xStart;
            YStart = yStart;
            XEnd = xEnd;
            YEnd = yEnd;
        }
    }

    private readonly struct VerticalOffsets
    {
        public readonly int OldPixelOffset;
        public readonly int NewPixelOffset;

        public VerticalOffsets(int oldPixelOffset, int newPixelOffset)
        {
            OldPixelOffset = oldPixelOffset;
            NewPixelOffset = newPixelOffset;
        }
    }

    public static void Blur(Span<byte> data, int sizeX, int sizeZ, int range)
    {
        BoxBlurHorizontal(data, sizeX, range, 0, 0, sizeX, sizeZ);
        BoxBlurVertical(data, sizeX, range, 0, 0, sizeX, sizeZ);
    }


    private static unsafe void BoxBlurHorizontal(Span<byte> map, int fullWidth, int range, int xStart, int yStart, int xEnd, int yEnd)
    {
        fixed (byte* pixels = map)
        {
            var context = new BlurContext(range / 2, xStart, yStart, xEnd, yEnd);
            var w = context.XEnd - context.XStart;
            var newColors = new byte[w];
            var index = context.YStart * fullWidth;

            for (var y = context.YStart; y < context.YEnd; y++)
            {
                ProcessHorizontalRow(pixels, index, newColors, context);
                ApplyHorizontalRow(pixels, index, newColors, context);
                index += fullWidth;
            }
        }
    }

    private static unsafe void ProcessHorizontalRow(byte* pixels, int index, byte[] newColors, BlurContext context)
    {
        var hits = 0;
        var r = 0;

        for (var x = context.XStart - context.HalfRange; x < context.XEnd; x++)
        {
            UpdateHorizontalRollingAverage(pixels, index, x, context, ref r, ref hits);

            if (x < context.XStart) 
                continue;

            var color = (byte)(hits > 0 ? r / hits : 0);
            newColors[x - context.XStart] = color;
        }
    }

    private static unsafe void UpdateHorizontalRollingAverage(byte* pixels, int index, int x, BlurContext context, ref int r, ref int hits)
    {
        var oldPixel = x - context.HalfRange - 1;
        if (oldPixel >= context.XStart)
        {
            var col = pixels[index + oldPixel];
            if (col != 0)
            {
                r -= col;
            }
            hits--;
        }

        var newPixel = x + context.HalfRange;
        if (newPixel < context.XEnd)
        {
            var col = pixels[index + newPixel];
            if (col != 0)
            {
                r += col;
            }
            hits++;
        }
    }

    private static unsafe void ApplyHorizontalRow(byte* pixels, int index, byte[] newColors, BlurContext context)
    {
        for (var x = context.XStart; x < context.XEnd; x++)
        {
            pixels[index + x] = newColors[x - context.XStart];
        }
    }

    private static unsafe void BoxBlurVertical(Span<byte> map, int fullWidth, int range, int xStart, int yStart, int xEnd, int yEnd)
    {
        fixed (byte* pixels = map)
        {
            var context = new BlurContext(range / 2, xStart, yStart, xEnd, yEnd);
            var h = context.YEnd - context.YStart;
            var newColors = new byte[h];
            var offsets = new VerticalOffsets(-(context.HalfRange + 1) * fullWidth, context.HalfRange * fullWidth);

            for (var x = context.XStart; x < context.XEnd; x++)
            {
                ProcessVerticalColumn(pixels, fullWidth, x, newColors, context, offsets);
                ApplyVerticalColumn(pixels, fullWidth, x, newColors, context);
            }
        }
    }

    private static unsafe void ProcessVerticalColumn(byte* pixels, int fullWidth, int x, byte[] newColors, BlurContext context, VerticalOffsets offsets)
    {
        var hits = 0;
        var r = 0;
        var index = context.YStart * fullWidth - context.HalfRange * fullWidth + x;

        for (var y = context.YStart - context.HalfRange; y < context.YEnd; y++)
        {
            UpdateVerticalRollingAverage(pixels, index, y, context, offsets, ref r, ref hits);

            if (y >= context.YStart)
            {
                var color = (byte)(hits > 0 ? r / hits : 0);
                newColors[y - context.YStart] = color;
            }

            index += fullWidth;
        }
    }

    private static unsafe void UpdateVerticalRollingAverage(byte* pixels, int index, int y, BlurContext context, VerticalOffsets offsets, ref int r, ref int hits)
    {
        var oldPixel = y - context.HalfRange - 1;
        if (oldPixel >= context.YStart)
        {
            var col = pixels[index + offsets.OldPixelOffset];
            if (col != 0)
            {
                r -= col;
            }
            hits--;
        }

        var newPixel = y + context.HalfRange;
        if (newPixel < context.YEnd)
        {
            var col = pixels[index + offsets.NewPixelOffset];
            if (col != 0)
            {
                r += col;
            }
            hits++;
        }
    }

    private static unsafe void ApplyVerticalColumn(byte* pixels, int fullWidth, int x, byte[] newColors, BlurContext context)
    {
        for (var y = context.YStart; y < context.YEnd; y++)
        {
            pixels[y * fullWidth + x] = newColors[y - context.YStart];
        }
    }


}