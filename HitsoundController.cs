using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using Newtonsoft.Json;
using Rhythm;
using UnityEngine;

namespace CustomHitSounds
{
    public static class HitsoundController
    {
        // Constants
        public static readonly MODE soundCreationMode = MODE.CREATESAMPLE | MODE.NONBLOCKING | MODE.ACCURATETIME;
        public static readonly string[] inGameHitsounds = ["PUNCHY", "TAMBOURINE", "DREAM", "SILENT"];

        // ChannelGroups
        public static ChannelGroup sfxGroup;
        public static ChannelGroup missGroup;
        public static ChannelGroup hitGroup;
        public static ChannelGroup holdLoopGroup;
        public static ChannelGroup dodgeGroup;

        // Sounds
        public static List<Sound> hitSounds = [];
        public static List<Sound> holdHitSounds = [];
        public static List<Sound> holdLoops = [];
        public static List<Sound> hitDuringHoldSounds = [];
        public static List<Sound> dodgeSounds = [];
        public static List<Sound> missSounds = [];
        public static Dictionary<Height, Channel> activeHoldLoops = [];

        // Other
        public static Dictionary<string, HitsoundConfig> configs = [];

        public static System.Random rng;

        public static bool usingCustomHitsounds;

        public static string[] hitsoundOptions;


        public static void Init()
        {
            ReadConfigFiles();

            // Set states
            rng = new System.Random();
            usingCustomHitsounds = false;

            UpdateSFXOptionProvider();
        }


        public static void ManualUpdate(RhythmController controller)
        {
            if (!RuntimeManager.IsInitialized || !usingCustomHitsounds) return;

            // Set pitch and paused state

            float timeScale = controller.songTracker.TimeScale;
            if (CustomHitSounds.changePitch.Value)
            {
                sfxGroup.setPitch(timeScale);
                sfxGroup.setPaused(timeScale == 0);
            }
            else
            {
                sfxGroup.setPaused(false);
                sfxGroup.setPitch(1);
            }


            // Set pitch and paused state of holds
            holdLoopGroup.setPitch(timeScale);
            holdLoopGroup.setPaused(timeScale == 0);


            // Stop hold loops when necessary
            if (timeScale != 0 && activeHoldLoops.ContainsKey(Height.Low) && !controller.player.heldLow && !controller.player.assist.heldLow)
            {
                activeHoldLoops[Height.Low].stop();
                activeHoldLoops.Remove(Height.Low);
            }

            if (timeScale != 0 && activeHoldLoops.ContainsKey(Height.Top) && !controller.player.heldTop && !controller.player.assist.heldTop)
            {
                activeHoldLoops[Height.Top].stop();
                activeHoldLoops.Remove(Height.Top);
            }
        }


        public static void OnGameplayStart()
        {
            try
            {
                ReadConfigFiles();

                if (!RuntimeManager.IsInitialized) return;

                // Assist mode custom hitsound
                if (FileStorage.beatmapOptions.useAssistMode && !CustomHitSounds.assistHitsound.Value.IsNullOrWhiteSpace() && configs.TryGetValue(CustomHitSounds.assistHitsound.Value.Trim(), out HitsoundConfig currentConfig))
                {
                    LoadAndCreateHitsoundList(currentConfig.dodge, ref dodgeSounds);
                    LoadAndCreateHitsoundList(currentConfig.hit, ref hitSounds);
                    LoadAndCreateHitsoundList(currentConfig.hit_during_hold, ref hitDuringHoldSounds);
                    LoadAndCreateHitsoundList(currentConfig.hit_hold, ref holdHitSounds);
                    LoadAndCreateHitsoundList(currentConfig.hold_loop, ref holdLoops, true);
                    LoadAndCreateHitsoundList(currentConfig.miss, ref missSounds);

                    usingCustomHitsounds = true;
                    sfxGroup.setPaused(false);
                }

                // Normal custom hitsound
                else if (!inGameHitsounds.Contains(CustomHitSounds.currentHitsound.Value) && configs.TryGetValue(CustomHitSounds.currentHitsound.Value, out currentConfig) && (!CustomHitSounds.hitsoundsNeedASIO.Value || FileStorage.options.useASIO))
                {
                    LoadAndCreateHitsoundList(currentConfig.dodge, ref dodgeSounds);
                    LoadAndCreateHitsoundList(currentConfig.hit, ref hitSounds);
                    LoadAndCreateHitsoundList(currentConfig.hit_during_hold, ref hitDuringHoldSounds);
                    LoadAndCreateHitsoundList(currentConfig.hit_hold, ref holdHitSounds);
                    LoadAndCreateHitsoundList(currentConfig.hold_loop, ref holdLoops, true);
                    LoadAndCreateHitsoundList(currentConfig.miss, ref missSounds);

                    usingCustomHitsounds = true;
                    sfxGroup.setPaused(false);

                    // Set FMOD parameter just in case
                    RuntimeManager.StudioSystem.setParameterByName("Combat SFX Type", (int)CombatSfxStyle.Silent, false);
                }
                else
                {
                    usingCustomHitsounds = false;
                    sfxGroup.setPaused(true);
                }
            }
            catch (Exception ex)
            {
                CustomHitSounds.Logger.LogWarning($"Failed to initialize custom hitsounds! {ex.Message}");
            }
        }


