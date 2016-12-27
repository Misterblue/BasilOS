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

        public string Vector3ToJSONArray(OMV.Vector3 vect) {
            StringBuilder buff = new StringBuilder();
            buff.Append("[ ");
            buff.Append(vect.X.ToString());
            buff.Append(", ");
            buff.Append(vect.Y.ToString());
            buff.Append(", ");
            buff.Append(vect.Z.ToString());
            buff.Append(" ]");
            return buff.ToString();
        }

        public string QuaternionToJSONArray(OMV.Quaternion vect) {
            StringBuilder buff = new StringBuilder();
            buff.Append("[ ");
            buff.Append(vect.X.ToString());
            buff.Append(", ");
            buff.Append(vect.Y.ToString());
            buff.Append(", ");
            buff.Append(vect.Z.ToString());
            buff.Append(", ");
            buff.Append(vect.W.ToString());
            buff.Append(" ]");
            return buff.ToString();
        }

        public static string Indent(int level) {
            string Ts = "\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t";
            return Ts.Substring(0, level);
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
            outt.Write("}");
        }
    }

    /* Thought I might need my own classes for serialization.
     * Going with the OMV vector classes.
    public class GltfVector3 : GltfClass {
        public float[] vector = new float[3];
        public float X { get { return vector[0]; } set { vector[0] = value; } }
        public float Y { get { return vector[1]; } set { vector[1] = value; } }
        public float Z { get { return vector[2]; } set { vector[2] = value; } }

        public GltfVector3(float pX, float pY, float pZ) : base() {
            vector[0] = pX; vector[1] = pY; vector[2] = pZ;
        }

        public override string ToJSON(StreamWriter outt, int level) {
            outt.Write("[");
            outt.Write(X.ToString());
            outt.Write(",");
            outt.Write(Y.ToString());
            outt.Write(",");
            outt.Write(Z.ToString());
            outt.Write("]");
        }
    }

    public class GltfVector4 : GltfClass {
        public float[] vector = new float[4];
        public float X { get { return vector[0]; } set { vector[0] = value; } }
        public float Y { get { return vector[1]; } set { vector[1] = value; } }
        public float Z { get { return vector[2]; } set { vector[2] = value; } }
        public float W { get { return vector[3]; } set { vector[3] = value; } }

        public GltfVector4(float pX, float pY, float pZ, float pW) : base() {
            vector[0] = pX; vector[1] = pY; vector[2] = pZ; vector[3] = pW;
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("[");
            outt.Write(X.ToString());
            outt.Write(",");
            outt.Write(Y.ToString());
            outt.Write(",");
            outt.Write(Z.ToString());
            outt.Write(",");
            outt.Write(W.ToString());
            outt.Write("]");
        }
    }
    */

    public class GltfVector16 : GltfClass {
        public float[] vector = new float[16];

        public GltfVector16() : base() {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("[");
            for (int ii = 0; ii < vector.Length; ii++) {
                if (ii > 0) outt.Write(",");
                outt.Write(vector[ii].ToString());
            }
            outt.Write("]");
        }
    }

    // =============================================================
    public class Gltf : GltfClass {
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

        public Gltf() : base() {
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

        // Meshes with OMVR.Faces have been added to the scene. Pass over all
        //   the meshes and create the Buffers, BufferViews, and Accessors.
        // Called before calling ToJSON().
        public void BuildPrimitives() {
            meshes.ForEach(mesh => {
                GltfMaterial theMaterial = null;
                int hash = mesh.underlyingMesh.TextureFace.GetHashCode();
                if (!gltfRoot.materials.GetHash(hash, out theMaterial)) {
                    // Material has not beeen created yet
                    theMaterial = new GltfMaterial(gltfRoot, mesh.ID + "_mat");
                    theMaterial.hash = hash;
                    OMV.Color4 aColor = mesh.underlyingMesh.TextureFace.RGBA;

                    GltfExtension ext = new GltfExtension(gltfRoot, "KHR_materials_common");
                    ext.technique = "LAMBERT";  // or 'BLINN' or 'PHONG'

                    // Define the material with  the extensions
                    theMaterial.values.Add("ambient", new Object[] { aColor.R, aColor.G, aColor.B, aColor.A });
                    theMaterial.values.Add("diffuse", new Object[] { 0, 0, 0, 1 });
                    theMaterial.values.Add("emission", new Object[] { 0, 0, 0, 1 });
                    theMaterial.values.Add("specular", new Object[] { 0, 0, 0, 1 });
                    GltfTechnique theTechnique = new GltfTechnique(gltfRoot, mesh.ID + "_tech");
                    OMV.UUID texID = mesh.underlyingMesh.TextureFace.TextureID;
                    GltfTexture theTexture;
                    if (texID != OMV.UUID.Zero && texID != OMV.Primitive.TextureEntry.WHITE_TEXTURE) {
                        if (gltfRoot.textures.GetByUUID(texID, out theTexture)) {
                            // The texture/image does not exist yet
                        }
                    }

                    // A material requires a technique
                    // A technique requires a program
                    // A program requires a fragmentShader and a vertexShader
                }
                mesh.primitives.material = theMaterial;
            });
        }

        // Meshes with OMVR.Faces have been added to the scene. Pass over all
        //   the meshes and create the Buffers, BufferViews, and Accessors.
        // Called before calling ToJSON().
        public void BuildBuffers() {
        }

        public void ToJSON(StreamWriter outt) {
            this.ToJSON(outt, 0);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{");

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

            outt.Write("}\n");
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
            outt.Write("{");
            bool first = true;
            foreach (KeyValuePair<string, Object> kvp in this) {
                if (first) {
                    outt.Write("\n");
                }
                else {
                    outt.Write(",\n");
                }
                if (kvp.Value is string)
                    outt.Write(GltfClass.Indent(level) + "\"" + kvp.Key + "\": \"" + kvp.Value + "\"");
                if (kvp.Value is int) {
                    outt.Write(GltfClass.Indent(level) + "\"" + kvp.Key + "\": " + kvp.Value.ToString() + "");
                }
                if (kvp.Value.GetType().IsArray) {
                    outt.Write(GltfClass.Indent(level) + "\"" + kvp.Key + "\": [");
                    Object[] values = (Object[])kvp.Value;
                    bool first2 = true;
                    for (int ii = 0; ii < values.Length; ii++) {
                        if (!first2) outt.Write(",");
                        first2 = false;
                        outt.Write(values[ii].ToString());
                    }
                    outt.Write("]");
                }
                first = false;
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
            outt.Write("]");
        }
    }

    // =============================================================
    public class GltfAsset : GltfClass {
        public string generator = "BasilConversion";
        public string premulitpliedAlpha = "false";
        public GltfAttributes profile;
        public int version = 1;

        public GltfAsset(Gltf pRoot) : base(pRoot, "") {
            profile = new GltfAttributes();
            profile.Add("api", "WebGL");
            profile.Add("version", "1.0");
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            outt.Write(GltfClass.Indent(level) + "\"generator\": \"" + generator + "\",\n");
            outt.Write(GltfClass.Indent(level) + "\"premultipliedAlpha\": \"" + premulitpliedAlpha + "\",\n");
            outt.Write(GltfClass.Indent(level) + "\"profile\": ");
            profile.ToJSON(outt, level+1);
            outt.Write(",\n");
            outt.Write(GltfClass.Indent(level) + "\"version\": " + version.ToString() + "\n");
            outt.Write("}\n");
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
            outt.Write("{\n");
            if (!String.IsNullOrEmpty(name))
                outt.Write(GltfClass.Indent(level) + "\"name\": \"" + name + "\",\n");

            outt.Write(GltfClass.Indent(level) + "\"nodes\": ");
            nodes.ToJSONArrayOfIDs(outt, level+1);

            outt.Write(GltfClass.Indent(level) + "}\n");
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
        public GltfVector16 matrix;
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
            matrix = null;
            rotation = new OMV.Quaternion();
            scale = new OMV.Vector3(1, 1, 1);
            translation = new OMV.Vector3(0, 0, 0);

            gltfRoot.nodes.Add(this);
            if (containingScene != null)
                containingScene.nodes.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            if (!String.IsNullOrEmpty(name))
                outt.Write(GltfClass.Indent(level) + "\"name\": \"" + name + "\",\n");
            outt.Write(GltfClass.Indent(level) + "\"translation\": " + Vector3ToJSONArray(translation));
            outt.Write(",\n");
            outt.Write(GltfClass.Indent(level) + "\"scale\": " + Vector3ToJSONArray(scale));
            outt.Write(",\n");
            outt.Write(GltfClass.Indent(level) + "\"rotation\": " + QuaternionToJSONArray(rotation));
            outt.Write(",\n");
            outt.Write(GltfClass.Indent(level) + "\"children\": ");
            children.ToJSONArrayOfIDs(outt, level+1);
            outt.Write(",\n");
            outt.Write(GltfClass.Indent(level) + "\"meshes\": ");
            meshes.ToJSONArrayOfIDs(outt, level+1);
            outt.Write("}");
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
        public GltfPrimitive primitives;
        public GltfAttributes attributes;
        public OMVR.Face underlyingMesh;
        public GltfMesh(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.meshes.Add(this);
            primitives = new GltfPrimitive(gltfRoot);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            if (!String.IsNullOrEmpty(name))
                outt.Write(GltfClass.Indent(level) + "\"name\": \"" + name + "\",\n");
            outt.Write(GltfClass.Indent(level) + "\"primitives\": ");
            primitives.ToJSON(outt, level+1);
            outt.Write("}");
        }
    }

    // =============================================================
    public class GltfPrimitives : GltfListClass<GltfMesh> {
        public GltfPrimitives(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }

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
            outt.Write("{\n");
            outt.Write(GltfClass.Indent(level) + "\"mode\": " + mode.ToString() + ",\n");
            if (indices != null) {
                outt.Write(GltfClass.Indent(level) + "\"indices\": \"" + indices.ID + "\",\n");
            }
            if (material != null) {
                outt.Write(GltfClass.Indent(level) + "\"material\": \"" + material.ID + "\",\n");
            }
            bool yesComma = false;
            outt.Write(GltfClass.Indent(level) + "\"attributes\": {\n");
            if (normals != null) {
                outt.Write(GltfClass.Indent(level+1) + "\"NORMAL\": \"" + normals.ID + "\"\n");
                yesComma = true;
            }
            if (position != null) {
                if (yesComma) outt.Write(",");
                outt.Write(GltfClass.Indent(level+1) + "\"POSITION\": \"" + position.ID + "\"\n");
                yesComma = true;
            }
            if (texcoord != null) {
                if (yesComma) outt.Write(",");
                outt.Write(GltfClass.Indent(level+1) + "\"TEXCOORD_0\": \"" + texcoord.ID + "\"\n");
                yesComma = true;
            }
            outt.Write(GltfClass.Indent(level) + "}\n");

            outt.Write(GltfClass.Indent(level) + "}");
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
            outt.Write("{\n");
            if (!String.IsNullOrEmpty(name))
                outt.Write(GltfClass.Indent(level) + "\"name\": \"" + name + "\",\n");
            if (values.Count > 0) {
                outt.Write(GltfClass.Indent(level) + "\"values\": ");
                values.ToJSON(outt, level+1);
            }
            if (extensions != null && extensions.Count > 0) {
                outt.Write(GltfClass.Indent(level) + "\"extensions\": \"");
                extensions.ToJSON(outt, level + 1);
            }
            outt.Write("}");
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
        public GltfAccessor(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.accessors.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            outt.Write("}");
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
        public GltfBuffer(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.buffers.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            outt.Write("}");
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
        public GltfBufferView(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.bufferViews.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            outt.Write("}");
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
            outt.Write("{\n");
            outt.Write("}");
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
            outt.Write("}");
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
            outt.Write("}");
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
        public GltfTexture(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.textures.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            outt.Write("}");
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
        public string URI;
        public GltfImage(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.images.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            if (!String.IsNullOrEmpty(name))
                outt.Write(GltfClass.Indent(level) + "\"name\": \"" + name + "\",\n");
            if (!String.IsNullOrEmpty(URI))
                outt.Write(GltfClass.Indent(level) + "\"uri\": \"" + URI + "\",\n");
            outt.Write("}");
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
        public GltfSampler(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.samplers.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            outt.Write("}");
        }
    }

    // =============================================================
    public class GltfExtensions : GltfListClass<GltfExtensions> {
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
            values = new GltfAttributes();
        }

        public GltfExtension MaterialsCommon(Gltf pRoot) {
            GltfExtension ext = new GltfExtension(pRoot, "KHR_materials_common");
            ext.technique = "COMMON";
            return ext;
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            if (!String.IsNullOrEmpty(technique))
                outt.Write(GltfClass.Indent(level) + "\"technique\": \"" + technique + "\",\n");

            outt.Write(GltfClass.Indent(level) + "\"values\": ");
            values.ToJSON(outt, level+1);

            outt.Write(GltfClass.Indent(level) + "}\n");
        }
    }


}
