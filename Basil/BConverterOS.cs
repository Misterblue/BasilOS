/*
 * Copyright (c) 2017 Robert Adams
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
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using log4net;

using RSG;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;


namespace org.herbal3d.BasilOS {
    // Convert things from OpenSimulator to EntityGroup things
    public class BConverterOS {

        private static string _logHeader = "BConverterOS";

        private IAssetFetcher _assetFetcher;
        private PrimToMesh _mesher;
        private BasilModuleContext _context;

        // Lists of similar faces indexed by the texture hash
        public class SimilarFaces : Dictionary<BHash, List<FaceInfo>> {
            public SimilarFaces() : base() {
            }
            public void AddSimilarFace(BHash pHash, FaceInfo pFace) {
                if (! this.ContainsKey(pHash)) {
                    this.Add(pHash, new List<FaceInfo>());
                }
                this[pHash].Add(pFace);
            }
        }

        public BConverterOS(IAssetFetcher assetFetcher, PrimToMesh mesher, BasilModuleContext context) {
            _assetFetcher = assetFetcher;
            _mesher = mesher;
            _context = context;
        }

        // Convert a SceneObjectGroup into an EntityGroup
        public IPromise<EntityGroup> Convert(SceneObjectGroup sog, IAssetFetcher assetFetcher) {
            var prom = new Promise<EntityGroup>();

            // Create meshes for all the parts of the SOG
            Promise<ExtendedPrimGroup>.All(
                sog.Parts.Select(sop => {
                    OMV.Primitive aPrim = sop.Shape.ToOmvPrimitive();
                    return _mesher.CreateMeshResource(sog, sop, aPrim, _assetFetcher, OMVR.DetailLevel.Highest, _context.stats);
                } )
            )
            // Tweak the converted parts individually (scale, texturize, ...)
            .Then(epgs => {
                return epgs.Select(epg => {
                    // If scaling is done in the mesh, do it now
                    if (!_context.parms.DisplayTimeScaling) {
                        PrimToMesh.ScaleMeshes(epg);
                        foreach (ExtendedPrim ep in epg.Values) {
                            ep.scale = new OMV.Vector3(1, 1, 1);
                        }
                    }

                    // The prims in the group need to be decorated with texture/image information
                    UpdateTextureInfo(epg, assetFetcher);

                    return epg;
                });
            })
            .Catch(e => {
                _context.log.ErrorFormat("{0} Failed meshing of SOG. ID={1}: {2}", _logHeader, sog.UUID, e);
                prom.Reject(new Exception(String.Format("failed meshing of SOG. ID={0}: {1}", sog.UUID, e)));
            })
            .Done (epgs => {
                prom.Resolve(new EntityGroup(epgs.ToList()));
            }) ;

            return prom;
        }

        // Convert a SceneObjectPart into an ExtendedPrimGroup
        public IPromise<ExtendedPrimGroup> Convert(SceneObjectGroup sog, SceneObjectPart sop) {
            return null;
        }

        /// <summary>
        /// Scan through all the ExtendedPrims and finish any texture updating.
        /// This includes UV coordinate mappings and fetching any image that goes with the texture.
        /// </summary>
        /// <param name="epGroup">Collections of meshes to update</param>
        /// <param name="assetFetcher">Fetcher for getting images, etc</param>
        /// <param name="pMesher"></param>
        private void UpdateTextureInfo(ExtendedPrimGroup epGroup, IAssetFetcher assetFetcher) {
            ExtendedPrim ep = epGroup.primaryExtendePrim;
            foreach (FaceInfo faceInfo in ep.faces) {

                // While we're in the neighborhood, map the texture coords based on the prim information
                _mesher.UpdateCoords(faceInfo, ep.fromOS.primitive);

                UpdateFaceInfoWithTexture(faceInfo, assetFetcher);
            }
        }

        // Check to see if the FaceInfo has a textureID and, if so, read it in and populate the FaceInfo
        //    with that texture data.
        public void UpdateFaceInfoWithTexture(FaceInfo faceInfo, IAssetFetcher assetFetcher) {
            // If the texture includes an image, read it in.
            OMV.UUID texID = faceInfo.textureEntry.TextureID;
            try {
                faceInfo.hasAlpha = (faceInfo.textureEntry.RGBA.A != 1.0f);
                if (texID != OMV.UUID.Zero && texID != OMV.Primitive.TextureEntry.WHITE_TEXTURE) {
                    faceInfo.textureID = texID;
                    faceInfo.persist = new BasilPersist(Gltf.MakeAssetURITypeImage, texID.ToString(), _context);
                    faceInfo.persist.GetUniqueTextureData(faceInfo, assetFetcher)
                        .Catch(e => {
                            // Could not get the texture. Print error and otherwise blank out the texture
                            faceInfo.textureID = null;
                            faceInfo.faceImage = null;
                            _context.log.ErrorFormat("{0} UpdateTextureInfo. {1}", _logHeader, e);
                        })
                        .Then(imgInfo => {
                            faceInfo.faceImage = imgInfo.image;
                            faceInfo.hasAlpha |= imgInfo.hasTransprency;
                        });
                }
            }
            catch (Exception e) {
                _context.log.ErrorFormat("{0}: UpdateFaceInfoWithTexture: exception updating faceInfo. id={1}: {2}",
                                    _logHeader, texID, e);
            }
        }

        // Loop through all the shared faces (faces that share the same material) and create
        //    one mesh for all the faces. This entails selecting one of the faces to be the
        //    root face and then displacing all the vertices, rotations, ... to be based
        //    from that root face.
        // We find the root face by looking for one "in the middle"ish so as to keep the offset
        //    math as small as possible.
        // This creates reorgScene.rebuildFaceEntities from reorgScene.similarFaces.
        public EntityGroupList ConvertEntitiesIntoSharedMaterialMeshes(EntityGroupList staticEntities) {

            // Go through all the static items and make a list of all the meshes with similar textures
            SimilarFaces similarFaces = new SimilarFaces();
            staticEntities.ForEachExtendedPrim(ep => {
                ep.faces.ForEach(faceInfo => {
                    OMV.Primitive.TextureEntryFace tef = faceInfo.textureEntry;
                    BHash hashCode = new BHashULong((ulong)tef.GetHashCode());
                    similarFaces.AddSimilarFace(hashCode, faceInfo);
                });
            });

            EntityGroupList rebuilt = new EntityGroupList(
                similarFaces.Values.Select(similarFaceList => {
                    var ep = CreateExtendedPrimFromSimilarFaces(similarFaceList);
                    // The created ExtendedPrim needs to be packaged into an EntityGroup
                    var eg = new EntityGroup();
                    eg.Add(new ExtendedPrimGroup(ep));
                    return eg;
                }).ToList()
            );

            return rebuilt;
        }

        // Check all the faces in an EntityGroup (usually a single SL entity) and
        //    merge faces using the same material into single meshes.
        // This reduces large linksets into smaller sets of meshes and also merges
        //    similar prim faces into single meshes.
        public EntityGroup ConvertEntityGroupIntoSharedMaterialMeshes(EntityGroup eg) {
            if (eg.Count == 1 && eg.First().primaryExtendePrim.faces.Count == 1) {
                // if there is only one entity and that entity has only one mesh, just return
                //     the thing passed.
                _context.log.DebugFormat("{0} ConvertEntityGroupIntoSharedMaterialMeshes: only one face in one entity.", _logHeader);
                return eg;
            }

            // Go through all the materialed meshes and see if there are meshes to share
            SimilarFaces similarFaces = new SimilarFaces();
            // int totalFaces = 0; // DEBUG DEBUG
            eg.ForEach(epg => {
                ExtendedPrim ep = epg.primaryExtendePrim;
                ep.faces.ForEach(faceInfo => {
                    OMV.Primitive.TextureEntryFace tef = faceInfo.textureEntry;
                    BHash hashCode = new BHashULong((ulong)tef.GetHashCode());
                    similarFaces.AddSimilarFace(hashCode, faceInfo);
                    // totalFaces++;
                });
            });
            // m_log.DebugFormat("{0} ConvertEntityGroupIntoSharedMaterialMeshes: EGs={1}, totalFaces={2}, similarFaces={3}",
            //         _logHeader, eg.Count, totalFaces, similarFaces.Count);

            EntityGroup rebuilt = new EntityGroup(
                similarFaces.Values.Select(similarFaceList => {
                    return new ExtendedPrimGroup(CreateExtendedPrimFromSimilarFaces(similarFaceList));
                }).ToList()
            );

            _context.log.DebugFormat("{0} ConvertEntityGroupIntoSharedMaterialMeshes: after build: {1}", _logHeader, rebuilt.Stats());

            return rebuilt;
        }

        // Given a list of faces, merge the meshes into a single mesh.
        // The returned ExtendedPrim has a location in the world and all the mesh vertices
        //    have been moved and oriented to that new location.
        private ExtendedPrim CreateExtendedPrimFromSimilarFaces(List<FaceInfo> similarFaceList) {
            // Loop through the faces and find the root. If this is faces from a single linkset, this
            //    will find the root prim as  the reference. Otherwise it will just find some root
            //    prim.
            // There might be a need to find the 'middle' prim of a cluster if position jitter
            //    becomes a problem.
            FaceInfo rootFace = null;
            foreach (FaceInfo faceInfo in similarFaceList) {
                if (faceInfo.containingPrim != null && faceInfo.containingPrim.isRoot) {
                    rootFace = faceInfo;
                    break;
                }
            }
            if (rootFace == null) {
                // If there wasn't a root entity in the list, just pick a random one
                rootFace = similarFaceList.First();
            }
            ExtendedPrim rootEp = rootFace.containingPrim;

            // Create the new combined object
            ExtendedPrim newEp = new ExtendedPrim(rootEp);
            newEp.ID = OMV.UUID.Random();
            newEp.coordSystem = rootEp.coordSystem;
            newEp.isRoot = true;
            newEp.positionIsParentRelative = false;

            // The merged mesh is located at the root's location with no rotation
            if (rootEp.fromOS.SOP != null) {
                newEp.translation = rootEp.fromOS.SOP.GetWorldPosition();
            }
            else {
                newEp.translation = OMV.Vector3.Zero;
            }
            newEp.rotation = OMV.Quaternion.Identity;

            newEp.scale = rootEp.scale;

            // The 'new ExtendedPrim' above copied the faceted mesh faces. We're doing it over so undo that.
            newEp.faces.Clear();
            FaceInfo newFace = new FaceInfo(999, rootEp);
            newFace.textureEntry = rootFace.textureEntry;
            newFace.textureID = rootFace.textureID;
            newFace.faceImage = rootFace.faceImage;
            newFace.hasAlpha = rootFace.hasAlpha;
            newEp.faces.Add(newFace);

            // m_log.DebugFormat("{0} ConvertSharedFacesIntoMeshes: newEp.trans={1}, newEp.rot={2}",
            //             _logHeader, newEp.translation, newEp.rotation);

            // Based of the root face, create a new mesh that holds all the faces
            similarFaceList.ForEach(faceInfo => {
                // m_log.DebugFormat("{0} ConvertSharedFacesIntoMeshes: adding {1} h={2}, verts={3}, ind={4}",
                //                 _logHeader, faceInfo.containingPrim.ID,
                //                 similarFaceKvp.Key, faceInfo.vertexs.Count, faceInfo.indices.Count);
                // 'faceInfo' and 'ep' is the vertex/indices we're adding to 'newFace'
                ExtendedPrim ep = faceInfo.containingPrim;
                // The indices of the mesh being added needs to be advanced 'indicesBase' since the vertices are
                //     added to the end of the existing list.
                int indicesBase = newFace.vertexs.Count;

                // Translate all the new vertices to world coordinates then subtract the 'newEp' location.
                // All rotation is removed to make computation simplier
                OMV.Vector3 worldPos = OMV.Vector3.Zero;
                OMV.Quaternion worldRot = OMV.Quaternion.Identity;
                if (ep.fromOS.SOP != null) {
                    worldPos = ep.fromOS.SOP.GetWorldPosition();
                    worldRot = ep.fromOS.SOP.GetWorldRotation();
                }
                // m_log.DebugFormat("{0} ConvertSharedFacesIntoMeshes: map {1}, wPos={2}, wRot={3}",
                //                 _logHeader, faceInfo.containingPrim.ID, worldPos, worldRot);
                newFace.vertexs.AddRange(faceInfo.vertexs.Select(vert => {
                    OMVR.Vertex newVert = new OMVR.Vertex();
                    var worldLocationOfVertex = vert.Position * worldRot + worldPos;
                    newVert.Position = worldLocationOfVertex - newEp.translation;
                    newVert.Normal = vert.Normal * worldRot;
                    newVert.TexCoord = vert.TexCoord;
                    return newVert;
                }));
                newFace.indices.AddRange(faceInfo.indices.Select(ind => (ushort)(ind + indicesBase)));
            });
            // m_log.DebugFormat("{0} ConvertSharedFacesIntoMeshes: COMPLETE: h={1}, verts={2}. ind={3}",
            //             _logHeader, similarFaceKvp.Key, newFace.vertexs.Count, newFace.indices.Count);
            return newEp;
        }

    }
}
