# Acceptance result

Built, rendered and reopened in Blender 4.0.2. Final native numerical checks pass; main FBX and all nine per-clip FBXs reopen in Blender.

Verified: same-model Closed/Open/MidTurn renders; zero cover drift during page turns; bound sheet centerline; leaf height clearance; maximum four normalized bone weights per leaf vertex; forward-readable front/back test words; packed native textures; nine actions; complete FBX binary parsing and import.

Revision verified: lower, rounder page curl with 9 mm free-corner radii; additional parchment edge wear with zero base-color change inside the writing-safe rectangle; exact opening/closing endpoints; sampled reverse-path agreement; 16-second page-turn review and 12-second opening/closing review rendered from the actual baked Blender actions. Native and exported clip durations are unchanged. Video durations, frame counts and framing evidence are recorded in the review JSON reports.

The closed exterior includes additional spine/ornament allowance: 40.0 x 33.7 x 8.9 cm, wider and thicker than the nominal 40 x 30 x 8 cm target. Cover boards themselves are 40 x 30.1 cm. Open width is 63.1 cm.

Untested: Unity Editor/pipeline material conversion, runtime page content and reusable-sheet reset, Animator transitions, exhaustive triangle collision detection, actual target-device performance and a separate optimized LOD.

Test lettering appears only in QA renders. The delivered pages remain blank. The Blender verification run did not modify a Unity project. Targeted integration changes are now included as a separate repository update package; Unity Editor validation remains pending.
