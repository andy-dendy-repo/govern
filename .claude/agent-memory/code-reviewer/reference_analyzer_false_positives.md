---
name: analyzer-false-positives
description: cwm-roslyn-navigator detect_antipatterns hits (AP010/AP009/AP002/AP005/AP003/AP007) that are expected noise in Govern and should not be reported
metadata:
  type: reference
---

`detect_antipatterns` noise in this repo:
- **AP010** (no AsNoTracking): fires on handlers that load aggregates tracked on purpose so they can mutate them (SignIn, RefreshToken), and on `AnyAsync`. Report it only for pure reads that materialise entities.
- **AP009** (no CancellationToken): fires on xUnit test methods. Tests use `TestContext.Current.CancellationToken` via `Ct`.
- **AP002/AP008**: fire on `obj/` generated files. Ignore them.
- **AP005** (catch Exception): fires on `ErrorHandlingPipeline`, which is a sanctioned boundary per error-handling.md.
- **AP003** (`new HttpClient()` in `Govern.Client/Program.cs`): standard in the Blazor WASM template, not server code.
- **AP007** (empty catch): fires on `catch (DbUpdateConcurrencyException)` blocks that hold only an explanatory comment (RefreshTokenHandler reuse path, SignOutHandler, SignOutEverywhereHandler retry filter). Deliberate race handling; report only if the comment is missing or the swallow is wrong.
