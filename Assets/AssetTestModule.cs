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

using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

[assembly: Addin("Basil_Assets", "1.0")]
[assembly: AddinDependency("OpenSim", "0.8.2")]

namespace Basil.Assets
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "Basil_Assets")]
    public class BasilAssets : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static String LogHeader = "[Basil.Assets]";

        private BasilParams m_params;
        private IConfig m_sysConfig = null;

        #region INonSharedRegionNodule
        // IRegionModuleBase.Name()
        public string Name { get { return "Basil.Assets"; } }        
        
        // IRegionModuleBase.ReplaceableInterface()
        public Type ReplaceableInterface { get { return null; } }
        
        // IRegionModuleBase.ReplaceableInterface()
        public void Initialise(IConfigSource source)
        {
            m_log.DebugFormat("{0}: INITIALIZED MODULE", LogHeader);
            m_params = new BasilParams();
            m_sysConfig = source.Configs["Basil.Assets"];
            if (m_sysConfig != null)
            {
                m_params.SetParameterConfigurationValues(m_sysConfig);
            }
            if (m_params.Enabled)
            {
                m_log.InfoFormat("{0}: Enabled", LogHeader);
            }
        }
        
        // IRegionModuleBase.Close()
        public void Close()
        {
            m_log.DebugFormat("{0}: CLOSED MODULE", LogHeader);
        }
        
        // IRegionModuleBase.AddRegion()
        public void AddRegion(Scene scene)
        {
            m_log.DebugFormat("{0}: REGION {1} ADDED", LogHeader, scene.RegionInfo.RegionName);
        }
        
        // IRegionModuleBase.RemoveRegion()
        public void RemoveRegion(Scene scene)
        {
            m_log.DebugFormat("{0}: REGION {1} REMOVED", LogHeader, scene.RegionInfo.RegionName);
        }        
        
        // IRegionModuleBase.RegionLoaded()
        public void RegionLoaded(Scene scene)
        {
            m_log.DebugFormat("{0}: REGION {1} LOADED", LogHeader, scene.RegionInfo.RegionName);
        }                
        #endregion // INonSharedRegionNodule
    }
}
