# Unit models for the 3D board

The 3D board draws units as characters from **KayKit Adventurers 1.0** by Kay Lousberg
(<https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0>). The pack is CC0, so no attribution is
required; its licence is kept next to the models in `src/Govern.Client/wwwroot/models/units/LICENSE.txt`.

`bake.mjs` turns the pack's rigged, animated and textured characters into the small static models the board loads:

1. It downloads `Knight`, `Barbarian`, `Rogue`, `Rogue_Hooded` and `Mage` (`.glb`) into `cache/` (git-ignored).
2. It reads the texture at each vertex into a vertex colour and drops the texture, so the board loads no images.
3. It poses each unit once from one of the pack's animations (`Idle`; `Sit_Chair_Idle` for the horseman's rider).
4. It keeps the unit's own body parts and its accessories (a sword and round shield for the soldier, a crossbow for the
   archer, a knife for the scout…), turns the soldier's sword into a spear for the spearman, and splits off the parts drawn in the owner's seat colour
   (capes, shields, the settler's hat), whose colours are reduced to light and shade.
5. It writes `src/Govern.Client/wwwroot/models/units/<model>.json`: `body` and `seat`, each with `positions`, `colours`
   (0–255) and `indices`, sized so the standing knight's body is one unit tall, standing on y = 0 with its body centred on the origin.

The units and their parts are listed in `units` at the top of `bake.mjs`; `js/board3d/pieces.js` loads the files named in
`FIGURES` (`GameUiTests.Board3DPieces_EveryFigureHasItsBakedModel` checks every one exists).

## Running it

It needs Node 18 or later and network access to `raw.githubusercontent.com`. The downloads are checked against the SHA-256
hashes in `bake.mjs`, so a changed upstream file stops the bake rather than changing the models.

```bash
cd tools/unit-models
npm ci
npm run bake
```

Commit the changed `.json` files. The horse and the city are not from the pack; they are built in code in `pieces.js`.
