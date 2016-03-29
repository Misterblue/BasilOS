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
/*
 * Some code covered by: Copyright (c) Contributors, http://opensimulator.org/
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using OpenMetaverse;
using Nini.Config;

namespace Basil.AssetTest
{
public static class BasilParams
{
    private static string LogHeader = "[BASIL PARAMS]";

    public static bool Enabled { get; private set; }

    // =====================================================================================
    // =====================================================================================
    // List of all of the externally visible parameters.
    // For each parameter, this table maps a text name to getter and setters.
    // To add a new externally referencable/settable parameter, add the paramter storage
    //    location somewhere in the program and make an entry in this table with the
    //    getters and setters.
    // It is easiest to find an existing definition and copy it.
    //
    // A ParameterDefn<T>() takes the following parameters:
    //    -- the text name of the parameter. This is used for console input and ini file.
    //    -- a short text description of the parameter. This shows up in the console listing.
    //    -- a default value
    //    -- a delegate for getting the value
    //    -- a delegate for setting the value
    //    -- an optional delegate to update the value in the world. Most often used to
    //          push the new value to an in-world object.
    //
    // The single letter parameters for the delegates are:
    //    s = Scene
    //    v = value (appropriate type)
    private static ParameterDefnBase[] ParameterDefinitions =
    {
        new ParameterDefn<bool>("Active", "If 'true', false then module is not active",
            false ),
    };

    // =====================================================================================
    // =====================================================================================

    // Base parameter definition that gets and sets parameter values via a string
    public abstract class ParameterDefnBase
    {
        public string name;         // string name of the parameter
        public string desc;         // a short description of what the parameter means
        public ParameterDefnBase(string pName, string pDesc)
        {
            name = pName;
            desc = pDesc;
        }
        // Set the parameter value to the default
        public abstract void AssignDefault(Scene s);
        // Get the value as a string
        public abstract string GetValue(Scene s);
        // Set the value to this string value
        public abstract void SetValue(Scene s, string valAsString);
    }

    // Specific parameter definition for a parameter of a specific type.
    public delegate T PGetValue<T>(Scene s);
    public delegate void PSetValue<T>(Scene s, T val);
    public sealed class ParameterDefn<T> : ParameterDefnBase
    {
        private T defaultValue;
        private PSetValue<T> setter;
        private PGetValue<T> getter;
        public ParameterDefn(string pName, string pDesc, T pDefault, PGetValue<T> pGetter, PSetValue<T> pSetter)
            : base(pName, pDesc)
        {
            defaultValue = pDefault;
            setter = pSetter;
            getter = pGetter;
            objectSet = null;
        }
        public ParameterDefn(string pName, string pDesc, T pDefault, PGetValue<T> pGetter, PSetValue<T> pSetter)
            : base(pName, pDesc)
        {
            defaultValue = pDefault;
            setter = pSetter;
            getter = pGetter;
        }
        // Simple parameter variable where property name is the same as the INI file name
        //     and the value is only a simple get and set.
        public ParameterDefn(string pName, string pDesc, T pDefault)
            : base(pName, pDesc)
        {
            defaultValue = pDefault;
            setter = (s, v) => { SetValueByName(s, name, v); };
            getter = (s) => { return GetValueByName(s, name); };
            objectSet = null;
        }
        // Use reflection to find the property named 'pName' in Param and assign 'val' to same.
        private void SetValueByName(Scene s, string pName, T val)
        {
            PropertyInfo prop = typeof(Param).GetProperty(pName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (prop == null)
            {
                // This should only be output when someone adds a new INI parameter and misspells the name.
                s.Logger.ErrorFormat("{0} SetValueByName: did not find '{1}'. Verify specified property name is the same as the given INI parameters name.", LogHeader, pName);
            }
            else
            {
                prop.SetValue(null, val, null);
            }
        }
        // Use reflection to find the property named 'pName' in Param and return the value in same.
        private T GetValueByName(Scene s, string pName)
        {
            PropertyInfo prop = typeof(Param).GetProperty(pName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (prop == null)
            {
                // This should only be output when someone adds a new INI parameter and misspells the name.
                s.Logger.ErrorFormat("{0} GetValueByName: did not find '{1}'. Verify specified property name is the same as the given INI parameter name.", LogHeader, pName);
            }
            return (T)prop.GetValue(null, null);
        }
        public override void AssignDefault(Scene s)
        {
            setter(s, defaultValue);
        }
        public override string GetValue(Scene s)
        {
            return getter(s).ToString();
        }
        public override void SetValue(Scene s, string valAsString)
        {
            // Get the generic type of the setter
            Type genericType = setter.GetType().GetGenericArguments()[0];
            // Find the 'Parse' method on that type
            System.Reflection.MethodInfo parser = null;
            try
            {
                parser = genericType.GetMethod("Parse", new Type[] { typeof(String) } );
            }
            catch (Exception e)
            {
                s.Logger.ErrorFormat("{0} Exception getting parser for type '{1}': {2}", LogHeader, genericType, e);
                parser = null;
            }
            if (parser != null)
            {
                // Parse the input string
                try
                {
                    T setValue = (T)parser.Invoke(genericType, new Object[] { valAsString });
                    // Store the parsed value
                    setter(s, setValue);
                    // s.Logger.DebugFormat("{0} Parameter {1} = {2}", LogHeader, name, setValue);
                }
                catch
                {
                    s.Logger.ErrorFormat("{0} Failed parsing parameter value '{1}' as type '{2}'", LogHeader, valAsString, genericType);
                }
            }
            else
            {
                s.Logger.ErrorFormat("{0} Could not find parameter parser for type '{1}'", LogHeader, genericType);
            }
        }
    }

    // Search through the parameter definitions and return the matching
    //    ParameterDefn structure.
    // Case does not matter as names are compared after converting to lower case.
    // Returns 'false' if the parameter is not found.
    internal static bool TryGetParameter(string paramName, out ParameterDefnBase defn)
    {
        bool ret = false;
        ParameterDefnBase foundDefn = null;
        string pName = paramName.ToLower();

        foreach (ParameterDefnBase parm in ParameterDefinitions)
        {
            if (pName == parm.name.ToLower())
            {
                foundDefn = parm;
                ret = true;
                break;
            }
        }
        defn = foundDefn;
        return ret;
    }

    // Pass through the settable parameters and set the default values
    internal static void SetParameterDefaultValues(Scene scene)
    {
        foreach (ParameterDefnBase parm in ParameterDefinitions)
        {
            parm.AssignDefault(scene);
        }
    }

    // Get user set values out of the ini file.
    internal static void SetParameterConfigurationValues(Scene scene, IConfig cfg)
    {
        foreach (ParameterDefnBase parm in ParameterDefinitions)
        {
            parm.SetValue(scene, cfg.GetString(parm.name, parm.GetValue(scene)));
        }
    }

}
}
