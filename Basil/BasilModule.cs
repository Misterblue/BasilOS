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
using System.Reflection;

using log4net;
using Mono.Addins;
using Nini.Config;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;


[assembly: Addin("Basil_Assets", "1.0")]
[assembly: AddinDependency("OpenSim", "0.8.2")]

namespace org.herbal3d.Basil {
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "Basil_Module")]
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
            m_log.DebugFormat("{0}: Initialise", LogHeader);

            // Load all the parameters
            m_params = new BasilParams();
            // Set the default values
            m_params.SetParameterDefaultValues();
            // Overlay the default parameter values with the settings in the INI file
            m_sysConfig = source.Configs["Basil"];
            if (m_sysConfig != null) {
                m_params.SetParameterConfigurationValues(m_sysConfig);
            }

            if (m_params.Enabled) {
                m_log.InfoFormat("{0}: Enabled", LogHeader);
            }
        }
        
        // IRegionModuleBase.Close()
        // Called when simulator is being shutdown
        public void Close() {
            m_log.DebugFormat("{0}: Close", LogHeader);
        }
        
        // IRegionModuleBase.AddRegion()
        // Called once for a NonSharedRegionModule when the region is initialized
        public void AddRegion(Scene scene) {
            if (m_params.Enabled) {
                m_scene = scene;
                m_log.DebugFormat("{0}: REGION {1} ADDED", LogHeader, scene.RegionInfo.RegionName);
            }
        }
        
        // IRegionModuleBase.RemoveRegion()
        // Called once for a NonSharedRegionModule when the region is being unloaded
        public void RemoveRegion(Scene scene) {
            m_log.DebugFormat("{0}: REGION {1} REMOVED", LogHeader, scene.RegionInfo.RegionName);
        }        
        
        // IRegionModuleBase.RegionLoaded()
        // Called once for a NonSharedRegionModule when the region is completed loading
        public void RegionLoaded(Scene scene) {
            if (m_params.Enabled) {
                m_log.DebugFormat("{0}: REGION {1} LOADED", LogHeader, scene.RegionInfo.RegionName);
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
            m_log.DebugFormat("{0}: ProcessConvert", LogHeader);

            using (PrimToMesh assetMesher = new PrimToMesh(m_log)) {

                using (IAssetFetcherWrapper assetFetcher = new OSAssetFetcher(m_scene, m_log)) {

                    m_scene.ForEachSOG(sog => {
                        ConvertSOG(sog, assetMesher, assetFetcher)
                            .Then(ePrimGroup => {
                            })
                            .Rejected(e => {
                            }
                        ); 

                    });
                }
            }
        }

        // Convert all prims in SOG into meshes and return the mesh group.
        private SimplePromise<EntityGroup> ConvertSOG(SceneObjectGroup sog, PrimToMesh mesher, IAssetFetcherWrapper assetFetcher ) {
            SimplePromise<EntityGroup> prom = new SimplePromise<EntityGroup>();

            EntityGroup meshes = new EntityGroup();

            int totalChildren = sog.Parts.GetLength(0);
            foreach (SceneObjectPart sop in sog.Parts) {
                OMV.Primitive aPrim = sop.Shape.ToOmvPrimitive();
                mesher.CreateMeshResource(sog, sop, aPrim, assetFetcher, OMVR.DetailLevel.Highest)
                    .Then(ePrimGroup => {
                        lock (meshes) {
                            m_log.DebugFormat("CreateAllMeshesInSOP: foreach oneSOP: {0}, primAsset={1}",
                                        sop.UUID, aPrim.ID);
                            meshes.Add(ePrimGroup);
                        }
                        if (--totalChildren <= 0) {
                            prom.Resolve(meshes);
                        }
                    })
                    .Rejected(e => {
                        prom.Reject(e);
                    });
            }
            return prom;
        }
    }
}
