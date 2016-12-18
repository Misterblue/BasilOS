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
using System.Text;

using log4net;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;

namespace org.herbal3d.BasilOS {
    class MeshCache {
        ILog m_log;
        String LogHeader = "MeshCache:";

        private Dictionary<int, OMVR.FacetedMesh> m_cache;

        public MeshCache(ILog pLog) {
            m_log = pLog;
            m_cache = new Dictionary<int, OMVR.FacetedMesh>();
        }

        public bool Contains(int pMeshHash) {
            return (m_cache.ContainsKey(pMeshHash));
        }

        public void Add(int pMeshHash, OMVR.FacetedMesh pFMesh) {
            m_log.DebugFormat("{0} Add. Add. hash={1}, id={2}",
                            LogHeader, pMeshHash, pFMesh.Prim.ID.ToString());
            m_cache.Add(pMeshHash, pFMesh);
        }

        public OMVR.FacetedMesh GetMesh(int pFHash) {
            OMVR.FacetedMesh ret = null;
            m_cache.TryGetValue(pFHash, out ret);
            m_log.DebugFormat("{0} Add. GetMesh. Hash={1}, id={2}",
                            LogHeader, pFHash, ret.Prim.ID.ToString());
            return ret;
        }
    }
}
