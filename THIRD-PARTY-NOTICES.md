# 第三方组件与许可声明（THIRD-PARTY NOTICES）

本仓库自有代码以 Apache-2.0 授权（见 `LICENSE`）。**该授权不覆盖下列第三方组件**，
各组件仍受其各自许可约束。本仓库**不分发**下列受限制组件的源码或二进制。

---

## 1. Spine Runtimes（C# / MonoGame 版）

- 版本：2.1.25
- 版权：Copyright (c) 2013, Esoteric Software. All rights reserved.
- 许可：**Spine Runtimes Software License Version 2.1**
- 在本项目中的位置（**不随仓库分发**）：`GflChibiDesktopLegacy/SpineLibrary/spine-runtimes-2.1.25/`

### 许可要点（摘自源码文件头的完整声明）

> You are granted a perpetual, non-exclusive, non-sublicensable and non-transferable license to
> install, execute and perform the Spine Runtimes Software (the "Software") **solely for internal
> use**. Without the written permission of Esoteric Software (typically granted by licensing Spine),
> you may not (a) modify, translate, adapt or otherwise create derivative works, improvements of
> the Software or develop new applications using the Software or (b) remove, delete, alter or
> obscure any trademarks or any copyright, trademark, patent or other intellectual property or
> proprietary rights notices on or in the Software, including any copy thereof.
> **Redistributions in binary or source form must include this license and terms.**

### 对本项目的约束

1. **使用前提**：使用 Spine 运行时需要自行持有有效的 Spine 授权（Spine license）。
2. **不得公开分发源码**：本运行时代码**不得**以源码形式随本仓库（或任何公开仓库）分发，
   故其源码已加入 `.gitignore`，历史中亦已清除。
3. **二进制分发需附许可**：若你的发行包内含编译后的运行时代码（例如 `GflChibiDesktopLegacy*.dll`），
   必须在发行物中附上**上述完整许可声明**（见本文件末尾的全文）。
4. **命名空间改造**：本项目内的运行时代码被改造过（命名空间 `Spine` → `Spine2_1_25`），
   该改造依据你持有的 Spine 授权进行。

### 获取方式（构建前必须完成）

运行时代码不随仓库分发，克隆后需自行获取，参见 `tools/fetch-spine-runtime.ps1`：

```powershell
# 推荐：从你本地已有的（已改造的）副本复制
pwsh -File tools/fetch-spine-runtime.ps1 -FromDir <你已有的 spine-runtimes 副本目录>

# 或：从官方仓库下载指定版本（会做命名空间改写）
pwsh -File tools/fetch-spine-runtime.ps1 -Ref 2.1.25
```

未获取时，legacy 工程会在编译前给出明确报错提示。

---

## 2. hdt-raylib-spine.dll（raylib/spine-c 运行时二进制）

- 来源：HuiDesktop / LightBuild（<https://github.com/HuiDesktop/LightBuild>）
- 内容：基于 Spine C 运行时构建的 FFI 动态库
- 在本项目中的位置（**不随仓库分发**）：`GflChibiDesktop2/runtime/lua/spine2125/hdt-raylib-spine.dll`
- 说明：该二进制同样受 Spine Runtimes 许可约束（见上文要点 1、3）；
  它以 `.gitignore` 排除，运行桌宠前需自行获取并放置到上述目录。

与之配套的 Lua FFI 绑定（`GflChibiDesktop2/runtime/lua/spine2125/{cdef,init,hdtmodule}.lua`）
为接口声明性质的绑定代码，随仓库分发；其描述的接口源自 Spine C 运行时。

---

## 3. 其它第三方组件

| 组件 | 位置 | 说明 |
|---|---|---|
| MonoGame.Framework.WindowsDX / DesktopGL 3.8.1.303 | NuGet 引用 | Microsoft Public License (Ms-PL) |
| raylib 5.1 | `GflChibiDesktop2/runtime/` | zlib/libpng 许可 |
| LuaJIT / lua51.dll | `GflChibiDesktop2/runtime/` | MIT 许可 |
| huMessageQueue.dll | `GflChibiDesktop2/`、`GflChibiDesktopLegacy/` | 随项目分发 |

---

## 附：Spine Runtimes Software License Version 2.1 全文

```
Spine Runtimes Software License
Version 2.1

Copyright (c) 2013, Esoteric Software
All rights reserved.

You are granted a perpetual, non-exclusive, non-sublicensable and
non-transferable license to install, execute and perform the Spine Runtimes
Software (the "Software") solely for internal use. Without the written
permission of Esoteric Software (typically granted by licensing Spine), you
may not (a) modify, translate, adapt or otherwise create derivative works,
improvements of the Software or develop new applications using the Software
or (b) remove, delete, alter or obscure any trademarks or any copyright,
trademark, patent or other intellectual property or proprietary rights
notices on or in the Software, including any copy thereof. Redistributions
in binary or source form must include this license and terms.

THIS SOFTWARE IS PROVIDED BY ESOTERIC SOFTWARE "AS IS" AND ANY EXPRESS OR
IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO
EVENT SHALL ESOTERIC SOFTARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```
