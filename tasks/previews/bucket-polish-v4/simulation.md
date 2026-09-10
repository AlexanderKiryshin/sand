# Sand simulation checks

Actual FallingSandSimulator, SandCollector, ConveyorBelt and BucketInventory; one grain per level cell for a bounded CPU check. No boosters, no player progress, no SDK.

Level | Simulation ticks | Sand left | Buckets left
--- | --- | --- | ---
23 | 2973 | 0 | 0
44 | 5887 | 0 | 0
100 | 5552 | 0 | 0
1 | 904 | 0 | 0
3 | 922 | 0 | 0
5 | 1545 | 0 | 0
7 | 1809 | 0 | 0
9 | 918 | 0 | 0
11 | 933 | 0 | 0
13 | 2257 | 0 | 0
15 | 1122 | 0 | 0
17 | 2704 | 0 | 0

PASS: all 12 sampled levels completed through real sand simulation without paid boosters at the restored production density of one grain per cell. This check does not measure device FPS.
