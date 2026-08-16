# Custom desktop pets

Hello Companion discovers character folders in:

```text
%LOCALAPPDATA%\HelloCompanion\Pets
```

Use **Open custom pets folder** in the app to open that location. Each character lives in its own folder and contains a `pet.json` manifest plus one or more transparent PNG frames.

## Example

```text
Pets
└── MyPet
    ├── pet.json
    ├── walk-01.png
    ├── walk-02.png
    └── walk-03.png
```

```json
{
  "id": "my-pet",
  "displayName": "My Pet",
  "frames": [
    "walk-01.png",
    "walk-02.png",
    "walk-03.png"
  ],
  "animations": {
    "roam": ["walk-01.png", "walk-02.png", "walk-03.png"],
    "idle": ["idle-01.png", "idle-02.png"],
    "sleep": ["sleep-01.png", "sleep-02.png"],
    "reminder": ["look-up.png", "talk.png"]
  },
  "animationFrameDurations": {
    "roam": 120,
    "idle": 400,
    "sleep": 700,
    "reminder": 250
  },
  "ambientBehaviors": [
    { "animation": "idle", "weight": 3, "minimumSeconds": 2, "maximumSeconds": 5 },
    { "animation": "sleep", "weight": 1, "minimumSeconds": 6, "maximumSeconds": 10 }
  ],
  "reminderBehavior": [
    { "action": "stop" },
    { "action": "play", "animation": "reminder" },
    { "action": "show-message" },
    { "action": "wait", "seconds": 5 },
    { "action": "hide-message" },
    { "action": "resume" }
  ],
  "clickBehavior": [
    { "action": "stop" },
    { "action": "play", "animation": "idle" },
    { "action": "wait", "seconds": 2 },
    { "action": "resume" }
  ],
  "frameDurationMilliseconds": 140,
  "spriteWidth": 112,
  "spriteHeight": 96,
  "speedPixelsPerSecond": 75
}
```

After copying the files, select **Reload characters**. Hello Companion cycles through all loaded characters when more than one pet is visible. A custom character with the same `id` as a built-in character overrides it.

## Asset guidance

- Use PNG files with a genuine transparent alpha channel.
- Keep every frame on the same canvas size and align the character's feet consistently.
- Author frames facing right; the engine mirrors them when moving left.
- Keep the complete character inside the canvas with a small transparent margin.
- Prefer compact source images. The engine scales each frame to `spriteWidth` × `spriteHeight`.
- The top-level `frameDurationMilliseconds` is the fallback timing. `animationFrameDurations` can give each named animation its own 50–2000 ms timing.
- List transition animations in `nonLoopingAnimations` when they should play once and hold their final frame until the behavior ends.
- Set `pixelArt` to `true` for small pixel sprites that must be enlarged with crisp nearest-neighbor scaling.
- `ambientBehaviors` is per-pet: it lists the animations that character may choose while roaming, their relative weights, and how long each state lasts. This lets one character sleep while another character uses entirely different state names.
- Dimensions are clamped to 32–512 px, speed to 10–500 px/s, and pet count to five.

Manifest frame paths cannot escape their character folder. Invalid characters are skipped without preventing other pets from loading.

## Per-pet behavior

Every visible character is an independent pet actor. A reminder is offered to one available actor, which runs its own `reminderBehavior`; other pets continue their current activities. Supported declarative reminder actions are `stop`, `play`, `show-message`, `wait`, `hide-message`, and `resume`. Unknown actions and missing animation names safely fall back without granting custom packages permission to execute code.

A character becomes directly clickable only when its manifest declares a non-empty `clickBehavior`. Pets without it remain fully click-through. Click behaviors support the safe `stop`, `play`, `wait`, and `resume` actions; a busy pet ignores additional clicks until its current action finishes.

The engine currently supports independently timed frame animations, per-character ambient states, horizontal facing, taskbar roaming, full-screen bouncing, multiple pets, anchored speech bubbles, and click-through overlays. Character-specific mechanics such as jumping, swinging, playing, and interacting with windows will be added as trusted behavior primitives later.
