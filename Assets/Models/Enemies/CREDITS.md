# Enemy models

- Dragon: "Skeleton_dragon ff" by TRSgamings, Sketchfab, CC-BY 4.0. Reduced to the clips the game uses
  and rescaled (tools/models/dragon.py).
- Skeleton: "Skeleton - low tier enemy", Sketchfab. Decimated and its materials baked into one
  (tools/models/skeleton.py).
- Tillagemon (stage 1 boss), Megademon (stage 3 boss): downloads of unknown origin; check their
  licenses before publishing anything (tools/models/tillagemon.py, megademon.py).
- Gideon (stage 2 boss): Paragon character by Epic Games, released for Unreal Engine projects. Used here
  in a non-commercial university project (tools/models/gideon.py).
- DeSaad (healer): "DeSaad(Textured)(Rigged)" by CAPTAAINR, Sketchfab, CC-BY 4.0
  (https://sketchfab.com/3d-models/desaadtexturedrigged-b32d07b15c5041bbaa58f2d0303031ae). Its sword taken
  out, reduced to the clips the game uses and rescaled (tools/models/desaad.py). The character belongs to
  DC Comics, which that license doesn't cover: check before publishing anything.
- SkeletonMage (ranged enemy): "Animated skeleton casts spell with hands", Sketchfab, rigged with
  Mixamo; its Idle, Walk, Hit and Death clips are Mixamo animations. Author and license still to check
  before publishing anything. Its 100k triangles are brought down to about 12k when the prefab is made
  (RuneArena > Create Ranged Mage Model), with UnityMeshSimplifier (MIT, Mattias Edlund) in Packages.
