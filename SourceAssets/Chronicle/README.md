# The finished Chronicle book

`Gilded_Fate_Chronicle.blend` is the actual Blender 4.0.2 project with packed textures, editable geometry, UVs and the page-turn bone rig. Runtime exports are at `Assets/Resources/Chronicle/BookModel/Chronicle.fbx`; its 43 image maps and the licensed Caveat handwriting font are alongside it. Nine separate animation exports are in `AnimationExports`.

The book opens and closes. Individual leaves turn while the covers remain fully open. The single reusable turning sheet is hidden before resetting to the source stack. The softer parchment curl and increased edge aging are included. Writing is never baked into page textures.

The Blender acceptance reports under `QA` include closed, fully open and mid-turn poses, both page-turn directions, opening and closing, normalized skin weights, UV orientation, and source/export reopening. The Unity Editor import and runtime image are not verified locally. The game importer generates the book prefab, seven materials, nine-state controller and two page anchors when Unity first imports the repository. It connects to Claude's existing book interface and story engine.

To regenerate exports on a PC, run Blender with `--background --python Scripts/build_chronicle.py -- --no-render`. Textures resolve to the runtime folder in this repository; alternatively set `CHR_TEXTURES` to a texture root and `CHR_OUTPUT` to a separate output directory. Rendering, texture generation and verification scripts are included. Blender 4.0+ and NumPy 1.26 were used for the tested export.

The generated prefab and material files live inside `Assets/Resources/Chronicle`; rebuild through **Gilded Fate > Chronicle > Rebuild Finished Book Assets**. The `.blend` remains the authored source.

## Git checkout binary restoration

The download ZIP contains the complete FBX and Blender project. A Git checkout stores these two large files as exact checksum-protected binary parts. Run `python SourceAssets/Chronicle/Scripts/restore_book_binaries.py` from the checkout to restore both. Unity also restores the FBX automatically before creating the prefab; this Editor helper has not been run in Unity here. No model geometry, material or animation bytes change during restoration.
