using System;

namespace VintageAtlas.Export;

public static class BlurTool
    {
        public static void Blur(Span<byte> data, int sizeX, int sizeZ, int range)
        {
            BoxBlurHorizontal(data, range, 0, 0, sizeX, sizeZ);
            BoxBlurVertical(data, range, 0, 0, sizeX, sizeZ);
        }


        private static unsafe void BoxBlurHorizontal(Span<byte> map, int range, int xStart, int yStart, int xEnd, int yEnd)
        {
            fixed (byte* pixels = map)
            {
                var w = xEnd - xStart;
                var h = yEnd - yStart;

                var halfRange = range / 2;
                var index = yStart * w;
                var newColors = new byte[w];

                for (var y = yStart; y < yEnd; y++)
                {
                    var hits = 0;
                    var r = 0;
                    for (var x = xStart - halfRange; x < xEnd; x++)
                    {
                        var oldPixel = x - halfRange - 1;
                        if (oldPixel >= xStart)
                        {
                            var col = pixels[index + oldPixel];
                            if (col != 0)
                            {
                                r -= col;
                            }
                            hits--;
                        }

                        var newPixel = x + halfRange;
                        if (newPixel < xEnd)
                        {
                            var col = pixels[index + newPixel];
                            if (col != 0)
                            {
                                r += col;
                            }
                            hits++;
                        }

                        if (x >= xStart)
                        {
                            var color = (byte)(r / hits);
                            newColors[x] = color;
                        }
                    }

                    for (var x = xStart; x < xEnd; x++)
                    {
                        pixels[index + x] = newColors[x];
                    }

                    index += w;
                }
            }
        }

        private static unsafe void BoxBlurVertical(Span<byte> map, int range, int xStart, int yStart, int xEnd, int yEnd)
        {
            fixed (byte* pixels = map)
            {
                var w = xEnd - xStart;
                var h = yEnd - yStart;

                var halfRange = range / 2;

                var newColors = new byte[h];
                var oldPixelOffset = -(halfRange + 1) * w;
                var newPixelOffset = halfRange * w;

                for (var x = xStart; x < xEnd; x++)
                {
                    var hits = 0;
                    var r = 0;
                    var index = yStart * w - halfRange * w + x;
                    for (var y = yStart - halfRange; y < yEnd; y++)
                    {
                        var oldPixel = y - halfRange - 1;
                        if (oldPixel >= yStart)
                        {
                            var col = pixels[index + oldPixelOffset];
                            if (col != 0)
                            {
                                r -= col;
                            }
                            hits--;
                        }

                        var newPixel = y + halfRange;
                        if (newPixel < yEnd)
                        {
                            var col = pixels[index + newPixelOffset];
                            if (col != 0)
                            {
                                r += col;
                            }
                            hits++;
                        }

                        if (y >= yStart)
                        {
                            var color = (byte)(r / hits);
                            newColors[y] = color;
                        }

                        index += w;
                    }

                    for (var y = yStart; y < yEnd; y++)
                    {
                        pixels[y * w + x] = newColors[y];
                    }
                }
            }
        }


    }