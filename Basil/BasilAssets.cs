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

using log4net;

using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

using OMV = OpenMetaverse;
using OMVA = OpenMetaverse.Assets;

namespace org.herbal3d.Basil {

    // A SimplePromise based interface to the asset fetcher
    public abstract class IAssetFetcherWrapper : IDisposable {
        public abstract SimplePromise<OMVA.AssetTexture> FetchTexture(EntityHandle handle);
        public abstract SimplePromise<byte[]> FetchRawAsset(EntityHandle handle);
        public abstract void Dispose();
    }

    // Fetch an asset from  the OpenSimulator asset system
    public class OSAssetFetcher : IAssetFetcherWrapper {
        private ILog m_log;
        private string LogHeader = "[OSAssetFetcher]";

        private Scene m_scene;

        public OSAssetFetcher(Scene scene, ILog logger) {
            m_scene = scene;
            m_log = logger;

            IAssetService frog = m_scene.AssetService;
        }

        public override SimplePromise<byte[]> FetchRawAsset(EntityHandle handle) {
            SimplePromise<byte[]> prom = new SimplePromise<byte[]>();

            // Don't bother with async -- this call will hang until the asset is fetched
            byte[] returnBytes = m_scene.AssetService.GetData(handle.GetOSAssetString());
            if (returnBytes.Length > 0) {
                prom.Resolve(returnBytes);
            }
            else {
                prom.Reject(new Exception("FetchRawAsset: could not fetch asset " + handle.ToString()));
            }
            return prom;
        }

        public override SimplePromise<OMVA.AssetTexture> FetchTexture(EntityHandle handle) {
            SimplePromise<OMVA.AssetTexture> prom = new SimplePromise<OMVA.AssetTexture>();

            // Don't bother with async -- this call will hang until the asset is fetched
            AssetBase asset = m_scene.AssetService.Get(handle.GetOSAssetString());
            if (asset.IsBinaryAsset && asset.Type == (sbyte)OMV.AssetType.Texture) {
                OMVA.AssetTexture tex = new OMVA.AssetTexture(handle.GetUUID(), asset.Data);
                if (tex.Decode()) {
                    prom.Resolve(tex);
                }
                else {
                    prom.Reject(new Exception("FetchTexture: could not decode JPEG2000 texture. ID=" + handle.ToString()));
                }
            }
            else {
                prom.Reject(new Exception("FetchTexture: asset was not of type texture. ID=" + handle.ToString()));
            }

            return prom;
        }

        public override void Dispose() {
            m_scene = null;
        }
    }

    class BasilAssetss {
    }
}
