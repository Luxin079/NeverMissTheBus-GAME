# Forest section — implementation and asset handoff

## Playable scope

After the glasses pickup and bus exit, the existing story now runs in spatial beats: search the village, approach the ranger sign, walk to the middle of the forest, approach the cabin, press E at the door, enter. Walking uses the actual player camera and companions follow. Dialogue locks movement and uses the first-person speaker look behavior. The sequence stops at free exploration of the cabin; the dead ranger, dog, scent interaction and subsequent chapters are not implemented by this change.

Generated objects are runtime-only children of `Last stop - inspector configurable`; original authored scenery is not overwritten. The route is a blockout and must be checked against the authored city before release.

## Inspector

- Forest Sign Point: optional empty at path entrance, feet height.
- Ranger House Point: optional empty at cabin entrance, feet height.
- Ranger House Prefab: optional final asset. Its origin must be the entrance and +Z lead inside. An assigned prefab's door currently requires its own opening interaction; the default E action opens only the generated placeholder door.
- Forest Tree Prefab: existing trees, with simple trunk placeholders as fallback.
- Interior Volumes: optional trigger BoxColliders defining exact interiors. Roof-plus-wall detection is the fallback; volumes are recommended for irregular buildings.
- Interior Ambient Multiplier / Interior Blend Seconds: darkness and smooth transition. Sprite brightness follows this indoor state too.
- Owl Call / Cricket Call / Distant Unsettling Sound: optional original/licensed clips. Synthesized placeholder sounds are used when missing. Night Sound Interval defaults to 18–38 seconds, with no overlapping one-shots and dialogue/pause suppression.

## Assets needed for final art/audio

1. Winter conifer/forest pack: trunks, ground textures, rocks and bushes with sensible colliders and URP materials.
2. Wooden ranger cabin with accessible interior, separate hinged door and window meshes.
3. Weathered directional sign (text remains editable).
4. Licensed owl, cricket, wind, wood creak and subtle eerie drone/resonance clips. Existing-game audio is not copied.
5. For the later ranger encounter: dog with idle/bark/sniff/walk, ranger character and room props.

## QA checklist

- Fresh run and loaded bus checkpoint; crash dialogue, Shift stand, driver inspection, E glasses pickup, exit.
- Walk every path section without jumping; verify no city obstacles or steep tile seams.
- Stop, sprint and crouch; companions stay grounded and do not enter player/cabin walls.
- Confirm front-facing sprites in dialogue and back views only during gameplay walking away.
- Advance every village/forest line; walk objectives must release control and dialogue must stop it.
- Open cabin door with E; enter and leave slowly to check lighting transitions.
- Pause and resume during a night sound; confirm one listener, no runaway playback and no dialogue masking.
- Check both narrow and wide camera FOVs around doorways and the cabin entrance.

`ForestSmokeTest.Run` is a batch Play Mode smoke check for startup, procedural route/cabin/sign construction and character shader errors. It is not a substitute for a visual traversal playtest.
