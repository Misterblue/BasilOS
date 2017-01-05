/* ==============================================================================
Copyright (c) 2016 Robert Adams

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
================================================================================ */

using System;
using System.Collections.Generic;

using OMV = OpenMetaverse;

namespace org.herbal3d.BasilOS {

    // Class for collecting all me mess around asset names.
    // All filename, type, and version conversions are done here.
    //
    // At the moment, an entity just has a UUID
    public class EntityHandle : IEqualityComparer<EntityHandle> {

        OMV.UUID m_uuid;

        public EntityHandle(OMV.UUID id) {
            m_uuid = id;
        }

        // OpenSim likes to specify assets with a simple string of the asset's UUID
        public string GetOSAssetString() {
            return m_uuid.ToString();
        }

        public OMV.UUID GetUUID() {
            return m_uuid;
        }

        public override string ToString() {
            return m_uuid.ToString();
        }

        // IComparable
        public int CompareTo(object obj) {
            int ret = 0;
            EntityHandle other = obj as EntityHandle;
            if (other == null) {
                throw new ArgumentException("CompareTo in EntityHandle: other type not EntityHandle");
            }
            if (this.m_uuid != other.m_uuid) {
                string thisOne = this.m_uuid.ToString();
                string otherOne = this.m_uuid.ToString();
                ret = thisOne.CompareTo(otherOne);
            }
            return ret;
        }

        // System.Object.GetHashCode()
        public override int GetHashCode() {
            return m_uuid.GetHashCode();
        }

        // IEqualityComparer.Equals
        public bool Equals(EntityHandle x, EntityHandle y) {
            return x.m_uuid.CompareTo(y.m_uuid) == 0;
        }

        // IEqualityComparer.GetHashCode
        public int GetHashCode(EntityHandle obj) {
            return obj.m_uuid.GetHashCode();
        }
    }
}
