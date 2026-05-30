# AliSpawn Gesture Showroom Case

Unity gesture-interaction showroom case extracted from `MediaPipeCameraRouteTest.unity`.

This repository keeps the scene, custom interaction scripts, and lightweight runtime assets needed to study the implementation. Large third-party binaries and marketplace art assets are intentionally excluded so the case can be shared safely.

## Scene

- Main scene: `Assets/Scenes/MediaPipeCameraRouteTest.unity`
- Unity version: `2022.3.62f2c1`
- Core scripts: `Assets/Scripts`

## What is included

- Camera route control and debug scrubbing.
- MediaPipe hand-input adapter and hand tracking manager integration.
- Pinch/cursor interaction logic for showroom items.
- Fungus bridge command for observation flow events.
- Scene lightmap data and small project settings required by Unity.
- Fungus, DOTween, MediaPipe sample UI scripts, and selected TextMesh Pro font assets already used by the scene.

## External dependencies

Before opening the scene in a fresh clone, restore these dependencies:

- MediaPipe Unity Plugin `com.github.homuler.mediapipe` version `0.16.3`.
  - In the source project it lived under `Packages/com.github.homuler.mediapipe`.
  - It is not committed here because the embedded package is about 389 MB and contains third-party binaries.
- Unity packages from `Packages/manifest.json`, especially URP, Cinemachine, TextMesh Pro, Unity UI, and Timeline.
- Marketplace/gallery art referenced by the scene, especially `AK Studio Art / Simple VR Gallery`, if you want the exact original showroom visuals.

## Open in Unity

1. Clone the repository.
2. Install Unity `2022.3.62f2c1` or another compatible Unity 2022.3 LTS editor.
3. Restore `com.github.homuler.mediapipe` into `Packages/com.github.homuler.mediapipe`, or install the same plugin version through your preferred workflow.
4. Open the project folder in Unity.
5. Open `Assets/Scenes/MediaPipeCameraRouteTest.unity`.

Some referenced art prefabs/materials may appear missing until the gallery art package is restored. The custom source code and interaction flow remain available for review.

## Case focus

The case demonstrates a camera-routed exhibition hall controlled by hand gestures:

- MediaPipe detects hand landmarks and gesture state.
- `HandInputAdapter` normalizes input for the showroom interaction layer.
- `ShowroomInteractionManager`, `ShowroomCursor`, `InteractableItem`, and related scripts map gesture intent to hover, pinch, focus, and observation behavior.
- `CameraRouteController` and `CameraRouteNode` drive route-based viewpoint transitions inside the showroom.

