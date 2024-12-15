using HarmonyLib;

namespace MultiplayerSFS.Mod.Patches
{
    public static class FieldRefExtension
    {
        public static ref F FieldRef<F>(this object instance, string field)
        {
            return ref AccessTools.FieldRefAccess<F>(instance.GetType(), field).Invoke(instance);
        }
    }
}

