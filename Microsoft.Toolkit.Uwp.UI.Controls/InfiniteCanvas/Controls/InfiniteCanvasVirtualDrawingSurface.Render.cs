// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage;
using Windows.UI;

namespace Microsoft.Toolkit.Uwp.UI.Controls
{
    /// <summary>
    /// The virtual Drawing surface renderer used to render the ink and text. This control is used as part of the <see cref="InfiniteCanvas"/>
    /// </summary>
    public partial class InfiniteCanvasVirtualDrawingSurface
    {
        private readonly List<IDrawable> _visibleList = new List<IDrawable>();
        private readonly List<IDrawable> _drawableList = new List<IDrawable>();

        internal void ReDraw(Rect viewPort)
        {
            _visibleList.Clear();
            double top = double.MaxValue,
                   bottom = double.MinValue,
                   left = double.MaxValue,
                   right = double.MinValue;

            foreach (var drawable in _drawableList)
            {
                if (drawable.IsVisible(viewPort))
                {
                    _visibleList.Add(drawable);

                    bottom = Math.Max(drawable.Bounds.Bottom, bottom);
                    right = Math.Max(drawable.Bounds.Right, right);
                    top = Math.Min(drawable.Bounds.Top, top);
                    left = Math.Min(drawable.Bounds.Left, left);
                }
            }

            Rect toDraw;
            if (_visibleList.Any())
            {
                toDraw = new Rect(Math.Max(left, 0), Math.Max(top, 0), Math.Max(right - left, 0), Math.Max(bottom - top, 0));

                toDraw.Union(viewPort);
            }
            else
            {
                toDraw = viewPort;
            }

            if (toDraw.Height > Height)
            {
                toDraw.Height = Height;
            }

            if (toDraw.Width > Width)
            {
                toDraw.Width = Width;
            }

            using (CanvasDrawingSession drawingSession = CanvasComposition.CreateDrawingSession(_drawingSurface, toDraw))
            {
                drawingSession.Clear(Colors.White);
                foreach (var drawable in _visibleList)
                {
                    drawable.Draw(drawingSession, toDraw);
                }
            }
        }

        internal async Task ExportAsPNG(IStorageFile file)
        {
            double width = double.MinValue,
                   height = double.MinValue;

            foreach (var drawable in _drawableList)
            {
                width = Math.Max(drawable.Bounds.Left + drawable.Bounds.Width, width);
                height = Math.Max(drawable.Bounds.Top + drawable.Bounds.Height, height);
            }

            var device = CanvasDevice.GetSharedDevice();
            var renderTarget = new CanvasRenderTarget(device, (float)width, (float)height, 96);
            using (var drawingSession = renderTarget.CreateDrawingSession())
            {
                drawingSession.Clear(Colors.White);
                foreach (var drawable in _visibleList)
                {
                    drawable.Draw(drawingSession, renderTarget.Bounds);
                }
            }

            CanvasBitmap bit = renderTarget;

            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await bit.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f);
            }
        }

        internal void ClearAll(Rect viewPort)
        {
            _visibleList.Clear();
            ExecuteClearAll();
            _drawingSurface.Trim(new RectInt32[0]);
        }

        internal string GetSerializedList()
        {
            var exportModel = new InkCanvasExportModel { DrawableList = _drawableList, Version = 1 };
            return JsonConvert.SerializeObject(exportModel, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto
            });
        }

        internal void RenderFromJsonAndDraw(Rect viewPort, string json)
        {
            _visibleList.Clear();
            _drawableList.Clear();
            _undoCommands.Clear();
            _redoCommands.Clear();

            var token = JToken.Parse(json);
            List<IDrawable> newList;
            if (token is JArray)
            {
                // first sin, because of creating a file without versioning so we have to be able to import without breaking changes.
                newList = JsonConvert.DeserializeObject<List<IDrawable>>(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
            }
            else
            {
                newList = JsonConvert.DeserializeObject<InkCanvasExportModel>(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto }).DrawableList;
            }

            foreach (var drawable in newList)
            {
                _drawableList.Add(drawable);
            }

            ReDraw(viewPort);
        }
    }
}
