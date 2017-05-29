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
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Addins;

using log4net;
using Nini.Config;

using OpenSim.Framework;
using OpenSim.Region.CoreModules.World.LegacyMap;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.PhysicsModules.SharedBase; // needed to test if objects are physical

using RSG;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;

namespace org.herbal3d.BasilOS {

    // Class passed around for global context for this region module instance
    public class BasilModuleContext {
        public BasilParams parms;
        public Scene scene;
        public ILog log;

        public BasilModuleContext(BasilParams pParms, ILog pLog) {
            parms = pParms;
            log = pLog;
            scene = null;
        }
    }

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "BasilModule")]
    public class BasilModule : INonSharedRegionModule {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static String LogHeader = "[Basil]";

        private BasilParams m_params;
        private IConfig m_sysConfig = null;

        private BasilModuleContext _context;

        private OMV.Vector3 m_regionDimensions = new OMV.Vector3(Constants.RegionSize, Constants.RegionSize, 10000f);
        private OMV.Vector3 m_regionCenter = new OMV.Vector3(Constants.RegionSize/2, Constants.RegionSize/2, 0f);

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

            _context = new BasilModuleContext(m_params, m_log);
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
                _context.scene = scene;
                m_regionDimensions.X = _context.scene.RegionInfo.RegionSizeX;
                m_regionDimensions.Y = _context.scene.RegionInfo.RegionSizeY;
                m_regionCenter.X = m_regionDimensions.X / 2f;
                m_regionCenter.Y = m_regionDimensions.Y / 2f;

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

        // Lists of similar faces indexed by the texture hash
        public class SimilarFaces : Dictionary<int, List<FaceInfo>> {
            public SimilarFaces() : base() {
            }
            public void AddSimilarFace(int pHash, FaceInfo pFace) {
                if (! this.ContainsKey(pHash)) {
                    this.Add(pHash, new List<FaceInfo>());
                }
                this[pHash].Add(pFace);
            }
        }

        // A structure to hold all the information about the reorganized scene
        public class ReorganizedScene : IDisposable {
            public string regionID;
            public EntityGroupList nonStaticEntities = new EntityGroupList();
            public EntityGroupList staticEntities = new EntityGroupList();
            public EntityGroupList rebuiltFaceEntities = new EntityGroupList();
            public EntityGroupList rebuiltNonStaticEntities = new EntityGroupList();

            public ReorganizedScene(string pRegionID) {
                regionID = pRegionID;
            }

            public void Dispose() {
                nonStaticEntities.Clear();
                staticEntities.Clear();
                rebuiltFaceEntities.Clear();
                rebuiltNonStaticEntities.Clear();
            }
        }

        // Convert all entities in the region to basil format
        private void ProcessConvert(string module, string[] cmdparms) {

            if (SceneManager.Instance == null || SceneManager.Instance.CurrentScene == null)
            {
                m_log.Error("Error: no region selected. Use 'change region' to select a region.");
                return;
            }

            // m_log.DebugFormat("{0} ProcessConvert. CurrentScene={1}, _context.scene={2}", LogHeader,
            //             SceneManager.Instance.CurrentScene.Name, _context.scene.Name);

            if (SceneManager.Instance.CurrentScene.Name == _context.scene.Name) {

                using (BasilStats stats = new BasilStats(_context.scene, m_log)) {

                    using (IAssetFetcher assetFetcher = new OSAssetFetcher(_context.scene, m_log, m_params)) {

                        try {

                            ConvertEntitiesToMeshes(assetFetcher, stats)
                                .Catch(e => {
                                    m_log.ErrorFormat("{0} exception in ConvertEntitiesToMeshes: {1}", LogHeader, e);
                                })
                                .Then(allSOGs => {
                                    // Everything has been converted into meshes and available in 'allSOGs'.
                                    m_log.DebugFormat("{0} Converted {1} scene entities", LogHeader, allSOGs.Count);
                                    if (m_params.LogDetailedEntityInfo) {
                                        stats.DumpDetailed(allSOGs);
                                    }

                                    // Scan the entities and reorganize into static/non-static and find shared face meshes
                                    ReorganizedScene reorgScene = ReorganizeScene(allSOGs, "region_" + _context.scene.Name.ToLower());

                                    // Scene objects in reorgScene.nonStaticEntities and reorgScene.staticEntities

                                    // Creates reorgScene.rebuiltFaceEntities from reorgScene.similarFaces
                                    //     by repositioning the vertices in the shared meshes so they act as one mesh
                                    if (m_params.MergeStaticMeshes) {
                                        try {
                                            reorgScene.rebuiltFaceEntities = ConvertEntitiesIntoSharedMaterialMeshes(reorgScene.staticEntities);
                                        }
                                        catch (Exception e) {
                                            m_log.ErrorFormat("{0} Exception calling ConvertEntitiesIntoSharedMaterialMeshes: {1}", LogHeader, e);
                                        }
                                    }
                                    else {
                                        // if we're not rebuilding the scene, the static entries are what's used
                                        reorgScene.rebuiltFaceEntities = reorgScene.staticEntities;
                                    }

                                    if (m_params.MergeNonStaticMeshes) {
                                        try {
                                            // Repack all the non-static entities
                                            // The non-static entities are packaged so they can move as a group.
                                            // This means the similar faces are only checked within the entity rather than across the region.
                                            reorgScene.rebuiltNonStaticEntities = new EntityGroupList(
                                                    reorgScene.nonStaticEntities.Select(eg => {
                                                        return ConvertEntityGroupIntoSharedMaterialMeshes(eg);
                                                    }).ToList()
                                                );
                                        }
                                        catch (Exception e) {
                                            m_log.ErrorFormat("{0} Exception calling ConvertEntityGroupIntoSharedMaterialMeshes: {1}", LogHeader, e);
                                        }
                                    }
                                    else {
                                        // if we're not rebuilding the scene, the static entries are what's used
                                        reorgScene.rebuiltNonStaticEntities = reorgScene.nonStaticEntities;
                                    }

                                    // Scan all the entities and extract statistics
                                    if (m_params.LogConversionStats) {
                                        stats.ExtractStatistics(reorgScene);
                                        stats.LogAll(LogHeader);
                                    }

                                    // The whole scene is now in reorgScene.nonStaticEntities and reorgScene.rebuiltFaceEntities

                                    // Build the GLTF structures from the reorganized scene
                                    Gltf gltf = null;
                                    try {
                                        // gltf = ConvertReorgSceneToGltf(reorgScene);
                                        var groupsToConvert = new EntityGroupList(reorgScene.rebuiltFaceEntities);
                                        groupsToConvert.AddRange(reorgScene.rebuiltNonStaticEntities);
                                        // m_log.DebugFormat("{0} Converting to GLTF. rebuiltFaceEntities={1}, rebuiltNonStaticEntities={2}, totalConverting={3}",
                                        //     LogHeader, reorgScene.rebuiltFaceEntities.Count,
                                        //     reorgScene.rebuiltNonStaticEntities.Count, groupsToConvert.Count);
                                        gltf = ConvertReorgSceneToGltf(groupsToConvert, reorgScene.regionID);
                                    }
                                    catch (Exception e) {
                                        m_log.ErrorFormat("{0} Exception calling ConvertReorgSceneToGltf: {1}", LogHeader, e);
                                    }

                                    // Scan through all the textures and convert them into PNGs for the Gltf scene
                                    try {
                                        if (m_params.ExportTextures) {
                                            m_log.DebugFormat("{0} exporting textures", LogHeader);
                                            WriteOutImages(reorgScene);
                                        }
                                    }
                                    catch (Exception e) {
                                        m_log.ErrorFormat("{0} Exception calling WriteOutImages: {1}", LogHeader, e);
                                    }

                                    // Write out the Gltf information
                                    if (gltf != null) {
                                        try {
                                            ExportSceneAsGltf(gltf, _context.scene.Name, m_params.GltfTargetDir);
                                        }
                                        catch (Exception e) {
                                            m_log.ErrorFormat("{0} Exception calling ExportSceneAsGltf: {1}", LogHeader, e);
                                        }
                                    }
                                    else {
                                        m_log.InfoFormat("{0} Not exporting GLTF files because conversion failed", LogHeader);
                                    }
                                })
                            ;
                        }
                        catch (Exception e) {
                            m_log.ErrorFormat("{0} Exception parocessing SOGs: {1}", LogHeader, e);
                        }
                    }
                }
            }
        }

        // For each of the SceneObjectGroups in the scene, create an EntityGroup with everything converted to meshes
        // Also add the terrain if needed.
        private IPromise<EntityGroupList> ConvertEntitiesToMeshes(IAssetFetcher assetFetcher, BasilStats stats) {
            Promise<EntityGroupList> prom = new Promise<EntityGroupList>();

            using (PrimToMesh assetMesher = new PrimToMesh(m_log)) {

                BConverterOS converter = new BConverterOS(assetFetcher, assetMesher, _context, stats);

                Promise<EntityGroup>.All(
                    _context.scene.GetSceneObjectGroups().Select(sog => {
                        return converter.Convert(sog, assetFetcher);
                    })
                )
                .Catch(e => {
                    m_log.ErrorFormat("{0} Error converting SOG. {1}", LogHeader, e);
                    prom.Reject(new Exception("Failed to convert SOG: " + e.ToString()));
                })
                .Done(eg => {
                    EntityGroupList egl = new EntityGroupList(eg.ToList());

                    // If terrain is requested, add it to the list of scene entities
                    if (m_params.AddTerrainMesh) {
                        m_log.DebugFormat("{0} ConvertEntitiesToMeshes: building and adding terrain", LogHeader);
                        try {
                            var ePrimGroup = CreateTerrainMesh(_context.scene, assetMesher, assetFetcher, stats);
                            m_log.DebugFormat("{0} ConvertEntitiesToMeshes: completed creation. Adding to mesh set", LogHeader);
                            egl.Add(ePrimGroup);
                            prom.Resolve(egl);
                        }
                        catch (Exception e) {
                            m_log.ErrorFormat("{0} Error creating terrain: {1}", LogHeader, e);
                            prom.Reject(new Exception("Failed to create terrain: " + e.ToString()));
                        }
                    }
                    else {
                        m_log.DebugFormat("{0} ConvertEntitiesToMeshes: not creating terrain. Just resolving", LogHeader);
                        prom.Resolve(egl);
                    }
                });
            }

            return prom;
        }

        // Create a mesh for the terrain of the current scene
        private EntityGroup CreateTerrainMesh(Scene pScene, PrimToMesh assetMesher,
                            IAssetFetcher assetFetcher, BasilStats stats) {

            int XSize = pScene.Heightmap.Width;
            int YSize = pScene.Heightmap.Height;

            float[,] heightMap = new float[XSize, YSize];
            if (m_params.HalfRezTerrain) {
                m_log.DebugFormat("{0}: CreateTerrainMesh. creating half sized terrain sized <{1},{2}>", LogHeader, XSize/2, YSize/2);
                // Half resolution mesh that approximates the heightmap
                heightMap = new float[XSize/2, YSize/2];
                for (int xx = 1; xx < XSize; xx += 2) {
                    for (int yy = 1; yy < YSize; yy += 2) {
                        float here = pScene.Heightmap.GetHeightAtXYZ(xx+0, yy+0, 26);
                        float ll = pScene.Heightmap.GetHeightAtXYZ(xx-1, yy-1, 26);
                        float lr = pScene.Heightmap.GetHeightAtXYZ(xx+1, yy-1, 26);
                        float ul = pScene.Heightmap.GetHeightAtXYZ(xx-1, yy+1, 26);
                        float ur = pScene.Heightmap.GetHeightAtXYZ(xx+1, yy+1, 26);
                        heightMap[(xx - 1) / 2, (yy - 1) / 2] = (here + ll + lr + ul + ur) / 5;
                    }
                }
            }
            else {
                m_log.DebugFormat("{0}: CreateTerrainMesh. creating terrain sized <{1},{2}>", LogHeader, XSize/2, YSize/2);
                heightMap = new float[XSize, YSize];
                for (int xx = 0; xx < XSize; xx++) {
                    for (int yy = 0; yy < YSize; yy++) {
                        heightMap[xx, yy] = pScene.Heightmap.GetHeightAtXYZ(xx, yy, 26);
                    }
                }
            }

            m_log.DebugFormat("{0}: CreateTerrainMesh. calling MeshFromHeightMap", LogHeader);
            ExtendedPrimGroup epg = assetMesher.MeshFromHeightMap(heightMap, (int)_context.scene.RegionInfo.RegionSizeX, (int)_context.scene.RegionInfo.RegionSizeY);

            // Number found in RegionSettings.cs as DEFAULT_TERRAIN_TEXTURE_3
            OMV.UUID defaultTextureID = new OMV.UUID("179cdabd-398a-9b6b-1391-4dc333ba321f");
            OMV.Primitive.TextureEntry te = new OMV.Primitive.TextureEntry(defaultTextureID);

            if (m_params.CreateTerrainSplat) {
                // Use the OpenSim maptile generator to create a texture for the terrain
                var terrainRenderer = new TexturedMapTileRenderer();
                terrainRenderer.Initialise(_context.scene, m_sysConfig.ConfigSource);

                var mapbmp = new Bitmap((int)_context.scene.Heightmap.Width, (int)_context.scene.Heightmap.Height,
                                        System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                terrainRenderer.TerrainToBitmap(mapbmp);

                // The built terrain mesh will have one face in the mesh
                OMVR.Face aFace = epg.primaryExtendePrim.fromOS.facetedMesh.Faces.First();
                FaceInfo fi = new FaceInfo(0, epg.primaryExtendePrim, aFace, te.CreateFace(0));
                fi.textureID = OMV.UUID.Random();
                fi.faceImage = mapbmp;
                fi.hasAlpha = false;
                fi.persist = new BasilPersist(Gltf.MakeAssetURITypeImage, fi.textureID.ToString(), _context);
                epg.primaryExtendePrim.faces.Add(fi);
            }
            else {
                // Fabricate a texture
                // The built terrain mesh will have one face in the mesh
                OMVR.Face aFace = epg.primaryExtendePrim.fromOS.facetedMesh.Faces.First();
                FaceInfo fi = new FaceInfo(0, epg.primaryExtendePrim, aFace, te.CreateFace(0));
                fi.textureID = defaultTextureID;
                assetFetcher.FetchTextureAsImage(new EntityHandle(defaultTextureID))
                    .Catch(e => {
                        m_log.ErrorFormat("{0} CreateTerrainMesh: unable to fetch default terrain texture: id={1}: {2}",
                                    LogHeader, defaultTextureID, e);
                    })
                    .Then(theImage => {
                        // This will happen later so hopefully soon enough for anyone using the image
                        fi.faceImage = theImage;
                    });
                fi.hasAlpha = false;
                epg.primaryExtendePrim.faces.Add(fi);
            }

            EntityGroup eg = new EntityGroup();
            eg.Add(epg);

            return eg;
        }

        // Pass over all the converted entities and sort into types of meshes.
        // Entities with scripts are deemed to be non-static. Everything else is static.
        // For the static elements, group all the mesh faces that have common textures/materials.
        private ReorganizedScene ReorganizeScene(EntityGroupList allSOGs, string reorgSceneName) {
            ReorganizedScene reorgScene = new ReorganizedScene(reorgSceneName);

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

            return reorgScene;
        }

        // Test to see if the object is dynamic (scripted or whatever and might change) vs
        //   being an entity that will not be moving around.
        // Return 'true' if a 'static' shape.
        private bool IsStaticShape(ExtendedPrim ep) {
            bool ret = true;
            if (ep.fromOS.SOP != null) {
                if ((ep.fromOS.SOP.PhysActor != null && ep.fromOS.SOP.PhysActor.IsPhysical) || (0 != (uint)ep.fromOS.SOP.ScriptEvents)) {
                    // if there are any script events, this cannot be a static object
                    ret = false;
                }
            }
            return ret;
        }

        // Loop through all the shared faces (faces that share the same material) and create
        //    one mesh for all the faces. This entails selecting one of the faces to be the
        //    root face and then displacing all the vertices, rotations, ... to be based
        //    from that root face.
        // We find the root face by looking for one "in the middle"ish so as to keep the offset
        //    math as small as possible.
        // This creates reorgScene.rebuildFaceEntities from reorgScene.similarFaces.
        private EntityGroupList ConvertEntitiesIntoSharedMaterialMeshes(EntityGroupList staticEntities) {

            // Go through all the static items and make a list of all the meshes with similar textures
            SimilarFaces similarFaces = new SimilarFaces();
            staticEntities.ForEachExtendedPrim(ep => {
                ep.faces.ForEach(faceInfo => {
                    OMV.Primitive.TextureEntryFace tef = faceInfo.textureEntry;
                    int hashCode = tef.GetHashCode();
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
        private EntityGroup ConvertEntityGroupIntoSharedMaterialMeshes(EntityGroup eg) {
            if (eg.Count == 1 && eg.First().primaryExtendePrim.faces.Count == 1) {
                // if there is only one entity and that entity has only one mesh, just return
                //     the thing passed.
                m_log.DebugFormat("{0} ConvertEntityGroupIntoSharedMaterialMeshes: only one face in one entity.", LogHeader);
                return eg;
            }

            // Go through all the materialed meshes and see if there are meshes to share
            SimilarFaces similarFaces = new SimilarFaces();
            // int totalFaces = 0; // DEBUG DEBUG
            eg.ForEach(epg => {
                ExtendedPrim ep = epg.primaryExtendePrim;
                ep.faces.ForEach(faceInfo => {
                    OMV.Primitive.TextureEntryFace tef = faceInfo.textureEntry;
                    int hashCode = tef.GetHashCode();
                    similarFaces.AddSimilarFace(hashCode, faceInfo);
                    // totalFaces++;
                });
            });
            // m_log.DebugFormat("{0} ConvertEntityGroupIntoSharedMaterialMeshes: EGs={1}, totalFaces={2}, similarFaces={3}",
            //         LogHeader, eg.Count, totalFaces, similarFaces.Count);

            EntityGroup rebuilt = new EntityGroup(
                similarFaces.Values.Select(similarFaceList => {
                    return new ExtendedPrimGroup(CreateExtendedPrimFromSimilarFaces(similarFaceList));
                }).ToList()
            );

            m_log.DebugFormat("{0} ConvertEntityGroupIntoSharedMaterialMeshes: after build: {1}", LogHeader, rebuilt.Stats());

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
            //             LogHeader, newEp.translation, newEp.rotation);

            // Based of the root face, create a new mesh that holds all the faces
            similarFaceList.ForEach(faceInfo => {
                // m_log.DebugFormat("{0} ConvertSharedFacesIntoMeshes: adding {1} h={2}, verts={3}, ind={4}",
                //                 LogHeader, faceInfo.containingPrim.ID,
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
                //                 LogHeader, faceInfo.containingPrim.ID, worldPos, worldRot);
                newFace.vertexs.AddRange(faceInfo.vertexs.Select(vert => {
                    OMVR.Vertex newVert = new OMVR.Vertex();
                    var worldLocationOfVertex = vert.Position * worldRot + worldPos;
                    newVert.Position = worldLocationOfVertex - newEp.translation;
                    newVert.Normal = vert.Normal * worldRot;
                    newVert.TexCoord = vert.TexCoord;
                    return newVert;
                }));
                newFace.indices.AddRange(faceInfo.indices.Select(ind => (ushort)(ind + indicesBase)));

                /* Old code kept for reference. Remove when above is working
                if (faceInfo == rootFace) {
                    // The vertices for the root face don't need translation.
                    newFace.vertexs.AddRange(faceInfo.vertexs);
                }
                else {
                    // Any other vertex must be moved to be world coords relative to new root
                    OMV.Vector3 worldPos = ep.fromOS.SOP.GetWorldPosition();
                    OMV.Quaternion worldRot = ep.fromOS.SOP.GetWorldRotation();
                    OMV.Quaternion invWorldRot = OMV.Quaternion.Inverse(worldRot);
                    OMV.Quaternion rotrot = invWorldRot * newEp.rotation;
                    m_log.DebugFormat("{0} ConvertSharedFacesIntoMeshes: wPos={1}, wRot={2}",
                                LogHeader, worldPos, worldRot);
                    newFace.vertexs.AddRange(faceInfo.vertexs.Select(vert => {
                        OMVR.Vertex newVert = new OMVR.Vertex();
                        newVert.Position = vert.Position * rotrot - worldPos + newEp.translation;
                        newVert.Normal = vert.Normal * rotrot;
                        newVert.TexCoord = vert.TexCoord;
                        m_log.DebugFormat("{0} ConvertSharedFacesIntoMeshes: vertPos={1}, nVerPos={2}",
                                        LogHeader, vert.Position, newVert.Position );
                        return newVert;
                    }));
                }
                END of old code */

                newFace.indices.AddRange(faceInfo.indices.Select(ind => (ushort)(ind + indicesBase)));
            });
            // m_log.DebugFormat("{0} ConvertSharedFacesIntoMeshes: COMPLETE: h={1}, verts={2}. ind={3}",
            //             LogHeader, similarFaceKvp.Key, newFace.vertexs.Count, newFace.indices.Count);
            return newEp;

            // EntityGroup eg = new EntityGroup();
            // eg.Add(new ExtendedPrimGroup(newEp));

            // return eg;
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
            // foreach (var faceInfo in ep.faces.Values) {
            ep.faces.ForEach(faceInfo => {
                if (faceInfo.faceImage != null) {
                    faceInfo.persist.WriteImage(faceInfo.faceImage);
                }
            });
        }
        // Build the GLTF structures from the reorganized scene
        // private Gltf ConvertReorgSceneToGltf(ReorganizedScene reorgScene) {
        private Gltf ConvertReorgSceneToGltf(EntityGroupList groupsToConvert, string sceneName) {
            Gltf gltf = new Gltf(m_log);

            GltfScene gScene = new GltfScene(gltf, sceneName);

            try {
                groupsToConvert.ForEach(eg => {
                    AddNodeToGltf(gltf, gScene, eg);
                });
            }
            catch (Exception e) {
                m_log.ErrorFormat("{0} ConvertReorgSceneToGltf: exception converting node: {1}", LogHeader, e);
            }

            // After adding all the meshes as nodes, create all the dependent structures
            gltf.BuildAccessorsAndBuffers(new BasilPersist(Gltf.MakeAssetURITypeImage, "", _context), _context);

            m_log.DebugFormat("{0} ConvertReorgSceneToGltf. Returniing gltf", LogHeader);
            return gltf;
        }

        private void AddNodeToGltf(Gltf gltf, GltfScene containingScene, EntityGroup eg) {
            // Find the root prim of this linkset
            ExtendedPrim rootPrim = null;
            eg.ForEach(epg => {
                ExtendedPrim ep = epg.primaryExtendePrim;
                if (ep.isRoot) {
                    rootPrim = ep;
                }
            });
            GltfNode gRootNode = GltfNodeFromExtendedPrim(gltf, containingScene, rootPrim);

            // Add any children of the root node
            eg.ForEach(epg => {
                ExtendedPrim ep = epg.primaryExtendePrim;
                if (ep != rootPrim) {
                    GltfNode gChildNode = GltfNodeFromExtendedPrim(gltf, null, ep);
                    gRootNode.children.Add(gChildNode);
                }
            });
        }

        // Copy all the Entity information into gltf Nodes and Meshes
        private GltfNode GltfNodeFromExtendedPrim(Gltf pGltf, GltfScene containingScene, ExtendedPrim ep) {
            string id = ep.ID.ToString();
            if (ep.fromOS.SOP == null) {
                // If there was no scene object, we assume this is terrain
                id += "_terrain";
            }
            else if (ep.isRoot) {
                id += "_root";
            }
            else {
                id += "_part" + ep.fromOS.SOP.LinkNum.ToString();
            }

            // Convert the extended prim's coordinate system to OpenGL-ness
            FixCoordinates(ep, new CoordSystem(CoordSystem.RightHand_Yup | CoordSystem.UVOriginLowerLeft));
            // FixCoordinates(ep, new CoordSystem(CoordSystem.RightHand_Zup)); // DEBUG DEBUG -- No change

            GltfNode newNode = new GltfNode(pGltf, containingScene, id);

            newNode.name = ep.Name;

            newNode.translation = ep.translation;
            newNode.rotation = ep.rotation;
            newNode.scale = ep.scale;
            if (ep.transform != null) {
                newNode.matrix = (OMV.Matrix4)ep.transform;
            }

            ep.faces.ForEach(faceInfo => {
                string meshID = ep.ID.ToString() + "_face" + faceInfo.num.ToString();
                GltfMesh mesh = new GltfMesh(pGltf, meshID);
                mesh.underlyingPrim = ep;
                mesh.faceInfo = faceInfo;
                newNode.meshes.Add(mesh);
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
            // true if need to flip the V in UV (origin from top left to bottom left)
            bool flipV = false;

            if (ep.coordSystem.system != newCoords.system) {

                OMV.Matrix4 coordTransform = OMV.Matrix4.Identity;
                OMV.Quaternion coordTransformQ = OMV.Quaternion.Identity;
                if (ep.coordSystem.getUpDimension == CoordSystem.Zup
                    && newCoords.getUpDimension == CoordSystem.Yup) {
                    // The one thing we know to do is change from Zup to Yup
                    coordTransformQ = OMV.Quaternion.CreateFromAxisAngle(1.0f, 0.0f, 0.0f, -(float)Math.PI / 2f);
                    // Make a clean matrix version.
                    // The libraries tend to create matrices with small numbers (1.119093e-07) for zero.
                    coordTransform = new OMV.Matrix4(
                                    1, 0, 0, 0,
                                    0, 0, -1, 0,
                                    0, 1, 0, 0,
                                    0, 0, 0, 1);
                }
                if (ep.coordSystem.getUVOrigin != newCoords.getUVOrigin) {
                    flipV = true;
                }

                // Fix the location in space
                if (!ep.positionIsParentRelative) {
                    ep.translation = ep.translation * coordTransformQ;
                    ep.rotation = coordTransformQ * ep.rotation;
                }

                // Go through all the vertices and change the UV coords if necessary
                if (flipV) {
                    PrimToMesh.OnAllVertex(ep, delegate (ref OMVR.Vertex vert) {
                        vert.TexCoord.Y = 1f - vert.TexCoord.Y;
                    });
                }

                // The ExtendedPrim is all converted
                ep.coordSystem = newCoords;
            }
            else {
                m_log.DebugFormat("{0} FixCoordinates. Not converting coord system. ep={1}",
                                LogHeader, ep.ID);
            }
        }

        // Convert the positions and all the vertices in an ExtendedPrim from one
        //     coordinate space to another. ExtendedPrim.coordSpace gives the current
        //     coordinates and we specify a new one here.
        // This is not a general solution -- it pretty much only works to convert
        //     right-handed,Z-up coordinates (OpenSimulator) to right-handed,Y-up
        //     (OpenGL).
        // DEPRECATED: this is a test version where tweaking happens.
        public void FixCoordinates2(ExtendedPrim ep, CoordSystem newCoords) {
            // true if need to flip the V in UV (origin from top left to bottom left)
            bool flipV = false;

            if (ep.coordSystem.system != newCoords.system) {

                OMV.Matrix4 coordTransform = OMV.Matrix4.Identity;
                OMV.Quaternion coordTransformQ = OMV.Quaternion.Identity;
                if (ep.coordSystem.getUpDimension == CoordSystem.Zup
                    && newCoords.getUpDimension == CoordSystem.Yup) {
                    // The one thing we know to do is change from Zup to Yup
                    coordTransformQ = OMV.Quaternion.CreateFromAxisAngle(1.0f, 0.0f, 0.0f, -(float)Math.PI / 2f);
                    // Make a clean matrix version.
                    // The libraries tend to create matrices with small numbers (1.119093e-07) for zero.
                    coordTransform = new OMV.Matrix4(
                                    1,  0,  0,  0,
                                    0,  0, -1,  0,
                                    0,  1,  0,  0,
                                    0,  0,  0,  1);
                }
                if (ep.coordSystem.getUVOrigin != newCoords.getUVOrigin) {
                    flipV = true;
                }

                // Fix the location in space
                // OMV.Vector3 transBefore = ep.translation;   // DEBUG DEBUG
                // OMV.Quaternion rotBefore = ep.rotation;   // DEBUG DEBUG
                if (ep.positionIsParentRelative) {
                    ep.translation = FixOneCoordinate(ep.translation, coordTransform);
                }
                else {
                    // If world relative, fix negative dimensions to be within region
                    // ep.translation = FixOneCoordinate(ep.translation, coordTransform, m_regionDimensions);
                    ep.translation = FixOneCoordinate(ep.translation, coordTransform);
                }
                ep.rotation = FixOneRotation(ep.rotation, coordTransform, coordTransformQ);
                // m_log.DebugFormat("{0} FixCoordinates. tBefore={1}, tAfter={2}, rBefore={3}, rAfter={4}",    // DEBUG DEBUG
                //         LogHeader, transBefore, ep.translation, rotBefore, ep.rotation);    // DEBUG DEBUG

                // Go through all the vertices and change the coordinate system
                PrimToMesh.OnAllVertex(ep, delegate (ref OMVR.Vertex vert) {
                    vert.Position = FixOneCoordinate(vert.Position, coordTransform);
                    vert.Normal = FixOneCoordinate(vert.Normal, coordTransform);
                    if (flipV) {
                        vert.TexCoord.Y = 1f - vert.TexCoord.Y;
                    }
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

        public OMV.Quaternion FixOneRotation(OMV.Quaternion rot, OMV.Matrix4 coordTransform, OMV.Quaternion coordTransformQ) {
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
            /*
            // OMV.Quaternion after = coordTransformQ * rot;
            OMV.Quaternion after = rot * coordTransformQ;
            */
            // m_log.DebugFormat("{0} FixOneRotation. before={1}, eulers={2}, convEulers={3}, after={4}",
            //             LogHeader, rot, eulers, convEulers, after);
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
            string targetDir = BasilPersist.ResolveAndCreateDir(pTargetDir);

            if (targetDir != null) {
                string gltfFilename = BasilPersist.JoinFilePieces(targetDir, regionName + ".gltf");
                using (StreamWriter outt = File.CreateText(gltfFilename)) {
                    gltf.ToJSON(outt);
                }
                gltf.WriteBinaryFiles(targetDir);
            }
        }

    }
}
