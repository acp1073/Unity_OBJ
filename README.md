# 202411581154
# OBJ 模型浏览器
这是一个基于 Unity 2022.3 的运行时 OBJ 浏览器，目前作为作业的基础版本。打开 `Assets/Scenes/SampleScene.unity` 后直接点击 Play，查看器会自动启动并加载 `Assets/Models/SampleCube.obj`。

## 使用方式

1. 在界面的路径输入框填写 OBJ 文件路径。支持绝对路径和项目内的 `Assets/...` 路径。
2. 点击“选择文件”可在 Unity 编辑器 Play 模式下打开文件选择面板；打包后可直接输入绝对路径。
3. 点击“加载”，使用右键旋转、中键平移、滚轮缩放，点击“聚焦”重新适配视图。

## 顶点数据入口

`ObjParser` 不依赖 Unity 的模型导入器，而是直接解析 OBJ 文本。加载完成后，模型对象上的 `ObjLoadedModel` 组件保留：

- `SourceVertices`：OBJ 文件中的原始 `v` 顶点数组，索引与 OBJ 顶点索引一致；
- `SourceUVs` / `SourceNormals`：OBJ 文件中的原始 UV 与法线数组；
- `SourceTriangleIndices`：由 OBJ 面三角化后得到的原始顶点索引；
- `RenderVertexSourceIndices`：Unity 渲染顶点到原始 OBJ 顶点的映射，用于处理 UV Seam 和硬法线拆点；
- `UnityMesh`：用于渲染的 `Mesh`，可直接访问 `UnityMesh.vertices`、`triangles`、`normals` 和 `uv`；
- `ParsedData`：完整的解析中间数据，包含顶点、法线、UV 和面。

因此，后续可以在 `ObjLoadedModel` 或新的分析脚本中直接进行顶点选取、包围盒、曲率、标记和几何计算。

当前支持 `v`、`vt`、`vn`、`f`、负索引、注释和多边形面（三角扇拆分）；材质库和纹理暂未导入。

## 导入并保存到场景

在 Unity 编辑器菜单中选择：

`Tools > OBJ Viewer > Import OBJ Into Current Scene...`

选择 OBJ 后，程序会在当前场景创建模型对象，并在 `Assets/GeneratedOBJ/` 生成 Mesh 和材质资产。模型对象会带有 `ObjLoadedModel` 组件，按 `Ctrl+S` 保存场景后即可长期保留。再次 Play 时，浏览器会优先使用场景中的模型，而不是自动加载示例立方体。

现在也可以直接把 `.obj` 从 Project 窗口或 Windows 文件管理器拖到 Scene View，程序会自动执行同样的导入流程并聚焦模型。

工程内还提供了一个可手工修改的示例：`Assets/Models/CustomPyramid.obj`。修改其中的 `v` 和 `f` 行后，可以用运行时路径加载，或通过编辑器菜单导入到场景。
