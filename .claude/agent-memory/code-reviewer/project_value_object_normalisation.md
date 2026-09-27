---
name: value-object-normalisation
description: Checks for Govern value objects that normalise input (Email): idempotence under the EF converter, Unicode order of operations, validator-vs-domain length bounds
metadata:
  type: project
---

Review 3 (2026-09-27, feature/strict-email-validation, follow-up #12) found `Normalize(FormC).ToLowerInvariant()` is not idempotent. Lower-casing can leave a decomposable pair that NFC would compose, e.g. `J+U+030C` -> `j+U+030C` (stored), but `Create` of that returns `U+01F0`. About 86 base+mark cases. NFD -> ToLowerInvariant -> NFC was empirically idempotent and case/canonical-consistent.

**Why:** `UserConfiguration` rehydrates with `Email.Create`, so a non-idempotent normaliser means the loaded `Value` differs from the stored column: `/me` shows an address that cannot sign in, and canonically-equal addresses can both register.
**How to apply:** for any normalising VO, verify `Create(Create(x).Value) == Create(x)` (brute-force it with a scratch console app; it takes seconds). Check that the Application validator's raw-length cap matches the domain's "after normalisation" rule and the docs. Check that the EF converter validates on read (tightening a rule can make old rows throw). Related: [[convention-sweep-gaps]].
