using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Valve.Newtonsoft.Json.Linq;
using Valve.VR;
using static ValheimVRMod.Utilities.LogUtils;

namespace ValheimVRMod.VRCore.Backends
{
    // Creates the SteamVR action sets and actions from the backend-neutral catalog (VRInputActions). This takes the
    // place of the SteamVR_Actions class that the SteamVR plugin generates from actions.json, and makes the same
    // public SteamVR_Input calls in the same order as its PreInitialize(). SteamVR_Input.Initialize() then gets a
    // handle for every action in the arrays filled here.
    static class SteamVRActionCatalog
    {
        static readonly Dictionary<string, SteamVR_Action> actions = new Dictionary<string, SteamVR_Action>();
        static readonly Dictionary<string, SteamVR_ActionSet> sets = new Dictionary<string, SteamVR_ActionSet>();

        public static bool IsBuilt { get; private set; }

        public static void Build(IReadOnlyList<VRActionSetDecl> setDecls, IReadOnlyList<VRActionDecl> actionDecls)
        {
            if (IsBuilt)
            {
                return;
            }
            // The arrays are replaced rather than appended to, so this is safe even where a stale SteamVR_Actions.dll
            // is still installed and SteamVR_Input's static constructor has already run its PreInitialize().
            var builtSets = new SteamVR_ActionSet[setDecls.Count];
            for (int i = 0; i < setDecls.Count; i++)
            {
                builtSets[i] = sets[setDecls[i].Path] = SteamVR_ActionSet.Create<SteamVR_ActionSet>(setDecls[i].Path);
            }
            SteamVR_Input.actionSets = builtSets;
            SteamVR_Input.PreinitializeActionSetDictionaries();

            var all = new List<SteamVR_Action>();
            foreach (var decl in actionDecls)
            {
                if (!sets.ContainsKey(decl.SetPath))
                {
                    throw new InvalidOperationException("Action " + decl.Path + " is in an undeclared action set");
                }
                var action = Create(decl.Kind, decl.Path);
                actions.Add(decl.Path, action);
                all.Add(action);
            }
            SteamVR_Input.actions = all.ToArray();
            SteamVR_Input.actionsIn = all.OfType<ISteamVR_Action_In>().ToArray();
            SteamVR_Input.actionsOut = all.OfType<ISteamVR_Action_Out>().ToArray();
            SteamVR_Input.actionsBoolean = all.OfType<SteamVR_Action_Boolean>().ToArray();
            SteamVR_Input.actionsSingle = all.OfType<SteamVR_Action_Single>().ToArray();
            SteamVR_Input.actionsVector2 = all.OfType<SteamVR_Action_Vector2>().ToArray();
            SteamVR_Input.actionsVector3 = all.OfType<SteamVR_Action_Vector3>().ToArray();
            SteamVR_Input.actionsPose = all.OfType<SteamVR_Action_Pose>().ToArray();
            SteamVR_Input.actionsSkeleton = all.OfType<SteamVR_Action_Skeleton>().ToArray();
            SteamVR_Input.actionsVibration = all.OfType<SteamVR_Action_Vibration>().ToArray();
            SteamVR_Input.actionsNonPoseNonSkeletonIn = all.OfType<ISteamVR_Action_In>()
                .Where(action => !(action is SteamVR_Action_Pose) && !(action is SteamVR_Action_Skeleton)).ToArray();
            SteamVR_Input.PreinitializeActionDictionaries();
            SteamVR_Input.PreinitializeFinishActionSets();
            IsBuilt = true;
        }

