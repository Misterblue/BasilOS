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

        public int numSimplePrims = 0;
        public int numMeshAssets = 0;
        public int numSculpties = 0;

        EntityGroupStats staticStats = null;
        EntityGroupStats nonStaticStats = null;
        EntityGroupStats rebuiltStats = null;

        public Scene m_scene;
        public ILog m_log;
        private static string LogHeader = "[Basil.Stats] ";

        public BasilStats(Scene pScene, ILog pLog) {
            m_scene = pScene;
            m_log = pLog;
        }

        // Gather statistics
        public void ExtractStatistics(BasilModule.ReorganizedScene reorgScene) {
            staticStats = StatsFromEntityGroupList("static", reorgScene.staticEntities);
            nonStaticStats = StatsFromEntityGroupList("nonStatic", reorgScene.nonStaticEntities);
            rebuiltStats = StatsFromEntityGroupList("rebuilt", reorgScene.rebuiltFaceEntities);
        }

        public class EntityGroupStats {
            public int numEntities = 0;
            public int numMeshes = 0;
            public int numLinksets = 0;
            public int numIndices = 0;
            public int numVertices = 0;
            public int numMaterials = 0;
            public int numTextures = 0;
        }

        public EntityGroupStats StatsFromEntityGroupList(string listName, EntityGroupList entityList) {
            EntityGroupStats egs = new EntityGroupStats();
            try {
                List<OMV.Primitive.TextureEntryFace> TEFs = new List<OMV.Primitive.TextureEntryFace>();
                List<OMV.UUID> TEXs = new List<OMV.UUID>();
                egs.numEntities = entityList.Count;
                entityList.ForEach(entity => {
                    if (entity.Count > 1) {
                        egs.numLinksets++;
                    }
                    entity.ForEach(epg => {
                        var ep = epg.primaryExtendePrim;
                        egs.numMeshes += ep.faces.Count;
                        foreach (FaceInfo fi in ep.faces.Values) {
                            egs.numIndices += fi.indices.Count;
                            egs.numVertices += fi.vertexs.Count;
                            if (!TEFs.Contains(fi.textureEntry)) {
                                TEFs.Add(fi.textureEntry);
                            }
                            if (fi.textureID != null && !TEXs.Contains((OMV.UUID)fi.textureID)) {
                                TEXs.Add((OMV.UUID)fi.textureID);
                            }
                        }
                    });
                });
                egs.numMaterials = TEFs.Count;
                egs.numTextures = TEXs.Count;
            }
            catch (Exception e) {
                m_log.ErrorFormat("{0}: Exception computing {1} stats: {2}", "StatsFromEntityGroupList", listName, e);
            }

            return egs;
        }


        // Output the non entitiy list info
        public void Log(string header) {
            m_log.InfoFormat("{0} numSimplePrims={1}", header, numSimplePrims);
            m_log.InfoFormat("{0} numSculpties={1}", header, numSculpties);
            m_log.InfoFormat("{0} numMeshAssets={1}", header, numMeshAssets);
        }

        public void Log(EntityGroupStats stats, string header) {
            m_log.InfoFormat("{0} numEntities={1}", header, stats.numEntities);
            m_log.InfoFormat("{0} numMeshes={1}", header, stats.numMeshes);
            m_log.InfoFormat("{0} numLinksets={1}", header, stats.numLinksets);
            m_log.InfoFormat("{0} numIndices={1}", header, stats.numIndices);
            m_log.InfoFormat("{0} numVertices={1}", header, stats.numVertices);
            m_log.InfoFormat("{0} numMaterials={1}", header, stats.numMaterials);
            m_log.InfoFormat("{0} numTextures={1}", header, stats.numTextures);
        }

        public void LogAll(string header) {
            Log(header + " " + m_scene.Name);

            if (staticStats != null) {
                Log(staticStats, header + " " + m_scene.Name + " static");
            }
            if (nonStaticStats != null) {
                Log(nonStaticStats, header + " " + m_scene.Name + " nonStatic");
            }
            if (rebuiltStats != null) {
                Log(rebuiltStats, header + " " + m_scene.Name + " rebuilt");
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
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
