# 第三方组件与许可声明（THIRD-PARTY NOTICES）

本仓库自有代码以 Apache-2.0 授权（见 `LICENSE`）。**该授权不覆盖下列第三方组件**，
各组件仍受其各自许可约束。本仓库**不分发**下列受限制组件的源码或二进制。

---

## 1. hdt-raylib-spine.dll（raylib/spine-c 运行时二进制）

- 来源：HuiDesktop / LightBuild（<https://github.com/HuiDesktop/LightBuild>）
- 内容：基于 Spine C 运行时构建的 FFI 动态库
- 在本项目中的位置（**不随仓库分发**）：`GflChibiDesktop2/runtime/lua/spine2125/hdt-raylib-spine.dll`
- 说明：该二进制同样受 Spine Runtimes 许可约束（见上文要点 1、3）；
  它以 `.gitignore` 排除，运行桌宠前需自行获取并放置到上述目录。

与之配套的 Lua FFI 绑定（`GflChibiDesktop2/runtime/lua/spine2125/{cdef,init,hdtmodule}.lua`）
为接口声明性质的绑定代码，随仓库分发；其描述的接口源自 Spine C 运行时。

---

## 2. 其它第三方组件

| 组件 | 位置 | 说明 |
|---|---|---|
| raylib 5.1 | `GflChibiDesktop2/runtime/` | zlib/libpng 许可 |
| LuaJIT / lua51.dll | `GflChibiDesktop2/runtime/` | MIT 许可 |
| huMessageQueue.dll | `GflChibiDesktop2/` | 随项目分发 |

---

## 附：Spine Runtimes Software License Version 2.1

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
