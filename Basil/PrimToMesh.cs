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

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;

using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;


namespace org.herbal3d.Basil {

    class PrimToMesh : IDisposable {
        OMVR.MeshmerizerR m_mesher;
        ILog m_log;
        String LogHeader = "PrimToMesh:";

        public PrimToMesh(ILog logger) {
            m_mesher = new OMVR.MeshmerizerR();
            m_log = logger;
        }

        /// <summary>
        /// Create and return a faceted mesh.
        /// </summary>
        public SimplePromise<ExtendedPrimGroup> CreateMeshResource(SceneObjectGroup sog, SceneObjectPart sop,
                    OMV.Primitive prim, IAssetFetcherWrapper assetFetcher, OMVR.DetailLevel lod) {

            SimplePromise<ExtendedPrimGroup> prom = new SimplePromise<ExtendedPrimGroup>();

            ExtendedPrimGroup mesh;
            try {
                if (prim.Sculpt != null) {
                    if (prim.Sculpt.Type == OMV.SculptType.Mesh) {
                        m_log.DebugFormat("{0}: CreateMeshResource: creating mesh", LogHeader);
                        MeshFromPrimMeshData(sog, sop, prim, assetFetcher, lod)
                            .Then(ePrimGroup => {
                                prom.Resolve(ePrimGroup);
                            })
                            .Rejected(e => {
                                prom.Reject(e);
                            });
                    }
                    else {
                        m_log.DebugFormat("{0}: CreateMeshResource: creating sculpty", LogHeader);
                        MeshFromPrimSculptData(sog, sop, prim, assetFetcher, lod)
                            .Then(fm => {
                                prom.Resolve(fm);
                            })
                            .Rejected(e => {
                                prom.Reject(e);
                            });
                    }
                }
                else {
                    m_log.DebugFormat("{0}: CreateMeshResource: creating primshape", LogHeader);
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

            OMVR.FacetedMesh mesh = m_mesher.GenerateFacetedMesh(prim, lod);

            ExtendedPrim extPrim = new ExtendedPrim();
            extPrim.facetedMesh = mesh;
            extPrim.SOG = sog;
            extPrim.SOP = sop;
            extPrim.primitive = prim;

            m_log.DebugFormat("{0} MeshFromPrimShapeData. faces={1}", LogHeader, mesh.Faces.Count);

            ExtendedPrimGroup extPrimGroup = new ExtendedPrimGroup();
            extPrimGroup.Add(PrimGroupType.lod1, extPrim);

            return extPrimGroup;
        }

        private SimplePromise<ExtendedPrimGroup> MeshFromPrimSculptData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, IAssetFetcherWrapper assetFetcher, OMVR.DetailLevel lod) {

            SimplePromise<ExtendedPrimGroup> prom = new SimplePromise<ExtendedPrimGroup>();

            // Get the asset that the sculpty is built on
            EntityHandle texHandle = new EntityHandle(prim.Sculpt.SculptTexture);
            assetFetcher.FetchTexture(texHandle)
                .Then((bm) => {
                    OMVR.FacetedMesh fMesh = m_mesher.GenerateFacetedSculptMesh(prim, bm.Image.ExportBitmap(), lod);

                    ExtendedPrim extPrim = new ExtendedPrim();
                    extPrim.facetedMesh = fMesh;
                    extPrim.SOG = sog;
                    extPrim.SOP = sop;
                    extPrim.primitive = prim;

                    if (fMesh.Faces.Count == 1) {
                        m_log.DebugFormat("{0} MeshFromSculptData. verts={1}, ind={2}", LogHeader,
                                fMesh.Faces[0].Vertices.Count, fMesh.Faces[0].Indices.Count);
                    }
                    else {
                        m_log.DebugFormat("{0} MeshFromSculptData. faces={1}", LogHeader, fMesh.Faces.Count);
                    }

                    ExtendedPrimGroup extPrimGroup = new ExtendedPrimGroup();
                    extPrimGroup.Add(PrimGroupType.lod1, extPrim);

                    prom.Resolve(extPrimGroup);
                })
                .Rejected((e) => {
                    m_log.ErrorFormat("{0} MeshFromPrimSculptData: Rejected FetchTexture: {1}: {2}", LogHeader, texHandle, e);
                    prom.Reject(e);
                });

            return prom;
        }

        private SimplePromise<ExtendedPrimGroup> MeshFromPrimMeshData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, IAssetFetcherWrapper assetFetcher, OMVR.DetailLevel lod) {

            SimplePromise<ExtendedPrimGroup> prom = new SimplePromise<ExtendedPrimGroup>();

            // Get the asset that the mesh is built on
            EntityHandle meshHandle = new EntityHandle(prim.Sculpt.SculptTexture);
            try {
                assetFetcher.FetchRawAsset(meshHandle)
                    .Then(meshBytes => {
                        ExtendedPrimGroup extPrimGroup = UnpackMeshData(prim, meshBytes);
                        prom.Resolve(extPrimGroup);
                    })
                    .Rejected((e) => {
                        m_log.ErrorFormat("{0} MeshFromPrimSculptData: Rejected FetchTexture: {1}", LogHeader, e);
                        prom.Reject(e);
                    });
            }
            catch (Exception e) {
                prom.Reject(e);
            }

            return prom;
        }

        // =========================================================
        public ExtendedPrimGroup UnpackMeshData(OMV.Primitive prim, byte[] rawMeshData) {
            ExtendedPrimGroup subMeshes = new ExtendedPrimGroup();

            OMVS.OSDMap meshOsd = new OMVS.OSDMap();
            List<PrimMesher.Coord> coords = new List<PrimMesher.Coord>();
            List<PrimMesher.Face> faces = new List<PrimMesher.Face>();

            long start = 0;
            using (MemoryStream data = new MemoryStream(rawMeshData)) {
                try {
                    OMVS.OSD osd = OMVS.OSDParser.DeserializeLLSDBinary(rawMeshData);
                    if (osd is OMVS.OSDMap)
                        meshOsd = (OMVS.OSDMap)osd;
                    else {
                        throw new Exception("UnpackMeshData: parsing mesh data did not return an OSDMap");
                    }
                }
                catch (Exception e) {
                    m_log.Error("UnpackMeshData: Exception deserializing mesh asset header:" + e.ToString());
                }
                start = data.Position;
            }

            Dictionary<String, String> lodSections = new Dictionary<string, string>() {
                {"high_lod", "lod1" },
                {"medium_lod", "lod2" },
                {"low_lod", "lod3" },
                {"lowest_lod", "lod4" },
                {"physics_shape", "lod4" },
                {"physics_mesh", "lod4" },
                {"physics_convex", "lod4" },
            };

            // TODO: refactor mesh unpacker in ubMeshmerizer to allow unpacking the various mesh versions rather
            //     than just unpacking the physical version
            foreach (KeyValuePair<string,string> lodSection in lodSections) {
            }

            return subMeshes;
        }

        public void Dispose() {
            m_mesher = null;
        }
    }
}
