﻿/*
 * Copyright (c) 2017 Robert Adams
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
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using log4net;

using RSG;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;


namespace org.herbal3d.BasilOS {
    public class BConverterOS {

        private static string LogHeader = "BConverterOS";

        private IAssetFetcher _assetFetcher;
        private PrimToMesh _mesher;
        private BasilStats _stats;
        private BasilModuleContext _context;

        public BConverterOS(IAssetFetcher assetFetcher, PrimToMesh mesher, BasilModuleContext context, BasilStats stats) {
            _assetFetcher = assetFetcher;
            _mesher = mesher;
            _context = context;
            _stats = stats;
        }

        // Convert a SceneObjectGroup into an EntityGroup
        public IPromise<EntityGroup> Convert(SceneObjectGroup sog, IAssetFetcher assetFetcher) {
            var prom = new Promise<EntityGroup>();

            // Create meshes for all the parts of the SOG
            Promise<ExtendedPrimGroup>.All(
                sog.Parts.Select(sop => {
                    OMV.Primitive aPrim = sop.Shape.ToOmvPrimitive();
                    return _mesher.CreateMeshResource(sog, sop, aPrim, _assetFetcher, OMVR.DetailLevel.Highest, _stats);
                } )
            )
            // Tweak the parts individually (scale, texturize, ...)
            .Then(epgs => {
                return epgs.Select(epg => {
                    // If scaling is done in the mesh, do it now
                    if (!_context.parms.DisplayTimeScaling) {
                        PrimToMesh.ScaleMeshes(epg);
                        foreach (ExtendedPrim ep in epg.Values) {
                            ep.scale = new OMV.Vector3(1, 1, 1);
                        }
                    }

                    // The prims in the group need to be decorated with texture/image information
                    UpdateTextureInfo(epg, assetFetcher);

                    return epg;
                });
            })
            .Catch(e => {
                _context.log.ErrorFormat("{0} Failed meshing of SOG. ID={1}: {2}", LogHeader, sog.UUID, e);
                prom.Reject(new Exception(String.Format("failed meshing of SOG. ID={0}: {1}", sog.UUID, e)));
            })
            .Done (epgs => {
                prom.Resolve(new EntityGroup(epgs.ToList()));
            }) ;

            return prom;
        }

        // Convert a SceneObjectPart into an ExtendedPrimGroup
        public IPromise<ExtendedPrimGroup> Convert(SceneObjectGroup sog, SceneObjectPart sop) {

            return null;
        }

        /// <summary>
        /// Scan through all the ExtendedPrims and finish any texture updating.
        /// This includes UV coordinate mappings and fetching any image that goes with the texture.
        /// </summary>
        /// <param name="epGroup">Collections of meshes to update</param>
        /// <param name="assetFetcher">Fetcher for getting images, etc</param>
        /// <param name="pMesher"></param>
        private void UpdateTextureInfo(ExtendedPrimGroup epGroup, IAssetFetcher assetFetcher) {
            ExtendedPrim ep = epGroup.primaryExtendePrim;
            foreach (FaceInfo faceInfo in ep.faces) {

                // While we're in the neighborhood, map the texture coords based on the prim information
                _mesher.UpdateCoords(faceInfo, ep.fromOS.primitive);

                UpdateFaceInfoWithTexture(faceInfo, assetFetcher);
            }
        }

        // Check to see if the FaceInfo has a textureID and, if so, read it in and populate the FaceInfo
        //    with that texture data.
        private void UpdateFaceInfoWithTexture(FaceInfo faceInfo, IAssetFetcher assetFetcher) {
            // If the texture includes an image, read it in.
            OMV.UUID texID = faceInfo.textureEntry.TextureID;
            try {
                faceInfo.hasAlpha = (faceInfo.textureEntry.RGBA.A != 1.0f);
                if (texID != OMV.UUID.Zero && texID != OMV.Primitive.TextureEntry.WHITE_TEXTURE) {
                    faceInfo.textureID = texID;
                    faceInfo.persist = new BasilPersist(Gltf.MakeAssetURITypeImage, texID.ToString(), _context);
                    faceInfo.persist.GetUniqueTextureData(faceInfo, assetFetcher)
                        .Catch(e => {
                            // Could not get the texture. Print error and otherwise blank out the texture
                            faceInfo.textureID = null;
                            faceInfo.faceImage = null;
                            _context.log.ErrorFormat("{0} UpdateTextureInfo. {1}", LogHeader, e);
                        })
                        .Then(theImage => {
                            faceInfo.faceImage = theImage;
                            if (!faceInfo.hasAlpha && Image.IsAlphaPixelFormat(theImage.PixelFormat)) {
                                // The image could have alpha values in it
                                Bitmap bitmapImage = theImage as Bitmap;
                                if (bitmapImage != null) {
                                    for (int xx = 0; xx < bitmapImage.Width; xx++) {
                                        for (int yy = 0; yy < bitmapImage.Height; yy++) {
                                            if (bitmapImage.GetPixel(xx, yy).A != 255) {
                                                faceInfo.hasAlpha = true;
                                                break;
                                            }
                                        }
                                        if (faceInfo.hasAlpha)
                                            break;
                                    }
                                }
                                else {
                                    _context.log.DebugFormat("{0} UpdateTextureInfo. Couldn't check for Alpha because image not a bitmap", LogHeader);
                                }
                            }
                        });
                }
            }
            catch (Exception e) {
                _context.log.ErrorFormat("{0}: UpdateFaceInfoWithTexture: exception updating faceInfo. id={1}: {2}",
                                    LogHeader, texID, e);
            }
        }


    }
}
