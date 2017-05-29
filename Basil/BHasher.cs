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

    // Basil wrapper for hash functions.
    // There are several different hashing systems ranging from int's to SHA versions.
    // The model here is to create a hasher of the desired type, do Add's of things to
    //    hash, and complete with a Finish() to return a BHash that contains the hash value.
    // Since some hash functions are incremental (doing Add's) while some are buffer
    //    oriented (create a hash of a byte buffer), the interface is built to cover both.
    //    Some optimizations are implemented internally (like not copying the buffer
    //    for buffer based hashers if Finish(bytes) is used).
    //
    // var hasher = new BHashSHA256();
    // BHash bHash = hasher.Finish(buffer);
    // byte[] theHash = bHash.ToByte();
    //
    // Note that BHash has IEquatable and IComparible so it can be used in Dictionaries
    //     and sorted Lists.

    // A create a BHasher, do a bunch of 'Add's, then Finish().
    public interface IBHasher {
        // Create a new object implementing BHasher, then Add values to be hashed
        void Add(byte c);
        void Add(ushort c);
        void Add(uint c);
        void Add(ulong c);
        void Add(byte[] c);

        BHash Finish();
        // Finish and add byte array. 
        // If no Add's before, can do the hashing without copying the byte array
        BHash Finish(byte[] c);
    }

    // ======================================================================
    public abstract class BHash : IEquatable<BHash>, IComparable<BHash> {
        public abstract override string ToString();
        public abstract byte[] ToByte();
        public abstract int ToInt32();  // returns the hash of the hash if not int based hash
        public abstract override int GetHashCode();
        public abstract bool Equals(BHash other);
        public abstract int CompareTo(BHash obj);
    }

    // A hash that is an Int32
    public class BHashInt : BHash {
        private int hash;
        public BHashInt() {
            hash = 0;
        }
        public BHashInt(int initialHash) {
            hash = initialHash;
        }
        public override string ToString() {
            return hash.ToString();
        }
        public override byte[] ToByte() {
            return BitConverter.GetBytes(hash);
        }
        public override int ToInt32() {
            return hash;
        }
        public override int GetHashCode() {
            return hash;
        }
        public override bool Equals(BHash other) {
            bool ret = false;
            if (other != null) {
                BHash bh = other as BHashInt;
                if (bh != null) {
                    ret = hash.Equals(bh.ToInt32());
                }
            }
            return ret;
        }
        public override int CompareTo(BHash other) {
            int ret = 1;
            if (other != null) {
                BHash bh = other as BHashInt;
                if (bh != null) {
                    ret = hash.CompareTo(bh.ToInt32());
                }
            }
            return ret;
        }
    }

    // A hash that is an array of bytes
    public class BHashBytes : BHash {
        private byte[] hash;
        public BHashBytes() {
            hash = new byte[0];
        }
        public BHashBytes(byte[] initialHash) {
            hash = initialHash;
        }
        public override string ToString() {
            return hash.ToString();
        }
        public override byte[] ToByte() {
            return hash;
        }
        public override int ToInt32() {
            return hash.GetHashCode();
        }
        public override int GetHashCode() {
            return hash.GetHashCode();
        }
        public override bool Equals(BHash other) {
            bool ret = false;
            if (other != null) {
                BHash bh = other as BHashBytes;
                if (bh != null) {
                    ret = hash.Equals(bh.ToInt32());
                }
            }
            return ret;
        }
        public override int CompareTo(BHash other) {
            int ret = 1;
            if (other != null) {
                BHash bh = other as BHashBytes;
                if (bh != null) {
                    byte[] otherb = bh.ToByte();
                    if (hash.Length != otherb.Length) {
                        ret = hash.Length.CompareTo(otherb.Length);
                    }
                    else {
                        ret = 0;    // start off assuming they are equal
                        for (int ii = 0; ii < hash.Length; ii++) {
                            ret = hash[ii].CompareTo(otherb[ii]);
                            if (ret != 0) break;
                        }
                    }
                }
            }
            return ret;
        }
    }

    // ======================================================================
    // ======================================================================
    public abstract class BHasher {
        protected byte[] building;
        protected int buildingLoc;

        public BHasher() {
            building = new byte[1000];
            buildingLoc = 0;
        }

        // Add the given number of bytes to the byte array being built
        protected void AddBytes(byte[] addition, int len) {
        }
    }

    // ======================================================================
    public class BHasherSHA256 : BHasher, IBHasher {
        public BHasherSHA256() : base() {
        }

        public void Add(byte c) {
            byte[] bytes = BitConverter.GetBytes(c);
            AddBytes(bytes, bytes.Length);
        }

        public void Add(ushort c) {
            byte[] bytes = BitConverter.GetBytes(c);
            AddBytes(bytes, bytes.Length);
        }

        public void Add(uint c) {
            byte[] bytes = BitConverter.GetBytes(c);
            AddBytes(bytes, bytes.Length);
        }

        public void Add(ulong c) {
            byte[] bytes = BitConverter.GetBytes(c);
            AddBytes(bytes, bytes.Length);
        }

        public void Add(byte[] c) {
            AddBytes(c, c.Length);
        }

        public BHash Finish() {
            throw new NotImplementedException();
        }

        public BHash Finish(byte[] c) {
            throw new NotImplementedException();
        }
    }


}
