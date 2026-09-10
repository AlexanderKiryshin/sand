PASS
- Existing scene starts with 300 campaign levels.
- Checks use an isolated in-memory save profile.
- Atlas sprite loads at 256x256: Magnet
- Atlas sprite loads at 256x256: Fill
- Atlas sprite loads at 256x256: Shuffle
- Atlas sprite loads at 256x256: Lock
- Legacy booster accessors share the same atlas sprites.
- Shop opens after removing old booster images.
- Shop closes.
- Magnet button activates its target mode.
- Gallery creates 20 previews from compressed level data.
- Changing gallery pages releases old textures.
- Next gallery page has 20 generated previews.
- Settings modal opens.
- Save API round trip succeeds in the preview profile.
- Level restart retains the selected level and inventory.
- No runtime errors during editor smoke checks.

Editor Search diagnostics (outside player runtime):
ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection.
Parameter name: index
System.Collections.Generic.List`1[T].get_Item (System.Int32 index) (at <1eb9db207454431c84a47bcd81e79c37>:0)
UnityEditor.Search.SearchDatabase+<EnumerateAll>d__80.MoveNext () (at <9b1d50235611470baf7c1b90bc27ac95>:0)
System.Linq.Enumerable.TryGetFirst[TSource] (System.Collections.Generic.IEnumerable`1[T] source, System.Boolean& found) (at <6faa6124236442759eb396cfbfdad508>:0)
System.Linq.Enumerable.First[TSource] (System.Collections.Generic.IEnumerable`1[T] source) (at <6faa6124236442759eb396cfbfdad508>:0)
UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase () (at <9b1d50235611470baf7c1b90bc27ac95>:0)
UnityEditor.Search.SearchInit.IndexationOnStartup () (at <9b1d50235611470baf7c1b90bc27ac95>:0)
UnityEditor.EditorApplication.Internal_CallDelayFunctions () (at <07f4737e5133458982cbf3761bb87dfd>:0)
