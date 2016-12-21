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
        public abstract void ToJSON(StreamWriter outt);

        public GltfClass() { }
        public GltfClass(Gltf pRoot, string pID) {
            gltfRoot = pRoot;
            ID = pID;
        }

        // To make the output pretty, add tabs to these values.
        // Values can be made empty to eliminate the chars on output
        public static string t1 = "\t";
        public static string t2 = "\t\t";
        public static string t3 = "\t\t\t";
        public static string t4 = "\t\t\t\t";

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
    }

    public abstract class GltfListClass<T> : List<T> {
        public Gltf gltfRoot;
        public string ID;
        public abstract void ToJSON(StreamWriter outt);
        public abstract void ToJSONIDArray(StreamWriter outt);
        public GltfListClass(Gltf pRoot) {
            gltfRoot = pRoot;
        }

        public void ToJSONArrayOfIDs(StreamWriter outt) {
            outt.Write("[ ");
            if (this.Count != 0)
                outt.Write("\n");
            bool first = true;
            this.ForEach(xx => {
                if (!first) {
                    outt.Write(",\n");
                }
                GltfClass gl = xx as GltfClass;
                outt.Write(GltfClass.t3 + "\"" + gl.ID +"\"");
                first = false;
            });
            outt.Write("]");
        }

        public void ToJSONMapOfJSON(StreamWriter outt) {
            outt.Write("{ ");
            if (this.Count != 0)
                outt.Write("\n");
            bool first = true;
            this.ForEach(xx => {
                if (!first) {
                    outt.Write(",\n");
                }
                GltfClass gl = xx as GltfClass;
                outt.Write(GltfClass.t1 + "\"" + gl.ID + "\": ");
                gl.ToJSON(outt);
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

        public override string ToJSON(StreamWriter outt) {
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

        public override void ToJSON(StreamWriter outt) {
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

        public override void ToJSON(StreamWriter outt) {
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
        public void BuildBuffers() {
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{");
            if (!String.IsNullOrEmpty(defaultSceneID)) {
                outt.Write("\"scene\": \"" + defaultSceneID + "\"");
                outt.Write(",\n");
            }

            scenes.ToJSON(outt);
            outt.Write(",\n");

            nodes.ToJSON(outt);
            outt.Write(",\n");

            meshes.ToJSON(outt);
            outt.Write(",\n");

            materials.ToJSON(outt);
            outt.Write(",\n");

            accessors.ToJSON(outt);
            outt.Write(",\n");

            bufferViews.ToJSON(outt);
            outt.Write(",\n");

            if (materials.Count > 0) {
                materials.ToJSON(outt);
                outt.Write(",\n");
            }

            if (techniques.Count > 0) {
                techniques.ToJSON(outt);
                outt.Write(",\n");
            }

            if (programs.Count > 0) {
                programs.ToJSON(outt);
                outt.Write(",\n");
            }

            if (shaders.Count > 0) {
                shaders.ToJSON(outt);
                outt.Write(",\n");
            }

            // there will always be a buffer and there doesn't need to be a comma after
            buffers.ToJSON(outt);
            outt.Write("\n");

            outt.Write("}\n");
        }
    }

    // =============================================================
    // A simple collection to keep name/value strings
    public class GltfAttributes : Dictionary<string, string> {
        public void ToJSON(StreamWriter outt) {
            outt.Write("{\n");
            bool first = true;
            foreach (KeyValuePair<string, string> kvp in this) {
                if (!first) {
                    outt.Write(",");
                }
                outt.Write(GltfClass.t2 + "\"" + kvp.Key + "\": \"" + kvp.Value + "\"\n");
                first = false;
            }
            outt.Write("}\n");
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

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{\n");
            outt.Write(GltfClass.t1 + "\"generator\": \"" + generator + "\",\n");
            outt.Write(GltfClass.t1 + "\"premultipliedAlpha\": \"" + premulitpliedAlpha + "\",\n");
            outt.Write(GltfClass.t1 + "\"profile\": ");
            profile.ToJSON(outt);
            outt.Write(",\n");
            outt.Write(GltfClass.t1 + "\"version\": " + version.ToString() + "\n");
            outt.Write("}\n");
        }
    }

    // =============================================================
    public class GltfScenes : GltfListClass<GltfScene> {
        public GltfScenes(Gltf pRoot) : base(pRoot) {
        }
        public override void ToJSON(StreamWriter outt) {
            outt.Write("\"scenes\":\n");
            this.ToJSONMapOfJSON(outt);
        }
        public override void ToJSONIDArray(StreamWriter outt) {
            outt.Write("\"scenes\":\n");
            this.ToJSONArrayOfIDs(outt);
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

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{\n");
            if (!String.IsNullOrEmpty(name))
                outt.Write(GltfClass.t2 + "\"name\": \"" + name + "\",\n");

            outt.Write(GltfClass.t2 + "\"nodes\": ");
            nodes.ToJSONArrayOfIDs(outt);

            outt.Write("}\n");
        }
    }

    // =============================================================
    public class GltfNodes : GltfListClass<GltfNode> {
        public GltfNodes(Gltf pRoot) : base(pRoot) {
        }
            
        public override void ToJSON(StreamWriter outt) {
            outt.Write("\"nodes\": ");
            this.ToJSONMapOfJSON(outt);
        }
        public override void ToJSONIDArray(StreamWriter outt) {
            outt.Write(GltfClass.t2 + "\"nodes\": ");
            this.ToJSONArrayOfIDs(outt);
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

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{\n");
            if (!String.IsNullOrEmpty(name))
                outt.Write(GltfClass.t2 + "\"name\": \"" + name + "\",\n");
            outt.Write(GltfClass.t2 + "\"translation\": " + Vector3ToJSONArray(translation));
            outt.Write(",\n");
            outt.Write(GltfClass.t2 + "\"scale\": " + Vector3ToJSONArray(scale));
            outt.Write(",\n");
            outt.Write(GltfClass.t2 + "\"rotation\": " + QuaternionToJSONArray(rotation));
            outt.Write(",\n");
            outt.Write(GltfClass.t2 + "\"children\": ");
            children.ToJSONArrayOfIDs(outt);
            outt.Write(",\n");
            outt.Write(GltfClass.t2 + "\"meshes\": ");
            meshes.ToJSONArrayOfIDs(outt);
            outt.Write("}");
        }
    }

    // =============================================================
    public class GltfMeshes : GltfListClass<GltfMesh> {
        public GltfMeshes(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("\"meshes\": ");
            this.ToJSONMapOfJSON(outt);
        }
        public override void ToJSONIDArray(StreamWriter outt) {
            outt.Write(GltfClass.t2 + "\"meshes\": ");
            this.ToJSONArrayOfIDs(outt);
        }
    }

    public class GltfMesh : GltfClass {
        public string name;
        public GltfPrimitive primitives;
        public OMVR.Face underlyingMesh;
        public GltfMesh(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.meshes.Add(this);
            primitives = new GltfPrimitive(gltfRoot);
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{\n");
            if (!String.IsNullOrEmpty(name))
                outt.Write(GltfClass.t2 + "\"name\": \"" + name + "\",\n");
            outt.Write(GltfClass.t2 + "\"primitives\": ");
            primitives.ToJSON(outt);
            outt.Write("}");
        }
    }

    // =============================================================
    public class GltfPrimitives : GltfListClass<GltfMesh> {
        public GltfPrimitives(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("\"primitives\": ");
            this.ToJSONMapOfJSON(outt);
        }
        public override void ToJSONIDArray(StreamWriter outt) {
            outt.Write(GltfClass.t2 + "\"primitives\": ");
            this.ToJSONArrayOfIDs(outt);
        }

        public void ToJSONArray(StreamWriter outt) {
            outt.Write(GltfClass.t2 + "\"primitives\": ");
            outt.Write("[");
            if (this.Count != 0)
                outt.Write("\n");
            bool first = true;
            this.ForEach(xx => {
                if (!first) {
                    outt.Write(",\n");
                }
                xx.ToJSON(outt);
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

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{");
            outt.Write(GltfClass.t3 + "\"mode\": " + mode.ToString() + ",\n");
            if (indices != null) {
                outt.Write(GltfClass.t3 + "\"indices\": \"" + indices.ID + "\",\n");
            }
            bool yesComma = false;
            outt.Write(GltfClass.t3 + "\"attributes\": {\n");
            if (normals != null) {
                outt.Write(GltfClass.t4 + "\"NORMAL\": \"" + normals.ID + "\"\n");
                yesComma = true;
            }
            if (position != null) {
                if (yesComma) outt.Write(",");
                outt.Write(GltfClass.t4 + "\"POSITION\": \"" + position.ID + "\"\n");
                yesComma = true;
            }
            if (texcoord != null) {
                if (yesComma) outt.Write(",");
                outt.Write(GltfClass.t4 + "\"TEXCOORD_0\": \"" + texcoord.ID + "\"\n");
                yesComma = true;
            }
            outt.Write(GltfClass.t3 + "}\n");
            if (material != null) {
                outt.Write(",");        // Take note of the need for the comma
                outt.Write(GltfClass.t3 + "\"material\": \"" + material.ID + "\"\n");
            }
            outt.Write("}");
        }
    }

    // =============================================================
    public class GltfMaterials : GltfListClass<GltfAccessor> {
        public GltfMaterials(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("\"materials\": ");
            this.ToJSONMapOfJSON(outt);
        }
        public override void ToJSONIDArray(StreamWriter outt) {
            outt.Write(GltfClass.t2 + "\"materials\": ");
            this.ToJSONArrayOfIDs(outt);
        }
    }

    public class GltfMaterial : GltfClass {
        public GltfMaterial(Gltf pRoot, string pID) : base(pRoot, pID) {
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{");
            outt.Write("}");
        }
    }

    // =============================================================
    public class GltfAccessors : GltfListClass<GltfAccessor> {
        public GltfAccessors(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("\"accessors\": ");
            this.ToJSONMapOfJSON(outt);
        }
        public override void ToJSONIDArray(StreamWriter outt) {
            outt.Write(GltfClass.t2 + "\"accessors\": ");
            this.ToJSONArrayOfIDs(outt);
        }
    }

    public class GltfAccessor : GltfClass {
        public GltfAccessor(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.accessors.Add(this);
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{\n");
            outt.Write("}");
        }
    }

    // =============================================================
    public class GltfBuffers : GltfListClass<GltfBuffer> {
        public GltfBuffers(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("\"buffers\": ");
            this.ToJSONMapOfJSON(outt);
        }
        public override void ToJSONIDArray(StreamWriter outt) {
            outt.Write(GltfClass.t2 + "\"buffers\": ");
            this.ToJSONArrayOfIDs(outt);
        }
    }

    public class GltfBuffer : GltfClass {
        public GltfBuffer(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.buffers.Add(this);
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{\n");
            outt.Write("}");
        }
    }

    // =============================================================
    public class GltfBufferViews : GltfListClass<GltfBufferView> {
        public GltfBufferViews(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("\"bufferViews\": ");
            this.ToJSONMapOfJSON(outt);
        }
        public override void ToJSONIDArray(StreamWriter outt) {
            outt.Write(GltfClass.t2 + "\"bufferViews\": ");
            this.ToJSONArrayOfIDs(outt);
        }
    }

    public class GltfBufferView : GltfClass {
        public GltfBufferView(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.bufferViews.Add(this);
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{\n");
            outt.Write("}");
        }
    }

    // =============================================================
    public class GltfTechniques : GltfListClass<GltfTechnique> {
        public GltfTechniques(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("\"bufferViews\": ");
            this.ToJSONMapOfJSON(outt);
        }
        public override void ToJSONIDArray(StreamWriter outt) {
            outt.Write(GltfClass.t2 + "\"bufferViews\": ");
            this.ToJSONArrayOfIDs(outt);
        }
    }

    public class GltfTechnique : GltfClass {
        public GltfTechnique(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.techniques.Add(this);
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{\n");
            outt.Write("}");
        }
    }

    // =============================================================
    public class GltfPrograms : GltfListClass<GltfProgram> {
        public GltfPrograms(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("\"bufferViews\": ");
            this.ToJSONMapOfJSON(outt);
        }
        public override void ToJSONIDArray(StreamWriter outt) {
            outt.Write(GltfClass.t2 + "\"bufferViews\": ");
            this.ToJSONArrayOfIDs(outt);
        }
    }

    public class GltfProgram : GltfClass {
        public GltfProgram(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.programs.Add(this);
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{\n");
            outt.Write("}");
        }
    }

    // =============================================================
    public class GltfShaders : GltfListClass<GltfShader> {
        public GltfShaders(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("\"bufferViews\": ");
            this.ToJSONMapOfJSON(outt);
        }
        public override void ToJSONIDArray(StreamWriter outt) {
            outt.Write(GltfClass.t2 + "\"bufferViews\": ");
            this.ToJSONArrayOfIDs(outt);
        }
    }

    public class GltfShader : GltfClass {
        public GltfShader(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.shaders.Add(this);
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{\n");
            outt.Write("}");
        }
    }

    // =============================================================
    public class GltfTextures : GltfListClass<GltfTexture> {
        public GltfTextures(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("\"textures\": ");
            this.ToJSONMapOfJSON(outt);
        }
        public override void ToJSONIDArray(StreamWriter outt) {
            outt.Write(GltfClass.t2 + "\"textures\": ");
            this.ToJSONArrayOfIDs(outt);
        }
    }

    public class GltfTexture : GltfClass {
        public GltfTexture(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.textures.Add(this);
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{\n");
            outt.Write("}");
        }
    }

    // =============================================================
    public class GltfImages : GltfListClass<GltfImage> {
        public GltfImages(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("\"images\": ");
            this.ToJSONMapOfJSON(outt);
        }
        public override void ToJSONIDArray(StreamWriter outt) {
            outt.Write(GltfClass.t2 + "\"images\": ");
            this.ToJSONArrayOfIDs(outt);
        }
    }

    public class GltfImage : GltfClass {
        public GltfImage(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.images.Add(this);
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{\n");
            outt.Write("}");
        }
    }

    // =============================================================
    public class GltfSamplers : GltfListClass<GltfSampler> {
        public GltfSamplers(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("\"samplers\": ");
            this.ToJSONMapOfJSON(outt);
        }
        public override void ToJSONIDArray(StreamWriter outt) {
            outt.Write(GltfClass.t2 + "\"samplers\": ");
            this.ToJSONArrayOfIDs(outt);
        }
    }

    public class GltfSampler : GltfClass {
        public GltfSampler(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.samplers.Add(this);
        }

        public override void ToJSON(StreamWriter outt) {
            outt.Write("{\n");
            outt.Write("}");
        }
    }

}
