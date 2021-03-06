// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Advanced.ParallelUtils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Overlays
{
    /// <summary>
    /// Sets the background color of the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BackgroundColorProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly BackgroundColorProcessor definition;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundColorProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="BackgroundColorProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public BackgroundColorProcessor(Configuration configuration, BackgroundColorProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.definition = definition;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            TPixel color = this.definition.Color.ToPixel<TPixel>();
            GraphicsOptions graphicsOptions = this.definition.GraphicsOptions;

            int startY = this.SourceRectangle.Y;
            int endY = this.SourceRectangle.Bottom;
            int startX = this.SourceRectangle.X;
            int endX = this.SourceRectangle.Right;

            // Align start/end positions.
            int minX = Math.Max(0, startX);
            int maxX = Math.Min(source.Width, endX);
            int minY = Math.Max(0, startY);
            int maxY = Math.Min(source.Height, endY);

            // Reset offset if necessary.
            if (minX > 0)
            {
                startX = 0;
            }

            if (minY > 0)
            {
                startY = 0;
            }

            int width = maxX - minX;

            var workingRect = Rectangle.FromLTRB(minX, minY, maxX, maxY);

            using (IMemoryOwner<TPixel> colors = source.MemoryAllocator.Allocate<TPixel>(width))
            using (IMemoryOwner<float> amount = source.MemoryAllocator.Allocate<float>(width))
            {
                // Be careful! Do not capture colorSpan & amountSpan in the lambda below!
                Span<TPixel> colorSpan = colors.GetSpan();
                Span<float> amountSpan = amount.GetSpan();

                colorSpan.Fill(color);
                amountSpan.Fill(graphicsOptions.BlendPercentage);

                PixelBlender<TPixel> blender = PixelOperations<TPixel>.Instance.GetPixelBlender(graphicsOptions);

                ParallelHelper.IterateRows(
                    workingRect,
                    this.Configuration,
                    rows =>
                        {
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                Span<TPixel> destination =
                                    source.GetPixelRowSpan(y - startY).Slice(minX - startX, width);

                                // This switched color & destination in the 2nd and 3rd places because we are applying the target color under the current one
                                blender.Blend(
                                    source.Configuration,
                                    destination,
                                    colors.GetSpan(),
                                    destination,
                                    amount.GetSpan());
                            }
                        });
            }
        }
    }
}
