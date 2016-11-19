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

[assembly: Addin("Basil_Assets", "1.0")]
[assembly: AddinDependency("OpenSim", "0.8.2")]

namespace org.herbal3d.Basil {
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "Basil_Module")]
    public class BasilAssets : INonSharedRegionModule {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static String LogHeader = "[Basil]";

        private BasilParams m_params;
        private IConfig m_sysConfig = null;

        #region INonSharedRegionNodule
        // IRegionModuleBase.Name()
        public string Name { get { return "BasilModule"; } }        
        
        // IRegionModuleBase.ReplaceableInterface()
        public Type ReplaceableInterface { get { return null; } }
        
        // IRegionModuleBase.ReplaceableInterface()
        // Called when simulator first loaded
        public void Initialise(IConfigSource source) {
            m_log.DebugFormat("{0}: Initialise", LogHeader);

            m_params = new BasilParams();
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
        }
    }
}
