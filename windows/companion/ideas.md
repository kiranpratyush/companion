# Hello Companion — Future Ideas

This file is a product-direction backlog, not a promise that every behavior should be enabled by default.

## Character platform

- Import user-created characters as folders containing `pet.json` and transparent PNG frames.
- Support character-specific action packs: walk, idle, jump, climb, sit, sleep, dance, and play.
- Add richer movement primitives such as swinging, wall climbing, flying, teleporting, and interacting with screen edges.
- Allow multiple characters to notice and play with one another.
- Add a character editor and sprite-sheet importer instead of requiring hand-written metadata.
- Keep character art and behavior definitions separate so licensed or user-owned characters can be added without changing the engine. Famous characters such as Spider-Man require appropriate rights and user-provided/licensed assets.

## Playful and mischievous behavior

- Characters can chase the cursor, hide behind windows, nap on the taskbar, or leave temporary visual props.
- Application launching must be explicit opt-in, limited to a user-maintained allowlist, rate-limited, and easy to disable. Characters must never launch arbitrary executables or interrupt critical/full-screen work by default.
- Add a global “focus mode” and instant hide/pause shortcut.

## Companion intelligence

- Optional LLM conversations with a clearly visible privacy boundary and local-only mode where possible.
- Break, hydration, posture, and focus reminders with quiet hours and snooze.
- Share a random concept, short learning prompt, or encouraging message at user-selected times.
- Ask for the current status of planned tasks and offer lightweight follow-ups.
- Integrate with task and calendar providers only after explicit connection and per-source permissions.
- Keep autonomous actions separate from conversational suggestions; consequential actions always require confirmation.

## Platform evolution

- Per-monitor and multi-monitor roaming.
- Character interaction zones around windows and taskbar icons.
- Low-power animation modes for battery saver and remote desktop.
- Signed character packages with versioned manifests and capability declarations.
- Accessibility controls for motion reduction, size, contrast, and notification frequency.
