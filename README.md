# ArtTools

ArtTools is an editor-only utility collection for organizing common art-production tasks in Unity.

## Requirements

- Unity 2017.1 through Unity 2022.3
- Built-in Render Pipeline, URP, and HDRP projects are supported by the editor tools; pipeline-specific material conversion still requires the destination pipeline and shaders to be installed.

## Installation

1. Import the supplied `ArtTools.unitypackage` into a Unity project.
2. Open **Art Tools > 美术工具中心** from Unity's main menu.
3. Select a utility from the hub.

## Included utilities

- Image export and image sequence capture
- Texture size inspection and resizing
- Unused texture discovery
- Asset organization, batch naming, material validation and conversion
- Missing Script scanning with Prefab and child-node location
- Decoration placement and scene quick opening
- Smooth Normal baking

## Notes

- All utilities run inside the Unity Editor; no runtime component is required for normal use.
- The editor utilities are render-pipeline independent. Use the generated Smooth Normal data with shaders appropriate to your chosen render pipeline.
- Back up project assets before applying batch operations.
- Development and test assets are intentionally excluded from the distributable package.

## Support

When requesting support, include the Unity version, render pipeline, the tool name, and any Console error output.
