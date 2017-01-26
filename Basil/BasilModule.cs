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
        OMV.Vector3 m_regionDimensions = new OMV.Vector3(Constants.RegionSize, Constants.RegionSize, 10000f);

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
                m_regionDimensions.X = m_scene.RegionInfo.RegionSizeX;
                m_regionDimensions.Y = m_scene.RegionInfo.RegionSizeY;

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
        public class MeshWithMaterial {
            public ExtendedPrim containingPrim;
            public int faceIndex;

            public MeshWithMaterial(ExtendedPrim pContainingPrim, int pFaceIndex) {
                containingPrim = pContainingPrim;
                faceIndex = pFaceIndex;
            }
        }

        // Lists of similar faces indexed by the texture hash
        public class SimilarFaces : Dictionary<int, List<MeshWithMaterial>> {
            public SimilarFaces() : base() {
            }
            public void AddSimilarFace(int pHash, MeshWithMaterial pFace) {
                if (! this.ContainsKey(pHash)) {
                    this.Add(pHash, new List<MeshWithMaterial>());
                }
                this[pHash].Add(pFace);
            }
        }

        // A structure to hold all the information about the reorganized scene
        public class ReorganizedScene : IDisposable {
            public string regionID;
            public EntityGroupList nonStaticEntities = new EntityGroupList();
            public EntityGroupList staticEntities = new EntityGroupList();
            public SimilarFaces similarFaces = new SimilarFaces();
            public EntityGroupList rebuiltFaceEntities = new EntityGroupList();

            public ReorganizedScene(string pRegionID) {
                regionID = pRegionID;
            }

            public void Dispose() {
                nonStaticEntities.Clear();
                staticEntities.Clear();
                similarFaces.Clear();
                rebuiltFaceEntities.Clear();
            }
        }

        // Convert all entities in the region to basil format
        private void ProcessConvert(string module, string[] cmdparms) {

            if (SceneManager.Instance == null || SceneManager.Instance.CurrentScene == null)
            {
                m_log.Error("Error: no region selected. Use 'change region' to select a region.");
                return;
            }

            // m_log.DebugFormat("{0} ProcessConvert. CurrentScene={1}, m_scene={2}", LogHeader,
            //             SceneManager.Instance.CurrentScene.Name, m_scene.Name);

            if (SceneManager.Instance.CurrentScene.Name == m_scene.Name) {

                using (ReorganizedScene reorgScene = new ReorganizedScene("region_" + m_scene.Name.ToLower())) {

                    using (BasilStats stats = new BasilStats(m_scene, m_log)) {

                        using (IAssetFetcherWrapper assetFetcher = new OSAssetFetcher(m_scene, m_log)) {

                            EntityGroupList allSOGs = new EntityGroupList();
                            ConvertEntitiesToMeshes(allSOGs, assetFetcher, stats);
                            // Everything has been converted into meshes and available in 'allSOGs'.
                            m_log.InfoFormat("{0} Converted {1} scene entities", LogHeader, allSOGs.Count);

                            // Scan the entities and reorganize into static/non-static and find shared face meshes
                            ReorganizeScene(allSOGs, reorgScene);

                            // Scan all the entities and extract statistics
                            if (m_params.LogConversionStats) {
                                stats.ExtractStatistics(reorgScene, stats);
                                stats.LogAll(LogHeader);
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
                                m_log.DebugFormat("{0} exporting textures", LogHeader);
                                WriteOutImages(reorgScene);
                            }

                            // Write out the Gltf information
                            ExportSceneAsGltf(gltf, m_scene.Name, m_params.GltfTargetDir);

                            allSOGs.Clear();
                        }
                    }

                }
            }
        }

        // For each of the SceneObjectGroups in the scene, create an EntityGroup with everything converted to meshes
        private void ConvertEntitiesToMeshes(EntityGroupList allSOGs, IAssetFetcherWrapper assetFetcher, BasilStats stats) {
            // m_log.DebugFormat("{0} ConvertEntitiesToMeshes:", LogHeader);
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
            // m_log.DebugFormat("{0}: ConvertSOG", LogHeader);
            var prom = new Promise<EntityGroup>();

            EntityGroup meshes = new EntityGroup(sog);

            int totalChildren = sog.Parts.GetLength(0);
            foreach (SceneObjectPart sop in sog.Parts) {

                // DEBUG DEBUG
                /*
                m_log.DebugFormat("{0} SOP {1} wPos={2}, gPos={3}, aPos={4}, oPos={5}, rOff={6}, wRot={7}, scale={8}",
                        LogHeader, sop.UUID,
                        sop.GetWorldPosition(),
                        sop.GroupPosition, sop.AbsolutePosition, sop.OffsetPosition,
                        sop.RotationOffset, sop.GetWorldRotation(), sop.Scale);
                */
                OMV.Vector3 rots = new OMV.Vector3();
                float radtodeg = 57.2958f;
                sop.GetWorldRotation().GetEulerAngles(out rots.X, out rots.Y, out rots.Z);
                m_log.DebugFormat("{0} SOP {1} wPos={2}, rot={3}, scale={4}",
                        LogHeader, sop.UUID,
                        sop.GetWorldPosition(), rots * radtodeg, sop.Scale);
                // END DEBUG DEBUG
                OMV.Primitive aPrim = sop.Shape.ToOmvPrimitive();
                mesher.CreateMeshResource(sog, sop, aPrim, assetFetcher, OMVR.DetailLevel.Highest, stats)
                    .Catch(e => {
                        m_log.ErrorFormat("{0}: ConvertSOG: failed conversion: {1}", LogHeader, e);
                        prom.Reject(e);
                    })
                    .Then(ePrimGroup => {
                        // If scaling is done in the mesh, do it now
                        if (!m_params.DisplayTimeScaling) {
                            PrimToMesh.ScaleMeshes(ePrimGroup);
                            foreach (ExtendedPrim ep in ePrimGroup.Values) {
                                ep.scale = new OMV.Vector3(1, 1, 1);
                            }
                        }

                        // The prims in the group need to be decorated with texture/image information
                        UpdateTextureInfo(ePrimGroup, aPrim, assetFetcher, mesher);

                        lock (meshes) {
                            // m_log.DebugFormat("{0}: CreateAllMeshesInSOP: foreach oneSOP: {1}", LogHeader, sop.UUID);
                            meshes.Add(ePrimGroup);
                        }
                        // can't tell what order the prims are completed in so wait until they are all meshed
                        // TODO: change the completion logic to use Promise.All()
                        // m_log.DebugFormat("{0}: ConvertSOG: id={1}, totalChildren={2}", LogHeader, sog.UUID, totalChildren);
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
            // m_log.DebugFormat("{0}: UpdateTextureInfo", LogHeader);
            ExtendedPrim ep = epGroup.primaryExtendePrim;
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
                    GetUniqueTextureData(new EntityHandle(texID), assetFetcher)
                        .Then(theImage => {
                            ep.faceImages.Add(ii, theImage);
                            string imageFilename = null;
                            string imageURI = null;
                            CreateAssetURI(Gltf.MakeAssetURITypeImage, texID.ToString(), out imageFilename, out imageURI);
                            ep.faceFilenames.Add(ii, imageFilename);
                        })
                        .Catch(e => {
                            m_log.ErrorFormat("{0} UpdateTextureInfo. {1}", LogHeader, e);
                        });
                }

                // While we're in the neighborhood, map the texture coords based on the prim information
                pMesher.UpdateCoords(face, tef);
            }
        }

        // Keep a cache if image data and either fetch and Image or return a cached instance.
        private Dictionary<int, Image> textureCache = new Dictionary<int, Image>();
        private Promise<Image> GetUniqueTextureData(EntityHandle textureHandle, IAssetFetcherWrapper assetFetcher) {

            Promise<Image> prom = new Promise<Image>();
            int hash = textureHandle.GetHashCode();
            if (textureCache.ContainsKey(hash)) {
                // m_log.DebugFormat("{0} GetUniqueTextureData. handle={1}, hash={2}, returning known", LogHeader, textureHandle, hash);
                prom.Resolve(textureCache[hash]);
            }
            else {
                assetFetcher.FetchRawAsset(textureHandle)
                .Then(theData => {
                    try {
                        Image theImage = CSJ2K.J2kImage.FromBytes(theData);
                        textureCache.Add(textureHandle.GetHashCode(), theImage);
                        // m_log.DebugFormat("{0} GetUniqueTextureData. handle={1}, hash={2}, caching", LogHeader, textureHandle, hash);
                        prom.Resolve(theImage);
                    }
                    catch (Exception e) {
                        prom.Reject(new Exception(String.Format("Texture conversion failed. handle={0}. e={1}", textureHandle, e)));
                    }
                });
            }
            return prom;
        }

        // Pass over all the converted entities and sort into types of meshes.
        // Entities with scripts are deemed to be non-static. Everything else is static.
        // For the static elements, group all the mesh faces that have common textures/materials.
        private void ReorganizeScene(EntityGroupList allSOGs, ReorganizedScene reorgScene) {
            allSOGs.ForEach(eGroup => {
                // Assume it is static and make dynmic if any prim in it is not static
                bool isStatic = true;
                // For each prim in the entity
                eGroup.ForEach(ePGroup => {
                    // only check for the primary mesh
                    ExtendedPrim ep = ePGroup.primaryExtendePrim;
                    if (!IsStaticShape(ep)) {
                        // If the prim has a script, it's a different layer
                        // If any of the prims in a linkset have a script, the whole entity is not static
                        isStatic = false;
                    }
                });
                if (isStatic) {
                    // This is a linkset without scripts or other changable qualities
                    reorgScene.staticEntities.Add(eGroup);
                }
                else {
                    // Something might change in this linkset
                    reorgScene.nonStaticEntities.Add(eGroup);
                }
            });
            // m_log.DebugFormat("{0} {1} CHECK num dynmaic elements={2}", LogHeader, m_scene.Name, reorgScene.nonStaticEntities.Count);
            // m_log.DebugFormat("{0} {1} CHECK num static elements={2}", LogHeader, m_scene.Name, reorgScene.staticEntities.Count);

            // Go through all the static items and make a list of all the meshes with similar textures
            // Transform reorgScene.staticEntities into reorgScene.similarFaces
            reorgScene.staticEntities.ForEachExtendedPrim(ep => {
                OMV.Primitive.TextureEntry tex = ep.SOP.Shape.Textures;
                int numFaces = ep.facetedMesh.Faces.Count;
                for (int ii = 0; ii < numFaces; ii++) {
                    OMV.Primitive.TextureEntryFace tef = tex.FaceTextures[ii];
                    if (tef == null) {
                        tef = tex.DefaultTexture;
                    }
                    int hashCode = tef.GetHashCode();
                    reorgScene.similarFaces.AddSimilarFace(hashCode, new MeshWithMaterial(ep, ii));
                }
            });
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

        // The building of the Gltf structures found a bunch of images. Write them out.
        private void WriteOutImages(ReorganizedScene reorgScene) {
            reorgScene.staticEntities.ForEachExtendedPrim(ep => {
                WriteOutImagesForEP(ep);
            });
            reorgScene.nonStaticEntities.ForEachExtendedPrim(ep => {
                WriteOutImagesForEP(ep);
            });
            return;
        }

        private void WriteOutImagesForEP(ExtendedPrim ep) {
            foreach (KeyValuePair<int, Image> kvp in ep.faceImages) {
                Image texImage = kvp.Value;
                string texFilename = ep.faceFilenames[kvp.Key];
                if (!File.Exists(texFilename)) {
                    try {
                        using (Bitmap textureBitmap = new Bitmap(texImage.Width, texImage.Height,
                                    System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
                            // convert the raw image into a channeled image
                            using (Graphics graphics = Graphics.FromImage(textureBitmap)) {
                                graphics.DrawImage(texImage, 0, 0);
                                graphics.Flush();
                            }
                            // Write out the converted image as PNG
                            textureBitmap.Save(texFilename, System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    catch (Exception e) {
                        m_log.ErrorFormat("{0} FAILED PNG FILE CREATION: {0}", e);
                    }
                }
            }
        }

        // Build the GLTF structures from the reorganized scene
        private Gltf ConvertReorgSceneToGltf(ReorganizedScene reorgScene) {
            Gltf gltf = new Gltf(m_log);

            GltfScene gScene = new GltfScene(gltf, reorgScene.regionID);
            gScene.name = reorgScene.regionID;

            // For each of the entities, create a gltfNode for the root and then add the linkset prims as the children
            reorgScene.nonStaticEntities.ForEach(eg => {
                m_log.DebugFormat("{0} ConvertReorgSceneToGltf. Adding non-static node to Gltf: {1}", LogHeader, eg.SOG.UUID);
                AddNodeToGltf(gltf, gScene, eg);
            });

            // DEBUG DEBUG: rebuiltFaceEntities is not working yet.
            // The rebuilt static entities are added next
            // reorgScene.rebuiltFaceEntities.ForEach(eg => {
            //     AddNodeToGltf(gltf, eg);
            // });

            // DEBUG DEBUG: for testing, just pass through the static elements
            reorgScene.staticEntities.ForEach(eg => {
                m_log.DebugFormat("{0} ConvertReorgSceneToGltf. Adding static node to Gltf: {1}", LogHeader, eg.SOG.UUID);
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
        // 'info' is what the asset wants to be called: a uuid for textures, mesh name, or buffer name.
        private void CreateAssetURI(string type, string info, out string filename, out string uri) {
            string fname = "";
            string uuri = "";

            string targetDir = ResolveAndCreateDir(m_params.GltfTargetDir);
            if (targetDir != null) {
                if (type == Gltf.MakeAssetURITypeImage) {
                    uuri = m_params.URIBase + info + ".png";
                    fname = JoinFilePieces(targetDir, info + ".png");
                }
                if (type == Gltf.MakeAssetURITypeBuff) {
                    uuri = m_params.URIBase + m_scene.Name + "_" + info + ".bin";
                    fname = JoinFilePieces(targetDir, m_scene.Name + "_" + info + ".bin");
                }
                if (type == Gltf.MakeAssetURITypeMesh) {
                    uuri = m_params.URIBase + info + ".mesh";
                    fname = JoinFilePieces(targetDir, info + ".mesh");
                }
            }
            filename = fname;
            uri = uuri;
        }

        private void AddNodeToGltf(Gltf gltf, GltfScene containingScene, EntityGroup eg) {
            // Find the root prim of this linkset
            ExtendedPrim rootPrim = null;
            eg.ForEach(epg => {
                ExtendedPrim ep = epg.primaryExtendePrim;
                if (ep.SOP.IsRoot) {
                    rootPrim = ep;
                }
            });
            GltfNode gRootNode = GltfNodeFromExtendedPrim(gltf, containingScene, rootPrim);

            // Add any children of the root node
            eg.ForEach(epg => {
                ExtendedPrim ep = epg.primaryExtendePrim;
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

            // Convert the extended prim's coordinate system to OpenGL-ness
            FixCoordinates(ep, new CoordSystem(CoordSystem.RightHand_Yup));
            // FixCoordinates(ep, new CoordSystem(CoordSystem.RightHand_Zup)); // DEBUG DEBUG -- No change

            GltfNode newNode = new GltfNode(pGltf, containingScene, id);

            newNode.name = ep.SOP.Name;

            newNode.translation = ep.translation;
            newNode.rotation = ep.rotation;
            newNode.scale = ep.scale;
            if (ep.transform != null) {
                newNode.matrix = (OMV.Matrix4)ep.transform;
            }
            // The following is needed if the child is it's own mesh separate from the parent
            // if (!ep.SOP.IsRoot) {
            //     // If this is a child, divide out the scale of the parent
            //     ret.scale /= ep.SOG.RootPart.Scale;
            // }

            int numFace = 0;
            ep.facetedMesh.Faces.ForEach(face => {
                string meshID = ep.SOP.UUID.ToString() + "_face" + numFace.ToString();
                GltfMesh mesh = new GltfMesh(pGltf, meshID);
                // m_log.DebugFormat("{0} GltfNodeFromExtendedPrim. Face. id={1}", LogHeader, meshID);
                mesh.underlyingPrim = ep;
                mesh.underlyingMesh = face;
                newNode.meshes.Add(mesh);
                numFace++;
            });

            return newNode;
        }

        // Convert the positions and all the vertices in an ExtendedPrim from one
        //     coordinate space to another. ExtendedPrim.coordSpace gives the current
        //     coordinates and we specify a new one here.
        // This is not a general solution -- it pretty much only works to convert
        //     right-handed,Z-up coordinates (OpenSimulator) to right-handed,Y-up
        //     (OpenGL).
        public void FixCoordinates(ExtendedPrim ep, CoordSystem newCoords) {
            if (ep.coordSystem.system != newCoords.system) {

                OMV.Matrix4 coordTransform = OMV.Matrix4.Identity;
                if (ep.coordSystem.getUpDimension == CoordSystem.Zup
                    && newCoords.getUpDimension == CoordSystem.Yup) {
                    // The one thing we know to do is change from Zup to Yup
                    coordTransform = new OMV.Matrix4(
                                    1,  0,  0,  0,
                                    0,  0, -1,  0,
                                    0,  1,  0,  0,
                                    0,  0,  0,  1);
                }
                m_log.DebugFormat("{0} coordTransform={1}", LogHeader, coordTransform);

                // Fix the location in space
                OMV.Vector3 transBefore = ep.translation;   // DEBUG DEBUG
                OMV.Quaternion rotBefore = ep.rotation;   // DEBUG DEBUG
                if (ep.positionIsParentRelative) {
                    ep.translation = FixOneCoordinate(ep.translation, coordTransform);
                }
                else {
                    // If world relative, fix negative dimensions to be within region
                    // ep.translation = FixOneCoordinate(ep.translation, coordTransform, m_regionDimensions);
                    ep.translation = FixOneCoordinate(ep.translation, coordTransform);
                }
                // if (!ep.positionIsParentRelative) {
                    ep.rotation = FixOneRotation(ep.rotation, coordTransform);
                // }
                m_log.DebugFormat("{0} FixCoordinates. tBefore={1}, tAfter={2}, rBefore={3}, rAfter={4}",
                        LogHeader, transBefore, ep.translation, rotBefore, ep.rotation);

                // Go through all the vertices and change the coordinate system
                PrimToMesh.OnAllVertex(ep, delegate (ref OMVR.Vertex vert) {
                    vert.Position = FixOneCoordinate(vert.Position, coordTransform);
                    vert.Normal = FixOneCoordinate(vert.Normal, coordTransform);
                });

                // The ExtendedPrim is all converted
                ep.coordSystem = newCoords;
            }
        }

        // Convert a single point in space from the previous coordinate system to the next.
        // If values go negative, presume the direction of that dimension changed and make positive on the other
        //     side of the region.
        public OMV.Vector3 FixOneCoordinate(OMV.Vector3 vect, OMV.Matrix4 coordTransform) {
            OMV.Vector3 newVect = OMV.Vector3.TransformNormal(vect, coordTransform);
            return newVect;
        }

        public OMV.Quaternion FixOneRotation(OMV.Quaternion rot, OMV.Matrix4 coordTransform) {
            OMV.Vector3 eulers = new OMV.Vector3();
            rot.GetEulerAngles(out eulers.X, out eulers.Y, out eulers.Z);
            // It looks like GetEulerAngles will return two PIs for a non-changing double axis inversion. Odd.
            const float fudge = 3.141590f;
            if (eulers.X > fudge && eulers.Z > fudge) {
                eulers.X = 0;
                eulers.Z = 0;
            }
            OMV.Vector3 convEulers = OMV.Vector3.TransformNormal(eulers, coordTransform);
            OMV.Quaternion after = OMV.Quaternion.CreateFromEulers(convEulers.X, convEulers.Y, convEulers.Z);
            m_log.DebugFormat("{0} FixOneRotation. before={1}, eulers={2}, convEulers={3}, after={4}",
                        LogHeader, rot, eulers, convEulers, after);
            return after;
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
            string separator = "" + Path.DirectorySeparatorChar;
            // string separator = "/";     // both .NET and mono are happy with forward slash
            string f = first;
            string l = last;
            while (f.EndsWith(separator)) f = f.Substring(f.Length - 1);
            while (l.StartsWith(separator)) l = l.Substring(1, l.Length - 1);
            return f + separator + l;
        }
    }
}
