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

// I hoped to keep the Gltf classes separate from the OMV requirement but
//    it doesn't make sense to copy all the mesh info into new structures.
using OMV = OpenMetaverse;
using OMVR = OpenMetaverse.Rendering;

namespace org.herbal3d.BasilOS {

    public abstract class GltfClass {
        public Gltf gltfRoot;
        public string ID;
        public abstract string toJSON();

        public GltfClass() { }
        public GltfClass(Gltf pRoot, string pID) {
            gltfRoot = pRoot;
            ID = pID;
        }
    }

    public abstract class GltfListClass<T> : List<T> {
        public Gltf gltfRoot;
        public string ID;
        public abstract string toJSON();
        public GltfListClass(Gltf pRoot) {
            gltfRoot = pRoot;
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

        public override string toJSON() {
            StringBuilder buff = new StringBuilder();
            buff.Append("[");
            buff.Append(X.ToString());
            buff.Append(",");
            buff.Append(Y.ToString());
            buff.Append(",");
            buff.Append(Z.ToString());
            buff.Append("]");
            return buff.ToString();
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

        public override string toJSON() {
            StringBuilder buff = new StringBuilder();
            buff.Append("[");
            buff.Append(X.ToString());
            buff.Append(",");
            buff.Append(Y.ToString());
            buff.Append(",");
            buff.Append(Z.ToString());
            buff.Append(",");
            buff.Append(W.ToString());
            buff.Append("]");
            return buff.ToString();
        }
    }
    */

    public class GltfVector16 : GltfClass {
        public float[] vector = new float[16];

        public GltfVector16() : base() {
        }

        public override string toJSON() {
            StringBuilder buff = new StringBuilder();
            buff.Append("[");
            for (int ii = 0; ii < vector.Length; ii++) {
                if (ii > 0) buff.Append(",");
                buff.Append(vector[ii].ToString());
            }
            return buff.ToString();
        }
    }

    public class Gltf : GltfClass {
        public string defaultSceneID;   // ID of default scene
        public GltfScenes scenes;       // scenes that make up this package
        public GltfNodes nodes;         // nodes in the scenes
        public GltfMeshes meshes;       // the meshes for the nodes
        public GltfAccessors accessors; // access to the mesh bin data
        public GltfBufferViews bufferViews; //
        public GltfBuffers buffers; //

        public Gltf() : base() {
            scenes = new GltfScenes(this);
            nodes = new GltfNodes(this);
            meshes = new GltfMeshes(this);
            accessors = new GltfAccessors(this);
            bufferViews = new GltfBufferViews(this);
            buffers = new GltfBuffers(this);
        }

        // Meshes with OMVR.Faces have been added to the scene. Pass over all
        //   the meshes and create the Buffers, BufferViews, and Accessors.
        // Called before calling toJSON().
        public void BuildBuffers() {
        }

        public override string toJSON() {
            StringBuilder buff = new StringBuilder();
            buff.Append("{");
            if (!String.IsNullOrEmpty(defaultSceneID)) {
                buff.Append("\"scene\": \"" + defaultSceneID + "\"");
                buff.Append(",\n");
            }
            buff.Append("\"scenes\": ");
            buff.Append(scenes.toJSON());
            buff.Append(",\n");

            buff.Append("\"nodes\": ");
            buff.Append(nodes.toJSON());
            buff.Append(",\n");

            buff.Append("\"meshes\": ");
            buff.Append(meshes.toJSON());
            buff.Append(",\n");

            buff.Append("\"accessors\": ");
            buff.Append(accessors.toJSON());
            buff.Append(",\n");

            buff.Append("\"bufferViews\": ");
            buff.Append(bufferViews.toJSON());
            buff.Append(",\n");

            buff.Append("\"buffers\": ");
            buff.Append(buffers.toJSON());
            buff.Append(",\n");

            buff.Append("}");
            return buff.ToString();
        }
    }

    public class GltfScenes : GltfListClass<GltfScene> {
        public GltfScenes(Gltf pRoot) : base(pRoot) {
        }
        public override string toJSON() {
            // return base.toJSON("scene", this);
            StringBuilder buff = new StringBuilder();
            buff.Append("{");
            bool first = true;
            this.ForEach(xx => {
                if (!first) {
                    buff.Append(",\n");
                }
                buff.Append("\"" + "scene" + "\": ");
                buff.Append(xx.toJSON());
                first = false;
            });
            buff.Append("}");
            return buff.ToString();
        }
    }

    public class GltfScene : GltfClass {
        public string[] nodes;      // IDs of top level nodes in the scene
        public string name;
        public string extensions;
        public string extras;

        public GltfScene(Gltf pRoot, string pID) : base(pRoot, pID) {
        }

        public override string toJSON() {
            return "";
        }
    }

    public class GltfNodes : GltfListClass<GltfNode> {
        public GltfNodes(Gltf pRoot) : base(pRoot) {
        }
        public override string toJSON() {
            // return base.toJSON("node", this);
            StringBuilder buff = new StringBuilder();
            buff.Append("{");
            bool first = true;
            this.ForEach(xx => {
                if (!first) {
                    buff.Append(",\n");
                }
                buff.Append("\"" + "node" + "\": ");
                buff.Append(xx.toJSON());
                first = false;
            });
            buff.Append("}");
            return buff.ToString();
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

        public GltfNode(Gltf pRoot, string pID) : base(pRoot, pID) {
            meshes = new GltfMeshes(gltfRoot);
            children = new GltfNodes(gltfRoot);
            matrix = null;
            rotation = new OMV.Quaternion();
            scale = new OMV.Vector3(1, 1, 1);
            translation = new OMV.Vector3(0, 0, 0);
        }

        public override string toJSON() {
            return "";
        }
    }

    public class GltfMeshes : GltfListClass<GltfMesh> {
        public GltfMeshes(Gltf pRoot) : base(pRoot) {
        }

        public override string toJSON() {
            StringBuilder buff = new StringBuilder();
            buff.Append("{");
            bool first = true;
            this.ForEach(xx => {
                if (!first) {
                    buff.Append(",\n");
                }
                buff.Append("\"" + "mesh" + "\": ");
                buff.Append(xx.toJSON());
                first = false;
            });
            buff.Append("}");
            return buff.ToString();
        }
    }

    public class GltfMesh : GltfClass {
        public OMVR.Face underlyingMesh;
        public GltfMesh(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.meshes.Add(this);
        }

        public override string toJSON() {
            return "";
        }
    }

    public class GltfAccessors : GltfListClass<GltfAccessor> {
        public GltfAccessors(Gltf pRoot) : base(pRoot) {
        }

        public override string toJSON() {
            return "";
        }
    }

    public class GltfAccessor : GltfClass {
        public GltfAccessor(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.accessors.Add(this);
        }

        public override string toJSON() {
            return "";
        }
    }

    public class GltfBuffers : GltfListClass<GltfBuffer> {
        public GltfBuffers(Gltf pRoot) : base(pRoot) {
        }

        public override string toJSON() {
            return "";
        }
    }

    public class GltfBuffer : GltfClass {
        public GltfBuffer(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.buffers.Add(this);
        }

        public override string toJSON() {
            return "";
        }
    }

    public class GltfBufferViews : GltfListClass<GltfBufferView> {
        public GltfBufferViews(Gltf pRoot) : base(pRoot) {
        }

        public override string toJSON() {
            return "";
        }
    }

    public class GltfBufferView : GltfClass {
        public GltfBufferView(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.bufferViews.Add(this);
        }

        public override string toJSON() {
            return "";
        }
    }

}
