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
using System.Reflection;
using Mono.Addins;

using log4net;
using Nini.Config;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using RSG;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;
using System.IO;

namespace org.herbal3d.BasilOS {

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "BasilModule")]
    public class BasilModule : INonSharedRegionModule {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static String LogHeader = "[Basil]";

        private BasilParams m_params;
        private IConfig m_sysConfig = null;

        protected Scene m_scene;

        #region INonSharedRegionNodule
        // IRegionModuleBase.Name()
        public string Name { get { return "BasilModule"; } }        
        
        // IRegionModuleBase.ReplaceableInterface()
        public Type ReplaceableInterface { get { return null; } }
        
        // IRegionModuleBase.ReplaceableInterface()
        // Called when simulator first loaded
        public void Initialise(IConfigSource source) {

            // Load all the parameters
            m_params = new BasilParams();
            // Overlay the default parameter values with the settings in the INI file
            m_sysConfig = source.Configs["Basil"];
            if (m_sysConfig != null) {
                m_params.SetParameterConfigurationValues(m_sysConfig);
            }

            if (m_params.Enabled) {
                m_log.InfoFormat("{0} Enabled", LogHeader);
            }
        }
        
        // IRegionModuleBase.Close()
        // Called when simulator is being shutdown
        public void Close() {
            m_log.DebugFormat("{0} Close", LogHeader);
        }
        
        // IRegionModuleBase.AddRegion()
        // Called once for a NonSharedRegionModule when the region is initialized
        public void AddRegion(Scene scene) {
            if (m_params.Enabled) {
                m_scene = scene;
                m_log.DebugFormat("{0} REGION {1} ADDED", LogHeader, scene.RegionInfo.RegionName);
            }
        }
        
        // IRegionModuleBase.RemoveRegion()
        // Called once for a NonSharedRegionModule when the region is being unloaded
        public void RemoveRegion(Scene scene) {
            m_log.DebugFormat("{0} REGION {1} REMOVED", LogHeader, scene.RegionInfo.RegionName);
        }        
        
        // IRegionModuleBase.RegionLoaded()
        // Called once for a NonSharedRegionModule when the region is completed loading
        public void RegionLoaded(Scene scene) {
            if (m_params.Enabled) {
                m_log.DebugFormat("{0} REGION {1} LOADED", LogHeader, scene.RegionInfo.RegionName);
                AddConsoleCommands();
            }
        }
        #endregion // INonSharedRegionNodule

        private void AddConsoleCommands() {
            MainConsole.Instance.Commands.AddCommand(
                "Regions", false, "basil convert",
                "basil convert",
                "Convert all entities in the region to basil format",
                ProcessConvert);

        }

        // Selection of a particular face of a prim. Contains index and ExtendedPrim
        //     so we have both the local coords and the group coords.
        private class MeshWithMaterial {
            public ExtendedPrim containingPrim;
            public int faceIndex;

            public MeshWithMaterial(ExtendedPrim pContainingPrim, int pFaceIndex) {
                containingPrim = pContainingPrim;
                faceIndex = pFaceIndex;
            }
        }

        // Lists of similar faces indexed by the texture hash
        private class SimilarFaces : Dictionary<int, List<MeshWithMaterial>> {
            public SimilarFaces() : base() {
            }
            public void AddSimilarFace(int pHash, MeshWithMaterial pFace) {
                if (! this.ContainsKey(pHash)) {
                    this.Add(pHash, new List<MeshWithMaterial>());
                }
                this[pHash].Add(pFace);
            }
        }

        // A collection of materials used in the scene
        private class FaceMaterials : Dictionary<int, OMV.Primitive.TextureEntryFace> {
        }

        // A structure to hold all the information about the reorganized scene
        private class ReorganizedScene {
            public string regionID;
            public EntityGroupList nonStaticEntities = new EntityGroupList();
            public EntityGroupList staticEntities = new EntityGroupList();
            public SimilarFaces similarFaces = new SimilarFaces();
            public EntityGroupList rebuiltFaceEntities = new EntityGroupList();
            public FaceMaterials faceMaterials = new FaceMaterials();

            public ReorganizedScene(string pRegionID) {
                regionID = pRegionID;
            }
        }

        // Convert all entities in the region to basil format
        private void ProcessConvert(string module, string[] cmdparms) {

            if (SceneManager.Instance == null || SceneManager.Instance.CurrentScene == null)
            {
                m_log.Error("Error: no region selected. Use 'change region' to select a region.");
                return;
            }

            m_log.DebugFormat("{0} ProcessConvert. CurrentScene={1}, m_scene={2}", LogHeader,
                        SceneManager.Instance.CurrentScene.Name, m_scene.Name);

            if (SceneManager.Instance.CurrentScene.Name == m_scene.Name) {

                List<EntityGroup> allSOGs = new List<EntityGroup>();
                ReorganizedScene reorgScene = new ReorganizedScene("region_" +m_scene.Name.ToLower());

                using (BasilStats stats = new BasilStats(m_scene, m_log)) {

                    using (IAssetFetcherWrapper assetFetcher = new OSAssetFetcher(m_scene, m_log)) {

                        ConvertEntitiesToMeshes(allSOGs, assetFetcher, stats);
                        // Everything has been converted into meshes and available in 'allSOGs'.
                        m_log.InfoFormat("{0} Converted {1} scene entities", LogHeader, allSOGs.Count);

                        // Scan the entities and reorganize into static/non-static and find shared face meshes
                        ReorganizeScene(allSOGs, reorgScene);

                        // Scan all the entities and extract statistics
                        if (m_params.LogConversionStats) {
                            ExtractStatistics(reorgScene, stats);
                        }

                        // print out information about the similar faces
                        if (m_params.LogDetailedSharedFaceStats) {
                            LogSharedFaceInformation(reorgScene);
                        }

                        // Creates reorgScene.rebuiltFaceEntities from reorgScene.similarFaces
                        //     by repositioning the vertices in the shared meshes so they act as one mesh
                        ConvertSharedFacesIntoMeshes(reorgScene);

                        // The whole scene is now in reorgScene.nonStaticEntities and reorgScene.rebuiltFaceEntities

                        // Build the GLTF structures from the reorganized scene
                        Gltf gltf = ConvertReorgSceneToGltf(reorgScene);

                        // Scan through all the textures and convert them into PNGs for the Gltf scene
                        if (m_params.ExportTextures) {
                            ExportTexturesForGltf(gltf, reorgScene, assetFetcher, m_params.GltfTargetDir);
                        }

                        // Write out the Gltf information
                        ExportSceneAsGltf(gltf, m_scene.Name, m_params.GltfTargetDir);
                    }
                }
            }
        }

        // For each of the SceneObjectGroups in the scene, create an EntityGroup with everything converted to meshes
        private void ConvertEntitiesToMeshes(List<EntityGroup> allSOGs, IAssetFetcherWrapper assetFetcher, BasilStats stats) {
            m_log.DebugFormat("{0} ConvertEntitiesToMeshes:", LogHeader);
            using (PrimToMesh assetMesher = new PrimToMesh(m_log)) {

                // TODO: This should be a Promise.All()
                m_scene.ForEachSOG(sog => {
                    ConvertSOG(sog, assetMesher, assetFetcher, stats)
                        .Catch(e => {
                            m_log.ErrorFormat("{0} Error converting SOG. UUID={1}: {2}", LogHeader, sog.UUID, e);
                        })
                        .Then(ePrimGroup => {
                            allSOGs.Add(ePrimGroup);
                        }
                    ); 
                });
            }
        }

        // Convert all prims in SOG into meshes and return the mesh group.
        private IPromise<EntityGroup> ConvertSOG(SceneObjectGroup sog, PrimToMesh mesher,
                        IAssetFetcherWrapper assetFetcher, BasilStats stats ) {
            m_log.DebugFormat("{0}: ConvertSOG", LogHeader);
            var prom = new Promise<EntityGroup>();

            EntityGroup meshes = new EntityGroup(sog);

            int totalChildren = sog.Parts.GetLength(0);
            foreach (SceneObjectPart sop in sog.Parts) {

                OMV.Primitive aPrim = sop.Shape.ToOmvPrimitive();
                mesher.CreateMeshResource(sog, sop, aPrim, assetFetcher, OMVR.DetailLevel.Highest, stats)
                    .Catch(e => {
                        m_log.ErrorFormat("{0}: ConvertSOG: failed conversion: {1}", LogHeader, e);
                        prom.Reject(e);
                    })
                    .Then(ePrimGroup => {
                        // The prims in the group need to be decorated with texture/image information
                        UpdateTextureInfo(ePrimGroup, aPrim, assetFetcher, mesher);

                        lock (meshes) {
                            // m_log.DebugFormat("{0}: CreateAllMeshesInSOP: foreach oneSOP: {1}", LogHeader, sop.UUID);
                            meshes.Add(ePrimGroup);
                        }
                        // can't tell what order the prims are completed in so wait until they are all meshed
                        // TODO: change the completion logic to use Promise.All()
                        if (--totalChildren <= 0) {
                            prom.Resolve(meshes);
                        }
                    });
            }
            return prom;
        }

        /// <summary>
        /// Scan through all the ExtendedPrims and update each with pointers to the material/texture information
        /// for the mesh. Additionally, read in the referenced images so they can be scanned for transparency
        /// and otherwise processed (used to create atlases, etc).
        /// </summary>
        /// <param name="epGroup">Collections of meshes to update</param>
        /// <param name="assetFetcher">Fetcher for getting images, etc</param>
        /// <param name="pMesher"></param>
        private void UpdateTextureInfo(ExtendedPrimGroup epGroup, OMV.Primitive pPrim,
                                    IAssetFetcherWrapper assetFetcher, PrimToMesh pMesher) {
            m_log.DebugFormat("{0}: UpdateTextureInfo", LogHeader);
            if (epGroup.ContainsKey(PrimGroupType.lod1)) {
                ExtendedPrim ep = epGroup[PrimGroupType.lod1];
                for (int ii = 0; ii < ep.facetedMesh.Faces.Count; ii++) {
                    OMVR.Face face = ep.facetedMesh.Faces[ii];
                    OMV.Primitive.TextureEntryFace tef = pPrim.Textures.FaceTextures[ii];
                    if (tef == null) {
                        tef = pPrim.Textures.DefaultTexture;
                    }
                    // Add the texture information for the face for later reference
                    ep.faceTextures.Add(ii, tef);

                    // If the texture includes an image, read it in.
                    OMV.UUID texID = tef.TextureID;
                    if (texID != OMV.UUID.Zero && texID != OMV.Primitive.TextureEntry.WHITE_TEXTURE) {
                        assetFetcher.FetchRawAsset(new EntityHandle(texID))
                        .Then(theData => {
                            ep.faceImages.Add(ii, CSJ2K.J2kImage.FromBytes(theData));
                        });
                    }

                    // While we're in the neighborhood, map the texture coords based on the prim information
                    pMesher.UpdateCoords(face, tef);
                }
            }
        }

        // Gather statistics
        private void ExtractStatistics(ReorganizedScene reorgScene, BasilStats stats) {
            stats.numEntities = reorgScene.staticEntities.Count + reorgScene.nonStaticEntities.Count;
            stats.numStaticEntities = reorgScene.staticEntities.Count;

            reorgScene.nonStaticEntities.ForEach(eGroup => {
                if (eGroup.Count > 1) {
                    // if the entity is made of multiple pieces, they are a linkset
                    stats.numLinksets++;
                }
            });
            reorgScene.nonStaticEntities.ForEachExtendedPrim(ep => {
                if (ep.facetedMesh != null) {
                    stats.numFaces = ep.facetedMesh.Faces.Count;
                }
            });

            reorgScene.staticEntities.ForEach(eGroup => {
                if (eGroup.Count > 1) {
                    stats.numStaticLinksets++;
                }
            });
            stats.numLinksets += stats.numStaticLinksets;
            reorgScene.staticEntities.ForEachExtendedPrim(ep => {
                if (ep.facetedMesh != null) {
                    stats.numFaces = ep.facetedMesh.Faces.Count;
                }
            });

            foreach (KeyValuePair<int, OMV.Primitive.TextureEntryFace> kvp in reorgScene.faceMaterials) {
                OMV.UUID textureID = kvp.Value.TextureID;
                if (!stats.textureIDs.Contains(textureID)) {
                    stats.textureIDs.Add(textureID);
                }
            }

            stats.numMaterials = reorgScene.faceMaterials.Count;

            stats.LogAll(LogHeader);

        }

        // Pass over all the converted entities and sort into types of meshes.
        // Entities with scripts are deemed to be non-static. Everything else is static.
        // For the static elements, group all the mesh faces that have common textures/materials.
        private void ReorganizeScene(List<EntityGroup> allSOGs, ReorganizedScene reorgScene) {
            allSOGs.ForEach(eGroup => {
                // For each prim in the entity
                eGroup.ForEach(ePGroup => {
                    // only check for the primary mesh
                    if (ePGroup.ContainsKey(PrimGroupType.lod1)) {
                        ExtendedPrim ep = ePGroup[PrimGroupType.lod1];
                        // if the prim has a script, it's a different layer
                        if (IsStaticShape(ep)) {
                            // the prim is not scripted so we add all its faces to the static group
                            reorgScene.staticEntities.AddUniqueEntity(eGroup);
                            // if (reorgScene.staticEntities.AddUniqueEntity(eGroup)) {
                            //     m_log.DebugFormat("{0} ReorganiseScene. Added to staticEntities: sog={1}", LogHeader, eGroup.SOG.UUID);
                            // }
                        }
                        else {
                            // if any of the prims in a linkset have a script, the whole entity is not static
                            reorgScene.nonStaticEntities.AddUniqueEntity(eGroup);
                            // if (reorgScene.nonStaticEntities.AddUniqueEntity(eGroup)) {
                            //     m_log.DebugFormat("{0} ReorganiseScene. Added to non-staticEntities: sog={1}", LogHeader, eGroup.SOG.UUID);
                            // }
                        }
                    }
                    else {
                        m_log.ErrorFormat("{0} Prim didn't have primary mesh. ID={1}", LogHeader, eGroup.SOG.UUID);
                    }
                });
            });
            m_log.DebugFormat("{0} {1} CHECK num script elements={2}", LogHeader, m_scene.Name, reorgScene.nonStaticEntities.Count);
            m_log.DebugFormat("{0} {1} CHECK num static elements={2}", LogHeader, m_scene.Name, reorgScene.staticEntities.Count);

            // Go through all the static items and make a list of all the meshes with similar textures
            // Transform reorgScene.staticEntities into reorgScene.similarFaces
            reorgScene.staticEntities.ForEachExtendedPrim(ep => {
                OMV.Primitive.TextureEntry tex = ep.SOP.Shape.Textures;
                int numFaces = ep.facetedMesh.Faces.Count;
                for (int ii = 0; ii < numFaces; ii++) {
                    OMV.Primitive.TextureEntryFace tef = tex.FaceTextures[ii];
                    if (tef != null) {
                        int hashCode = tef.GetHashCode();
                        reorgScene.similarFaces.AddSimilarFace(hashCode, new MeshWithMaterial(ep, ii));

                        // Also create a collection of all the materails being used in the scene
                        if (!reorgScene.faceMaterials.ContainsKey(hashCode)) {
                            reorgScene.faceMaterials.Add(hashCode, tef);
                        }
                    }
                }
            });

            // Scan through the non-static entities and add materials to the material collection
            reorgScene.nonStaticEntities.ForEachExtendedPrim(ep => {
                OMV.Primitive.TextureEntry tex = ep.SOP.Shape.Textures;
                int numFaces = ep.facetedMesh.Faces.Count;
                for (int ii = 0; ii < numFaces; ii++) {
                    OMV.Primitive.TextureEntryFace tef = ep.faceTextures[ii];
                    if (tef != null) {
                        int hashCode = tef.GetHashCode();
                        if (!reorgScene.faceMaterials.ContainsKey(hashCode)) {
                            reorgScene.faceMaterials.Add(hashCode, tef);
                        }
                    }
                }
            });

            m_log.InfoFormat("{0} {1} CHECK num similar faces={2}", LogHeader, m_scene.Name, reorgScene.similarFaces.Count);
        }

        // Test to see if the object is dynamic (scripted or whatever and might change) vs
        //   being an entity that will not be moving around.
        // Return 'true' if a 'static' shape.
        private bool IsStaticShape(ExtendedPrim ep) {
            bool ret = true;
            if (0 != (uint)ep.SOP.ScriptEvents) {
                // if there are any script events, this cannot be a static object
                ret = false;
            }
            return ret;
        }

        // Log stats for each of the shared faces.
        private void LogSharedFaceInformation(ReorganizedScene reorgScene) {
            int totalIndices = 0;
            int totalVertices = 0;
            int totalUniqueVertices = 0;
            foreach (int key in reorgScene.similarFaces.Keys) {
                // Go through the list of faces that have the same texture
                int totalIndicesPerUnique = 0;
                int totalVerticesPerUnique = 0;
                List<OMVR.Vertex> uniqueVertices = new List<OMVR.Vertex>();

                List<MeshWithMaterial> similar = reorgScene.similarFaces[key];
                similar.ForEach(oneSimilarFace => {
                    ExtendedPrim ep = oneSimilarFace.containingPrim;
                    int ii = oneSimilarFace.faceIndex;
                    OMVR.Face oneFace = ep.facetedMesh.Faces[ii];
                    int indicesForFace = oneFace.Indices.Count;
                    totalIndicesPerUnique += indicesForFace;
                    int verticesForFace = oneFace.Vertices.Count;
                    totalVerticesPerUnique += verticesForFace;
                    oneFace.Vertices.ForEach(v => {
                        if (!uniqueVertices.Contains(v)) {
                            uniqueVertices.Add(v);
                        }
                    });
                });
                totalVertices += totalVerticesPerUnique;
                totalIndices += totalIndicesPerUnique;
                totalUniqueVertices += uniqueVertices.Count;
                m_log.InfoFormat("{0} {1} {2}: totalIndices={3}, totalVertices={4}, uniqueVertices={5}",
                            LogHeader, m_scene.Name, key, totalIndicesPerUnique, totalVerticesPerUnique, uniqueVertices.Count);
            }
            m_log.InfoFormat("{0} {1} totalIndices={2}, totalVertices={3}, uniqueVertices={4}",
                        LogHeader, m_scene.Name, totalIndices, totalVertices, totalUniqueVertices);
        }

        // Loop through all the shared faces (faces that share the same material) and create
        //    one mesh for all the faces. This entails selecting one of the faces to be the
        //    root face and then displacing all the vertices, rotations, ... to be based
        //    from that root face.
        // We find the root face by looking for one "in the middle"ish so as to keep the offset
        //    math as small as possible.
        // This creates reorgScene.rebuildFaceEntities from reorgScene.similarFaces.
        private void ConvertSharedFacesIntoMeshes(ReorganizedScene reorgScene) {

            foreach (int key in reorgScene.similarFaces.Keys) {
                // This is the list of faces that use one particular face material
                List<MeshWithMaterial> similar = reorgScene.similarFaces[key];

                // Loop through the faces and find the 'middle one'
                MeshWithMaterial rootFace = null;
                // similar.ForEach(oneSimilarFace => {
                // });
                // for the moment, just select the first one.
                // If coordinate jitter becomes a problem, fix this code to find the middle one.
                rootFace = similar[0];

                ExtendedPrim newEp = new ExtendedPrim();    // the new object being created
                OMVR.FacetedMesh newFacetedMesh= new OMVR.FacetedMesh();  // the new mesh
                OMVR.Face newFace = new OMVR.Face();  // the new mesh
                // Based of the root face, create a new mesh that holds all the faces
                similar.ForEach(oneSimilarFace => {
                    ExtendedPrim ep = oneSimilarFace.containingPrim;
                    if (oneSimilarFace == rootFace) {
                        // The root entity becomes the identity of the whole thing
                        newEp.SOG = ep.SOG;
                        newEp.SOP = ep.SOP;
                        newEp.primitive = ep.primitive;
                        newEp.facetedMesh = newFacetedMesh;
                        newFace.TextureFace = ep.primitive.Textures.CreateFace((uint)oneSimilarFace.faceIndex);
                    }
                    if (ep.SOP.ParentID != 0) {
                        // if the prim for this face is part of a linkset, its position must be rotated from the base
                        // TODO:
                    }
                    else {
                        // If just a prim in space, offset coords to be relative to the root face
                        // TODO:
                    }
                });
                EntityGroup eg = new EntityGroup(newEp.SOG);
                eg.Add(new ExtendedPrimGroup(newEp));
                reorgScene.rebuiltFaceEntities.AddUniqueEntity(eg);
            }
        }

        private void ExportTexturesForGltf(Gltf gltf, ReorganizedScene reorgScene, IAssetFetcherWrapper assetFetcher, string targetDir) {
            // TODO:
            return;
        }

        // Build the GLTF structures from the reorganized scene
        private Gltf ConvertReorgSceneToGltf(ReorganizedScene reorgScene) {
            Gltf gltf = new Gltf(m_log);

            GltfScene gScene = new GltfScene(gltf, reorgScene.regionID);
            gScene.name = reorgScene.regionID;

            // For each of the entities, create a gltfNode for the root and then add the linkset prims as the children
            reorgScene.nonStaticEntities.ForEach(eg => {
                AddNodeToGltf(gltf, gScene, eg);
            });

            // DEBUG DEBUG: rebuiltFaceEntities is not working yet.
            // The rebuilt static entities are added next
            // reorgScene.rebuiltFaceEntities.ForEach(eg => {
            //     AddNodeToGltf(gltf, eg);
            // });

            // DEBUG DEBUG: for testing, just pass through the static elements
            reorgScene.staticEntities.ForEach(eg => {
                AddNodeToGltf(gltf, gScene, eg);
            });

            // Scan all the meshes and build the materials from the face texture information
            gltf.BuildPrimitives(CreateAssetURI);

            // Scan all the created meshes and create the Buffers, BufferViews, and Accessors
            gltf.BuildBuffers(CreateAssetURI);
            
            return gltf;
        }

        // When calling into the Gltf routines to build structures, there need to be URI's 
        //     added to the structures. This routine is called to generate the storage filename
        //     and reference URI for the item 'info' of type 'type'.
        private void CreateAssetURI(string type, string info, out string filename, out string uri) {
            // TODO: make this be smarter.
            string fname = "";
            string uuri = "";
            if (type == Gltf.MakeAssetURITypeImage) {
                uri = "./" +  info + ".png";
                fname = "./" +  info + ".png";
            }
            if (type == Gltf.MakeAssetURITypeBuff) {
                uri = "./" +  info + ".bin";
                fname = "./" +  info + ".bin";
            }
            if (type == Gltf.MakeAssetURITypeMesh) {
                uri = "./" + info + ".mesh";
                fname = "./" + info + ".mesh";
            }
            filename = fname;
            uri = uuri;
        }

        private void AddNodeToGltf(Gltf gltf, GltfScene containingScene, EntityGroup eg) {
            // Find the root prim of this linkset
            ExtendedPrim rootPrim = null;
            eg.ForEach(epg => {
                ExtendedPrim ep = epg[PrimGroupType.lod1];
                if (ep.SOP.IsRoot) {
                    rootPrim = ep;
                }
            });
            GltfNode gRootNode = GltfNodeFromExtendedPrim(gltf, containingScene, rootPrim);

            // Add any children of the root node
            eg.ForEach(epg => {
                ExtendedPrim ep = epg[PrimGroupType.lod1];
                if (!ep.SOP.IsRoot) {
                    GltfNode gChildNode = GltfNodeFromExtendedPrim(gltf, null, ep);
                    gRootNode.children.Add(gChildNode);
                }
            });
        }

        // Copy all the Entity information into gltf Nodes and Meshes
        private GltfNode GltfNodeFromExtendedPrim(Gltf pGltf, GltfScene containingScene, ExtendedPrim ep) {
            string id = ep.SOP.UUID.ToString();
            if (ep.SOP.IsRoot) {
                id += "_root";
            }
            else {
                id += "_part" + ep.SOP.LinkNum.ToString();
            }
            GltfNode ret = new GltfNode(pGltf, containingScene, id);

            ret.name = ep.SOP.Name;

            if (ep.SOP.IsRoot) {
                ret.translation = ep.SOP.GetWorldPosition();
                ret.rotation = ep.SOP.GetWorldRotation();
                // m_log.DebugFormat("{0} GltfNodeFromExtendedPrim. IsRoot. pos={1}, rot={2}",
                //             LogHeader, ret.translation, ret.rotation);
            }
            else {
                ret.translation = ep.SOP.RelativePosition;
                ret.rotation = ep.SOP.RotationOffset;
                // m_log.DebugFormat("{0} GltfNodeFromExtendedPrim. Child. pos={1}, rot={2}",
                //             LogHeader, ret.translation, ret.rotation);
            }

            int numFace = 0;
            ep.facetedMesh.Faces.ForEach(face => {
                string meshID = ep.SOP.UUID.ToString() + "_face" + numFace.ToString();
                GltfMesh mesh = new GltfMesh(pGltf, meshID);
                // m_log.DebugFormat("{0} GltfNodeFromExtendedPrim. Face. id={1}", LogHeader, meshID);
                mesh.underlyingPrim = ep;
                mesh.underlyingMesh = face;
                ret.meshes.Add(mesh);
                numFace++;
            });

            return ret;
        }

        /// <summary>
        /// Write out the Gltf as one JSON file into the specified directory.
        /// </summary>
        /// <param name="gltf">A built GLTF scene</param>
        /// <param name="regionName">The base name to use for the .gltf file</param>
        /// <param name="pTargetDir">Directory to write the .gltf file into.
        ///              Created if it does not exist</param>
        private void ExportSceneAsGltf(Gltf gltf, string regionName, string pTargetDir) {
            string targetDir = ResolveAndCreateDir(pTargetDir);

            if (targetDir != null) {
                string gltfFilename = JoinFilePieces(targetDir, regionName + ".gltf");
                using (StreamWriter outt = File.CreateText(gltfFilename)) {
                    gltf.ToJSON(outt);
                }
                gltf.WriteBinaryFiles(targetDir);
            }
        }

        /// <summary>
        /// Turn the passed relative path name into an absolute directory path and
        /// create the directory if it does not exist.
        /// </summary>
        /// <param name="pDir">Absolute or relative path to a directory</param>
        /// <returns>Absolute path to directory or 'null' if cannot resolve or create the directory</returns>
        private string ResolveAndCreateDir(string pDir) {
            string absDir = null;
            try {
                absDir = Path.GetFullPath(pDir);
                if (!Directory.Exists(absDir)) {
                    Directory.CreateDirectory(absDir);
                }
            }
            catch (Exception e) {
                m_log.ErrorFormat("{0} Failed creation of GLTF file directory. dir={1}, e: {2}",
                            LogHeader, absDir, e);
                return null;
            }
            return absDir;
        }

        /// <summary>
        /// Combine two filename pieces so there is one directory separator between.
        /// This replaces System.IO.Path.Combine which has the nasty feature that it
        /// ignores the first string if the second begins with a separator.
        /// It assumes that it's root and you don't want to join. Wish they had asked me.
        /// </summary>
        /// <param name="first"></param>
        /// <param name="last"></param>
        /// <returns></returns>
        public static string JoinFilePieces(string first, string last) {
            // string separator = "" + Path.DirectorySeparatorChar;
            string separator = "/";     // both .NET and mono are happy with forward slash
            string f = first;
            string l = last;
            while (f.EndsWith(separator)) f = f.Substring(f.Length - 1);
            while (l.StartsWith(separator)) l = l.Substring(1, l.Length - 1);
            return f + separator + l;
        }

    }
}
