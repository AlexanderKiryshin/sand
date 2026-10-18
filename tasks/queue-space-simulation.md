# Sand simulation checks

Actual FallingSandSimulator, SandCollector, ConveyorBelt and BucketInventory; one grain per level cell for a bounded CPU check. No boosters, no player progress, no SDK.

Level | Simulation ticks | Sand left | Buckets left
--- | --- | --- | ---
18 | 2915 | 0 | 0
20 | 2090 | 0 | 0
26 | 5701 | 0 | 0
28 | 2137 | 0 | 0
48 | 4183 | 0 | 0
58 | 7596 | 0 | 0
68 | 5235 | 0 | 0
80 | 6007 | 0 | 0
104 | 2736 | 0 | 0
112 | 4464 | 0 | 0
200 | 2388 | 0 | 0
300 | 6109 | 0 | 0

PASS: all 12 sampled random levels completed through real sand simulation without paid boosters at one grain per cell and the production emission budget. This check does not measure device FPS.
