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

    // A hash algorighm is created, do a bunch of 'Add's, then Finish().
    public interface IBHasher {
        // Create a new object implementing BHasher, then Add values to be hashed
        void Add(byte c);
        void Add(ushort c);
        void Add(uint c);
        void Add(ulong c);
        void Add(byte[] c);

        void Finish();
        // Finish and add byte array. 
        // If no Add's before, can do the hashing without copying the byte array
        void Finish(byte[] c);

        // After Finish(), get the hashed value
        string ToString();
        byte[] ToByte();
    }

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

    public class BHasherSHA256 : BHasher, IBHasher {
        public BHasherSHA256() : base() {
        }

        public void Add(byte c) {
            throw new NotImplementedException();
        }

        public void Add(ushort c) {
            throw new NotImplementedException();
        }

        public void Add(uint c) {
            throw new NotImplementedException();
        }

        public void Add(ulong c) {
            throw new NotImplementedException();
        }

        public void Add(byte[] c) {
            AddBytes(c, c.Length);
        }

        public void Finish() {
            throw new NotImplementedException();
        }

        public void Finish(byte[] c) {
            throw new NotImplementedException();
        }

        public byte[] ToByte() {
            throw new NotImplementedException();
        }
    }


}
