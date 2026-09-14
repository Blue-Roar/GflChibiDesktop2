# 少前桌面Q宠V2

从2020年祖传下来的烂代码，重构了也还是一大坨……

## 构建前置：Spine 运行时需自行获取

本项目**不包含** Spine 运行时代码。Spine Runtimes 受 Esoteric Software 的
Spine Runtimes Software License 约束（仅限内部使用、未经书面许可不得创建衍生作品、
二进制或源码再分发须附许可），因此不随本仓库公开分发，相关路径已加入 `.gitignore`。

使用与构建前需：

1. 自行持有有效的 Spine 授权（Spine license）；
2. 获取运行时代码到 `GflChibiDesktopLegacy/SpineLibrary/spine-runtimes-2.1.25/`：

```powershell
# 推荐：从你本地已有的（命名空间已改造为 Spine2_1_25 的）副本复制
powershell -ExecutionPolicy Bypass -File tools/fetch-spine-runtime.ps1 -FromDir <你已有的副本目录>

# 或：从官方仓库下载指定版本（会自动改写命名空间）
powershell -ExecutionPolicy Bypass -File tools/fetch-spine-runtime.ps1 -Ref 2.1.25
```

3. raylib 渲染链路还需自行获取 `hdt-raylib-spine.dll`，放置到
   `GflChibiDesktop2/runtime/lua/spine2125/`（该文件同样受 Spine 许可约束，未入库）。

未完成上述步骤时，legacy 工程会在编译前给出明确报错。

第三方组件与完整许可声明见 [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)。
本仓库自有代码以 Apache-2.0 授权（见 `LICENSE`）；该授权不覆盖上述第三方组件。
