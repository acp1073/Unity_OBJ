# 202411581154 — Unity 计算机动画算法技术作业

Unity 2022.3 项目，包含 OBJ 模型浏览器和参数曲线建模工具。

## 作业1：OBJ 模型浏览器

这是一个基于 Unity 的运行时 OBJ 浏览器。打开 `Assets/Scenes/SampleScene.unity` 并点击 **Play**，查看器会自动启动并加载 `Assets/Models/SampleCube.obj`。

### 使用方式

1. 在界面中的路径输入框填写 OBJ 文件路径，支持绝对路径和项目内的 `Assets/...` 路径。
2. 点击 **选择文件** 可在 Unity 编辑器的 Play 模式下打开文件选择面板；打包后可直接输入绝对路径。
3. 点击 **加载** 导入模型。右键旋转、中键平移、滚轮缩放，点击 **聚焦** 重新适配视图。

### 顶点数据

`ObjParser` 直接解析 OBJ 文本，不依赖 Unity 模型导入器。加载完成后，模型对象上的 `ObjLoadedModel` 组件保留：

- `SourceVertices`：OBJ 原始 `v` 顶点数组，索引与 OBJ 顶点索引一致；
- `SourceUVs` / `SourceNormals`：原始 UV 与法线数组；
- `SourceTriangleIndices`：OBJ 面三角化后的原始顶点索引；
- `RenderVertexSourceIndices`：Unity 渲染顶点到原始 OBJ 顶点的映射，用于处理 UV 接缝和硬法线拆点；
- `UnityMesh`：用于渲染的 Unity `Mesh`，可访问 `vertices`、`triangles`、`normals` 和 `uv`；
- `ParsedData`：包含顶点、法线、UV 和面的完整解析数据。

当前支持 `v`、`vt`、`vn`、`f`、负索引、注释和多边形面（三角扇拆分）；材质库和纹理暂未导入。后续可在 `ObjLoadedModel` 或其他分析脚本中扩展顶点选取、包围盒、曲率、标记和几何计算。

### 导入并保存到场景

在 Unity 菜单中选择 **Tools > OBJ Viewer > Import OBJ Into Current Scene...**，然后选择 OBJ 文件。程序会在当前场景创建模型对象，并在 `Assets/GeneratedOBJ/` 生成 Mesh 和材质资产。模型对象带有 `ObjLoadedModel` 组件；保存场景后即可保留。再次 Play 时，浏览器优先使用场景中的模型。

也可以把 `.obj` 从 Project 窗口或 Windows 文件管理器拖到 Scene 视图导入并聚焦模型。工程内的 `Assets/Models/CustomPyramid.obj` 可作为手工编辑示例；修改其中的 `v` 和 `f` 行后，可通过运行时路径或编辑器菜单加载。

## 作业二：参数曲线建模与交互

参数曲线工具在 Unity 编辑器中创建和编辑曲线。曲线由可访问的控制点数据生成，并使用 `LineRenderer` 预览。

### 创建曲线

可在菜单中选择：

- **Tools > Parametric Curves > Create Curve Object**：在当前场景中创建曲线对象；
- **Tools > Parametric Curves > Create Demo Scene**：创建并打开 `Assets/Scenes/ParametricCurveScene.unity` 示例场景；
- **Tools > Parametric Curves > Open Demo Scene**：打开已创建的示例场景；
- **GameObject > Parametric Curves > Curve Object**：从 GameObject 菜单创建曲线对象。

创建后，在 Hierarchy 中选中 `Parametric Curve`，并在 Inspector 中调整曲线类型、采样数和控制点。**Add Point** 添加控制点，**Remove Last** 删除末尾控制点，**Reset** 恢复默认点，**Rebuild** 重新生成曲线。

### Scene 视图交互

在 **Scene** 视图中直接按住鼠标左键拖动黄色的 `P0`、`P1` 等控制点。拖动会实时更新控制点数据和曲线预览，并支持 Unity Undo/Redo。滚轮缩放，右键旋转，中键平移视图。对于平面曲线，可启用 Scene 视图的 **2D** 模式方便编辑。

### 曲线类型与转换

Inspector 的 **Curve Type** 支持：

- **Bezier**：贝塞尔曲线；
- **CatmullRom**：Catmull–Rom 曲线；
- **UniformBSpline**：三次均匀 B 样条；
- **Lagrange**：拉格朗日插值曲线。

选择 **Convert To** 目标类型并点击 **Convert Curve** 可转换当前表示。转换通过对原曲线采样后重建控制点完成，因此属于近似转换，并不保证转换前后数学上完全相同。

### 数据与扩展

- `ParametricCurveModel.ControlPoints` 可访问当前控制点；控制点以对象局部坐标保存。
- `ParametricCurveMath.Evaluate` 可按参数 `t` 求曲线位置；`Sample` 可生成离散采样点。
- `ParametricCurveModel` 管理场景组件和 `LineRenderer` 预览；纯曲线算法集中在 `ParametricCurveMath`，便于后续扩展测长、曲率、路径动画、导出和曲面建模等功能。