        static SteamVR_Action Create(VRActionKind kind, string path)
        {
            switch (kind)
            {
                case VRActionKind.Boolean: return SteamVR_Action.Create<SteamVR_Action_Boolean>(path);
                case VRActionKind.Single: return SteamVR_Action.Create<SteamVR_Action_Single>(path);
                case VRActionKind.Vector2: return SteamVR_Action.Create<SteamVR_Action_Vector2>(path);
                // SteamVR_Action_Skeleton does not derive from SteamVR_Action_Pose, so OfType<> keeps them apart above.
                case VRActionKind.Pose: return SteamVR_Action.Create<SteamVR_Action_Pose>(path);
                case VRActionKind.Skeleton: return SteamVR_Action.Create<SteamVR_Action_Skeleton>(path);
                case VRActionKind.Haptic: return SteamVR_Action.Create<SteamVR_Action_Vibration>(path);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static T Action<T>(string path) where T : SteamVR_Action
        {
            if (!actions.TryGetValue(path, out var action))
            {
                throw new ArgumentException("Undeclared action: " + path);
            }
            if (!(action is T typed))
            {
                throw new ArgumentException("Action " + path + " is a " + action.GetType().Name + ", not a " + typeof(T).Name);
            }
            return typed;
        }

        public static SteamVR_ActionSet ActionSet(string path)
        {
            if (!sets.TryGetValue(path, out var set))
            {
                throw new ArgumentException("Undeclared action set: " + path);
            }
            return set;
        }

        // Logs an error for every way the shipped actions.json differs from the catalog. SteamVR takes the action
        // names, types and default bindings from that file and the actions themselves from the catalog, so an action
        // missing from either side cannot be bound or is not read.
        public static void CheckManifest(IReadOnlyList<VRActionSetDecl> setDecls, IReadOnlyList<VRActionDecl> actionDecls)
        {
            string path;
            string json;
            try
            {
                path = SteamVR_Input.GetActionsFilePath();
                json = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                LogError("Could not read the SteamVR action manifest to check it against the action catalog: " + e.Message);
                return;
            }
            var problems = CompareManifest(json, setDecls, actionDecls);
            foreach (var problem in problems)
            {
                LogError("Action catalog and " + path + " differ: " + problem);
            }
            if (problems.Count == 0)
            {
                LogDebug("Action catalog matches " + path);
            }
        }

        // Also run offline by tests/BackendRefactor.
        internal static List<string> CompareManifest(string json, IReadOnlyList<VRActionSetDecl> setDecls, IReadOnlyList<VRActionDecl> actionDecls)
        {
            var problems = new List<string>();
            var manifest = JObject.Parse(json);

            var manifestSets = ((JArray)manifest["action_sets"] ?? new JArray()).Select(set => (string)set["name"]).ToList();
            var catalogSets = setDecls.Select(set => set.Path).ToList();
            foreach (var missing in catalogSets.Except(manifestSets))
            {
                problems.Add("action set " + missing + " is not in actions.json");
            }
            foreach (var missing in manifestSets.Except(catalogSets))
            {
                problems.Add("action set " + missing + " is not in the catalog");
            }

            var manifestActions = new Dictionary<string, JToken>();
            foreach (var action in (JArray)manifest["actions"] ?? new JArray())
            {
                manifestActions[(string)action["name"]] = action;
            }
            var names = new Dictionary<string, string>();
            foreach (var localization in (JArray)manifest["localization"] ?? new JArray())
            {
                if ((string)localization["language_tag"] != "en_US")
                {
                    continue;
                }
                foreach (var entry in ((JObject)localization).Properties())
                {
                    names[entry.Name] = (string)entry.Value;
                }
            }

            foreach (var decl in actionDecls)
            {
                if (!manifestActions.TryGetValue(decl.Path, out var action))
                {
                    problems.Add("action " + decl.Path + " is not in actions.json");
                    continue;
                }
                var type = (string)action["type"];
                if (type != ManifestType(decl.Kind))
                {
                    problems.Add("action " + decl.Path + " is a " + ManifestType(decl.Kind) + " in the catalog but a " + type + " in actions.json");
                }
                var requirement = (string)action["requirement"] ?? "suggested";
                if (requirement != decl.Requirement.ToString().ToLowerInvariant())
                {
                    problems.Add("action " + decl.Path + " is " + decl.Requirement.ToString().ToLowerInvariant() + " in the catalog but " + requirement + " in actions.json");
                }
                // actions.json does not name every action, but those it names have to agree.
                if (names.TryGetValue(decl.Path, out var name) && name != decl.LocalizedName)
                {
                    problems.Add("action " + decl.Path + " is named \"" + decl.LocalizedName + "\" in the catalog but \"" + name + "\" in actions.json");
                }
            }
            foreach (var missing in manifestActions.Keys.Except(actionDecls.Select(decl => decl.Path)))
            {
                problems.Add("action " + missing + " is not in the catalog");
            }
            return problems;
        }

        static string ManifestType(VRActionKind kind)
        {
            switch (kind)
            {
                case VRActionKind.Boolean: return "boolean";
                case VRActionKind.Single: return "vector1";
                case VRActionKind.Vector2: return "vector2";
                case VRActionKind.Pose: return "pose";
                case VRActionKind.Skeleton: return "skeleton";
                case VRActionKind.Haptic: return "vibration";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
