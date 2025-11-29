# EC9-KX6000 GPIO Manager (F75111 跨平台控制方案)

## 📖 项目简介
本项目是专为 **EC9-KX6000-58** 主板（兆芯 KX-6000 平台）设计的 GPIO 软硬件交互解决方案。
核心通过操作 **Fintek F75111** 芯片，实现了 8 路 GPIO 的输入/输出控制。

项目包含两套完整的实现方案，分别适配 Windows 和 Linux 环境，旨在验证国产化平台的软硬件兼容性与底层驱动开发能力。

## 🏗️ 项目架构

### 📂 1. Windows 版本 (GUI)
*   **路径：** `/Windows_WinForms`
*   **架构：** C# / WinForms / .NET Framework
*   **驱动：** WinRing0 (x64)
*   **功能：**
    *   可视化界面设置 8 路 GPIO 的 Input/Output 模式。
    *   实时读取电平状态与手动控制高低电平输出。
    *   集成 WinRing0 库实现 Ring3 到 Ring0 的端口访问。

### 📂 2. Linux 版本 (Driver & CLI)
*   **路径：** `/Linux_Driver`
*   **架构：** C Language / Linux Kernel Module
*   **系统：** Ubuntu 20.04 LTS 及以上
*   **功能：**
    *   **内核驱动 (`.ko`)：** 编写了 Linux 字符设备驱动，创建 `/dev/f75111_gpio` 设备节点。
    *   **命令行工具：** 提供 `gpio-mode`、`gpio-set` 等指令工具。
    *   **C API 封装：** 提供 `gpio_api.h` 供上层应用开发调用。

---

## 🚀 快速上手 (Quick Start)

### 💻 Windows 环境
1.  使用 Visual Studio 2022 打开解决方案。
2.  编译生成 Exe 文件。
3.  **注意：** 必须右键以 **“管理员身份运行”**，否则无法加载底层驱动。

### 🐧 Linux (Ubuntu) 环境
**1. 编译与驱动安装**
```bash
cd f75111_gpio
make                # 编译驱动模块与工具
sudo make install   # 安装驱动
sudo modprobe f75111_gpio  # 加载内核模块
