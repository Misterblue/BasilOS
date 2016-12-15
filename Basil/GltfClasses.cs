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

namespace org.herbal3d.BasilOS {
    public abstract class GltfClasses {
        public abstract string toJSON();
    }

    public class Gltf : GltfClasses {

        public override string toJSON() {
            return null;
        }
    }

    public class GltfScenes : GltfClasses {
        public string defaultSceneID;
        public List<GltfScene>;

        public override string toJSON() {
            return null;
        }
    }

    public class GltfScene : GltfClasses {
        public string[] nodes;      // IDs of top level nodes in the scene
        public string name;
        public string extensions;
        public string extras;

        public override string toJSON() {
            return null;
        }
    }

    public class GltfNode : GltfClasses {
        public string camera;       // non-empty of a camera definition
        public string[] children;   // IDs of children
        public string[] skeleton;   // IDs of skeletons
        public string skin;
        public string jointName;
        public string[] meshes;     // IDs of meshes for this node
        // has either 'matrix' or 'rotation/scale/translation'
        public float[] matrix = new float[16];
        public float[] rotation = new float[4];
        public float[] scale = new float[3];
        public float[3] translation = new float[3];
        public string name;
        public string extensions;   // more JSON describing the extensions used
        public string extras;       // more JSON with additional, beyond-the-standard values
        public GltfNode(GltfScene parentScene) {
        }

        public override string toJSON() {
            return null;
        }
    }

    public class GltfMesh : GltfClasses {
        public GltfMesh(GltfScene parentScene) {
        }

        public override string toJSON() {
            return null;
        }
    }

}