        public static void OnGameplayStop()
        {
            sfxGroup.stop();
            hitGroup.stop();
            holdLoopGroup.stop();
            dodgeGroup.stop();
            missGroup.stop();
            sfxGroup.setPaused(true);
        }


        public static void ReadConfigFiles()
        {
            if (!Directory.Exists(CustomHitSounds.hitsoundFolder)) Directory.CreateDirectory(CustomHitSounds.hitsoundFolder);

            configs.Clear();

            foreach (string configFilePath in Directory.EnumerateFiles(CustomHitSounds.hitsoundFolder, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    // Deserialize config
                    HitsoundConfig config = JsonConvert.DeserializeObject<HitsoundConfig>(File.ReadAllText(configFilePath));
                    if (config == null || config.name.IsNullOrWhiteSpace())
                    {
                        CustomHitSounds.Logger.LogWarning($"Hitsound config file \"{Path.GetFileName(configFilePath)}\" is invalid.");
                        continue;
                    }

                    config.name = config.name.Trim();

                    // Make sure there are no duplicates
                    if (configs.ContainsKey(config.name) || inGameHitsounds.Contains(config.name))
                    {
                        string originalName = config.name;
                        int i = 1;
                        while (configs.ContainsKey(config.name))
                        {
                            i++;
                            config.name = originalName + $"({i})";
                        }
                        config.name = originalName;
                    }

                    // Check for null arrays
                    config.dodge ??= [];
                    config.hit ??= [];
                    config.hit_during_hold ??= [];
                    config.hit_hold ??= [];
                    config.hold_loop ??= [];
                    config.miss ??= [];

                    // Add to config files
                    configs.Add(config.name, config);
                }
                catch (Exception ex)
                {
                    CustomHitSounds.Logger.LogWarning($"Failed to parse hitsound config file \"{Path.GetFileName(configFilePath)}\"! {ex.Message}");
                }
            }

            UpdateSFXOptionProvider();
        }


        public static void UpdateSFXOptionProvider()
        {
            if (OptionsProvider.OptionProviders.TryGetValue(OptionsProvider.Option.CombatSFXStyle, out OptionsProvider.OptionProvider provider))
            {
                OptionsProvider.TextOptionProvider sfxProvider = provider as OptionsProvider.TextOptionProvider;
                sfxProvider.isOptionsLocalized = false;

                // Update option provider to include the new config options
                string[] customHitsounds = [.. configs.Keys];
                if (customHitsounds.Count() == 0) customHitsounds = ["CUSTOM"];

                sfxProvider._baseOptions = hitsoundOptions = [.. inGameHitsounds, .. customHitsounds];
            }
            else
            {
                hitsoundOptions = inGameHitsounds;
            }
        }



        public static void CheckAndUpdateFMODParameter()
        {
            if (CustomHitSounds.hitsoundsNeedASIO.Value && !FileStorage.options.useASIO) RuntimeManager.StudioSystem.setParameterByName("Combat SFX Type", (int)CombatSfxStyle.Silent, false);
            else RuntimeManager.StudioSystem.setParameterByName("Combat SFX Type", FileStorage.options.GetCombatSfxStyle(), false);
        }



        public static void PlayHitSound()
        {
            if (usingCustomHitsounds)
            {
                if (CustomHitSounds.hitsoundMode.Value == HitsoundMode.Cutoff) hitGroup.stop();
                PlaySoundInternal(ref hitSounds, hitGroup, out _);
            }
        }

        public static void PlayHoldHitSound()
        {
            if (usingCustomHitsounds)
            {
                if (CustomHitSounds.hitsoundMode.Value == HitsoundMode.Cutoff) hitGroup.stop();
                PlaySoundInternal(ref holdHitSounds, hitGroup, out _);
            }
        }

        public static void PlayHitDuringHoldSound()
        {
            if (usingCustomHitsounds)
            {
                if (CustomHitSounds.hitsoundMode.Value == HitsoundMode.Cutoff) hitGroup.stop();
                PlaySoundInternal(ref hitDuringHoldSounds, hitGroup, out _);
            }
        }

