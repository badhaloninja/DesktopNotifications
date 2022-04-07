using CloudX.Shared;
using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BaseX;

namespace DesktopNotifications
{
    public class DesktopNotifications : NeosMod
    {
        public override string Name => "DesktopNotifications";
        public override string Author => "badhaloninja";
        public override string Version => "1.1.0";
        public override string Link => "https://github.com/badhaloninja/DesktopNotifications";


        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float2> distanceFromCenter = new ModConfigurationKey<float2>("distanceFromCenter", "Distance From Center", () => new float2(0.7f, 0.32f));


        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<Alignment> desktopAlignment = new ModConfigurationKey<Alignment>("desktopAlignment", "Desktop Alignment", () => Alignment.TopRight);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> indicatorSize = new ModConfigurationKey<float>("indicatorSize", "Indicator Size", () => 1f);

        private static ModConfiguration config;

        private static float3 offsetPosition
        {
            get
            {
                // Convert enum into a matrix to multiply the offet by
                var alignmentInt = (int)config.GetValue(desktopAlignment); // Convert to int for funny math
                var alignmentMul = new float3(alignmentInt % 3 - 1, (alignmentInt / 3 - 1) * -1, 0f); //mmhm fun stuff
                /* X
                 * Left | Center | Right
                 *  -1  |   0    |   1
                 * ----------------------
                 * Y
                 * Top  | Middle | Bottom
                 *  1   |   0    |   -1
                 */

                return config.GetValue(distanceFromCenter).xy_ * alignmentMul; // :D
            }
        }

        public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder)
        { // Wipe previous version of the config due to enum saving differently
            builder.Version(new Version(1, 1, 0));
        }

        public override void OnEngineInit()
        {
            config = GetConfiguration();

            Harmony harmony = new Harmony("me.badhaloninja.DesktopNotifications");
            harmony.PatchAll();
            
            config.OnThisConfigurationChanged += handleConfigChanged;
        }


        [HarmonyPatch(typeof(NotificationPanel), "OnCommonUpdate")]
        class NotificationPanel_OnCommonUpdate_Patch
        {
            public static void Postfix(NotificationPanel __instance)
            {
                Slot overlayRoot = __instance.World.GetGloballyRegisteredComponent<OverlayManager>().OverlayRoot;
                Slot notificationOffsetSlot = overlayRoot.FindInChildren("NotificationOffset");
                if (notificationOffsetSlot == null)
                { // Create overlay offset slot
                    notificationOffsetSlot = overlayRoot.AddSlot("NotificationOffset");
                    notificationOffsetSlot.LocalPosition = offsetPosition;
                    notificationOffsetSlot.LocalRotation = floatQ.AxisAngle(float3.Up, 45f);
                    notificationOffsetSlot.LocalScale = float3.One * config.GetValue(indicatorSize);
                };

                if (!__instance.InputInterface.VR_Active)
                { // Desktop
                    if (__instance.Slot.Parent.Parent != notificationOffsetSlot)
                    { // Set parent if not already notificationOffsetSlot
                        __instance.Slot.Parent.SetParent(notificationOffsetSlot, false);
                    }
                    return;
                }
                if (__instance.Slot.Parent.Parent == notificationOffsetSlot)
                { // Reset parent if in vr and is set to notificationOffsetSlot
                    __instance.Slot.Parent.SetParent(Userspace.UserspaceWorld.GetRadiantDash().Slot, false);
                }
            }
        }



        private static void handleConfigChanged(ConfigurationChangedEvent evt)
        { // Update Positiion
            Slot overlayRoot = Userspace.Current.World.GetGloballyRegisteredComponent<OverlayManager>().OverlayRoot;
            Slot notificationOffsetSlot = overlayRoot.FindInChildren("NotificationOffset");
            if (notificationOffsetSlot == null) return;


            notificationOffsetSlot.LocalPosition = offsetPosition;
            notificationOffsetSlot.LocalRotation = floatQ.AxisAngle(float3.Up, 45f);
            notificationOffsetSlot.LocalScale = float3.One * config.GetValue(indicatorSize);
        }



        [HarmonyPatch(typeof(NotificationPanel), "AddNotification", new Type[] { typeof(string), typeof(string), typeof(Uri), typeof(color), typeof(string), typeof(Uri), typeof(IAssetProvider<AudioClip>) })]
        class NotificationPanel_AddNotification_Patch
        { // Make notifications generate in desktop mode again
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
            { // Pretty good tutorial, https://gist.github.com/JavidPack/454477b67db8b017cb101371a8c49a1c
                var code = new List<CodeInstruction>(instructions);
                int end = -1;

                for (int i = 0; i < code.Count; i++) //Find where to inject code
                {
                    if (code[i].opcode == OpCodes.Callvirt && code[i].operand is MethodInfo && (code[i].operand as MethodInfo).Name == "get_VR_Active") //find a get_VR_Active call
                    { // Check if base.InputInterface.VR_Active is being called
                        if (code[i - 2].opcode != OpCodes.Ldarg_0 || code[i + 2].opcode != OpCodes.Ret) // make sure it is the correct one *:*)
                        { // Skip if it does not call a return immediately after
                            continue;
                        }
                        end = i + 2; // Store location of return
                        break;
                    }
                }

                if (end != -1)
                {
                    code.RemoveAt(end); // Remove return
                }
                /* Target code
                 * 
                  if (!base.InputInterface.VR_Active)
	              {
		              return; // Remove this line 
	              }
                */

                return code;
            }
        }
    }
}