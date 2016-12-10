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
            m_log.DebugFormat("{0} Initialise", LogHeader);

            // Load all the parameters
            m_params = new BasilParams();
            // Overlay the default parameter values with the settings in the INI file
            m_sysConfig = source.Configs["Basil"];
            if (m_sysConfig != null) {
                m_log.DebugFormat("{0} before calling SetParameterConfigurationValues", LogHeader);
                m_params.SetParameterConfigurationValues(m_sysConfig);
                m_log.DebugFormat("{0} after calling SetParameterConfigurationValues", LogHeader);
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

                using (BasilStats stats = new BasilStats(m_scene, m_log)) {

                    using (IAssetFetcherWrapper assetFetcher = new OSAssetFetcher(m_scene, m_log)) {

                        using (PrimToMesh assetMesher = new PrimToMesh(m_log)) {

                            m_scene.ForEachSOG(sog => {
                                ConvertSOG(sog, assetMesher, assetFetcher, stats)
                                    .Then(ePrimGroup => {
                                        allSOGs.Add(ePrimGroup);
                                    })
                                    .Catch(e => {
                                        m_log.ErrorFormat("{0} Error converting SOG. UUID={1}: {2}", LogHeader, sog.UUID, e);
                                    }
                                ); 

                            });
                        }

                        // Everything has been converted into meshes and available in 'allSOGs'.
                        stats.numEntities = allSOGs.Count;  // total number of entities
                        allSOGs.ForEach(eGroup => {
                            eGroup.ForEach(ePGroup => {
                                if (ePGroup.Count > 1) {
                                    // if the entity is made of multiple pieces, they are a linkset
                                    stats.numLinksets++;
                                }
                                foreach (KeyValuePair<PrimGroupType, ExtendedPrim> kvp in ePGroup) {
                                    ExtendedPrim ep = kvp.Value;
                                    if (0 != (int)ep.SOP.ScriptEvents) {
                                        stats.numEntitiesWithScripts++;
                                    }
                                    OMV.Primitive.TextureEntry tex = ep.SOP.Shape.Textures;

                                }
                            });
                        });
                    }

                    m_log.InfoFormat("{0} ", LogHeader);
                    m_log.InfoFormat("{0} {1} numPrims={2}", LogHeader, m_scene.Name, stats.numPrims);
                    m_log.InfoFormat("{0} {1} numSculpties={2}", LogHeader, m_scene.Name, stats.numSculpties);
                    m_log.InfoFormat("{0} {1} numMeshes={2}", LogHeader, m_scene.Name, stats.numMeshes);
                    m_log.InfoFormat("{0} {1} numEntities={2}", LogHeader, m_scene.Name, stats.numEntities);
                    m_log.InfoFormat("{0} {1} numLinksets={2}", LogHeader, m_scene.Name, stats.numLinksets);
                    m_log.InfoFormat("{0} {1} numEntitiesWithScripts={2}", LogHeader, m_scene.Name, stats.numEntitiesWithScripts);
                }
            }
        }

        // Convert all prims in SOG into meshes and return the mesh group.
        private IPromise<EntityGroup> ConvertSOG(SceneObjectGroup sog, PrimToMesh mesher,
                        IAssetFetcherWrapper assetFetcher, BasilStats stats ) {
            m_log.DebugFormat("{0}: ConvertSOG", LogHeader);
            var prom = new Promise<EntityGroup>();

            EntityGroup meshes = new EntityGroup();

            int totalChildren = sog.Parts.GetLength(0);
            foreach (SceneObjectPart sop in sog.Parts) {
                OMV.Primitive aPrim = sop.Shape.ToOmvPrimitive();
                mesher.CreateMeshResource(sog, sop, aPrim, assetFetcher, OMVR.DetailLevel.Highest, stats)
                    .Then(ePrimGroup => {
                        lock (meshes) {
                            m_log.DebugFormat("{0}: CreateAllMeshesInSOP: foreach oneSOP: {1}",
                                        LogHeader, sop.UUID);
                            meshes.Add(ePrimGroup);
                        }
                        // can't tell what order the prims are completed in so wait until they are all meshed
                        if (--totalChildren <= 0) {
                            prom.Resolve(meshes);
                        }
                    })
                    .Catch(e => {
                        m_log.ErrorFormat("{0}: ConvertSOG: failed conversion: {1}", LogHeader, e);
                        prom.Reject(e);
                    });
            }
            return prom;
        }
    }
}
