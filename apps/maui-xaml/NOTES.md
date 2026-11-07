# maui-xaml notes
- `ipc` step: MAUI XAML has no UI/host boundary (UI and I/O are the same .NET process and thread), so `Ping` is a direct `Task.FromResult` call. The number is reported but is not comparable to the bridged variants.
- "Wait for render" = two `Microsoft.UI.Xaml.Media.CompositionTarget.Rendering` ticks.
- Store mutations are coalesced into one `Refresh()` per dispatcher turn (analogue of React's batched render).
- Build: unpackaged, self-contained (.NET + Windows App SDK), ReadyToRun → large on-disk size but no runtime prerequisites.
