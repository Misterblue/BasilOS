using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;

using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace org.herbal3d.BasilOS {
    // An extended description of an entity that includes the original
    //     prim description as well as the mesh.
    // All the information about the meshed piece is collected here so other mappings
    //     can happen with the returned information (creating Basil Entitities, etc)
    public class ExtendedPrim {
        public SceneObjectGroup SOG;
        public SceneObjectPart SOP;
        public OMV.Primitive primitive;
        public OMVA.PrimObject primObject;
        public OMVR.FacetedMesh facetedMesh;

        public ExtendedPrim() {
        }

        public ExtendedPrim(SceneObjectGroup pSOG, SceneObjectPart pSOP, OMV.Primitive pPrim, OMVR.FacetedMesh pFMesh) {
            SOG = pSOG;
            SOP = pSOP;
            primitive = pPrim;
            facetedMesh = pFMesh;
        }

        public override int GetHashCode() {
            int ret = 0;
            if (primitive != null) {
                ret = primitive.GetHashCode();
            }
            else {
                ret = base.GetHashCode();
            }
            return ret;
        }
    };

    // A prim mesh can be made up of many versions
    public enum PrimGroupType {
        physics,
        lod1,   // this is default and what is built for a standard prim
        lod2,
        lod3,
        lod4
    };

    // Some prims (like the mesh type) have multiple versions to make one entity
    public class ExtendedPrimGroup: Dictionary<PrimGroupType, ExtendedPrim> {
        public ExtendedPrimGroup() : base() {
        }

        // Create with a single prim
        public ExtendedPrimGroup(ExtendedPrim singlePrim) : base() {
            this.Add(PrimGroupType.lod1, singlePrim);
        }
    }

    // some entities are made of multiple prims (linksets)
    public class EntityGroup : List<ExtendedPrimGroup> {
        public SceneObjectGroup SOG;
        public EntityGroup(SceneObjectGroup pSOG) : base() {
            SOG = pSOG;
        }
    }

    // list of entities ... can safely add and entity multiple times
    public class EntityGroupList : List<EntityGroup> {
        public EntityGroupList() : base() {
        }

        // Add the entity group to the list if it is not alreayd in the list
        public void AddUniqueEntity(EntityGroup added) {
            if (!base.Contains(added)) {
                base.Add(added);
            }
        }
    }
}
