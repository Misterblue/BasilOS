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
using System.Drawing;
using System.IO;
using System.Text;

using log4net;

using RSG;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;

using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace org.herbal3d.BasilOS {

    class PrimToMesh : IDisposable {
        private OMVR.MeshmerizerR m_mesher;
        ILog m_log;
        String LogHeader = "[Basil.PrimToMesh]";

        public PrimToMesh(ILog logger) {
            m_mesher = new OMVR.MeshmerizerR();
            m_log = logger;
        }

        /// <summary>
        /// Create and return a faceted mesh.
        /// </summary>
        public IPromise<ExtendedPrimGroup> CreateMeshResource(SceneObjectGroup sog, SceneObjectPart sop,
                    OMV.Primitive prim, IAssetFetcherWrapper assetFetcher, OMVR.DetailLevel lod, BasilStats stats) {

            var prom = new Promise<ExtendedPrimGroup>();

            ExtendedPrimGroup mesh;
            try {
                if (prim.Sculpt != null) {
                    if (prim.Sculpt.Type == OMV.SculptType.Mesh) {
                        // m_log.DebugFormat("{0}: CreateMeshResource: creating mesh", LogHeader);
                        stats.numMeshes++;
                        MeshFromPrimMeshData(sog, sop, prim, assetFetcher, lod)
                            .Then(ePrimGroup => {
                                prom.Resolve(ePrimGroup);
                            })
                            .Catch(e => {
                                prom.Reject(e);
                            });
                    }
                    else {
                        // m_log.DebugFormat("{0}: CreateMeshResource: creating sculpty", LogHeader);
                        stats.numSculpties++;
                        MeshFromPrimSculptData(sog, sop, prim, assetFetcher, lod)
                            .Then(fm => {
                                prom.Resolve(fm);
                            })
                            .Catch(e => {
                                prom.Reject(e);
                            });
                    }
                }
                else {
                    // m_log.DebugFormat("{0}: CreateMeshResource: creating primshape", LogHeader);
                    stats.numPrims++;
                    mesh = MeshFromPrimShapeData(sog, sop, prim, lod);
                    prom.Resolve(mesh);
                }
            }
            catch (Exception e) {
                prom.Reject(e);
            }

            return prom;
        }

        private ExtendedPrimGroup MeshFromPrimShapeData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, OMVR.DetailLevel lod) {
            OMVR.FacetedMesh mesh;

            mesh = m_mesher.GenerateFacetedMesh(prim, lod);

            ExtendedPrim extPrim = new ExtendedPrim(sog, sop, prim, mesh);

            // m_log.DebugFormat("{0} MeshFromPrimShapeData. faces={1}", LogHeader, mesh.Faces.Count);

            ExtendedPrimGroup extPrimGroup = new ExtendedPrimGroup(extPrim);

            return extPrimGroup;
        }

        private IPromise<ExtendedPrimGroup> MeshFromPrimSculptData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, IAssetFetcherWrapper assetFetcher, OMVR.DetailLevel lod) {

            var prom = new Promise<ExtendedPrimGroup>();

            // Get the asset that the sculpty is built on
            EntityHandle texHandle = new EntityHandle(prim.Sculpt.SculptTexture);
            assetFetcher.FetchTexture(texHandle)
                .Then((bm) => {
                    OMVR.FacetedMesh fMesh = m_mesher.GenerateFacetedSculptMesh(prim, bm.Image.ExportBitmap(), lod);

                    ExtendedPrim extPrim = new ExtendedPrim(sog, sop, prim, fMesh);

                    // if (fMesh.Faces.Count == 1) {
                    //     m_log.DebugFormat("{0} MeshFromSculptData. verts={1}, ind={2}", LogHeader,
                    //             fMesh.Faces[0].Vertices.Count, fMesh.Faces[0].Indices.Count);
                    // }
                    // else {
                    //     m_log.DebugFormat("{0} MeshFromSculptData. faces={1}", LogHeader, fMesh.Faces.Count);
                    // }

                    ExtendedPrimGroup extPrimGroup = new ExtendedPrimGroup(extPrim);

                    prom.Resolve(extPrimGroup);
                })
                .Catch((e) => {
                    m_log.ErrorFormat("{0} MeshFromPrimSculptData: Rejected FetchTexture: {1}: {2}", LogHeader, texHandle, e);
                    prom.Reject(e);
                });

            return prom;
        }

        private IPromise<ExtendedPrimGroup> MeshFromPrimMeshData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, IAssetFetcherWrapper assetFetcher, OMVR.DetailLevel lod) {

            var prom = new Promise<ExtendedPrimGroup>();

            // Get the asset that the mesh is built on
            EntityHandle meshHandle = new EntityHandle(prim.Sculpt.SculptTexture);
            try {
                assetFetcher.FetchRawAsset(meshHandle)
                    .Then(meshBytes => {
                        OMVA.AssetMesh meshAsset = new OMVA.AssetMesh(prim.ID, meshBytes);
                        OMVR.FacetedMesh fMesh;
                        if (OMVR.FacetedMesh.TryDecodeFromAsset(prim, meshAsset, lod, out fMesh)) {
                            ExtendedPrim extPrim = new ExtendedPrim(sog, sop, prim, fMesh);
                            // m_log.DebugFormat("{0} MeshFromPrimMeshData: created mesh.. Faces={1}", 
                            //                     LogHeader, extPrim.facetedMesh.Faces.Count);

                            ExtendedPrimGroup eGroup = new ExtendedPrimGroup(extPrim);
                            prom.Resolve(eGroup);
                        }
                        else {
                            prom.Reject(new Exception("MeshFromPrimMeshData: could not decode mesh information from asset. ID="
                                            + prim.ID.ToString()));
                        }
                    })
                    .Catch((e) => {
                        m_log.ErrorFormat("{0} MeshFromPrimSculptData: Rejected FetchTexture: {1}", LogHeader, e);
                        prom.Reject(e);
                    });
            }
            catch (Exception e) {
                prom.Reject(e);
            }

            return prom;
        }

        public void Dispose() {
            m_mesher = null;
        }

        public void UpdateCoords(OMVR.Face pFace, OMV.Primitive.TextureEntryFace pTef) {
            if (pFace.Vertices != null) {
                m_mesher.TransformTexCoords(pFace.Vertices, pFace.Center, pTef, new OMV.Vector3(1,1,1));
            }
        }

        // Walk through all the vertices and scale the included meshes

        public static void ScaleMeshes(ExtendedPrimGroup ePG) {
            foreach (ExtendedPrim ep in ePG.Values) {
                OMV.Vector3 scale = ep.primitive.Scale;
                if (scale.X != 1.0 || scale.Y != 1.0 || scale.Z != 1.0) {
                    OnAllVertex(ep, delegate (ref OMVR.Vertex vert) {
                        vert.Position *= scale;
                    });
                }
            }
        }

        // Loop over all the vertices in an ExtendedPrim and perform some operation on them
        public delegate void OperateOnVertex(ref OMVR.Vertex vert);
        public static void OnAllVertex(ExtendedPrim ep, OperateOnVertex vertOp) {
            // DEBUG DEBUG DumpScaleTest(ep, "Before");
            for (int ii = 0; ii < ep.facetedMesh.Faces.Count; ii++) {
                OMVR.Face aFace = ep.facetedMesh.Faces[ii];
                for (int jj = 0; jj < aFace.Vertices.Count; jj++) {
                    OMVR.Vertex aVert = aFace.Vertices[jj];
                    vertOp(ref aVert);
                    aFace.Vertices[jj] = aVert;
                }
                ep.facetedMesh.Faces[ii] = aFace;
            }
        }
    }
}
