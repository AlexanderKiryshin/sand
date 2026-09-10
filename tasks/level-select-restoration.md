PASS — Unity 6000.3.4f1 Play Mode, disposable validation project and in-memory progress.

The Levels button opens CampaignGallery with painting previews. Replay remains limited to completed levels; the collection entry point keeps its existing unlock policy.

Verified with UI raycasts: opening, all 20 preview textures and their aspect ratios, the current page, all 15 campaign pages through level 300, boundary navigation, completed-level selection, fresh-profile restrictions, repeated opening, closing, pause restoration, and opening/closing during the sand intro.

Visual review: tasks/previews/level-select-restored.png (540 × 960).

Unity's editor search index logged its existing startup ArgumentOutOfRangeException; the runtime menu checks all passed. This was an Editor Play Mode check, not a player build.
