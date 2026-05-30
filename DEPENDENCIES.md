# Dependency Notes

## Included third-party folders

- `Assets/Fungus`: visual scripting/dialogue runtime used by the scene flow.
- `Assets/Plugins/DOTween`: tweening runtime used by interaction animation scripts.
- `Assets/MediaPipeUnity`: sample-side UI/helpers referenced by the scene.

## Excluded large or licensed assets

- `Packages/com.github.homuler.mediapipe`: MediaPipe Unity Plugin 0.16.3, excluded because the embedded package is large.
- `Assets/AK Studio Art`: gallery art/prefab package, excluded because it appears to be a third-party art asset.
- Large original content folders such as `Art`, `Models`, `内容素材`, and `Display room`.

If you need a fully visual-identical reconstruction, restore those assets from the original Unity project or from their licensed sources.

