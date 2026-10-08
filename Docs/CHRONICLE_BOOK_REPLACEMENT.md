# Replacing the temporary Chronicle book with the finished Blender model

The story, page text, red corrections and illustrations never reference a book object. They talk to one interface,
`IChronicleBookRig`, and the game picks a rig at runtime: **if a finished model exists it is used, otherwise the temporary
book is.** Deleting the model brings the temporary book straight back, so it stays available for debugging.

## What the finished model must provide

1. **A prefab** at `Assets/Resources/Chronicle/ChronicleBook.prefab` containing the whole book and an **Animator** (on the root or any child).
2. **Animator states** for these actions. The names are yours to choose; you map them in step 4.

   | Action | Meaning | Must be true during it |
   | --- | --- | --- |
   | Open | closed to fully open | ends in the open reading pose |
   | Close | open to closed | ends closed |
   | TurnForward | one sheet turns right to left | **covers stay completely open**; only the sheet moves |
   | TurnBack | one sheet turns left to right | same |
   | Idle | open, at rest | optional |
   | ReactCorrection | small glow / tremor when a sentence is struck in red | optional |
   | ReactMajor | large magical reaction | optional |

3. **Two empty transforms** that mark the readable area of each page. The game lays the handwriting and illustration over these.
   Name them `PageAnchor_Left` and `PageAnchor_Right` (or change the names in the config). Orientation, with the book open and flat:
   **+X toward the page's right edge, +Y out of the paper (up), +Z toward the top of the page.** Centre each on the middle of the text area.
   Set `Page Size` in the config to that area's width and height in the model's units.
4. **A flat reading pose.** The reading camera looks straight down at the open book so the overlay lines up exactly. Pages should lie flat
   while the book is at rest. (Curved pages while turning are fine; the overlay fades out mid-turn.)

## Steps

1. Export from Blender (FBX or glTF) into `Assets/`, create the prefab, add the Animator Controller with the states above.
2. Add the two page anchors as children of the prefab.
3. Move the prefab to `Assets/Resources/Chronicle/ChronicleBook.prefab`.
4. Assets > Create > Gilded Fate > Chronicle Book Rig Config. Save it as `Assets/Resources/Chronicle/ChronicleBookRigConfig.asset`, then fill in:
   - the **Animator state names** for each action (this is the clip-name mapping; no code changes needed if names differ),
   - optionally assign **clips** so their lengths set the durations (otherwise the seconds fields are used),
   - `Page Size`, and the **Reading camera** distance and target so the open book fills the view,
   - optionally `Spread Parameter`, an Animator float set to the current spread number, for page-block blend trees.
5. Enter Play Mode and use **Gilded Fate > Chronicle > Play On Book (Play Mode)**. Check: the book opens, pages turn with the covers still, text sits on the pages.
6. Fix any warning in the Console. A state name the Animator does not contain is reported once as `The book's Animator has no state named '...'`.

## What stays the same

Timing is owned by `ChronicleBookAnimator` (pure logic, unit-tested), not by the model. The story waits exactly as long as the configured
duration for each action, so the model's clip lengths and the story stay in step. Overlay visibility, page-turn fades, reactions and the
"covers never move during a page turn" rule behave identically for either rig.

## Known limits of the first version

- The overlay is a flat rectangle over a straight-down view. If the finished book needs an angled reading camera or curved reading pages,
  page content would have to be rendered onto the page surface instead; that is a larger step and is not built.
- Only one flipping sheet is shown at a time.
