# ArtTools · Unity 美术工具箱

![ArtTools QQ 交流群二维码](./QQ群二维码.png)

**扫码加入 ArtTools QQ 交流群，或在 QQ 中搜索群号 1124864329。** 欢迎交流工具使用、反馈问题和提出功能建议。

ArtTools 是一套仅在 Unity 编辑器中运行的美术工具，用于整理常见的美术制作流程。

## 使用要求

- Unity 2017.1 至 Unity 2022.3。
- 编辑器工具支持 Built-in、URP 和 HDRP 项目；使用特定渲染管线的材质转换功能时，项目中仍需安装目标渲染管线和对应 Shader。

## 安装方法

1. 将提供的 `ArtTools.unitypackage` 导入 Unity 项目。
2. 从 Unity 顶部菜单打开 **Art Tools > 美术工具中心**。
3. 在工具中心选择需要使用的功能。

如需通过 Unity Package Manager 安装或更新，请使用持续维护的 [ArtTools-UPM 仓库](https://github.com/lishuo0617/ArtTools-UPM)。

## 包含的工具

- 图片导出与序列图录制
- 贴图尺寸检查与调整
- 未使用贴图查找
- 资源整理、批量命名、材质检查与转换
- Missing Script 扫描及 Prefab、子节点定位
- 场景装饰物放置与场景快速打开
- 平滑法线烘焙

## 注意事项

- 所有工具都在 Unity 编辑器中运行，正常使用不需要运行时组件。
- 编辑器工具不依赖特定渲染管线。使用生成的平滑法线数据时，请选择适合项目渲染管线的 Shader。
- 执行批量操作前，请备份项目资源。
- 发布包不包含开发和测试资源。

## 交流与反馈

扫码加入上方 QQ 交流群。反馈问题时，建议附上 Unity 版本、渲染管线、工具名称以及 Console 中的错误信息。
