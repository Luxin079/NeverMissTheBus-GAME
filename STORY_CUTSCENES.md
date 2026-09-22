# First-person story

The build starts in `Assets/Scenes/MainMenu.unity`. START GAME loads `SampleScene` and resumes the JSON checkpoint if present. The existing Player, CharacterController, MouseLook and camera are used during playable sections.

## Opening

1. A slow black fade reveals a front-of-school establishing shot. After a short quiet hold, Ethan, Lucas and Noah leave the school door one by one, line up and walk under the school entrance. The camera follows them smoothly and shifts to each speaker.
2. At "Hey, the bus!", the camera turns to show an earlier bus pause at the real bus stop and then drive away slowly, before returning to the boys. A fade moves them to the bus stop.
3. The selected School_Bus_damage is followed from a diagonal, elevated tracking camera on approach. It stops at Bus Stop Parking Anchor (or the fallback parking shift). At the instant it stops, the view switches to the existing Player camera. The chauffeur says “Get on, boys”; Lucas and Noah board one at a time while the player watches in first person.
4. Walk into the bus yourself using WASD/mouse. Crossing the middle of the aisle triggers a fade to the three seats. No seat-selection key is required.
   The distant story camera stays disabled from bus arrival through this walk; First Person Eye Height adjusts the existing Player camera for the prefab's scale.
5. The ride dialogue and crash stay inside the same playable bus. The seated first-person shot looks forward from the rear row, glancing to the driver or friends when they speak. After the final sleep line there is an adjustable quiet pause, a full fade to black, and a second adjustable black hold before impact. The boys stand up; the Player can move in first person. Crash reactions play, but dialogue about the missing driver waits until the Player reaches the empty driver's seat. The glasses remain there for the Player to pick up with E before leaving the bus in first person.

Dialogue shows only Name: text. After a sentence finishes revealing, E, Space or click advances one line and plays an original retro blip. There are no portraits, choices or Enter/Escape/F8 story shortcuts.

## Authoring

Select Story Cutscenes - Placeholder Director. Assign the three character art prefabs in Actors; Visual Scale controls each size. The Editor button **Add editable positions for all boys in each scene** fills each Shot's Actor Positions. Camera Offset controls dialogue angles. School Establishing Camera Offset, Opening Fade In Seconds and Opening School Hold Seconds tune the introduction; School Wide Camera Offset controls the later moving shot. School Exit Stagger Seconds spaces their doorway exits. Waiting Shelter Anchor, Bus Stop Parking Anchor and Missed Bus Departure Target allow exact world positions; Parking Shift Along Bus, Waiting Offset, Last Bus Approach Distance, Missed Bus Departure Speed and Missed Bus Shot Field Of View are fallback adjustments. Bus Stop Camera Offset sets the elevated tracking shot. Seated Character Locals, Seated Actor Scale and Seated Camera Local control the bus shot. Boarding Layer defaults to Unity layer 1. The fade-out and fade-in lengths for the bus stop, seating and crash, plus Sleep Before Fade Seconds and Sleep To Crash Black Hold Seconds, are separate Inspector fields. Driver Seat Inspection Anchor, Inspection Local and Inspection Radius set where the Player must approach before the missing-driver dialogue starts; a yellow gizmo shows the trigger.

The opening camera dynamically frames and smoothly follows the three boys, based on the earlier cutscene camera system. During each spoken line it follows and locks its aim on that speaker (including the bus driver). Camera Follow Smooth Time, Camera Rotation Speed, and each Shot's Camera Offset are editable in the Inspector.

Glasses Pickup Prefab is ONE complete object: the glasses artwork, a PickUp component on its root, and a child for the Press E text. Set PickUp > Prompt Object to that child. The director does not clone the old scene UI. If no prefab is assigned, a temporary glasses sprite and simple text prompt are created. Other pickups use the same PickUp setup. Adjust Prompt Offset, Prompt Scale and Pickup Range on the prefab.

Runtime objects include the companions, boarding ramp and a small forest corridor. The bus is no longer teleported to an empty area at the crash, and the entire city is not cloned (that caused the observed stall). After collecting the glasses, the player walks out of the bus while Lucas and Noah follow. A complete extended city layout, dog tracking, puzzles and animated chase gameplay are still prototype work. The runtime terrain surface uses the assigned Terrain Surface Layer on a temporary copy of TerrainData, leaving the source asset untouched.

Audio clips for school, street, bus interior, horror ambience, bus engine, bus door, departure, footsteps, crash and dialogue are optional Inspector assignments. Procedural placeholder loops and effects play if no clip is assigned. The last stop adds fog, darker ambient light and URP exposure adjustment.

Escape opens a simple pause menu with Resume, Settings (mouse sensitivity) and Save & Exit. Progress and sensitivity are JSON files in `Application.persistentDataPath`. START GAME reloads the last checkpoint. Press `0` three times within two seconds in the game to erase the progress save and restart from school; settings are retained.

Verification: scripts compile against Unity's project assembly references. Camera placement, doorway path, seats and pickups still need a visual Unity Play Mode pass.
