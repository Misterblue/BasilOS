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

using log4net;

using OpenSim.Region.Framework.Scenes;

using OMV = OpenMetaverse;

namespace org.herbal3d.BasilOS {
    public class BasilStats : IDisposable {

        public int numPrims = 0;
        public int numSculpties = 0;
        public int numMeshes = 0;
        public int numEntities = 0;
        public int numLinksets = 0;
        public int numEntitiesWithScripts = 0;
        public int numFaces = 0;
        public int numNullTexturedFaces = 0;

        public Dictionary<int, int> textureCount = new Dictionary<int, int>();
        public List<OMV.UUID> textureIDs = new List<OMV.UUID>();

        public Scene m_scene;
        public ILog m_log;

        public BasilStats(Scene pScene, ILog pLog) {
            m_scene = pScene;
            m_log = pLog;
            textureCount = new Dictionary<int, int>();
            textureIDs = new List<OMV.UUID>();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    textureCount.Clear();
                    textureCount = null;
                    textureIDs.Clear();
                    textureIDs = null;
                }
                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~BasilStats() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
