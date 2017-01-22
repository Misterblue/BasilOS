using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;

using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace org.herbal3d.BasilOS {
    public class CoordSystem {
        public const int Handedness = 0x200;    // the bit that specifies the handedness
        public const int UpDimension = 0x00F;   // the field that specifies the up dimension
        public const int RightHand = 0x000;
        public const int LeftHand = 0x200;
        public const int Yup = 0x001;
        public const int Zup = 0x002;
        public const int RightHand_Yup = RightHand + Yup;
        public const int LeftHand_Yup = LeftHand + Yup;
        public const int RightHand_Zup = RightHand + Zup;
        public const int LeftHand_Zup = LeftHand + Zup;
        // RightHand_Zup: SL
        // RightHand_Yup: OpenGL
        // LeftHand_Yup: DirectX, Babylon, Unity

        public int system;
        public CoordSystem() {
            system = RightHand_Zup; // default to SL
        }
        public CoordSystem(int initCoord) {
            system = initCoord;
        }
        public int getUpDimension { get  { return system & UpDimension; } }
        public int getHandedness { get  { return system & Handedness; } }
        public bool isHandednessChanging(CoordSystem nextSystem) {
            return (system & Handedness) != (nextSystem.system & Handedness);
        }
        public string SystemName { get { return SystemNames[system]; } }
        public static Dictionary<int, string> SystemNames = new Dictionary<int, string>() {
            { RightHand_Yup, "RightHand,Y-up" },
            { RightHand_Zup, "RightHand,Z-up" },
            { LeftHand_Yup, "LeftHand,Y-up" },
            { LeftHand_Zup, "LeftHand,Z-up" }
        };
    }

    // An extended description of an entity that includes the original
    //     prim description as well as the mesh.
    // All the information about the meshed piece is collected here so other mappings
    //     can happen with the returned information (creating Basil Entitities, etc)
    public class ExtendedPrim {
        public SceneObjectGroup SOG { get; set; }
        public SceneObjectPart SOP { get; set; }
        public OMV.Primitive primitive { get; set; }
        public OMVR.FacetedMesh facetedMesh { get; set; }

        public CoordSystem coordSystem; // coordinate system of this prim
        public OMV.Vector3 translation;
        public OMV.Quaternion rotation;
        public OMV.Vector3 scale;
        public bool positionIsParentRelative;
        // Texture information for the faces
        public Dictionary<int, OMV.Primitive.TextureEntryFace> faceTextures { get; set; }
        // Images for a fae if it is specified
        public Dictionary<int, Image> faceImages { get; set; }
        public Dictionary<int, string> faceFilenames { get; set; }

        public ExtendedPrim() {
        }

        // Initialize an ExtendedPrim from the OpenSimulator structures.
        // Note that the translation and rotation are copied into the ExtendedPrim for later coordinate modification.
        public ExtendedPrim(SceneObjectGroup pSOG, SceneObjectPart pSOP, OMV.Primitive pPrim, OMVR.FacetedMesh pFMesh) {
            SOG = pSOG;
            SOP = pSOP;
            primitive = pPrim;
            facetedMesh = pFMesh;
            if (SOP != null) {
                if (SOP.IsRoot) {
                    translation = SOP.GetWorldPosition();
                    rotation = SOP.GetWorldRotation();
                    positionIsParentRelative = false;
                }
                else {
                    translation = SOP.OffsetPosition;
                    rotation = SOP.RotationOffset;
                    positionIsParentRelative = true;
                }
            }
            scale = SOP.Scale;
            faceTextures = new Dictionary<int, OMV.Primitive.TextureEntryFace>();
            faceImages = new Dictionary<int, Image>();
            faceFilenames = new Dictionary<int, string>();
            coordSystem = new CoordSystem(CoordSystem.RightHand_Zup);    // default to SL coordinates
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

        // Return the primary version of this prim which is the highest LOD verstion.
        public ExtendedPrim primaryExtendedPrim {
            get {
                ExtendedPrim ret = null;
                this.TryGetValue(PrimGroupType.lod1, out ret);
                return ret;
            }
        }
    }

    // some entities are made of multiple prims (linksets)
    public class EntityGroup : List<ExtendedPrimGroup> {
        public SceneObjectGroup SOG { get; protected set;  }
        public EntityGroup(SceneObjectGroup pSOG) : base() {
            SOG = pSOG;
        }
    }

    // list of entities ... can safely add and entity multiple times
    public class EntityGroupList : List<EntityGroup> {
        public EntityGroupList() : base() {
        }

        // Add the entity group to the list if it is not alreayd in the list
        public bool AddUniqueEntity(EntityGroup added) {
            bool ret = false;
            if (!base.Contains(added)) {
                base.Add(added);
                ret = true;
            }
            return ret;
        }

        // Perform an action on every extended prim in this EntityGroupList
        public void ForEachExtendedPrim(Action<ExtendedPrim> aeg) {
            this.ForEach(eGroup => {
                eGroup.ForEach(ePGroup => {
                    ExtendedPrim ep = ePGroup.primaryExtendedPrim;  // the interesting one is the high rez one
                    aeg(ep);
                });
            });
        }
    }
}
