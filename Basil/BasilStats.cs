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

        public int numEntities = 0;
        public int numStaticEntities = 0;
        public int numPrims = 0;
        public int numSculpties = 0;
        public int numMeshes = 0;
        public int numLinksets = 0;
        public int numStaticLinksets = 0;
        public int numFaces = 0;
        public int numNullTexturedFaces = 0;
        public int numMaterials = 0;

        public Dictionary<int, int> materialCount = new Dictionary<int, int>();
        public List<OMV.UUID> textureIDs = new List<OMV.UUID>();

        public Scene m_scene;
        public ILog m_log;

        public BasilStats(Scene pScene, ILog pLog) {
            m_scene = pScene;
            m_log = pLog;
            materialCount = new Dictionary<int, int>();
            textureIDs = new List<OMV.UUID>();
        }

        public void LogAll(string header) {
            m_log.InfoFormat("{0} ", header);
            m_log.InfoFormat("{0} {1} numEntities={2}", header, m_scene.Name, this.numEntities);
            m_log.InfoFormat("{0} {1} numStaticEntities={2}", header, m_scene.Name, this.numStaticEntities);
            m_log.InfoFormat("{0} {1} numLinksets={2}", header, m_scene.Name, this.numLinksets);
            m_log.InfoFormat("{0} {1} numStaticLinksets={2}", header, m_scene.Name, this.numStaticLinksets);
            m_log.InfoFormat("{0} {1} numPrims={2}", header, m_scene.Name, this.numPrims);
            m_log.InfoFormat("{0} {1} numSculpties={2}", header, m_scene.Name, this.numSculpties);
            m_log.InfoFormat("{0} {1} numMeshes={2}", header, m_scene.Name, this.numMeshes);
            m_log.InfoFormat("{0} {1} numFaces={2}", header, m_scene.Name, this.numFaces);
            m_log.InfoFormat("{0} {1} num unique materials={2}", header, m_scene.Name, this.numMaterials);
            m_log.InfoFormat("{0} {1} num null textured faces={2}", header, m_scene.Name, this.numNullTexturedFaces);
            m_log.InfoFormat("{0} {1} num unique texture IDs={2}", header, m_scene.Name, this.textureIDs.Count);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    materialCount.Clear();
                    materialCount = null;
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
