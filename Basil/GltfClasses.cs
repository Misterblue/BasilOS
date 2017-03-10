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
using System.Text;
using System.Collections.Generic;
using System.IO;

using log4net;

// I hoped to keep the Gltf classes separate from the OMV requirement but
//    it doesn't make sense to copy all the mesh info into new structures.
using OMV = OpenMetaverse;
using OMVR = OpenMetaverse.Rendering;

namespace org.herbal3d.BasilOS {

    public abstract class GltfClass {
        public Gltf gltfRoot;
        public string ID;
        public abstract void ToJSON(StreamWriter outt, int level);

        public GltfClass() { }
        public GltfClass(Gltf pRoot, string pID) {
            gltfRoot = pRoot;
            ID = pID;
        }

        //====================================================================
        // Useful routines for creating the JSON output
        public static string ParamsToJSONArray(params Object[] vals) {
            StringBuilder buff = new StringBuilder();
            buff.Append(" [ ");
            bool first = true;
            foreach (object obj in vals) {
                if (!first) buff.Append(", ");
                buff.Append(obj.ToString());
                first = false;
            }
            buff.Append(" ] ");
            return buff.ToString();
        }

        public static string ArrayToJSONArray(float[] vals) {
            StringBuilder buff = new StringBuilder();
            buff.Append(" [ ");
            bool first = true;
            foreach (object obj in vals) {
                if (!first) buff.Append(", ");
                buff.Append(obj.ToString());
                first = false;
            }
            buff.Append(" ]");
            return buff.ToString();
        }

        public static string Indent(int level) {
            return "\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t".Substring(0, level);
        }

        // Used to output lines of JSON values. Used in the pattern:
        //    public void ToJSON(StreamWriter outt, int level) {
        //        outt.Write(" { ");
        //        bool first = true;
        //        foreach (KeyValuePair<string, Object> kvp in this) {
        //            first = WriteJSONValueLine(outt, level, first, kvp.Key, kvp.Value);
        //        }
        //        outt.Write("\n" + GltfClass.Indent(level) + "}\n");
        //    }
        public static void WriteJSONValueLine(StreamWriter outt, int level, ref bool first, string key, Object val) {
            if (val != null) {
                WriteJSONLineEnding(outt, ref first);
                outt.Write(GltfClass.Indent(level) + "\"" + key + "\": " + CreateJSONValue(val));
            }
        }

        // Used to end the last line of output JSON. If there was something before, a comma is needed
        public static void WriteJSONLineEnding(StreamWriter outt, ref bool first) {
            if (first)
                outt.Write("\n");
            else
                outt.Write(",\n");
            first = false;
        }

        // Examines passed object and creates the correct form of a JSON value.
        // Strings are closed in quotes, arrays get square bracketed, and numbers are stringified.
        public static string CreateJSONValue(Object val) {
            string ret = String.Empty;
            if (val is string) {
                ret = "\"" + val + "\""; 
            }
            else if (val is bool) {
                ret = (bool)val ? "true" : "false";
            }
            else if (val is OMV.Color4) {
                OMV.Color4 col = (OMV.Color4)val;
                ret = ParamsToJSONArray(col.R, col.G, col.B, col.A);
            }
            else if (val is OMV.Matrix4) {
                OMV.Matrix4 mat = (OMV.Matrix4)val;
                ret = ParamsToJSONArray(
                    mat[0,0], mat[0,1], mat[0,2], mat[0,3],
                    mat[1,0], mat[1,1], mat[1,2], mat[1,3],
                    mat[2,0], mat[2,1], mat[2,2], mat[2,3],
                    mat[3,0], mat[3,1], mat[3,2], mat[3,3]
                );
            }
            else if (val is OMV.Vector3) {
                OMV.Vector3 vect = (OMV.Vector3)val;
                ret = ParamsToJSONArray(vect.X, vect.Y, vect.Z);
            }
            else if (val is OMV.Quaternion) {
                OMV.Quaternion quan = (OMV.Quaternion)val;
                ret = ParamsToJSONArray(quan.X, quan.Y, quan.Z, quan.W);
            }
            else if (val.GetType().IsArray) {
                ret = " [ ";
                object[] values = (object[])val;
                bool first = true;
                for (int ii = 0; ii < values.Length; ii++) {
                    if (!first) ret += ",";
                    first = false;
                    ret += CreateJSONValue(values[ii]);
                }
                ret += " ]";
            }
            else {
                ret = val.ToString();
            }
            return ret;
        }

    }

    public abstract class GltfListClass<T> : List<T> {
        public Gltf gltfRoot;
        public string ID;
        public abstract void ToJSON(StreamWriter outt, int level);
        public abstract void ToJSONIDArray(StreamWriter outt, int level);
        public GltfListClass(Gltf pRoot) {
            gltfRoot = pRoot;
        }

        public void ToJSONArrayOfIDs(StreamWriter outt, int level) {
            outt.Write("[ ");
            if (this.Count != 0)
                outt.Write("\n");
            bool first = true;
            this.ForEach(xx => {
                if (!first) {
                    outt.Write(",\n");
                }
                GltfClass gl = xx as GltfClass;
                outt.Write(GltfClass.Indent(level) + "\"" + gl.ID +"\"");
                first = false;
            });
            outt.Write("]");
        }

        public void ToJSONMapOfJSON(StreamWriter outt, int level) {
            outt.Write("{ ");
            if (this.Count != 0)
                outt.Write("\n");
            bool first = true;
            this.ForEach(xx => {
                if (!first) {
                    outt.Write(",\n");
                }
                GltfClass gl = xx as GltfClass;
                outt.Write(GltfClass.Indent(level) + "\"" + gl.ID + "\": ");
                gl.ToJSON(outt, level+1);
                first = false;
            });
            outt.Write(" }");
        }
    }

