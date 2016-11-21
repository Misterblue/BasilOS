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

namespace org.herbal3d.Basil {
    /* A simple implementation of the Promise pattern.
     *
     * This class is a simple implementation of the promise coding pattern
     *    that looks more like the Promise/Future interface used in other languages.
     *    There exist complete and featureful alternatives
     *    (https://github.com/Real-Serious-Games/C-Sharp-Promise for instance)
     *    but this is a very simple pattern that is not dependent on any external
     *    library or package.
     *
     *  Only implements the simple:
     *    SimplePromise<T> someDay = new SimplePromise(resolver, rejecter);
     *      or
     *    SimplePromise<T> someDay = new SimplePromise();
     *    somday.Then(resolver).Rejected(rejecter);
     *  The execution routine calls:
     *    someDay.Resolve(T value);
     *       or
     *    someDay.Reject(Exception e);
     */

    public class SimplePromise<T> {
        private enum ResolutionState {
            NoValueOrResolver,
            HaveResolver,
            HaveValue,
            ResolutionComplete
        };

        private ResolutionState resolverState;
        private Object resolverStateLock = new object();   // access lock for the above state variable
        private Action<T> resolver;
        private ResolutionState rejectorState;
        private Action<Exception> rejecter;

        // We either get the value or the resolver first.
        private T resolveValue;
        private Exception rejectValue;

        public SimplePromise() {
            resolverState = ResolutionState.NoValueOrResolver;
            rejectorState = ResolutionState.NoValueOrResolver;

        }

        // public SimplePromise(Action<T> resolve, Action<Exception> reject) {
        //     resolver = resolve;
        //     rejecter = reject;
        // }

        // Called by the one doing the action to complete the promise
        public void Resolve(T val) {
            bool doit = false;
            lock (resolverStateLock) {
                switch (resolverState) {
                    case ResolutionState.NoValueOrResolver:
                        resolveValue = val;
                        resolverState = ResolutionState.HaveValue;
                        break;
                    case ResolutionState.HaveResolver:
                        doit = true;
                        resolveValue = val;
                        resolverState = ResolutionState.ResolutionComplete;
                        break;
                    case ResolutionState.HaveValue:
                        throw new Exception("SimplePromise.Resolve: double resolving of value");
                    case ResolutionState.ResolutionComplete:
                        throw new Exception("SimplePromise.Resolve: resolving of value after completion");
                }
            }
            if (doit) resolver(resolveValue);
        }

        // Called by the one doing the action to reject the promise
        public void Reject(Exception e) {
            bool doit = false;
            lock (resolverStateLock) {
                switch (rejectorState) {
                    case ResolutionState.NoValueOrResolver:
                        rejectValue = e;
                        rejectorState = ResolutionState.HaveValue;
                        break;
                    case ResolutionState.HaveResolver:
                        doit = true;
                        rejectValue = e;
                        rejectorState = ResolutionState.ResolutionComplete;
                        break;
                    case ResolutionState.HaveValue:
                        throw new Exception("SimplePromise.Reject: double rejection");
                    case ResolutionState.ResolutionComplete:
                        throw new Exception("SimplePromise.Reject: rejection after completion");
                }
            }
            if (doit) rejecter(rejectValue);
        }

        public SimplePromise<T> Then(Action<T> resolve) {
            bool doit = false;
            lock (resolverStateLock) {
                switch (resolverState) {
                    case ResolutionState.NoValueOrResolver:
                        resolver = resolve;
                        resolverState = ResolutionState.HaveResolver;
                        break;
                    case ResolutionState.HaveValue:
                        doit = true;
                        resolver = resolve;
                        resolverState = ResolutionState.ResolutionComplete;
                        break;
                    case ResolutionState.HaveResolver:
                        throw new Exception("SimplePromise.Then: double resolving");
                    case ResolutionState.ResolutionComplete:
                        throw new Exception("SimplePromise.Then: resolving after completion");
                }
            }
            if (doit) resolver(resolveValue);
            return this;
        }

        public SimplePromise<T> Rejected(Action<Exception> reject) {
            bool doit = false;
            lock (resolverStateLock) {
                switch (rejectorState) {
                    case ResolutionState.NoValueOrResolver:
                        rejecter = reject;
                        rejectorState = ResolutionState.HaveResolver;
                        break;
                    case ResolutionState.HaveValue:
                        doit = true;
                        rejecter = reject;
                        rejectorState = ResolutionState.ResolutionComplete;
                        break;
                    case ResolutionState.HaveResolver:
                        throw new Exception("SimplePromise.Rejected: double rejection");
                    case ResolutionState.ResolutionComplete:
                        throw new Exception("SimplePromise.Then: rejecting after completion");
                }
            }
            if (doit) rejecter(rejectValue);
            return this;
        }

    }
}
