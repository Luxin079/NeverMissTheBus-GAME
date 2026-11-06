# Winter boy poses

Generated with the built-in image generation tool using the user's three-boy image as the identity and costume reference. The existing front sprites are preserved.

Asset: `WinterBoysPoses.png`. Columns: Mass, Aremi, Dimash. Top: standing back. Bottom: seated side profile with face toward viewer. Transparent PNG, point filtering, no compression. Runtime alpha-trims each cell; row boundary is 544 pixels from the top of the 1536x1024 sheet. Seated pivot approximates the hip/seat contact rather than the boots.

Generation prompt:
Create one production game sprite sheet using attached image as identity/style reference. Transparent background. Exactly SIX isolated full body sprites arranged in a precise 3-column 2-row grid with equal cells and generous transparent margins, no text. Columns: red curly-haired Mass, blue Aremi, purple glasses Dimash, same clothing/hats/boots/backpacks and pixelated low-resolution phototexture style as reference. Top row: standing BACK views of each boy facing directly away, backpacks visible, no faces. Bottom row: each boy seated without chair, body in side profile facing image left, thighs horizontal and knees bent 90 degrees, boots below knees, hands on lap; heads turned toward viewer showing recognizable face, natural neck not twisted. Entire bodies inside each cell, consistent character scale. No chairs, scenery, shadows outside sprites, glow, labels or extra characters. Preserve exact costume colors and child proportions.

Cleanup prompt:
Edit this exact six-character sprite sheet. Remove ALL background and colored glow completely to genuine alpha transparency, including gaps between all sprites. Keep ONLY the six boy silhouettes with clean hard pixel edges. Preserve all six poses, faces, colors, positions, pixel detail and image dimensions exactly. No other changes, no ground shadows. Background must be fully transparent not black or colored.

Validation: PNG background alpha sampled at the outer corner and between characters is zero. C# build passes. Unity Play Mode seat alignment, pose transitions, and safe standing location still require visual verification. These are billboard portraits with baked head poses, not separately rigged head bones.