        public static void PlayDodgeSound()
        {
            if (usingCustomHitsounds)
            {
                if (CustomHitSounds.hitsoundMode.Value == HitsoundMode.Cutoff) dodgeGroup.stop();
                PlaySoundInternal(ref dodgeSounds, dodgeGroup, out _);
            }
        }

        public static void PlayMissSound()
        {
            if (usingCustomHitsounds)
            {
                if (CustomHitSounds.hitsoundMode.Value != HitsoundMode.Normal) missGroup.stop();
                PlaySoundInternal(ref missSounds, missGroup, out _);
            }
        }

        public static void PlayHoldLoop(Height height)
        {
            if (usingCustomHitsounds)
            {
                if (!activeHoldLoops.ContainsKey(height))
                {
                    PlaySoundInternal(ref holdLoops, holdLoopGroup, out Channel holdLoop);
                    activeHoldLoops.Add(height, holdLoop);
                }
            }
        }



        private static void PlaySoundInternal(ref List<Sound> sounds, ChannelGroup targetGroup, out Channel channel)
        {
            channel = default;

            if (sounds.Count == 0) return;

            // Get sound
            Sound sound;

            if (sounds.Count == 1) sound = sounds.First();
            else sound = sounds[rng.Next(0, sounds.Count)];

            // Check if it is valid and play it
            if (sound.hasHandle())
            {
                RuntimeManager.CoreSystem.playSound(sound, targetGroup, false, out channel);
            }
        }


        private static void LoadAndCreateHitsoundList(List<string> files, ref List<Sound> createdSounds, bool loop = false)
        {
            createdSounds.Clear();

            foreach (string file in files)
            {
                if (file.IsNullOrWhiteSpace() || file.Equals("none", StringComparison.OrdinalIgnoreCase) || file.Equals("silent", StringComparison.OrdinalIgnoreCase))
                {
                    // Add default entry for silent
                    createdSounds.Add(default);
                }
                else
                {

                    string fullPath = file;
                    if (!Path.IsPathFullyQualified(file)) fullPath = Path.Join(CustomHitSounds.hitsoundFolder, file);

                    if (!File.Exists(fullPath))
                    {
                        // Add default if not exists
                        createdSounds.Add(default);
                    }
                    else
                    {
                        // If sound exists, don't create a new one and just add the same reference
                        Sound duplicateSound = createdSounds.FirstOrDefault(sound => sound.getName(out string name, 128) == RESULT.OK && name == Path.GetFileName(file));
                        if (duplicateSound.hasHandle())
                        {
                            createdSounds.Add(duplicateSound);

                        }
                        else
                        {
                            // Add to list
                            RuntimeManager.CoreSystem.createSound(fullPath, soundCreationMode | (loop ? MODE.LOOP_NORMAL : MODE.LOOP_OFF), out Sound sound);

                            // Set looping
                            if (loop)
                            {
                                sound.getLength(out uint numSamples, TIMEUNIT.PCM);
                                sound.setLoopPoints(0, TIMEUNIT.PCM, numSamples - 1, TIMEUNIT.PCM);
                                sound.setLoopCount(-1);
                            }
                            else
                            {
                                sound.setLoopCount(0);
                            }

                            createdSounds.Add(sound);
                        }
                    }
                }
            }
        }


        public static IEnumerator WaitForFMODAndCreateChannels()
        {
            while (!RuntimeManager.IsInitialized) yield return null;

            // Get bus
            Bus sfxBus = RuntimeManager.GetBus("bus:/sfx");
            sfxBus.lockChannelGroup();

            // Assign group to SFX bus
            ChannelGroup mainSFXGroup;
            while (sfxBus.getChannelGroup(out mainSFXGroup) != RESULT.OK) yield return null;

            RuntimeManager.StudioSystem.flushCommands();

            // Create channel groups
            RuntimeManager.CoreSystem.createChannelGroup("Custom Hit Sounds Parent", out sfxGroup);
            mainSFXGroup.addGroup(sfxGroup);

            RuntimeManager.CoreSystem.createChannelGroup("Hit Sounds", out hitGroup);
            sfxGroup.addGroup(hitGroup);
            RuntimeManager.CoreSystem.createChannelGroup("Miss Sounds", out missGroup);
            sfxGroup.addGroup(missGroup);
            RuntimeManager.CoreSystem.createChannelGroup("Hold Loops", out holdLoopGroup);
            sfxGroup.addGroup(holdLoopGroup);
            RuntimeManager.CoreSystem.createChannelGroup("Dodge Sounds", out dodgeGroup);
            sfxGroup.addGroup(dodgeGroup);

            sfxGroup.setPaused(true);

            CustomHitSounds.Logger.LogInfo("Created channel groups!");
        }
    }
}