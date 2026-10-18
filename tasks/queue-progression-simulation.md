# Sand simulation checks

Actual FallingSandSimulator, SandCollector, ConveyorBelt and BucketInventory; one grain per level cell for a bounded CPU check. No boosters, no player progress, no SDK.

Level | Simulation ticks | Sand left | Buckets left
--- | --- | --- | ---
18 | 2654 | 0 | 0
20 | 1827 | 0 | 0
26 | 4351 | 0 | 0
28 | 1591 | 0 | 0
48 | 2239 | 0 | 0
58 | 4219 | 0 | 0
68 | 4715 | 0 | 0
80 | 6007 | 0 | 0
104 | 2736 | 0 | 0
112 | 2581 | 0 | 0
200 | 2388 | 0 | 0
300 | 5855 | 0 | 0

PASS: all 12 sampled random levels completed through real sand simulation without paid boosters at one grain per cell and the production emission budget. This check does not measure device FPS.
