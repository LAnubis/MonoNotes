# 📝 MonoNotes

MonoNotes 是一款基于 **.NET MAUI + Blazor Hybrid** 构建的跨平台、本地优先（Local-First）Markdown 知识管理与双链笔记软件。

它拥有极其极客的底层架构：极速的纯文本 JSON 索引、无缝的 WebDAV 后台同步、多标签页全屏沉浸编辑，以及完全属于你自己的知识图谱。你的数据完全掌握在自己手中，拒绝云端绑架。

## ✨ 核心特性

### 🧠 高级知识管理
- **双向链接与知识图谱**：支持 `《《笔记名称》》` 语法，自动构建笔记间的网状关联，并在侧边栏实时渲染反向链接（Backlinks）与出链。
- **动态大纲提取**：实时解析 Markdown 标题，生成可点击跳转的悬浮大纲目录。
- **多知识库支持（Workspaces）**：一键切换不同的工作区，工作与生活物理隔离。
- **模板引擎**：支持将任意笔记另存为模板，并在新建时通过左右分栏的沉浸式 UI 进行预览和应用。

### ⚡ 极速与本地优先
- **无感迁移与物理隔离**：笔记与系统文件（`.assets`, `.templates`）在物理硬盘上严格隔离，确保最高的结构稳定性。
- **毫秒级加载**：摒弃繁重的 SQLite，采用基于 C# Source Generator 的 `JsonIndexService`，搭配内存缓存与防抖（Debounce）写入，实现海量笔记瞬间加载。
- **多标签页并发编辑**：类似浏览器的 Tab 栏设计，支持同时打开最多 7 篇笔记，且只有打开的笔记才会加载完整正文至内存。

### ☁️ 工业级 WebDAV 同步
- **后台静默同步**：支持坚果云等标准 WebDAV 服务，定时自动在后台拉取和推送。
- **细粒度冲突处理中心**：采用“双向补齐”与“严格时间戳校验”策略。当多端修改同一文件时，自动生成冲突副本，并提供直观的 UI 面板供用户比对采纳。
- **防刷限流与死者复生防护**：内置请求冷却机制（Rate Limiting突破）与并发锁，配合云端+本地双重校验，彻底解决云同步中臭名昭著的“已删文件复活”问题。
- **安全第一**：WebDAV 应用密码调用底层系统级 API (DPAPI / Keychain) 进行硬件加密存储，绝不明文落盘。

### 🎨 现代桌面体验
- **沉浸式原生窗口**：深度调用 Windows (WinUI3) 和 Mac (Catalyst) 底层 API，启动即全屏沉浸。
- **智能标签云**：侧边栏自动计算标签使用频率，横向流式展现 Top 8 高频标签，并提供支持批量重命名/全局删除的标签管理中心。
- **纯净 Markdown 渲染**：引入 `Markdig` 引擎处理 HTML 导出，搭配原生 UI 弹窗，支持一键将笔记导出为 `.md`, `.txt` 或排版精美的 `.html` 网页。
- **图片本地化**：一键抓取 Markdown 正文中的外链网络图片，自动下载至本地 `.assets` 并替换链接，防止图床防盗链或失效。

---

## 🏗️ 架构设计

项目采用严格的职责分离（Clean Architecture）设计：

* **`MonoNotes.Core`**: 领域模型（Models）、接口契约（Interfaces），零外部依赖。
* **`MonoNotes.Storage`**: 本地仓储实现。包含极致优化的 `JsonIndexService` 和直接操作物理文件的 `LocalNoteRepository`。
* **`MonoNotes.Sync`**: 云端引擎。包含底层的 `WebDavService` 封装和负责并发/冲突调度的 `WebDavSyncService`。
* **`MonoNotes.UI`**: 纯净的 Razor 类库（RCL）。包含所有界面逻辑（如 `WorkspaceView.razor`），通过依赖注入调用底层接口，为未来平移到 WebAssembly 预留了完美空间。
* **`MonoNotes.App`**: MAUI 原生宿主壳。负责系统原生弹窗拦截、DI 容器组装和原生全屏窗口控制。

---

## 🚀 快速开始

### 环境要求
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) 或更高版本
- Visual Studio 2026 / JetBrains Rider  (安装 MAUI 工作负载)

### 编译与运行
1. 克隆代码到本地：
   ```bash
   git clone [https://github.com/LAnubis/MonoNotes.git](https://github.com/LAnubis/MonoNotes.git)
   git clone [https://gitee.com/drshawnliu/mono-notes.git](https://gitee.com/drshawnliu/mono-notes.git)
   
   cd MonoNotes