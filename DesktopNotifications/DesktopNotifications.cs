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
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/badhaloninja/DesktopNotifications";


        // BaseX.float2 is not supported with the NeosModLoader config saving currently 
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> verticalDistanceFromCenter = new ModConfigurationKey<float>("verticalDistanceFromCenter", "Vertical Distance From Center", () => 0.32f);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> horizontalDistanceFromCenter = new ModConfigurationKey<float>("horizontalDistanceFromCenter", "Horizontal Distance From Center", () => 0.7f);


        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<Alignment> desktopAlignment = new ModConfigurationKey<Alignment>("desktopAlignment", "Desktop Alignment", () => Alignment.TopRight);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> indicatorSize = new ModConfigurationKey<float>("indicatorSize", "Indicator Size", () => 1f);

        private static ModConfiguration config;

        private static float3 offsetPosition
        {
            get
            {
                var alignmentInt = (int)config.GetValue(desktopAlignment);
                var alignmentMul = new float3(alignmentInt % 3 - 1, (alignmentInt / 3 - 1) * -1, 0f);

                return new float3(config.GetValue(horizontalDistanceFromCenter), config.GetValue(verticalDistanceFromCenter)) * alignmentMul; // *:*)
            }
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
                {
                    notificationOffsetSlot = overlayRoot.AddSlot("NotificationOffset");
                    notificationOffsetSlot.LocalPosition = offsetPosition;
                    notificationOffsetSlot.LocalRotation = floatQ.AxisAngle(float3.Up, 45f);
                    notificationOffsetSlot.LocalScale = float3.One * config.GetValue(indicatorSize);
                };

                if (!__instance.InputInterface.VR_Active)
                {
                    if (__instance.Slot.Parent.Parent != notificationOffsetSlot)
                    {
                        __instance.Slot.Parent.SetParent(notificationOffsetSlot, false);
                    }
                    return;
                }
                if (__instance.Slot.Parent.Parent == notificationOffsetSlot)
                {
                    __instance.Slot.Parent.SetParent(Userspace.UserspaceWorld.GetRadiantDash().Slot, false);
                }
            }
        }



        private static void handleConfigChanged(ConfigurationChangedEvent evt)
        {
            Slot overlayRoot = Userspace.Current.World.GetGloballyRegisteredComponent<OverlayManager>().OverlayRoot;
            Slot notificationOffsetSlot = overlayRoot.FindInChildren("NotificationOffset");
            if (notificationOffsetSlot == null) return;


            notificationOffsetSlot.LocalPosition = offsetPosition;//new float3(0.7f, 0.32f);
            notificationOffsetSlot.LocalRotation = floatQ.AxisAngle(float3.Up, 45f);
            notificationOffsetSlot.LocalScale = float3.One * config.GetValue(indicatorSize);
        }



        [HarmonyPatch(typeof(NotificationPanel), "AddNotification", new Type[] { typeof(string), typeof(string), typeof(Uri), typeof(color), typeof(string), typeof(Uri), typeof(IAssetProvider<AudioClip>) })]
        class NotificationPanel_AddNotification_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
            { // Pretty good tutorial, https://gist.github.com/JavidPack/454477b67db8b017cb101371a8c49a1c
                var code = new List<CodeInstruction>(instructions);
                int end = -1;

                for (int i = 0; i < code.Count; i++) //Find where to inject code
                {
                    if (code[i].opcode == OpCodes.Callvirt && code[i].operand is MethodInfo && (code[i].operand as MethodInfo).Name == "get_VR_Active") //find a get_VR_Active call
                    {
                        if (code[i - 2].opcode != OpCodes.Ldarg_0 || code[i + 2].opcode != OpCodes.Ret) // make sure it is the correct one *:*)
                        {
                            continue;
                        }
                        end = i + 2;
                        break;
                    }
                }

                if (end != -1)
                {
                    code.RemoveAt(end);
                }
                /* Debug
                for (var i = 0; i < code.Count; i++)
                {
                    Msg("IL_" + i.ToString("0000") + ": " + code[i].ToString());
                }*/
                return code;
            }
        }
    }
}