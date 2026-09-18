# Windows 前端项目部署工具

![程序图标](assets/app-icon.png)

一个面向前端开发人员的 Windows Server 图形化部署工具。把本地构建生成的 ZIP 上传到服务器后，可通过可视化界面完整替换旧站点，并单独管理语言包与 Nginx。

## 功能

- 多项目配置，每个项目独立保存主程序目录和可选语言包目录。
- ZIP 选择与拖放导入。
- ZIP 先完整解压并校验，再替换目标目录。
- 新版本写入失败时自动恢复旧目录。
- 可选部署前 ZIP 备份。
- 自动去掉 ZIP 中唯一的 `dist` 等最外层目录。
- ZIP 路径穿越、危险目录、符号链接和目录联接防护。
- 内置 Nginx 配置编辑器和缩进格式化。
- 自动识别 UTF-8、UTF-8 BOM、GBK、GB18030、UTF-16 配置文件。
- 保存 `nginx.conf` 前执行 `nginx -t`，失败时不覆盖原配置。
- Nginx 启用、停用、状态检测和平滑重载。
- 中文部署日志、配置备份和管理员权限重启。

## 系统要求

- Windows 10/11 或 Windows Server。
- .NET Framework 4.5 或更高版本。
- 使用 Nginx 管理功能时，需要服务器上已有 Windows 版 Nginx。

运行程序不需要 PowerShell，也不需要安装 .NET SDK。

## 下载与运行

从仓库的 [Releases](../../releases) 下载最新版 ZIP，解压后双击：

```text
前端项目部署工具.exe
```

不要把工具放进任何项目的部署目录中。程序会在 EXE 所在目录维护：

```text
projects.json              项目配置
settings.json              Nginx 路径配置
logs/                      部署日志
backups/                   项目部署备份
nginx-config-backups/      nginx.conf 备份
```

## 项目部署流程

1. 点击“管理项目”，配置项目名称和目标目录。
2. 选择“主程序”或“语言包”。
3. 选择或拖入 ZIP。
4. 核对目标目录并点击“开始部署”。
5. 工具先在临时目录安全解压 ZIP。
6. 旧目录被临时移出，新版本写入成功后旧目录被彻底删除。
7. 如果写入失败，工具自动删除未完成的新目录并恢复旧目录。

## Nginx 管理

主界面点击“Nginx 管理”，配置 `nginx.exe` 与 `nginx.conf` 路径。

- “格式化缩进”只修改编辑器内容，不会自动保存。
- “检测并保存”先用临时配置执行 `nginx -t`，通过后才覆盖原配置。
- 原 `nginx.conf` 会备份到 `nginx-config-backups/`。
- “重启/重载”实际执行平滑重载；Nginx 未运行时会启动。
- 如果配置中文乱码，可手动选择 GBK、GB18030 或 UTF-8 后点击“按编码重载”。

## 从源码构建

在 Windows PowerShell 中运行：

```powershell
.\build.ps1
```

构建结果位于：

```text
dist\Windows前端项目部署工具EXE\前端项目部署工具.exe
dist\Windows前端项目部署工具EXE-v1.1.1.zip
```

构建脚本使用系统自带的 .NET Framework C# 编译器，不会下载依赖。

## 测试

```powershell
.\test.ps1
```

测试内容包括：

- 旧站点完整替换与部署前备份。
- ZIP 最外层目录处理。
- Nginx 配置格式化。
- GBK/UTF-8 中文配置读取。
- 有效和无效 Nginx 配置检测。
- 主窗口与 Nginx 管理窗口控件检查。

测试只操作隔离的临时目录，不会启动、停止或重载真实 Nginx。

## 安全说明

- 部署前请再次核对目标目录。
- 程序会拒绝磁盘根目录、Windows 系统目录、工具自身目录及重解析目录。
- Nginx 配置检测失败时不会覆盖原配置。
- 历史备份不会自动删除，请根据服务器磁盘空间定期清理。
