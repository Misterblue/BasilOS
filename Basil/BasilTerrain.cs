/*
 * Copyright (c) 2016 Robert Adams
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;

namespace org.herbal3d.BasilOS {
    public class BasilTerrain {

        // A structure to hold vertex information that also includes the index for building indices.
        private struct Vert {
            public OMV.Vector3 Position;
            public OMV.Vector3 Normal;
            public OMV.Vector2 TexCoord;
            public ushort index;
        }

        // PrimMesher has a terrain mesh generator but it doesn't compute normals.
        // TODO: Optimize by removing vertices that are just mid points.
        //    Having a vertex for every height is very inefficient especially for flat areas.
        public static OMVR.Face TerrainMesh(float[,] heights, float realSizeX, float realSizeY) {

            List<ushort> indices = new List<ushort>();

            int sizeX = heights.GetLength(0);
            int sizeY = heights.GetLength(1);

            // build the vertices in an array for computing normals and eventually for
            //    optimizations.
            Vert[,] vertices = new Vert[sizeX, sizeY];

            float stepX = realSizeX / sizeX;    // the real dimension step for each heightmap step
            float stepY = realSizeY / sizeY;
            float coordStepX = realSizeX / sizeX;    // the coordinate dimension step for each heightmap step
            float coordStepY = realSizeY / sizeY;

            ushort index = 0;
            for (int xx = 0; xx < sizeX; xx++) {
                for (int yy = 0; yy < sizeY; yy++) {
                    Vert vert = new Vert();
                    vert.Position = new OMV.Vector3(stepX * xx, stepY * yy, heights[xx, yy]);
                    vert.Normal = new OMV.Vector3(0f, 1f, 0f);  // normal pointing up for the moment
                    vert.TexCoord = new OMV.Vector2(coordStepX * xx, coordStepY * yy);
                    vert.index = index++;
                    vertices[xx, yy] = vert;
                }
            }

            // Compute the normals
            // Take three corners of each quad and calculate the normal for the vector
            //   a--b--e--...
            //   |  |  |
            //   d--c--h--...
            // The triangle a-b-d calculates the normal for a, etc
            for (int xx = 0; xx < sizeX-1; xx++) {
                for (int yy = 0; yy < sizeY-1; yy++) {
                    vertices[xx,yy].Normal = MakeNormal(vertices[xx, yy], vertices[xx + 1, yy], vertices[xx, yy + 1]);
                }
            }
            // The vertices along the edges need an extra pass to compute the normals
            for (int xx = 0; xx < sizeX-1 ; xx++) {
                vertices[xx, sizeY - 1].Normal = MakeNormal(vertices[xx, sizeY - 1], vertices[xx + 1, sizeY - 1], vertices[xx, sizeY - 2]);
            }
            for (int yy = 0; yy < sizeY - 1; yy++) {
                vertices[sizeX -1, yy].Normal = MakeNormal(vertices[sizeX -1 , yy], vertices[sizeX - 1, yy + 1], vertices[sizeX - 2, yy]);
            }
            vertices[sizeX -1, sizeY - 1].Normal = MakeNormal(vertices[sizeX -1 , sizeY - 1], vertices[sizeX - 2, sizeY - 1], vertices[sizeX - 1, sizeY - 2]);

            // Make indices for all the vertices.
            // Pass over the matrix and create two triangles for each quad
            // Counter Clockwise
            for (int xx = 0; xx < sizeX - 1; xx++) {
                for (int yy = 0; yy < sizeY - 1; yy++) {
                    indices.Add(vertices[xx + 0, yy + 0].index);
                    indices.Add(vertices[xx + 1, yy + 0].index);
                    indices.Add(vertices[xx + 0, yy + 1].index);
                    indices.Add(vertices[xx + 0, yy + 1].index);
                    indices.Add(vertices[xx + 1, yy + 0].index);
                    indices.Add(vertices[xx + 1, yy + 1].index);
                }
            }

            // Listify the vertices
            List<OMVR.Vertex> vertexList = new List<OMVR.Vertex>();
            for (int xx = 0; xx < sizeX; xx++) {
                for (int yy = 0; yy < sizeY; yy++) {
                    Vert vert = vertices[xx, yy];
                    OMVR.Vertex oVert = new OMVR.Vertex();
                    oVert.Position = vert.Position;
                    oVert.Normal = vert.Normal;
                    oVert.TexCoord = vert.TexCoord;
                    vertexList.Add(oVert);
                }
            }
            OMVR.Face aface = new OMVR.Face();
            aface.Vertices = vertexList;
            aface.Indices = indices;
            return aface;
        }

        // Given a root (aa) and two adjacent vertices (bb, cc), computer the normal for aa
        private static OMV.Vector3 MakeNormal(Vert aa, Vert bb, Vert cc) {
            OMV.Vector3 mm = aa.Position - bb.Position;
            OMV.Vector3 nn = aa.Position - cc.Position;
            OMV.Vector3 theNormal = OMV.Vector3.Cross(mm, nn);
            theNormal.Normalize();
            return theNormal;
        }
    }
}
