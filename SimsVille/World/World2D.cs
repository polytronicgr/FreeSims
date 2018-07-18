﻿/*
 * This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
 * If a copy of the MPL was not distributed with this file, You can obtain one at
 * http://mozilla.org/MPL/2.0/. 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using FSO.LotView.Model;
using Microsoft.Xna.Framework;
using FSO.Common.Utils;
using FSO.Common.Rendering.Framework;
using FSO.LotView.Components;
using System.IO;
using FSO.LotView.Utils;

namespace FSO.LotView
{
    /// <summary>
    /// Handles rendering the 2D world
    /// </summary>
    public class World2D
    {
        public static SurfaceFormat[] BUFFER_SURFACE_FORMATS = new SurfaceFormat[] {
            /** Thumbnail buffer **/
            SurfaceFormat.Color,

            /** Static Object Buffers **/
            SurfaceFormat.Color,
            SurfaceFormat.Color, //depth, using a 24-bit packed format

            /** Terrain Color **/
            SurfaceFormat.Color,

            /** Object ID buffer **/
            SurfaceFormat.Color,

            /** Floor buffers **/
            SurfaceFormat.Color,
            SurfaceFormat.Color, //depth

            /** Terrain Depth **/
            SurfaceFormat.Color,

            /** Wall buffers **/
            SurfaceFormat.Color,
            SurfaceFormat.Color, //depth

            //Thumbnail depth
            SurfaceFormat.Color,
        };

        public static bool[] FORMAT_ALWAYS_DEPTHSTENCIL = new bool[] {
            /** Thumbnail buffer **/
            true,

            /** Static Object Buffers **/
            true,
            false, //depth, using a 24-bit packed format

            /** Terrain Color **/
            true,

            /** Object ID buffer **/
            true,

            /** Floor buffers **/
            true,
            false, //depth

            /** Terrain Depth **/
            false,

            /** Wall buffers **/
            true,
            false, //depth

            //Thumbnail depth
            false,
        };

        public static readonly int NUM_2D_BUFFERS = 11;
        public static readonly int BUFFER_THUMB = 0; //used for drawing thumbnails
        public static readonly int BUFFER_STATIC_OBJECTS_PIXEL = 1;
        public static readonly int BUFFER_STATIC_OBJECTS_DEPTH = 2;
        public static readonly int BUFFER_STATIC_TERRAIN = 3;
        public static readonly int BUFFER_OBJID = 4;
        public static readonly int BUFFER_FLOOR_PIXEL = 5;
        public static readonly int BUFFER_FLOOR_DEPTH = 6;
        public static readonly int BUFFER_STATIC_TERRAIN_DEPTH = 7;

        public static readonly int BUFFER_WALL_PIXEL = 8;
        public static readonly int BUFFER_WALL_DEPTH = 9;
        public static readonly int BUFFER_THUMB_DEPTH = 10;


        public static readonly int SCROLL_BUFFER = 512; //resolution to add to render size for scroll reasons


        private Blueprint Blueprint;
        private Dictionary<WorldComponent, WorldObjectRenderInfo> RenderInfo = new Dictionary<WorldComponent, WorldObjectRenderInfo>();

        private List<_2DDrawBuffer> StaticObjectsCache = new List<_2DDrawBuffer>();
        private ScrollBuffer StaticObjects;

        private List<_2DDrawBuffer> StaticFloorCache = new List<_2DDrawBuffer>();
        private List<_2DDrawBuffer> StaticWallCache = new List<_2DDrawBuffer>();
        private ScrollBuffer StaticFloor;
        private ScrollBuffer StaticWall;

        private int TicksSinceLight = 0;

        public void Init(Blueprint blueprint)
        {
            this.Blueprint = blueprint;
        }

        private WorldObjectRenderInfo GetRenderInfo(WorldComponent component)
        {
            return ((ObjectComponent)component).renderInfo;
        }

        /// <summary>
        /// Gets an object's ID given an object's screen position.
        /// </summary>
        /// <param name="x">The object's X position.</param>
        /// <param name="y">The object's Y position.</param>
        /// <param name="gd">GraphicsDevice instance.</param>
        /// <param name="state">WorldState instance.</param>
        /// <returns>Object's ID if the object was found at the given position.</returns>
        public short GetObjectIDAtScreenPos(int x, int y, GraphicsDevice gd, WorldState state)
        {
            /** Draw all objects to a texture as their IDs **/
            var oldCenter = state.CenterTile;
            var tileOff = state.WorldSpace.GetTileFromScreen(new Vector2(x, y));
            state.CenterTile += tileOff;
            var pxOffset = state.WorldSpace.GetScreenOffset();
            var _2d = state._2D;
            Promise<Texture2D> bufferTexture = null;

            var worldBounds = new Rectangle((int)(-pxOffset).X, (int)(-pxOffset).Y, 1, 1);

            state.TempDraw = true;
            state._2D.OBJIDMode = true;
            state._3D.OBJIDMode = true;
            using (var buffer = state._2D.WithBuffer(BUFFER_OBJID, ref bufferTexture))
            {
                _2d.SetScroll(-pxOffset);
                
                while (buffer.NextPass())
                {
                    foreach (var obj in Blueprint.Objects) { 

                                var tilePosition = obj.Position;

                                if (obj.Level != state.Level) continue;

                                var oPx = state.WorldSpace.GetScreenFromTile(tilePosition);
                                obj.ValidateSprite(state);
                                var offBound = new Rectangle(obj.Bounding.Location.X + (int)oPx.X, obj.Bounding.Location.Y + (int)oPx.Y, obj.Bounding.Width, obj.Bounding.Height);
                                if (!offBound.Intersects(worldBounds)) continue;

                                var renderInfo = GetRenderInfo(obj);
                                
                                _2d.OffsetPixel(oPx);
                                _2d.OffsetTile(tilePosition);
                                _2d.SetObjID(obj.ObjectID);
                                obj.Draw(gd, state);
                    }

                    state._3D.Begin(gd);
                    foreach (var avatar in Blueprint.Avatars)
                    {
                        _2d.OffsetPixel(state.WorldSpace.GetScreenFromTile(avatar.Position));
                        _2d.OffsetTile(avatar.Position);
                        avatar.Draw(gd, state);
                    }
                    state._3D.End();
                }
                
            }
            state._3D.OBJIDMode = false;
            state._2D.OBJIDMode = false;
            state.TempDraw = false;
            state.CenterTile = oldCenter;

            var tex = bufferTexture.Get();
            Color[] data = new Color[1];
            tex.GetData<Color>(data);
            var f = Vector3.Dot(new Vector3(data[0].R / 255.0f, data[0].G / 255.0f, data[0].B / 255.0f), new Vector3(1.0f, 1/255.0f, 1/65025.0f));
            return (short)Math.Round(f*65535f);
        }

        /// <summary>
        /// Gets the current lot's thumbnail.
        /// </summary>
        /// <param name="objects">The object components to draw.</param>
        /// <param name="gd">GraphicsDevice instance.</param>
        /// <param name="state">WorldState instance.</param>
        /// <returns>Object's ID if the object was found at the given position.</returns>
        public virtual Texture2D GetLotThumb(GraphicsDevice gd, WorldState state, Action<Texture2D> rooflessCallback)
        {
            if (!(state.Camera is WorldCamera)) return new Texture2D(gd, 8, 8);
            var oldZoom = state.Zoom;
            var oldRotation = state.Rotation;
            var oldLevel = state.Level;
            var oldCutaway = Blueprint.Cutaway;
            var wCam = (WorldCamera)state.Camera;
            var oldViewDimensions = wCam.ViewDimensions;
            //wCam.ViewDimensions = new Vector2(-1, -1);
            var oldPreciseZoom = state.PreciseZoom;

            //full invalidation because we must recalculate all object sprites. slow but necessary!
            state.Zoom = WorldZoom.Far;
            state.Rotation = WorldRotation.TopLeft;
            state.Level = Blueprint.Stories;
            state.PreciseZoom = 1 / 4f;
            state._2D.PreciseZoom = state.PreciseZoom;
            state.WorldSpace.Invalidate();
            state.InvalidateCamera();

            var oldCenter = state.CenterTile;
            state.CenterTile -= state.WorldSpace.GetTileFromScreen(new Vector2((576 - state.WorldSpace.WorldPxWidth), (576 - state.WorldSpace.WorldPxHeight)) / 2);
            var pxOffset = -state.WorldSpace.GetScreenOffset();
            state.TempDraw = true;
            Blueprint.Cutaway = new bool[Blueprint.Cutaway.Length];

            var _2d = state._2D;
            Promise<Texture2D> bufferTexture = null;
            var lastLight = state.OutsideColor;
            state.OutsideColor = Color.White;
            state._2D.OBJIDMode = false;
            using (var buffer = state._2D.WithBuffer(BUFFER_THUMB_DEPTH, ref bufferTexture))
            {
                _2d.SetScroll(pxOffset);
                while (buffer.NextPass())
                {
                    _2d.Pause();
                    _2d.Resume();
                    //Blueprint.SetLightColor(WorldContent.GrassEffect, Color.White, Color.White);
                    Blueprint.Terrain.Draw(gd, state);
                    //Blueprint.Terrain.DrawMask(gd, state, state.Camera.View, state.Camera.Projection);
                    Blueprint.WallComp.Draw(gd, state);
                    _2d.Pause();
                    _2d.Resume();
                    foreach (var obj in Blueprint.Objects)
                    {
                        var renderInfo = GetRenderInfo(obj);
                        var tilePosition = obj.Position;
                        _2d.OffsetPixel(state.WorldSpace.GetScreenFromTile(tilePosition));
                        _2d.OffsetTile(tilePosition);
                        obj.Draw(gd, state);
                    }
                    _2d.Pause();
                    _2d.Resume();
                    rooflessCallback?.Invoke(bufferTexture.Get());
                    Blueprint.RoofComp.Draw(gd, state);
                }

            }

            Blueprint.Damage.Add(new BlueprintDamage(BlueprintDamageType.LIGHTING_CHANGED));
            Blueprint.Damage.Add(new BlueprintDamage(BlueprintDamageType.FLOOR_CHANGED));
            //return things to normal
            //state.PrepareLighting();
            state.OutsideColor = lastLight;
            state.PreciseZoom = oldPreciseZoom;
            state.WorldSpace.Invalidate();
            state.InvalidateCamera();
            wCam.ViewDimensions = oldViewDimensions;
            state.TempDraw = false;
            state.CenterTile = oldCenter;

            state.Zoom = oldZoom;
            state.Rotation = oldRotation;
            state.Level = oldLevel;
            Blueprint.Cutaway = oldCutaway;

            var tex = bufferTexture.Get();
            return tex; //TextureUtils.Clip(gd, tex, bounds);
        }


        /// <summary>
        /// Gets an object group's thumbnail provided an array of objects.
        /// </summary>
        /// <param name="objects">The object components to draw.</param>
        /// <param name="gd">GraphicsDevice instance.</param>
        /// <param name="state">WorldState instance.</param>
        /// <returns>Object's ID if the object was found at the given position.</returns>
        public Texture2D GetObjectThumb(ObjectComponent[] objects, Vector3[] positions, GraphicsDevice gd, WorldState state)
        {
            var oldZoom = state.Zoom;
            var oldRotation = state.Rotation;
            /** Center average position **/
            Vector3 average = new Vector3();
            for (int i = 0; i < positions.Length; i++)
            {
                average += positions[i];
            }
            average /= positions.Length;

            state.SilentZoom = WorldZoom.Near;
            state.SilentRotation = WorldRotation.BottomRight;
            state.WorldSpace.Invalidate();
            state.InvalidateCamera();
            state.TempDraw = true;
            var pxOffset = new Vector2(442, 275) - state.WorldSpace.GetScreenFromTile(average);

            var _2d = state._2D;
            Promise<Texture2D> bufferTexture = null;
            Promise<Texture2D> depthTexture = null;
            state._2D.OBJIDMode = false;
            Rectangle bounds = new Rectangle();
            using (var buffer = state._2D.WithBuffer(BUFFER_THUMB, ref bufferTexture, BUFFER_THUMB_DEPTH, ref depthTexture))
            {
                _2d.SetScroll(new Vector2());
                while (buffer.NextPass())
                {
                    for (int i=0; i<objects.Length; i++)
                    {
                        var obj = objects[i];
                        var tilePosition = positions[i];

                        //we need to trick the object into believing it is in a set world state.
                        var oldObjRot = obj.Direction;
                        var oldRoom = obj.Room;

                        obj.Direction = Direction.NORTH;
                        obj.Room = 65535;
                        state.SilentZoom = WorldZoom.Near;
                        state.SilentRotation = WorldRotation.BottomRight;
                        obj.OnRotationChanged(state);
                        obj.OnZoomChanged(state);

                        _2d.OffsetPixel(state.WorldSpace.GetScreenFromTile(tilePosition) + pxOffset);
                        _2d.OffsetTile(tilePosition);
                        _2d.SetObjID(obj.ObjectID);
                        obj.Draw(gd, state);

                        //return everything to normal
                        obj.Direction = oldObjRot;
                        obj.Room = oldRoom;
                        state.SilentZoom = oldZoom;
                        state.SilentRotation = oldRotation;
                        obj.OnRotationChanged(state);
                        obj.OnZoomChanged(state);
                    }
                    bounds = _2d.GetSpriteListBounds();
                }
            }
            bounds.X = Math.Max(0, Math.Min(1023, bounds.X));
            bounds.Y = Math.Max(0, Math.Min(1023, bounds.Y));
            if (bounds.Width + bounds.X > 1024) bounds.Width = 1024 - bounds.X;
            if (bounds.Height + bounds.Y > 1024) bounds.Height = 1024 - bounds.Y;

            //return things to normal
            state.WorldSpace.Invalidate();
            state.InvalidateCamera();
            state.TempDraw = false;

            var tex = bufferTexture.Get();
            return TextureUtils.Clip(gd, tex, bounds);
        }

        /// <summary>
        /// Prep work before screen is painted
        /// </summary>
        /// <param name="gd"></param>
        /// <param name="state"></param>
        public void PreDraw(GraphicsDevice gd, WorldState state)
        {
            var pxOffset = -state.WorldSpace.GetScreenOffset();
            var damage = Blueprint.Damage;
            var _2d = state._2D;

            /**
             * Tasks:
             *  If zoom or rotation has changed, redraw all static layers
             *  If scroll has changed, redraw static layer if the scroll is outwith the buffered region
             *  If architecture has changed, redraw appropriate static layer
             *  If there is a new object in the static layer, redraw the static layer
             *  If an objects in the static layer has changed, redraw the static layer and move the object to the dynamic layer
             *  If wall visibility has changed, redraw wall layer (should think about how this works with breakthrough wall mode
             */

            var redrawStaticObjects = false;
            var redrawFloor = false;
            var redrawWall = false;

            var recacheWalls = false;
            var recacheFloors = false;
            var recacheTerrain = false;
            var recacheObjects = false;

            if (TicksSinceLight++ > 60 * 4) damage.Add(new BlueprintDamage(BlueprintDamageType.LIGHTING_CHANGED));

            WorldObjectRenderInfo info = null;

            foreach (var item in damage){
                switch (item.Type){
                    case BlueprintDamageType.ROTATE:
                    case BlueprintDamageType.ZOOM:
                    case BlueprintDamageType.LEVEL_CHANGED:
                        recacheObjects = true;
                        recacheWalls = true;
                        recacheFloors = true;
                        recacheTerrain = true;
                        break;
                    case BlueprintDamageType.SCROLL:
                        if (StaticObjects == null || StaticObjects.PxOffset != GetScrollIncrement(pxOffset))
                        {
                            redrawFloor = true;
                            redrawWall = true;
                            redrawStaticObjects = true;
                        }
                        break;
                    case BlueprintDamageType.LIGHTING_CHANGED:
                        redrawFloor = true;
                        redrawWall = true;
                        redrawStaticObjects = true;

                        Blueprint.GenerateRoomLights();
                        state.OutsideColor = Blueprint.RoomColors[1];
                        state._3D.RoomLights = Blueprint.RoomColors;
                        state._2D.AmbientLight.SetData(Blueprint.RoomColors);
                        TicksSinceLight = 0;
                        break;
                    case BlueprintDamageType.OBJECT_MOVE:
                        /** Redraw if its in static layer **/
                        info = GetRenderInfo(item.Component);
                        if (info.Layer == WorldObjectRenderLayer.STATIC){
                            recacheObjects = true;
                            info.Layer = WorldObjectRenderLayer.DYNAMIC;
                        }
                        if (item.Component is ObjectComponent) ((ObjectComponent)item.Component).DynamicCounter = 0;
                        break;
                    case BlueprintDamageType.OBJECT_GRAPHIC_CHANGE:
                        /** Redraw if its in static layer **/
                        info = GetRenderInfo(item.Component);
                        if (info.Layer == WorldObjectRenderLayer.STATIC){
                            recacheObjects = true;
                            info.Layer = WorldObjectRenderLayer.DYNAMIC;
                        }
                        if (item.Component is ObjectComponent) ((ObjectComponent)item.Component).DynamicCounter = 0;
                        break;
                    case BlueprintDamageType.OBJECT_RETURN_TO_STATIC:
                        info = GetRenderInfo(item.Component);
                        if (info.Layer == WorldObjectRenderLayer.DYNAMIC)
                        {
                            recacheObjects = true;
                            info.Layer = WorldObjectRenderLayer.STATIC;
                        }
                        break;
                    case BlueprintDamageType.WALL_CUT_CHANGED:
                        recacheWalls = true;
                        break;
                    case BlueprintDamageType.ROOF_STYLE_CHANGED:
                        Blueprint.RoofComp.StyleDirty = true;
                        break;
                    case BlueprintDamageType.FLOOR_CHANGED:
                    case BlueprintDamageType.WALL_CHANGED:
                        recacheTerrain = true;
                        recacheFloors = true;
                        recacheWalls = true;
                        Blueprint.RoofComp.ShapeDirty = true;
                        break;
                }
                if (recacheFloors || recacheTerrain) redrawFloor = true;
                if (recacheWalls) redrawWall = true;
                if (recacheObjects) redrawStaticObjects = true;
            }
            damage.Clear();

            var tileOffset = state.WorldSpace.GetTileFromScreen(-pxOffset);
            //scroll buffer loads in increments of SCROLL_BUFFER
            var newOff = GetScrollIncrement(pxOffset);
            var oldCenter = state.CenterTile;
            state.CenterTile += state.WorldSpace.GetTileFromScreen(newOff-pxOffset); //offset the scroll to the position of the scroll buffer.
            tileOffset = state.CenterTile;

            pxOffset = newOff;

            if (recacheTerrain)
                Blueprint.Terrain.RegenTerrain(gd, state, Blueprint);

            if (recacheWalls)
            {
                _2d.Pause();
                _2d.Resume(); //clear the sprite buffer before we begin drawing what we're going to cache
                Blueprint.WallComp.Draw(gd, state);
                ClearDrawBuffer(StaticWallCache);
                _2d.End(StaticWallCache, true);
            }

            if (recacheFloors)
            {
                _2d.Pause();
                _2d.Resume(); //clear the sprite buffer before we begin drawing what we're going to cache
                Blueprint.FloorComp.Draw(gd, state);
                ClearDrawBuffer(StaticFloorCache);
                _2d.End(StaticFloorCache, true);
            }

            if (redrawFloor)
            {
                /** Draw archetecture to a texture **/
                Promise<Texture2D> bufferTexture = null;
                Promise<Texture2D> depthTexture = null;
                using (var buffer = state._2D.WithBuffer(BUFFER_FLOOR_PIXEL, ref bufferTexture, BUFFER_FLOOR_DEPTH, ref depthTexture))
                {
                    _2d.SetScroll(pxOffset);
                    while (buffer.NextPass())
                    {
                        _2d.RenderCache(StaticFloorCache);
                        Blueprint.Terrain.DepthMode = _2d.OutputDepth;
                        Blueprint.Terrain.Draw(gd, state);
                    }
                }
                StaticFloor = new ScrollBuffer(bufferTexture.Get(), depthTexture.Get(), pxOffset, new Vector3(tileOffset, 0));
            }

            if (redrawWall)
            {
                Promise<Texture2D> bufferTexture = null;
                Promise<Texture2D> depthTexture = null;
                using (var buffer = state._2D.WithBuffer(BUFFER_WALL_PIXEL, ref bufferTexture, BUFFER_WALL_DEPTH, ref depthTexture))
                {
                    _2d.SetScroll(pxOffset);
                    while (buffer.NextPass())
                    {
                        _2d.RenderCache(StaticWallCache);
                    }
                }
                StaticWall = new ScrollBuffer(bufferTexture.Get(), depthTexture.Get(), pxOffset, new Vector3(tileOffset, 0));
            }

            if (recacheObjects)
            {
                _2d.Pause();
                _2d.Resume();

                foreach (var obj in Blueprint.Objects)
                {
                    if (obj.Level > state.Level) continue;
                    var renderInfo = GetRenderInfo(obj);
                    if (renderInfo.Layer == WorldObjectRenderLayer.STATIC)
                    {
                        var tilePosition = obj.Position;
                        _2d.OffsetPixel(state.WorldSpace.GetScreenFromTile(tilePosition));
                        _2d.OffsetTile(tilePosition);
                        _2d.SetObjID(obj.ObjectID);
                        obj.Draw(gd, state);
                    }
                }
                ClearDrawBuffer(StaticObjectsCache);
                _2d.End(StaticObjectsCache, true);
            }

            if (redrawStaticObjects){
                /** Draw static objects to a texture **/
                Promise<Texture2D> bufferTexture = null;
                Promise<Texture2D> depthTexture = null;
                using (var buffer = state._2D.WithBuffer(BUFFER_STATIC_OBJECTS_PIXEL, ref bufferTexture, BUFFER_STATIC_OBJECTS_DEPTH, ref depthTexture))
                {
                    _2d.SetScroll(pxOffset);
                    while (buffer.NextPass())
                    {
                        _2d.RenderCache(StaticObjectsCache);
                    }
                }
                StaticObjects = new ScrollBuffer(bufferTexture.Get(), depthTexture.Get(), pxOffset, new Vector3(tileOffset, 0));
            }
            state.CenterTile = oldCenter; //revert to our real scroll position
        }

        public Vector2 GetScrollIncrement(Vector2 pxOffset)
        {
            return new Vector2((float)Math.Floor(pxOffset.X / SCROLL_BUFFER) * SCROLL_BUFFER, (float)Math.Floor(pxOffset.Y / SCROLL_BUFFER) * SCROLL_BUFFER);
        }

        /// <summary>
        /// Paint to screen
        /// </summary>
        /// <param name="gd"></param>
        /// <param name="state"></param>
        public void Draw(GraphicsDevice gd, WorldState state){

            var _2d = state._2D;
            /**
             * Draw static layers
             */
            _2d.OffsetPixel(Vector2.Zero);
            _2d.SetScroll(new Vector2());

            var pxOffset = -state.WorldSpace.GetScreenOffset();
            var tileOffset = state.CenterTile;
            _2d.Begin(state.Camera);
            if (StaticFloor != null)
            {
                _2d.DrawScrollBuffer(StaticFloor, pxOffset, new Vector3(tileOffset, 0), state);
                _2d.Pause();
                _2d.Resume();
            }
            if (StaticWall != null)
            {
                _2d.DrawScrollBuffer(StaticWall, pxOffset, new Vector3(tileOffset, 0), state);
                _2d.Pause();
                _2d.Resume();
            }
            if (StaticObjects != null)
            {
                _2d.DrawScrollBuffer(StaticObjects, pxOffset, new Vector3(tileOffset, 0), state);
                _2d.Pause();
                _2d.Resume();
            }

            _2d.End();
            _2d.Begin(state.Camera);

            /**
             * Draw dynamic objects. If an object has been static for X frames move it back into the static layer
             */

            _2d.SetScroll(pxOffset);

            var size = new Vector2(state.WorldSpace.WorldPxWidth, state.WorldSpace.WorldPxHeight);
            var mainBd = state.WorldSpace.GetScreenFromTile(state.CenterTile);
            var diff = pxOffset - mainBd;
            var worldBounds = new Rectangle((int)pxOffset.X, (int)pxOffset.Y, (int)size.X, (int)size.Y);

            foreach (var obj in Blueprint.Objects)
            {
                if (obj.Level > state.Level) continue;
                var renderInfo = GetRenderInfo(obj);
                if (renderInfo.Layer == WorldObjectRenderLayer.DYNAMIC)
                {
                    var tilePosition = obj.Position;
                    var oPx = state.WorldSpace.GetScreenFromTile(tilePosition);
                    obj.ValidateSprite(state);
                    var offBound = new Rectangle(obj.Bounding.Location.X + (int)oPx.X, obj.Bounding.Location.Y + (int)oPx.Y, obj.Bounding.Width, obj.Bounding.Height);
                    if (!offBound.Intersects(worldBounds)) continue;
                    _2d.OffsetPixel(oPx);
                    _2d.OffsetTile(tilePosition);
                    _2d.SetObjID(obj.ObjectID);
                    obj.Draw(gd, state);
                }
            }
        }

        public void ClearDrawBuffer(List<_2DDrawBuffer> buf)
        {
            foreach (var b in buf) b.Dispose();
            buf.Clear();
        }
    }

    public class WorldObjectRenderInfo
    {
        public WorldObjectRenderLayer Layer = WorldObjectRenderLayer.STATIC;
    }

    public enum WorldObjectRenderLayer
    {
        STATIC,
        DYNAMIC
    }

    public struct WorldTileRenderingInfo
    {
        public bool Dirty;
        public Texture2D Pixel;
        public Texture2D ZBuffer;
    }
}
