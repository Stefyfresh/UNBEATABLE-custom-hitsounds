using BepInEx;
using BepInEx.Logging;
using System.Collections.Generic;
using HarmonyLib;
using Rhythm;
using DG.Tweening;
using UnityEngine;
using Arcade.UI.SongSelect;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System;
using System.Linq;
using FMODUnity;
using FMOD.Studio;
using FMOD;
using System.Collections;
using BepInEx.Configuration;

namespace CustomHitSounds
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInProcess("UNBEATABLE.exe")]
    public class CustomHitSounds : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "com.stefyfresh.CustomHitSounds";
        public const string PLUGIN_NAME = "Custom Hit Sounds";
        public const string PLUGIN_VERSION = "1.1.3";
        internal static new ManualLogSource Logger;
        public static RhythmTracker sfxTracker;
        public static string hitsoundFolder;

        public static ConfigEntry<string> currentHitsound;
        public static ConfigEntry<HitsoundMode> hitsoundMode;
        public static ConfigEntry<bool> changePitch;
        public static ConfigEntry<bool> hitsoundsNeedASIO;
        public static ConfigEntry<string> assistHitsound;
        public static ConfigEntry<string> missConfig;


        private void Awake()
        {
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {PLUGIN_GUID} is loaded!");

            var harmony = new Harmony(PLUGIN_GUID);
            harmony.PatchAll();

            hitsoundFolder = Path.Join(Application.persistentDataPath, "Hitsounds");

            currentHitsound = Config.Bind(
                "General",                // Section Name
                "Current Hitsound",             // Key Name
                "SILENT",                    // Default Value
                new ConfigDescription(
                    "The name of the hitsound that you are currently using.\nIncludes in game hitsounds for the purposes of making the menu work properly.\nNot meant to be manually changed, it is autoset by the mod.",
                    null,
                    new ConfigurationManagerAttributes { Browsable = false }
                )
            );

            hitsoundMode = Config.Bind(
                "General",
                "Hitsound Mode",
                HitsoundMode.Normal,
                "Sets the way that the sounds should function when there is more than one sound playing.\nNormal will play overlapping sounds, Miss Cutoff will only allow one playing miss sound at once, and Cutoff will only allow one playing sound per type.\nCutoff mode excludes hold loops."
            );

            changePitch = Config.Bind(
                "General",
                "Change Pitch With Speed",
                true,
                "Changes the sound pitch depending on the speed of the game. This includes pausing sounds when the game is paused.\nHold loops will always have this enabled to prevent looping in the pause menu."
            );

            assistHitsound = Config.Bind(
                "General",
                "Assist Mode Custom Hitsound",
                string.Empty,
                "Sets the custom hitsound that should automatically be enabled when assist mode is used. Leave blank to disable this feature.\nAn invalid name will be treated as disabled."
            );

            hitsoundsNeedASIO = Config.Bind(
                "General",
                "Require ASIO For Hitsounds",
                false,
                "Disables hitsounds when ASIO is disabled in the main audio settings. This is designed for situations where you may not always want to use hitsounds depending on if you are using low latency ASIO or not.\nThis setting does not affect assist mode."
            );

            missConfig = Config.Bind(
                "General",
                "Miss Sound Config",
                string.Empty,
                "The name of a config that will ALWAYS be used as your miss sound no matter what hitsound config you have selected.\nThis allows you to also use custom miss sounds with the base-game hitsounds and with silent hitsounds (or with ASIO off when that config is enabled)."
            );

            // Update FMOD parameter when setting changed
            hitsoundsNeedASIO.SettingChanged += (s, a) => HitsoundController.CheckAndUpdateFMODParameter();

            // Init
            HitsoundController.Init();
            StartCoroutine(HitsoundController.WaitForFMODAndCreateChannels());
        }
    }



    [HarmonyPatch(typeof(RhythmController))]
    [HarmonyPatch(nameof(RhythmController.Awake))]
    internal class ControllerAwakePatch
    {
        static void Postfix()
        {
            HitsoundController.OnGameplayStart();
        }
    }



    [HarmonyPatch(typeof(RhythmController))]
    [HarmonyPatch(nameof(RhythmController.Miss), [])]
    internal class ControllerMissPatch
    {
        static void Postfix()
        {
            HitsoundController.PlayMissSound();
        }
    }



    [HarmonyPatch(typeof(RhythmController))]
    [HarmonyPatch(nameof(RhythmController.Hit))]
    internal class ControllerHitPatch
    {
        static void Postfix(ref RhythmController __instance, Height height, bool silent)
        {
            if (silent) HitsoundController.PlayHoldHitSound(height);
            else
            {
                if ((__instance.player.heldTop || __instance.player.assist.heldTop) && height == Height.Low) HitsoundController.PlayHitDuringHoldSound();
                else if ((__instance.player.heldLow || __instance.player.assist.heldLow) && height == Height.Top) HitsoundController.PlayHitDuringHoldSound();
                else HitsoundController.PlayHitSound();
            }
        }
    }



    [HarmonyPatch(typeof(RhythmController))]
    [HarmonyPatch(nameof(RhythmController.Dodge))]
    internal class ControllerDodge
    {
        static void Postfix()
        {
            HitsoundController.PlayDodgeSound();
        }
    }



    [HarmonyPatch(typeof(RhythmController))]
    [HarmonyPatch(nameof(RhythmController.Update))]
    internal class ControllerUpdatePatch
    {
        static void Postfix(ref RhythmController __instance)
        {
            HitsoundController.ManualUpdate(__instance);
        }
    }



    [HarmonyPatch(typeof(RhythmBaseCharacter))]
    [HarmonyPatch(nameof(RhythmBaseCharacter.Hold))]
    internal class CharacterHoldPatch
    {
        static void Postfix(Height height)
        {
            HitsoundController.PlayHoldLoop(height);
        }
    }



    [HarmonyPatch(typeof(RhythmController))]
    [HarmonyPatch(nameof(RhythmController.OnDestroy))]
    internal class ControllerOnDestroyPatch
    {
        static void Postfix()
        {
            HitsoundController.OnGameplayStop();
        }
    }



    [HarmonyPatch(typeof(SystemOptions))]
    [HarmonyPatch(nameof(SystemOptions.SetAndApplyCombatSfxStyle))]
    internal class SetAndApplyCombatSfxStylePatch
    {
        static bool Prefix(ref SystemOptions __instance, ref int style)
        {
            if (style == -1)
            {
                // Set FMOD parameter when starting up
                HitsoundController.CheckAndUpdateFMODParameter();
                return false;
            }

            // Set custom hitsound
            CustomHitSounds.currentHitsound.Value = HitsoundController.hitsoundOptions[style];


            // Set FMOD parameter
            RuntimeManager.StudioSystem.setParameterByName("Combat SFX Type", style <= (int)CombatSfxStyle.Silent ? style : (int)CombatSfxStyle.Silent, false);

            // Reset to -1 since we are handling it
            style = -1;
            __instance.combatSfxStyle = (CombatSfxStyle)style;

            return false;
        }
    }



    [HarmonyPatch(typeof(SystemOptions))]
    [HarmonyPatch(nameof(SystemOptions.GetCombatSfxStyle))]
    internal class GetCombatSfxStylePatch
    {
        static bool Prefix(ref int __result)
        {
            if (CustomHitSounds.currentHitsound.Value != string.Empty)
            {
                HitsoundController.UpdateSFXOptionProvider();

                // Get current hitsound name and return the index
                int currentIndex = Array.IndexOf(HitsoundController.hitsoundOptions, CustomHitSounds.currentHitsound.Value);
                if (currentIndex != -1)
                {
                    __result = currentIndex;
                    return false;
                }
            }

            __result = (int)CombatSfxStyle.Silent;
            return false;
        }
    }



    [HarmonyPatch(typeof(SystemOptions))]
    [HarmonyPatch(nameof(SystemOptions.SetAndApplyUseASIO))]
    internal class SetAndApplyUseASIOPatch
    {
        static void Postfix()
        {
            // Set FMOD parameter when ASIO state is changed
            HitsoundController.CheckAndUpdateFMODParameter();
        }
    }



    [HarmonyPatch(typeof(ArcadeSongDatabase))]
    [HarmonyPatch(nameof(ArcadeSongDatabase.Awake))]
    internal class ArcadeSongDatabaseAwakePatch
    {
        static void Postfix()
        {
            HitsoundController.ReadConfigFiles();
        }
    }



    [HarmonyPatch(typeof(RhythmController))]
    [HarmonyPatch(nameof(RhythmController.Miss), [])]
    internal class ControllerMissTranspiler
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);

            // Remove original PlayOneShot from Miss()
            codeMatcher.MatchForward(
                    false,
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(RuntimeManager), nameof(RuntimeManager.PlayOneShot), [typeof(EventReference), typeof(Vector3)]))
                )
                .ThrowIfInvalid("Could not find a call to RuntimeManager.PlayOneShot.");

            codeMatcher.RemoveInstructionsInRange(codeMatcher.Pos - 5, codeMatcher.Pos);

            return codeMatcher.Instructions();
        }
    }
}