# Sand simulation checks

Actual FallingSandSimulator, SandCollector, ConveyorBelt and BucketInventory; one grain per level cell for a bounded CPU check. No boosters, no player progress, no SDK.

Level | Simulation ticks | Sand left | Buckets left
--- | --- | --- | ---
1 | 1094 | 0 | 0
2 | 931 | 0 | 0
3 | 1325 | 0 | 0
4 | 1026 | 0 | 0
5 | 769 | 0 | 0
13 | 1589 | 0 | 0
300 | 5985 | 0 | 0
301 | 3006 | 0 | 0
400 | 3328 | 0 | 0
600 | 4436 | 0 | 0
800 | 3988 | 0 | 0
1000 | 2296 | 0 | 0
1001 | 2228 | 0 | 0
1038 | 3228 | 0 | 0
1050 | 5211 | 0 | 0
1058 | 2888 | 0 | 0
1080 | 1511 | 0 | 0
1100 | 2798 | 0 | 0
1200 | 2756 | 0 | 0

PASS: all 19 sampled random levels completed through real sand simulation without paid boosters at one grain per cell and the production emission budget. This check does not measure device FPS.
