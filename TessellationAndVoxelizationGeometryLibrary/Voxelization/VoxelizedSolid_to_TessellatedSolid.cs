﻿// ***********************************************************************
// Assembly         : TessellationAndVoxelizationGeometryLibrary
// Author           : Design Engineering Lab
// Created          : 02-27-2015
//
// Last Modified By : Matt Campbell
// Last Modified On : 09-21-2017
// ***********************************************************************
// <copyright file="VoxelizedSolid.cs" company="Design Engineering Lab">
//     Copyright ©  2017
// </copyright>
// <summary></summary>
// ***********************************************************************

using StarMathLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace TVGL.Voxelization
{
    /// <summary>
    /// Class VoxelizedSolid.
    /// </summary>
    public partial class VoxelizedSolid
    {

        public TessellatedSolid ConvertToTessellatedSolid(double minEdgeLength = -1)
        {
            var faceCollection = new ConcurrentBag<PolygonalFace>();
            var voxelVertexDictionary = Voxels(VoxelRoleTypes.Partial).ToDictionary(v => v.ID, v => new Vertex(v.BottomCoordinate));
            var boundaryVertexDictionary = new Dictionary<long, Vertex>();

            //Parallel.ForEach(Voxels(VoxelRoleTypes.Partial), v =>
            foreach(var v in Voxels(VoxelRoleTypes.Partial))
            {
                var neighbors = GetNeighbors(v);
                var neighborX = neighbors[3];
                var neighborY = neighbors[4];
                var neighborZ = neighbors[5];
                var neighborXY = neighborX == null ? null : GetNeighbor(neighborX, VoxelDirections.YPositive);
                var neighborYZ = neighborY == null ? null : GetNeighbor(neighborY, VoxelDirections.ZPositive);
                var neighborZX = neighborX == null ? null : GetNeighbor(neighborX, VoxelDirections.ZPositive);
                var neighborXYZ = neighborXY == null ? null : GetNeighbor(neighborZX, VoxelDirections.ZPositive);
                var sideLength = VoxelSideLengths[v.Level];
                var vertexbase = voxelVertexDictionary[v.ID];
                var vertexX = neighborX != null ? GetOrMakeVoxelVertex(neighborX, voxelVertexDictionary)
                    : MakeBoundaryVertex(v, boundaryVertexDictionary, new[] { sideLength, 0, 0 });
                var vertexY = neighborY != null ? GetOrMakeVoxelVertex(neighborY, voxelVertexDictionary)
                    : MakeBoundaryVertex(v, boundaryVertexDictionary, new[] { 0.0, sideLength, 0 });
                var vertexZ = neighborZ != null ? GetOrMakeVoxelVertex(neighborZ, voxelVertexDictionary)
                    : MakeBoundaryVertex(v, boundaryVertexDictionary, new[] { 0.0, 0.0, sideLength });
                var vertexXY = neighborXY != null ? GetOrMakeVoxelVertex(neighborXY, voxelVertexDictionary)
                    : MakeBoundaryVertex(v, boundaryVertexDictionary, new[] { sideLength, sideLength, 0 });
                var vertexYZ = neighborYZ != null ? GetOrMakeVoxelVertex(neighborYZ, voxelVertexDictionary)
                    : MakeBoundaryVertex(v, boundaryVertexDictionary, new[] { 0.0, sideLength, sideLength, });
                var vertexZX = neighborZX != null ? GetOrMakeVoxelVertex(neighborZX, voxelVertexDictionary)
                    : MakeBoundaryVertex(v, boundaryVertexDictionary, new[] { sideLength, 0.0, sideLength });
                var vertexXYZ = neighborXYZ != null ? GetOrMakeVoxelVertex(neighborXYZ, voxelVertexDictionary)
                    : MakeBoundaryVertex(v, boundaryVertexDictionary, new[] { sideLength, sideLength, sideLength });
                if (neighbors[0] == null || neighbors[0].Role == VoxelRoleTypes.Empty)
                    MakeFaces(faceCollection, vertexX,vertexbase, vertexY, vertexXY);
                if (neighbors[1] == null || neighbors[1].Role == VoxelRoleTypes.Empty)
                    MakeFaces(faceCollection, vertexbase, vertexX, vertexZX, vertexZ);
                if (neighbors[2] == null || neighbors[2].Role == VoxelRoleTypes.Empty)
                    MakeFaces(faceCollection, vertexbase, vertexZ, vertexYZ, vertexY);
                if (neighborX == null || neighborX.Role == VoxelRoleTypes.Empty)
                    MakeFaces(faceCollection, vertexZX, vertexX, vertexXY, vertexXYZ);
                if (neighborY == null || neighborY.Role == VoxelRoleTypes.Empty)
                    MakeFaces(faceCollection, vertexYZ, vertexXYZ, vertexXY, vertexY);
                if (neighborZ == null || neighborZ.Role == VoxelRoleTypes.Empty)
                    MakeFaces(faceCollection, vertexZ, vertexZX, vertexXYZ, vertexYZ);
            } //);
            var vertices = voxelVertexDictionary.Values.ToList();
            vertices.AddRange(boundaryVertexDictionary.Values);
            return new TessellatedSolid(faceCollection.ToList(), vertices);
        }

        private void MakeFaces(ConcurrentBag<PolygonalFace> faceCollection, Vertex v1, Vertex v2, Vertex v3, Vertex v4)
        {
            faceCollection.Add(new PolygonalFace(new[] { v1, v2, v3 }));
            faceCollection.Add(new PolygonalFace(new[] { v1, v3, v4 }));
        }

        private Vertex MakeBoundaryVertex(IVoxel baseVoxel, Dictionary<long, Vertex> boundaryDictionary, double[] shift)
        {
            var hash = baseVoxel.ID;
            if (shift[0] > 0) hash = hash & Constants.maskOutX;
            if (shift[1] > 0) hash = hash & Constants.maskOutY;
            if (shift[2] > 0) hash = hash & Constants.maskOutZ;
            lock (boundaryDictionary)
            {
                if (boundaryDictionary.ContainsKey(hash))
                    return boundaryDictionary[hash];
                var newVertex = new Vertex(baseVoxel.BottomCoordinate.add(shift));
                boundaryDictionary.Add(hash, newVertex);
                return newVertex;
            }
        }
        private Vertex GetOrMakeVoxelVertex(IVoxel baseVoxel, Dictionary<long, Vertex> voxelDictionary)
        {
            lock (voxelDictionary)
            {
                if (voxelDictionary.ContainsKey(baseVoxel.ID))
                    return voxelDictionary[baseVoxel.ID];
                var newVertex = new Vertex(baseVoxel.BottomCoordinate);
                voxelDictionary.Add(baseVoxel.ID, newVertex);
                return newVertex;
            }
        }

    }
}