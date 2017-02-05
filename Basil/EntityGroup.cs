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
        public const int Handedness = 0x200;    // bit that specifies the handedness
        public const int UpDimension = 0x00F;   // field that specifies the up dimension
        public const int UVOrigin = 0x030;      // field that specifies UV origin location
        public const int RightHand = 0x000;
        public const int LeftHand = 0x200;
        public const int Yup = 0x001;
        public const int Zup = 0x002;
        public const int UVOriginUpperLeft = 0x000; // most of the world has origin in upper left
        public const int UVOriginLowerLeft = 0x010; // OpenGL specifies UV origin in lower left
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
        public int getUVOrigin { get  { return system & UVOrigin; } }
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

    public class FaceInfo {
        public int num;                 // number of this face on the prim
        public List<OMVR.Vertex> vertexs;
        public List<ushort> indices;

        public ExtendedPrim containingPrim;

        // Information about the material decorating the vertices
        public OMV.Primitive.TextureEntryFace textureEntry;
        public OMV.UUID? textureID;     // UUID of the texture if there is one
        public Image faceImage;
        public bool hasAlpha;          // true if there is some transparancy in the surface
        public bool fullAlpha;         // true if the alpha is everywhere
        public string imageFilename;    // filename built for this face material
        public string imageURI;    // filename built for this face material

        public FaceInfo(int pNum, ExtendedPrim pContainingPrim) {
            num = pNum;
            containingPrim = pContainingPrim;
            vertexs = new List<OMVR.Vertex>();
            indices = new List<ushort>();
            hasAlpha = false;
            fullAlpha = false;
            faceImage = null;       // flag saying if an image is present
            textureID = null;       // flag saying if an image is present
        }
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
        public OMV.UUID ID;
        public string Name;

        public CoordSystem coordSystem; // coordinate system of this prim
        public OMV.Vector3 translation;
        public OMV.Quaternion rotation;
        public OMV.Vector3 scale;
        public OMV.Matrix4? transform;
        public bool positionIsParentRelative;

        // The data is taken out of the structures above and copied here for mangling
        public Dictionary<int, FaceInfo> faces;

        // This logic is here mostly because there are some entities that are not scene objects.
        // Terrain, in particular.
        public bool isRoot {
            get {
                bool ret = true;
                if (SOP != null && !SOP.IsRoot)
                    ret = false;
                return ret;
            }
        }

        // A very empty ExtendedPrim. You must initialize everything by hand after creating this.
        public ExtendedPrim() {
            transform = null;
            coordSystem = new CoordSystem(CoordSystem.RightHand_Zup);    // default to SL coordinates
            faces = new Dictionary<int, FaceInfo>();
        }

        // Initialize an ExtendedPrim from the OpenSimulator structures.
        // Note that the translation and rotation are copied into the ExtendedPrim for later coordinate modification.
        public ExtendedPrim(SceneObjectGroup pSOG, SceneObjectPart pSOP, OMV.Primitive pPrim, OMVR.FacetedMesh pFMesh) {
            SOG = pSOG;
            SOP = pSOP;
            primitive = pPrim;
            facetedMesh = pFMesh;
            translation = new OMV.Vector3(0, 0, 0);
            rotation = OMV.Quaternion.Identity;
            scale = OMV.Vector3.One;
            transform = null;       // matrix overrides the translation/rotation
            coordSystem = new CoordSystem(CoordSystem.RightHand_Zup);    // default to SL coordinates

            if (SOP != null) {
                ID = SOP.UUID;
                Name = SOP.Name;
            }
            else {
                ID = OMV.UUID.Random();
                Name = "Custom";
            }

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
                scale = SOP.Scale;
            }

            // Copy the vertex information into our face information array.
            // Only the vertex and indices information is put into the face info.
            //       The texture info must be added later.
            faces = new Dictionary<int, FaceInfo>();
            for (int ii = 0; ii < pFMesh.Faces.Count; ii++) {
                OMVR.Face aFace = pFMesh.Faces[ii];
                FaceInfo faceInfo = new FaceInfo(ii, this);
                faceInfo.vertexs = aFace.Vertices.ToList();
                faceInfo.indices = aFace.Indices.ToList();

                faces.Add(ii, faceInfo);
            }
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
        public ExtendedPrim primaryExtendePrim {
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
                    ExtendedPrim ep = ePGroup.primaryExtendePrim;  // the interesting one is the high rez one
                    aeg(ep);
                });
            });
        }
    }
}
