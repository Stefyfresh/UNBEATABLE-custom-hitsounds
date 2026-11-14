# UNBEATABLE Custom Hitsounds

A mod to add custom hit, dodge, and miss sounds to the hit rhythm game UNBEATABLE!

<br>

Disclaimer: This is not affiliated with or endorsed by D-CELL GAMES.

## Features

- Allows independent setting of each type of hitsound
- Can fully emulate the in-game hitsounds if the audio files are provided
- Does not have offset issues like the in-game hitsounds do
- Allows multiple different hitsound configurations to be saved and dynamically switched out in-game

## Configuration

When you install the mod, it will automatically create a Hitsounds folder in the UNBEATABLE data directory (the same folder that CustomSongs is in). In that folder is where the data and sounds for the mod live. You create custom hitsound profiles by making a JSON file with a specific syntax. An example is shown below:

```json
{
    "name": "Example Hitsound Name",
    "hit_default": [
        "Example Hitsound/Hit 1.wav",
        "Example Hitsound/Hit 2.wav"
    ],
    "hit_hold": [
        "Example Hitsound/Hit 1.wav"
    ],
    "hit_during_hold": [
        "Example Hitsound/Hit 2.wav"
    ],
    "hold_loop": [],
    "dodge": [
        "silent"
    ],
    "miss": [
        "Miss.wav"
    ]
}
```

This creates a hitsound that plays one of two hit sounds when you hit regular notes, plays one of them for hitting holds, plays the other when you are hitting while holding a hold, has no loop or dodge sound, and plays miss sounds. You can omit entries by not including them or naming them `"none"` or `"silent"`. The path to the audio files can be relative OR absolute, by default it will be relative to the Hitsounds folder if you just put in an audio file name. You can customize this as you please and put as many audio files as you want! (including the same one multiple times to increase the chance of it playing!) 

Since the paths of the JSON files are relative, to set a custom miss sound just make sure that all your JSON files point to the same miss.wav and put in a custom miss sound!

To change your hitsound, simply go to the audio menu in-game and change it with the native option list! There are options in mod menu that you can check out that change how the mod works! Also the config files are reloaded every time you restart a chart, and the mod is smart if you remove or replace audio files / config files.

## Hitsound Types

### hit_default

This is the hitsound that is used for most notes in game.

### hit_hold

This hitsound is used for specifically hitting the start of a hold note. It is separate as in-game the Punchy hitsound uses a modified normal hit to mesh with the looping hold sound.

### hit_during_hold

This hitsound is used when you are holding a note and hitting on the opposite lane from it.

### hold_loop

This is the looping sound that plays when you are holding a hold note. Up to two sounds can be active at once, one for any holds on the upper lanes and one for hold on the lower lanes.

### dodge

This is the sound that is used when dodging spikes.

### miss

This is the sound that is used when you miss a note.

<br>

Because you can set the same path for multiple different hitsound configuration files, this allows you to reuse sounds between different configurations (like setting a custom miss sound for all the different hitsounds!)

## Mod Installation Instructions

For installation instructions, refer to my mod installation guide here:

https://gist.github.com/Stefyfresh/1140bf18c5f3895bd807258c16483887

---

###### All code for Custom Hitsounds was written by me, by hand.
