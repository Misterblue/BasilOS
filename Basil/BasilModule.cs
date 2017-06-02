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
        public IConfig sysConfig;
        public BasilParams parms;
        public BasilStats stats;
        public ILog log;
        public string contextName;  // a unique identifier for this context -- used in filenames, ...

        public BasilModuleContext(IConfig pSysConfig, BasilParams pParms, ILog pLog) {
            sysConfig = pSysConfig;
            parms = pParms;
            log = pLog;
            stats = null;
            contextName = String.Empty;
        }
    }

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "BasilModule")]
    public class BasilModule : INonSharedRegionModule {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static String _logHeader = "[Basil]";

        private BasilParams _params;
        private IConfig _sysConfig = null;

        private BasilModuleContext _context;

        private Scene _scene;

        private OMV.Vector3 _regionDimensions = new OMV.Vector3(Constants.RegionSize, Constants.RegionSize, 10000f);
        private OMV.Vector3 _regionCenter = new OMV.Vector3(Constants.RegionSize/2, Constants.RegionSize/2, 0f);

        #region INonSharedRegionNodule
        // IRegionModuleBase.Name()
        public string Name { get { return "BasilModule"; } }        
        
        // IRegionModuleBase.ReplaceableInterface()
        public Type ReplaceableInterface { get { return null; } }
        
        // IRegionModuleBase.ReplaceableInterface()
        // Called when simulator first loaded
        public void Initialise(IConfigSource source) {

            // Load all the parameters
            _params = new BasilParams();
            // Overlay the default parameter values with the settings in the INI file
            _sysConfig = source.Configs["Basil"];
            if (_sysConfig != null) {
                _params.SetParameterConfigurationValues(_sysConfig);
            }

            if (_params.Enabled) {
                _log.InfoFormat("{0} Enabled", _logHeader);
            }

            _context = new BasilModuleContext(_sysConfig, _params, _log);
        }
        
        // IRegionModuleBase.Close()
        // Called when simulator is being shutdown
        public void Close() {
            _log.DebugFormat("{0} Close", _logHeader);
        }
        
        // IRegionModuleBase.AddRegion()
        // Called once for a NonSharedRegionModule when the region is initialized
        public void AddRegion(Scene scene) {
            if (_params.Enabled) {
                _scene = scene;
                _context.contextName = _scene.Name;
                _regionDimensions.X = _scene.RegionInfo.RegionSizeX;
                _regionDimensions.Y = _scene.RegionInfo.RegionSizeY;
                _regionCenter.X = _regionDimensions.X / 2f;
                _regionCenter.Y = _regionDimensions.Y / 2f;

                _log.DebugFormat("{0} REGION {1} ADDED", _logHeader, scene.RegionInfo.RegionName);
            }
        }
        
        // IRegionModuleBase.RemoveRegion()
        // Called once for a NonSharedRegionModule when the region is being unloaded
        public void RemoveRegion(Scene scene) {
            _log.DebugFormat("{0} REGION {1} REMOVED", _logHeader, scene.RegionInfo.RegionName);
        }        
        
        // IRegionModuleBase.RegionLoaded()
        // Called once for a NonSharedRegionModule when the region is completed loading
        public void RegionLoaded(Scene scene) {
            if (_params.Enabled) {
                _log.DebugFormat("{0} REGION {1} LOADED", _logHeader, scene.RegionInfo.RegionName);
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
                _log.Error("Error: no region selected. Use 'change region' to select a region.");
                return;
            }

            // m_log.DebugFormat("{0} ProcessConvert. CurrentScene={1}, _context.scene={2}", LogHeader,
            //             SceneManager.Instance.CurrentScene.Name, _context.scene.Name);

            if (SceneManager.Instance.CurrentScene.Name == _scene.Name) {

                _context.stats = new BasilStats(_scene, _log);
                BConverter converter = new BConverter(_context);

                using (IAssetFetcher assetFetcher = new OSAssetFetcher(_scene, _log, _params)) {

                    try {

                        ConvertEntitiesToMeshes(assetFetcher)
                            .Catch(e => {
                                _log.ErrorFormat("{0} exception in ConvertEntitiesToMeshes: {1}", _logHeader, e);
                            })
                            .Then(allSOGs => {
                                // Everything has been converted into meshes and available in 'allSOGs'.
                                _log.DebugFormat("{0} Converted {1} scene entities", _logHeader, allSOGs.Count);
                                if (_params.LogDetailedEntityInfo) {
                                    _context.stats.DumpDetailed(allSOGs);
                                }

                                // Scan the entities and reorganize into static/non-static and find shared face meshes
                                ReorganizedScene reorgScene = ReorganizeScene(allSOGs, "region_" + _scene.Name.ToLower());

                                // Scene objects in reorgScene.nonStaticEntities and reorgScene.staticEntities

                                // Creates reorgScene.rebuiltFaceEntities from reorgScene.similarFaces
                                //     by repositioning the vertices in the shared meshes so they act as one mesh
                                if (_params.MergeStaticMeshes) {
                                    try {
                                        reorgScene.rebuiltFaceEntities = converter.ConvertEntitiesIntoSharedMaterialMeshes(reorgScene.staticEntities);
                                    }
                                    catch (Exception e) {
                                        _log.ErrorFormat("{0} Exception calling ConvertEntitiesIntoSharedMaterialMeshes: {1}", _logHeader, e);
                                    }
                                }
                                else {
                                    // if we're not rebuilding the scene, the static entries are what's used
                                    reorgScene.rebuiltFaceEntities = reorgScene.staticEntities;
                                }

                                if (_params.MergeNonStaticMeshes) {
                                    try {
                                        // Repack all the non-static entities
                                        // The non-static entities are packaged so they can move as a group.
                                        // This means the similar faces are only checked within the entity rather than across the region.
                                        reorgScene.rebuiltNonStaticEntities = new EntityGroupList(
                                                reorgScene.nonStaticEntities.Select(eg => {
                                                    return converter.ConvertEntityGroupIntoSharedMaterialMeshes(eg);
                                                }).ToList()
                                            );
                                    }
                                    catch (Exception e) {
                                        _log.ErrorFormat("{0} Exception calling ConvertEntityGroupIntoSharedMaterialMeshes: {1}", _logHeader, e);
                                    }
                                }
                                else {
                                    // if we're not rebuilding the scene, the static entries are what's used
                                    reorgScene.rebuiltNonStaticEntities = reorgScene.nonStaticEntities;
                                }

                                // Scan all the entities and extract statistics
                                if (_params.LogConversionStats) {
                                    _context.stats.ExtractStatistics(reorgScene);
                                    _context.stats.LogAll(_logHeader);
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
                                    _log.ErrorFormat("{0} Exception calling ConvertReorgSceneToGltf: {1}", _logHeader, e);
                                }

                                // Scan through all the textures and convert them into PNGs for the Gltf scene
                                try {
                                    if (_params.ExportTextures) {
                                        _log.DebugFormat("{0} exporting textures", _logHeader);
                                        WriteOutImages(reorgScene);
                                    }
                                }
                                catch (Exception e) {
                                    _log.ErrorFormat("{0} Exception calling WriteOutImages: {1}", _logHeader, e);
                                }

                                // Write out the Gltf information
                                if (gltf != null) {
                                    try {
                                        ExportSceneAsGltf(gltf, _scene.Name, _params.GltfTargetDir);
                                    }
                                    catch (Exception e) {
                                        _log.ErrorFormat("{0} Exception calling ExportSceneAsGltf: {1}", _logHeader, e);
                                    }
                                }
                                else {
                                    _log.InfoFormat("{0} Not exporting GLTF files because conversion failed", _logHeader);
                                }
                            })
                        ;
                    }
                    catch (Exception e) {
                        _log.ErrorFormat("{0} Exception parocessing SOGs: {1}", _logHeader, e);
                    }
                }
            }
        }

        // For each of the SceneObjectGroups in the scene, create an EntityGroup with everything converted to meshes
        // Also add the terrain if needed.
        private IPromise<EntityGroupList> ConvertEntitiesToMeshes(IAssetFetcher assetFetcher) {
            Promise<EntityGroupList> prom = new Promise<EntityGroupList>();

            using (PrimToMesh assetMesher = new PrimToMesh(_log)) {

                BConverterOS converter = new BConverterOS(assetFetcher, assetMesher, _context);

                Promise<EntityGroup>.All(
                    _scene.GetSceneObjectGroups().Select(sog => {
                        return converter.Convert(sog, assetFetcher);
                    })
                )
                .Catch(e => {
                    _log.ErrorFormat("{0} Error converting SOG. {1}", _logHeader, e);
                    prom.Reject(new Exception("Failed to convert SOG: " + e.ToString()));
                })
                .Done(eg => {
                    EntityGroupList egl = new EntityGroupList(eg.ToList());

                    // If terrain is requested, add it to the list of scene entities
                    if (_params.AddTerrainMesh) {
                        _log.DebugFormat("{0} ConvertEntitiesToMeshes: building and adding terrain", _logHeader);
                        try {
                            var ePrimGroup = BasilTerrain.CreateTerrainMesh(_context, _scene, assetMesher, assetFetcher);
                            _log.DebugFormat("{0} ConvertEntitiesToMeshes: completed creation. Adding to mesh set", _logHeader);
                            egl.Add(ePrimGroup);
                            prom.Resolve(egl);
                        }
                        catch (Exception e) {
                            _log.ErrorFormat("{0} Error creating terrain: {1}", _logHeader, e);
                            prom.Reject(new Exception("Failed to create terrain: " + e.ToString()));
                        }
                    }
                    else {
                        _log.DebugFormat("{0} ConvertEntitiesToMeshes: not creating terrain. Just resolving", _logHeader);
                        prom.Resolve(egl);
                    }
                });
            }
            return prom;
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
            Gltf gltf = new Gltf(_log);

            GltfScene gScene = new GltfScene(gltf, sceneName);

            try {
                groupsToConvert.ForEach(eg => {
                    AddNodeToGltf(gltf, gScene, eg);
                });
            }
            catch (Exception e) {
                _log.ErrorFormat("{0} ConvertReorgSceneToGltf: exception converting node: {1}", _logHeader, e);
            }

            // After adding all the meshes as nodes, create all the dependent structures
            gltf.BuildAccessorsAndBuffers(new BasilPersist(Gltf.MakeAssetURITypeImage, "", _context), _context);

            _log.DebugFormat("{0} ConvertReorgSceneToGltf. Returniing gltf", _logHeader);
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
                _log.DebugFormat("{0} FixCoordinates. Not converting coord system. ep={1}",
                                _logHeader, ep.ID);
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