    public class GltfVector16 : GltfClass {
        public float[] vector = new float[16];

        public GltfVector16() : base() {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" [ ");
            for (int ii = 0; ii < vector.Length; ii++) {
                if (ii > 0) outt.Write(",");
                outt.Write(vector[ii].ToString());
            }
            outt.Write(" ] ");
        }
    }

    // =============================================================
    public class Gltf : GltfClass {
        ILog m_log;
        private static string LogHeader = "Gltf";

        public GltfAttributes extensionsUsed;   // list of extensions used herein

        public string defaultSceneID;   // ID of default scene

        public GltfAsset asset;
        public GltfScenes scenes;       // scenes that make up this package
        public GltfNodes nodes;         // nodes in the scenes
        public GltfMeshes meshes;       // the meshes for the nodes
        public GltfMaterials materials; // materials that make up the meshes
        public GltfAccessors accessors; // access to the mesh bin data
        public GltfBufferViews bufferViews; //
        public GltfBuffers buffers; //
        public GltfTechniques techniques;
        public GltfPrograms programs;
        public GltfShaders shaders;
        public GltfTextures textures;
        public GltfImages images;
        public GltfSamplers samplers;

        public Gltf(ILog pLogger) : base() {
            m_log = pLogger;

            gltfRoot = this;

            extensionsUsed = new GltfAttributes();
            asset = new GltfAsset(this);
            scenes = new GltfScenes(this);
            nodes = new GltfNodes(this);
            meshes = new GltfMeshes(this);
            materials = new GltfMaterials(this);
            accessors = new GltfAccessors(this);
            bufferViews = new GltfBufferViews(this);
            buffers = new GltfBuffers(this);
            techniques = new GltfTechniques(this);
            programs = new GltfPrograms(this);
            shaders = new GltfShaders(this);
            textures = new GltfTextures(this);
            images = new GltfImages(this);
            samplers = new GltfSamplers(this);
        }

        // Say this scene is using the extension.
        public void UsingExtension(string extName) {
            if (!extensionsUsed.ContainsKey(extName)) {
                extensionsUsed.Add(extName, null);
            }
        }

        // Function called below to create a URI from an asset ID.
        // 'type' may be one of 'image', 'mesh', ?
        // public delegate string MakeAssetURI(string type, OMV.UUID uuid);
        public delegate void MakeAssetURI(string type, string info, out string filename, out string uri);
        public const string MakeAssetURITypeImage = "image";    // image of type PNG
        public const string MakeAssetURITypeMesh = "mesh";
        public const string MakeAssetURITypeBuff = "buff";      // binary buffer

        // Meshes with FaceInfo's have been added to the scene. Pass over all
        //   the meshes and create the Primitives, Materials, and Images.
        // Called before calling ToJSON().
        public void BuildPrimitives(MakeAssetURI makeAssetURI) {
            // 20170201: ThreeJS defaults to GL_CLAMP but GLTF should default to GL_REPEAT/WRAP
            // Create a sampler for all the textures that forces WRAPing
            GltfSampler defaultSampler = new GltfSampler(gltfRoot, "simpleTextureRepeat");
            defaultSampler.values.Add("magFilter", WebGLConstants.LINEAR);
            defaultSampler.values.Add("minFilter", WebGLConstants.LINEAR_MIPMAP_LINEAR);
            defaultSampler.values.Add("wrapS", WebGLConstants.REPEAT);
            defaultSampler.values.Add("wrapT", WebGLConstants.REPEAT);

            meshes.ForEach(mesh => {
                GltfMaterial theMaterial = null;
                int hash = mesh.faceInfo.textureEntry.GetHashCode();
                if (!gltfRoot.materials.GetHash(hash, out theMaterial)) {
                    // Material has not beeen created yet
                    theMaterial = new GltfMaterial(gltfRoot, mesh.ID + "_mat");
                    theMaterial.hash = hash;

                    GltfExtension ext = new GltfExtension(gltfRoot, "KHR_materials_common");
                    ext.technique = "LAMBERT";  // or 'BLINN' or 'PHONG'

                    OMV.Color4 aColor = mesh.faceInfo.textureEntry.RGBA;
                    ext.values.Add(GltfExtension.valAmbient, aColor);
                    ext.values.Add(GltfExtension.valDiffuse, aColor);
                    // ext.values.Add(GltfExtension.valSpecular, aColor);    // not a value in LAMBERT
                    ext.values.Add(GltfExtension.valTransparency, aColor.A);
                    // ext.values.Add(GltfExtension.valEmission, new OMV.Color4(0.05f, 0.05f, 0.05f, 1.0f));
                    
                    if (mesh.faceInfo.textureID != null) {
                        // There is an image texture with this mesh.
                        // Create all the structures for an image.
                        GltfTexture theTexture = null;
                        OMV.UUID texID = (OMV.UUID)mesh.faceInfo.textureID;
                        // Look up the texture to see if already created. If not, build texture info.
                        if (!gltfRoot.textures.GetByUUID(texID, out theTexture)) {
                            theTexture = new GltfTexture(gltfRoot, texID.ToString() + "_tex");
                            theTexture.underlyingUUID = texID;
                            theTexture.target = WebGLConstants.TEXTURE_2D;
                            theTexture.type = WebGLConstants.UNSIGNED_BYTE;
                            theTexture.format = WebGLConstants.RGBA;
                            theTexture.internalFormat = WebGLConstants.RGBA;
                            theTexture.sampler = defaultSampler;
                            GltfImage theImage = null;
                            if (!gltfRoot.images.GetByUUID(texID, out theImage)) {
                                theImage = new GltfImage(gltfRoot, texID.ToString() + "_img");
                                theImage.underlyingUUID = texID;
                                makeAssetURI(MakeAssetURITypeImage, texID.ToString(), out theImage.filename, out theImage.uri);
                            }
                            theTexture.source = theImage;
                        }
                        // Remove the defaults created above and add new values for the texture
                        ext.values.Remove(GltfExtension.valDiffuse);
                        ext.values.Add(GltfExtension.valDiffuse, theTexture.ID);

                        ext.values.Remove(GltfExtension.valTransparent);
                        ext.values.Add(GltfExtension.valTransparent, mesh.faceInfo.hasAlpha);
                    }

                    theMaterial.extensions.Add(ext);
                }
                mesh.onePrimitive.material = theMaterial;
            });
        }

        // Meshes with FaceInfo's have been added to the scene. Pass over all
        //   the meshes and create the Buffers, BufferViews, and Accessors.
        // Called before calling ToJSON().
        public void BuildBuffers(MakeAssetURI makeAssetURI, int maxVerticesPerBuffer) {
            // Partition the meshes into smaller groups based on number of vertices going out
            List<GltfMesh> partial = new List<GltfMesh>();
            int totalVertices = 0;
            meshes.ForEach(mesh => {
                // If adding this mesh will push the total vertices in this buffer over the max, flush this buffer.
                if ((totalVertices + mesh.faceInfo.vertexs.Count) > maxVerticesPerBuffer) {
                    BuildBufferForSomeMeshes(partial, makeAssetURI);
                    partial.Clear();
                    totalVertices = 0;
                }
                totalVertices += mesh.faceInfo.vertexs.Count;
                partial.Add(mesh);
            });
            if (partial.Count > 0) {
                BuildBufferForSomeMeshes(partial, makeAssetURI);
            }
        }

        // For a collection of meshes, create the buffers and accessors.
        public void BuildBufferForSomeMeshes(List<GltfMesh> someMeshes, MakeAssetURI makeAssetURI) {
            // Pass over all the vertices in all the meshes and collect common vertices into 'vertexCollection'
            int numMeshes = 0;
            int numVerts = 0;
            Dictionary<OMVR.Vertex, ushort> vertexIndex = new Dictionary<OMVR.Vertex, ushort>();
            List<OMVR.Vertex> vertexCollection = new List<OMVR.Vertex>();
            ushort vertInd = 0;
            someMeshes.ForEach(mesh => {
                numMeshes++;
                FaceInfo faceInfo = mesh.faceInfo;
                faceInfo.vertexs.ForEach(vert => {
                    numVerts++;
                    if (!vertexIndex.ContainsKey(vert)) {
                        vertexIndex.Add(vert, vertInd);
                        vertexCollection.Add(vert);
                        vertInd++;
                    }
                });
            });
            m_log.DebugFormat("{0} BuildBuffers: total meshes = {1}", LogHeader, numMeshes);
            m_log.DebugFormat("{0} BuildBuffers: total vertices = {1}", LogHeader, numVerts);
            m_log.DebugFormat("{0} BuildBuffers: total unique vertices = {1}", LogHeader, vertInd);

            // Remap all the indices to the new, compacted vertex collection.
            //     mesh.underlyingMesh.face to mesh.newIndices
            // TODO: if num verts > ushort.maxValue, create array if uint's
            int numIndices = 0;
            someMeshes.ForEach(mesh => {
                FaceInfo faceInfo = mesh.faceInfo;
                ushort[] newIndices = new ushort[faceInfo.indices.Count];
                for (int ii = 0; ii < faceInfo.indices.Count; ii++) {
                    OMVR.Vertex aVert = faceInfo.vertexs[(int)faceInfo.indices[ii]];
                    newIndices[ii] = vertexIndex[aVert];
                }
                mesh.newIndices = newIndices;
                numIndices += newIndices.Length;
            });
            m_log.DebugFormat("{0} BuildBuffers: total indices = {1}", LogHeader, numIndices);

            // The vertices have been unique'ified into 'vertexCollection' and each mesh has
            //    updated indices in GltfMesh.newIndices.

            int sizeofVertices = vertexCollection.Count * sizeof(float) * 8;
            int sizeofOneIndices = sizeof(ushort);
            int sizeofIndices = numIndices * sizeofOneIndices;
            // The offsets must be multiples of a good access unit so thing align
            int padUnit = sizeof(float) * 8;
            int paddedSizeofIndices = sizeofIndices;
            // There might be padding for each mesh. An over estimate but hopefully not too bad.
            paddedSizeofIndices += someMeshes.Count * sizeof(float);
            paddedSizeofIndices += (padUnit - (paddedSizeofIndices % padUnit)) % padUnit;

            // A key added to the buffer, vertices, and indices names to uniquify them
            string buffNum =  String.Format("{0:000}", buffers.Count + 1);
            byte[] binBuffRaw = new byte[paddedSizeofIndices + sizeofVertices];
            string buffFilename = null;
            string buffURI = null;
            string buffName = "buffer" + buffNum;
            makeAssetURI(Gltf.MakeAssetURITypeBuff, buffName, out buffFilename, out buffURI);
            // m_log.DebugFormat("{0} BuildBuffers: make buffer: name={1}, filename={2}, uri={3}", LogHeader, buffName, buffFilename, buffURI);
            GltfBuffer binBuff = new GltfBuffer(gltfRoot, buffName, "arraybuffer", buffFilename, buffURI);
            binBuff.bufferBytes = binBuffRaw;

            GltfBufferView binIndicesView = new GltfBufferView(gltfRoot, "bufferViewIndices" + buffNum);
            binIndicesView.buffer = binBuff;
            binIndicesView.byteOffset = 0;
            binIndicesView.byteLength = paddedSizeofIndices;
            binIndicesView.target = WebGLConstants.ELEMENT_ARRAY_BUFFER;

            GltfBufferView binVerticesView = new GltfBufferView(gltfRoot, "bufferViewVertices" + buffNum);
            binVerticesView.buffer = binBuff;
            binVerticesView.byteOffset = paddedSizeofIndices;
            binVerticesView.byteLength = sizeofVertices;
            binVerticesView.target = WebGLConstants.ARRAY_BUFFER;
            m_log.DebugFormat("{0} BuildBuffers: indOffset={1}, indLen={2}, verOff={3}, verLen={4}", LogHeader,
                        binIndicesView.byteOffset, binIndicesView.byteLength,
                        binVerticesView.byteOffset, binVerticesView.byteLength);

            // Copy the vertices into the output binary buffer 
            // Buffer.BlockCopy only moves primitives. Copy the vertices into a float array.
            // This also separates the verts from normals from texCoord since the Babylon
            //     Gltf reader doesn't handle stride.
            float[] floatVertexRemapped = new float[vertexCollection.Count * sizeof(float) * 8];
            int vertexBase = 0;
            int normalBase = vertexCollection.Count * 3;
            int texCoordBase = normalBase + vertexCollection.Count * 3;
            int jj = 0; int kk = 0;
            vertexCollection.ForEach(vert => {
                floatVertexRemapped[vertexBase + 0 + jj] = vert.Position.X;
                floatVertexRemapped[vertexBase + 1 + jj] = vert.Position.Y;
                floatVertexRemapped[vertexBase + 2 + jj] = vert.Position.Z;
                floatVertexRemapped[normalBase + 0 + jj] = vert.Normal.X;
                floatVertexRemapped[normalBase + 1 + jj] = vert.Normal.Y;
                floatVertexRemapped[normalBase + 2 + jj] = vert.Normal.Z;
                floatVertexRemapped[texCoordBase + 0 + kk] = vert.TexCoord.X;
                floatVertexRemapped[texCoordBase + 1 + kk] = vert.TexCoord.Y;
                jj += 3;
                kk += 2;
            });
            Buffer.BlockCopy(floatVertexRemapped, 0, binBuffRaw, binVerticesView.byteOffset, binVerticesView.byteLength);
            floatVertexRemapped = null;

            // For each mesh, copy the indices into the binary output buffer and create the accessors
            //    that point from the mesh into the binary info.
            int indicesOffset = binIndicesView.byteOffset;
            someMeshes.ForEach(mesh => {
                int meshIndicesSize = mesh.newIndices.Length * sizeofOneIndices;
                Buffer.BlockCopy(mesh.newIndices, 0, binBuffRaw, indicesOffset, meshIndicesSize);

                GltfAccessor indicesAccessor = new GltfAccessor(gltfRoot, mesh.ID + "_accInd");
                indicesAccessor.bufferView = binIndicesView;
                indicesAccessor.count = mesh.newIndices.Length;
                indicesAccessor.byteOffset = indicesOffset;
                indicesAccessor.byteStride = sizeofOneIndices;
                indicesAccessor.componentType = WebGLConstants.UNSIGNED_SHORT;
                indicesAccessor.type = "SCALAR";
                ushort imin = UInt16.MaxValue; ushort imax = 0;
                for (int ii = 0; ii < mesh.newIndices.Length; ii++) {
                    imin = Math.Min(imin, mesh.newIndices[ii]);
                    imax = Math.Max(imax, mesh.newIndices[ii]);
                }
                indicesAccessor.min = new object[1] { imin };
                indicesAccessor.max = new object[1] { imax };

                // m_log.DebugFormat("{0} indices: meshIndSize={1}, cnt={2}, offset={3}", LogHeader,
                //                 meshIndicesSize, indicesAccessor.count, indicesOffset);

                indicesOffset += meshIndicesSize;
                // Align the indices to float boundries
                indicesOffset += (sizeof(float) - (indicesOffset % sizeof(float)) % sizeof(float));

                // Gltf requires min and max values for all the mesh vertex collections
                OMV.Vector3 vmin = new OMV.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                OMV.Vector3 vmax = new OMV.Vector3(float.MinValue, float.MinValue, float.MinValue);
                OMV.Vector3 nmin = new OMV.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                OMV.Vector3 nmax = new OMV.Vector3(float.MinValue, float.MinValue, float.MinValue);
                OMV.Vector2 umin = new OMV.Vector2(float.MaxValue, float.MaxValue);
                OMV.Vector2 umax = new OMV.Vector2(float.MinValue, float.MinValue);
                mesh.faceInfo.vertexs.ForEach(vert => {
                    // OMV.Vector3 has a Min and Max function but it does a 'new' which causes lots of GC thrash
                    vmin.X = Math.Min(vmin.X, vert.Position.X);
                    vmin.Y = Math.Min(vmin.Y, vert.Position.Y);
                    vmin.Z = Math.Min(vmin.Z, vert.Position.Z);
                    vmax.X = Math.Max(vmax.X, vert.Position.X);
                    vmax.Y = Math.Max(vmax.Y, vert.Position.Y);
                    vmax.Z = Math.Max(vmax.Z, vert.Position.Z);

                    nmin.X = Math.Min(nmin.X, vert.Normal.X);
                    nmin.Y = Math.Min(nmin.Y, vert.Normal.Y);
                    nmin.Z = Math.Min(nmin.Z, vert.Normal.Z);
                    nmax.X = Math.Max(nmax.X, vert.Normal.X);
                    nmax.Y = Math.Max(nmax.Y, vert.Normal.Y);
                    nmax.Z = Math.Max(nmax.Z, vert.Normal.Z);

                    umin.X = Math.Min(umin.X, vert.TexCoord.X);
                    umin.Y = Math.Min(umin.Y, vert.TexCoord.Y);
                    umax.X = Math.Max(umax.X, vert.TexCoord.X);
                    umin.Y = Math.Min(umin.Y, vert.TexCoord.Y);
                });

                GltfAccessor vertexAccessor = new GltfAccessor(gltfRoot, mesh.ID + "_accCVer");
                vertexAccessor.bufferView = binVerticesView;
                vertexAccessor.count = vertexCollection.Count;
                vertexAccessor.byteOffset = 0;
                vertexAccessor.byteStride = sizeof(float) * 3;
                vertexAccessor.componentType = WebGLConstants.FLOAT;
                vertexAccessor.type = "VEC3";
                vertexAccessor.min = new object[3] { vmin.X, vmin.Y, vmin.Z };
                vertexAccessor.max = new object[3] { vmax.X, vmax.Y, vmax.Z };

                GltfAccessor normalsAccessor = new GltfAccessor(gltfRoot, mesh.ID + "_accNor");
                normalsAccessor.bufferView = binVerticesView;
                normalsAccessor.count = vertexCollection.Count;
                normalsAccessor.byteOffset = normalBase * sizeof(float);
                normalsAccessor.byteStride = sizeof(float) * 3;
                normalsAccessor.componentType = WebGLConstants.FLOAT;
                normalsAccessor.type = "VEC3";
                normalsAccessor.min = new object[3] { nmin.X, nmin.Y, nmin.Z };
                normalsAccessor.max = new object[3] { nmax.X, nmax.Y, nmax.Z };

                GltfAccessor UVAccessor = new GltfAccessor(gltfRoot, mesh.ID + "_accUV");
                UVAccessor.bufferView = binVerticesView;
                UVAccessor.count = vertexCollection.Count;
                UVAccessor.byteOffset = texCoordBase * sizeof(float);
                UVAccessor.byteStride = sizeof(float) * 2;
                UVAccessor.componentType = WebGLConstants.FLOAT;
                UVAccessor.type = "VEC2";
                UVAccessor.min = new object[2] { umin.X, umin.Y };
                UVAccessor.max = new object[2] { umax.X, umax.Y };

                mesh.onePrimitive.indices = indicesAccessor;
                mesh.onePrimitive.position = vertexAccessor;
                mesh.onePrimitive.normals = normalsAccessor;
                mesh.onePrimitive.texcoord = UVAccessor;
            });
        }

        public void ToJSON(StreamWriter outt) {
            this.ToJSON(outt, 0);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" {\n");

            if (extensionsUsed.Count > 0) {
                outt.Write(GltfClass.Indent(level) + "\"extensionsUsed\": ");
                // the extensions are listed here as an array of names
                extensionsUsed.ToJSONIDArray(outt, level+1);
                outt.Write(",\n");
            }

            if (!String.IsNullOrEmpty(defaultSceneID)) {
                outt.Write("\"scene\": \"" + defaultSceneID + "\"");
                outt.Write(",\n");
            }

            outt.Write(GltfClass.Indent(level) + "\"asset\": ");
            asset.ToJSON(outt, level+1);
            outt.Write(",\n");

            outt.Write(GltfClass.Indent(level) + "\"scenes\": ");
            scenes.ToJSON(outt, level+1);
            outt.Write(",\n");

            outt.Write(GltfClass.Indent(level) + "\"nodes\": ");
            nodes.ToJSON(outt, level+1);
            outt.Write(",\n");

            outt.Write(GltfClass.Indent(level) + "\"meshes\": ");
            meshes.ToJSON(outt, level+1);
            outt.Write(",\n");

            outt.Write(GltfClass.Indent(level) + "\"accessors\": ");
            accessors.ToJSON(outt, level+1);
            outt.Write(",\n");

            outt.Write(GltfClass.Indent(level) + "\"bufferViews\": ");
            bufferViews.ToJSON(outt, level+1);
            outt.Write(",\n");

            if (materials.Count > 0) {
                outt.Write(GltfClass.Indent(level) + "\"materials\": ");
                materials.ToJSON(outt, level+1);
                outt.Write(",\n");
            }

            if (techniques.Count > 0) {
                outt.Write(GltfClass.Indent(level) + "\"techniques\": ");
                techniques.ToJSON(outt, level+1);
                outt.Write(",\n");
            }

            if (textures.Count > 0) {
                outt.Write(GltfClass.Indent(level) + "\"textures\": ");
                textures.ToJSON(outt, level+1);
                outt.Write(",\n");
            }

            if (images.Count > 0) {
                outt.Write(GltfClass.Indent(level) + "\"images\": ");
                images.ToJSON(outt, level+1);
                outt.Write(",\n");
            }

            if (samplers.Count > 0) {
                outt.Write(GltfClass.Indent(level) + "\"samplers\": ");
                samplers.ToJSON(outt, level+1);
                outt.Write(",\n");
            }

            if (programs.Count > 0) {
                outt.Write(GltfClass.Indent(level) + "\"programs\": ");
                programs.ToJSON(outt, level+1);
                outt.Write(",\n");
            }

            if (shaders.Count > 0) {
                outt.Write(GltfClass.Indent(level) + "\"shaders\": ");
                shaders.ToJSON(outt, level+1);
                outt.Write(",\n");
            }

            // there will always be a buffer and there doesn't need to be a comma after
            outt.Write(GltfClass.Indent(level) + "\"buffers\": ");
            buffers.ToJSON(outt, level+1);
            outt.Write("\n");

            outt.Write(" }\n");
        }

        // Write the binary files into the specified target directory
        public void WriteBinaryFiles(string targetDir) {
            buffers.ForEach(buff => {
                string outFilename = buff.filename;
                // m_log.DebugFormat("{0} WriteBinaryFiles: filename={1}", LogHeader, outFilename);
                File.WriteAllBytes(outFilename, buff.bufferBytes);
            });
        }
    }


    // =============================================================
    // A simple collection to keep name/value strings
    // The value is an Object so it can hold strings, numbers, or arrays and have the
    //     values serialized properly in the output JSON.
    public class GltfAttributes : Dictionary<string, Object> {

        // Output a JSON map of the key/value pairs.
        // The value Objects are inspected and output properly as JSON strings, arrays, or numbers.
        // Note: to add an array, do: GltfAttribute.Add(key, new Object[] { 1, 2, 3, 4 } );
        public void ToJSON(StreamWriter outt, int level) {
            outt.Write(" {\n");
            bool first = true;
            foreach (KeyValuePair<string, Object> kvp in this) {
                Gltf.WriteJSONValueLine(outt, level, ref first, kvp.Key, kvp.Value);
            }
            outt.Write("\n" + GltfClass.Indent(level) + "}\n");
        }

        // Output an array of the keys. 
        public void ToJSONIDArray(StreamWriter outt, int level) {
            outt.Write("[ ");
            if (this.Count != 0)
                outt.Write("\n");
            bool first = true;
            foreach (string key in this.Keys) {
                if (!first) {
                    outt.Write(",\n");
                }
                outt.Write(GltfClass.Indent(level) + "\"" + key +"\"");
                first = false;
            }
            outt.Write(" ]");
        }
    }

    // =============================================================
    public class GltfAsset : GltfClass {
        public string generator = "BasilConversion";
        public bool premulitpliedAlpha = false;
        public string version = "1.1";
        public GltfAttributes profile;

        public GltfAsset(Gltf pRoot) : base(pRoot, "") {
            profile = new GltfAttributes();
            profile.Add("api", "WebGL");
            profile.Add("version", "1.0");
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            Gltf.WriteJSONValueLine(outt, level, ref first, "generator", generator);
            Gltf.WriteJSONValueLine(outt, level, ref first, "premultipliedAlpha", premulitpliedAlpha);
            Gltf.WriteJSONValueLine(outt, level, ref first, "version", version);
            Gltf.WriteJSONLineEnding(outt, ref first);
            outt.Write(GltfClass.Indent(level) + "\"profile\": ");
            profile.ToJSON(outt, level+1);
            outt.Write("\n" + GltfClass.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfScenes : GltfListClass<GltfScene> {
        public GltfScenes(Gltf pRoot) : base(pRoot) {
        }
        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfScene : GltfClass {
        public GltfNodes nodes;      // IDs of top level nodes in the scene
        public string name;
        public string extensions;
        public string extras;

        public GltfScene(Gltf pRoot, string pID) : base(pRoot, pID) {
            nodes = new GltfNodes(gltfRoot);
            gltfRoot.scenes.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            Gltf.WriteJSONValueLine(outt, level, ref first, "name", name);
            Gltf.WriteJSONLineEnding(outt, ref first);
            outt.Write(GltfClass.Indent(level) + "\"nodes\": ");
            nodes.ToJSONArrayOfIDs(outt, level+1);
            outt.Write("\n" + GltfClass.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfNodes : GltfListClass<GltfNode> {
        public GltfNodes(Gltf pRoot) : base(pRoot) {
        }
            
        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            outt.Write(GltfClass.Indent(level) + "\"nodes\": ");
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfNode : GltfClass {
        public string camera;       // non-empty if a camera definition
        public GltfNodes children;
        public string[] skeleton;   // IDs of skeletons
        public string skin;
        public string jointName;
        public GltfMeshes meshes;
        // has either 'matrix' or 'rotation/scale/translation'
        public OMV.Matrix4 matrix;
        public OMV.Quaternion rotation;
        public OMV.Vector3 scale;
        public OMV.Vector3 translation;
        public string name;
        public string extensions;   // more JSON describing the extensions used
        public string extras;       // more JSON with additional, beyond-the-standard values

        // Add a node that is not top level in a scene
        public GltfNode(Gltf pRoot, string pID) : base(pRoot, pID) {
            NodeInit(pRoot, null);
        }

        // Add a node that is top level in a scene
        public GltfNode(Gltf pRoot, GltfScene containingScene, string pID) : base(pRoot, pID) {
            NodeInit(pRoot, containingScene);
        }

        private void NodeInit(Gltf pRoot, GltfScene containingScene) {
            meshes = new GltfMeshes(gltfRoot);
            children = new GltfNodes(gltfRoot);
            matrix = OMV.Matrix4.Zero;
            rotation = new OMV.Quaternion();
            scale = new OMV.Vector3(1, 1, 1);
            translation = new OMV.Vector3(0, 0, 0);

            gltfRoot.nodes.Add(this);
            if (containingScene != null)
                containingScene.nodes.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {

            outt.Write(" { ");
            bool first = true;
            Gltf.WriteJSONValueLine(outt, level, ref first, "name", name);
            if (matrix != OMV.Matrix4.Zero) {
                Gltf.WriteJSONValueLine(outt, level, ref first, "matrix", matrix);
            }
            else {
                Gltf.WriteJSONValueLine(outt, level, ref first, "translation", translation);
                Gltf.WriteJSONValueLine(outt, level, ref first, "scale", scale);
                Gltf.WriteJSONValueLine(outt, level, ref first, "rotation", rotation);
            }
            Gltf.WriteJSONLineEnding(outt, ref first);
            outt.Write(GltfClass.Indent(level) + "\"children\": ");
            children.ToJSONArrayOfIDs(outt, level+1);
            Gltf.WriteJSONLineEnding(outt, ref first);
            outt.Write(GltfClass.Indent(level) + "\"meshes\": ");
            meshes.ToJSONArrayOfIDs(outt, level+1);
            outt.Write("\n" + GltfClass.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfMeshes : GltfListClass<GltfMesh> {
        public GltfMeshes(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfMesh : GltfClass {
        public string name;
        public GltfPrimitives primitives;
        public GltfPrimitive onePrimitive;  // a mesh has one primitive
        public GltfAttributes attributes;
        public FaceInfo faceInfo;
        public ExtendedPrim underlyingPrim;
        public ushort[] newIndices; // remapped indices posinting to global vertex list
        public GltfMesh(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.meshes.Add(this);
            primitives = new GltfPrimitives(gltfRoot);
            onePrimitive = new GltfPrimitive(gltfRoot);
            primitives.Add(onePrimitive);

        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            bool first = true;
            Gltf.WriteJSONValueLine(outt, level, ref first, "name", name);
            Gltf.WriteJSONLineEnding(outt, ref first);
            outt.Write(GltfClass.Indent(level) + "\"primitives\": ");
            primitives.ToJSONArray(outt, level+1);
            outt.Write("\n" + GltfClass.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfPrimitives : GltfListClass<GltfPrimitive> {
        public GltfPrimitives(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }

        // primitives don't have names and are output as an array
        public void ToJSONArray(StreamWriter outt, int level) {
            outt.Write("[");
            if (this.Count != 0)
                outt.Write("\n");
            bool first = true;
            this.ForEach(xx => {
                if (!first) {
                    outt.Write(",\n");
                }
                xx.ToJSON(outt, level+1);
                first = false;
            });
            outt.Write("]");
        }
    }

    public class GltfPrimitive : GltfClass {
        public int mode;
        public GltfAccessor indices;
        public GltfAccessor normals;
        public GltfAccessor position;
        public GltfAccessor texcoord;
        public GltfMaterial material;
        public GltfPrimitive(Gltf pRoot) : base(pRoot, "primitive") {
            mode = 4;
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{ ");
            bool first = true;
            Gltf.WriteJSONValueLine(outt, level, ref first, "mode", mode);

            if (indices != null) {
                Gltf.WriteJSONValueLine(outt, level, ref first, "indices", indices.ID);
            }
            if (material != null) {
                Gltf.WriteJSONValueLine(outt, level, ref first, "material", material.ID);
            }
            Gltf.WriteJSONLineEnding(outt, ref first);
            bool first2 = true;
            outt.Write(GltfClass.Indent(level) + "\"attributes\": {\n");
            if (normals != null) {
                Gltf.WriteJSONValueLine(outt, level+1, ref first2, "NORMAL", normals.ID);
            }
            if (position != null) {
                Gltf.WriteJSONValueLine(outt, level+1, ref first2, "POSITION", position.ID);
            }
            if (texcoord != null) {
                Gltf.WriteJSONValueLine(outt, level+1, ref first2, "TEXCOORD_0", texcoord.ID);
            }
            outt.Write("\n" + GltfClass.Indent(level) + "}");
            outt.Write("\n" + GltfClass.Indent(level) + " }");
        }
    }

    // =============================================================
    public class GltfMaterials : GltfListClass<GltfMaterial> {
        public GltfMaterials(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }

        // Find the material in this collection that has the hash from the texture entry
        public bool GetHash(int hash, out GltfMaterial foundMaterial) {
            foreach (GltfMaterial mat in this) {
                if (mat.hash == hash) {
                    foundMaterial = mat;
                    return true;
                }
            }
            foundMaterial = null;
            return false;
        }
    }

    public class GltfMaterial : GltfClass {
        public string name;
        public int hash;
        public GltfAttributes values;
        public GltfExtensions extensions;
        public GltfMaterial(Gltf pRoot, string pID) : base(pRoot, pID) {
            values = new GltfAttributes();
            extensions = new GltfExtensions(pRoot);
            hash = 0;
            gltfRoot.materials.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            Gltf.WriteJSONValueLine(outt, level, ref first, "name", name);
            if (values.Count > 0) {
                Gltf.WriteJSONLineEnding(outt, ref first);
                outt.Write(GltfClass.Indent(level) + "\"values\": ");
                values.ToJSON(outt, level+1);
            }
            if (extensions != null && extensions.Count > 0) {
                Gltf.WriteJSONLineEnding(outt, ref first);
                outt.Write(GltfClass.Indent(level) + "\"extensions\": ");
                extensions.ToJSON(outt, level + 1);
            }
            outt.Write("\n" + GltfClass.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfAccessors : GltfListClass<GltfAccessor> {
        public GltfAccessors(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfAccessor : GltfClass {
        public GltfBufferView bufferView;
        public int count;
        public uint componentType;
        public string type;
        public int byteOffset;
        public int byteStride;
        public object[] min;
        public object[] max;
        public GltfAccessor(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.accessors.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            Gltf.WriteJSONValueLine(outt, level, ref first, "bufferView", bufferView.ID);
            Gltf.WriteJSONValueLine(outt, level, ref first, "count", count);
            Gltf.WriteJSONValueLine(outt, level, ref first, "componentType", componentType);
            Gltf.WriteJSONValueLine(outt, level, ref first, "type", type);
            Gltf.WriteJSONValueLine(outt, level, ref first, "byteOffset", byteOffset);
            Gltf.WriteJSONValueLine(outt, level, ref first, "byteStride", byteStride);
            Gltf.WriteJSONValueLine(outt, level, ref first, "min", min);
            Gltf.WriteJSONValueLine(outt, level, ref first, "max", max);
            outt.Write("\n" + GltfClass.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfBuffers : GltfListClass<GltfBuffer> {
        public GltfBuffers(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfBuffer : GltfClass {
        public byte[] bufferBytes;
        public string type;
        public string filename;
        public string uri;
        public GltfBuffer(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.buffers.Add(this);
        }

        public GltfBuffer(Gltf pRoot, string pID, string pType, string pFilename, string pUri) : base(pRoot, pID) {
            type = pType;
            filename = pFilename;
            uri = pUri;
            gltfRoot.buffers.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            Gltf.WriteJSONValueLine(outt, level, ref first, "byteLength", bufferBytes.Length);
            Gltf.WriteJSONValueLine(outt, level, ref first, "type", "arraybuffer");
            Gltf.WriteJSONValueLine(outt, level, ref first, "uri", uri);
            outt.Write("\n" + GltfClass.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfBufferViews : GltfListClass<GltfBufferView> {
        public GltfBufferViews(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfBufferView : GltfClass {
        public GltfBuffer buffer;
        public int byteOffset;
        public int byteLength;
        public uint target;

        public GltfBufferView(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.bufferViews.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            Gltf.WriteJSONValueLine(outt, level, ref first, "buffer", buffer.ID);
            Gltf.WriteJSONValueLine(outt, level, ref first, "byteOffset", byteOffset);
            if (byteLength > 0)
                Gltf.WriteJSONValueLine(outt, level, ref first, "byteLength", byteLength);
            if (target > 0)
                Gltf.WriteJSONValueLine(outt, level, ref first, "target", target);
            outt.Write("\n" + GltfClass.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfTechniques : GltfListClass<GltfTechnique> {
        public GltfTechniques(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfTechnique : GltfClass {
        public GltfTechnique(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.techniques.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
        /*
            outt.Write(" { ");
            bool first = true;
            Gltf.WriteJSONValueLine(outt, level, ref first, "name", name);
            Gltf.WriteJSONLineEnding(outt, ref first);
            outt.Write(GltfClass.Indent(level) + "\"nodes\": ");
            nodes.ToJSONArrayOfIDs(outt, level+1);
            outt.Write("\n" + GltfClass.Indent(level) + "}\n");
            */
            outt.Write("{\n");
            outt.Write(" }");
        }
    }

    // =============================================================
    public class GltfPrograms : GltfListClass<GltfProgram> {
        public GltfPrograms(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfProgram : GltfClass {
        public GltfProgram(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.programs.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            outt.Write(" }");
        }
    }

    // =============================================================
    public class GltfShaders : GltfListClass<GltfShader> {
        public GltfShaders(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfShader : GltfClass {
        public GltfShader(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.shaders.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            outt.Write(" }");
        }
    }

    // =============================================================
    public class GltfTextures : GltfListClass<GltfTexture> {
        public GltfTextures(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }

        public bool GetByUUID(OMV.UUID aUUID, out GltfTexture theTexture) {
            foreach (GltfTexture tex in this) {
                if (tex.underlyingUUID != null && tex.underlyingUUID == aUUID) {
                    theTexture = tex;
                    return true;
                }
            }
            theTexture = null;
            return false;
        }
    }

    public class GltfTexture : GltfClass {
        public OMV.UUID underlyingUUID;
        public uint target;
        public uint type;
        public uint format;
        public uint internalFormat;
        public GltfImage source;
        public GltfSampler sampler;
        public GltfTexture(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.textures.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            Gltf.WriteJSONValueLine(outt, level, ref first, "target", target);
            Gltf.WriteJSONValueLine(outt, level, ref first, "type", type);
            Gltf.WriteJSONValueLine(outt, level, ref first, "format", format);
            if (internalFormat != 0)
                Gltf.WriteJSONValueLine(outt, level, ref first, "internalFormat", internalFormat);
            if (source != null)
                Gltf.WriteJSONValueLine(outt, level, ref first, "source", source.ID);
            if (sampler != null)
                Gltf.WriteJSONValueLine(outt, level, ref first, "sampler", sampler.ID);
            outt.Write("\n" + GltfClass.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfImages : GltfListClass<GltfImage> {
        public GltfImages(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }

        public bool GetByUUID(OMV.UUID aUUID, out GltfImage theImage) {
            foreach (GltfImage img in this) {
                if (img.underlyingUUID != null && img.underlyingUUID == aUUID) {
                    theImage = img;
                    return true;
                }
            }
            theImage = null;
            return false;
        }
    }

    public class GltfImage : GltfClass {
        public OMV.UUID underlyingUUID;
        public string name;
        public string uri;
        public string filename;
        public GltfImage(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.images.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            Gltf.WriteJSONValueLine(outt, level, ref first, "name", name);
            Gltf.WriteJSONValueLine(outt, level, ref first, "uri", uri);
            outt.Write("\n" + GltfClass.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfSamplers : GltfListClass<GltfSampler> {
        public GltfSamplers(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfSampler : GltfClass {
        public GltfAttributes values;
        public GltfSampler(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.samplers.Add(this);
            values = new GltfAttributes();
        }

        public override void ToJSON(StreamWriter outt, int level) {
            values.ToJSON(outt, level + 1);
        }
    }

    // =============================================================
    public class GltfExtensions : GltfListClass<GltfExtension> {
        public GltfExtensions(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfExtension : GltfClass {
        public string technique;
        public GltfAttributes values;
        // possible entries in 'values'
        public static string valAmbient = "ambient";    // ambient color of surface (OMV.Vector4)
        public static string valDiffuse = "diffuse";    // diffuse color of surface (OMV.Vector4 or textureID)
        public static string valDoubleSided = "doubleSided";    // whether surface has backside ('true' or 'false')
        public static string valEmission = "emission";    // light emitted by surface (OMV.Vector4 or textureID)
        public static string valSpecular = "specular";    // color reflected by surface (OMV.Vector4 or textureID)
        public static string valShininess = "shininess";  // specular reflection from surface (float)
        public static string valTransparency = "transparency";  // transparency of surface (float)
        public static string valTransparent = "transparent";  // whether the surface has transparency ('true' or 'false;)

        public GltfExtension(Gltf pRoot, string pID) : base(pRoot, pID) {
            pRoot.UsingExtension(pID);
            values = new GltfAttributes();
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            Gltf.WriteJSONValueLine(outt, level, ref first, "technique", technique);
            Gltf.WriteJSONLineEnding(outt, ref first);
            outt.Write(GltfClass.Indent(level) + "\"values\": ");
            values.ToJSON(outt, level+1);
            outt.Write("\n" + GltfClass.Indent(level) + "}\n");
        }
    }
}
