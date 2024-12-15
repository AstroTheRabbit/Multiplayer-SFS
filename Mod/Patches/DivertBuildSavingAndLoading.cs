using System.Threading;
using UnityEngine;
using HarmonyLib;
using SFS;
using SFS.IO;
using SFS.Builds;
using SFS.WorldBase;

namespace MultiplayerSFS.Mod.Patches
{
    /// <summary>
    /// Diverts the use of the `BuildPersistent` folder when in multiplayer.
    /// </summary>
    public class DivertBuildSavingAndLoading
    {
        [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.SaveBuildPersistent))]
        public class SavingCache_SaveBuildPersistent
        {
            public static bool Prefix(SavingCache __instance, Blueprint new_BuildPersistent, bool cache)
            {
                if (ClientManager.multiplayerEnabled.Value)
                {
                    __instance.FieldRef<SavingCache.Data<Blueprint>>("buildPersistent") = SavingCache.Data<Blueprint>.Cache(new_BuildPersistent, cache);
                    SavingCache.SaveAsync(delegate
                    {
                        Blueprint.Save(Main.buildPersistentFolder, new_BuildPersistent, Application.version);
                    });
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.Preload_BlueprintPersistent))]
        public class SavingCache_Preload_BlueprintPersistent
        {
            public static bool Prefix(SavingCache __instance)
            {
                if (ClientManager.multiplayerEnabled.Value)
                {
                    ref SavingCache.Data<Blueprint> buildPersistent = ref __instance.FieldRef<SavingCache.Data<Blueprint>>("buildPersistent");
                    if (buildPersistent == null)
                    {
                        FolderPath path = Main.buildPersistentFolder;
                        MsgCollector logger = new MsgCollector();
                        buildPersistent = new SavingCache.Data<Blueprint>
                        {
                            thread = new Thread((ThreadStart)delegate
                        {
                            ref SavingCache.Data<Blueprint> buildPersistent_Thread = ref __instance.FieldRef<SavingCache.Data<Blueprint>>("buildPersistent");
                            if (path.FolderExists() && Blueprint.TryLoad(path, logger, out Blueprint blueprint))
                            {
                                buildPersistent_Thread.result = (true, blueprint, (logger.msg.Length > 0) ? logger.msg.ToString() : null);
                            }
                            else
                            {
                                buildPersistent_Thread.result = (false, null, null);
                            }
                        })
                        };
                        buildPersistent.thread.Start();
                    }
                    return false;
                }
                return true;
            }
        }
    }
}

