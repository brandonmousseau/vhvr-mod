using UnityEngine;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.Utilities
{
    public class Helper
    {
        public static void logComponents(Transform obj)
        {
            
            string output = "LOG COMPONENTS OF " + obj + ": ";
            output += getComponentTypes(obj);
            LogDebug(output);
            
        }
        
        public static void logChildTree(Transform obj, bool withComponents = false)
        {
            LogDebug("LOG CHILD TREE:");
            loopChildren(obj, withComponents);
        }
        
        private static string getComponentTypes(Transform obj)
        {
            string output = "";
            
            Component[] components = obj.GetComponents(typeof(Component));
            for (int i = 0; i < components.Length; i++)
            {
                output += components[i].GetType() + ";";
            }

            return output;
        }

        private static void loopChildren(Transform obj, bool withComponents = false, int spaceOffset = 0)
        {
            
            string msg = "";
                
            for (int i = 0; i < spaceOffset; i++)
            {
                msg += "   |";
            }
            
            msg += obj.name;

            if (withComponents)
            {
                msg += " (" + getComponentTypes(obj) + ")";
            }
                
            LogDebug(msg);

            for (int i = 0; i < obj.childCount; i++)
            {
                loopChildren(obj.GetChild(i), withComponents, spaceOffset++);
            }
        }
    }
}