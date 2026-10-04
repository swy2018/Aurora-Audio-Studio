# 版本保留说明

## 2026-10-05 清理完成

- 已按用户批准的准确清单移除 `v2.0.1-beta.2` Release、6 个资产及云端同名标签；旧 Beta 2 下载链接不再可用。
- 保留 `v1.9.9`、`v2.0.0`、`v2.0.1` 的全部正式发布资产与标签，Latest 为 `v2.0.1`。Windows 与 Mac 的 Stable / Beta 通道目前均选择 2.0.1 正式版。
- 全部既有云端分支、完整 Git 提交历史保留；Beta 2 提交仍可从 main 历史追溯。没有截断历史、改写提交或强制推送。
- 本地清除 17 项获批的旧安装包、编译中间文件及隔离验证产物，保留最新源码、正式 DMG、校验文件与发布证据；已安装应用、模型、设置和用户成品未变。

## 2026-10-04 · 2.0.1

Windows 与 Mac 2.0.1 正式发布，保留现有 `v1.9.9`、`v2.0.0` 和 `v2.0.1-beta.2` 的全部资产与标签。Windows 与 Mac 均可显式回退到 2.0.0。Mac 2.0.1 已完成原生构建、公证与升级回退验证。旧 Beta 及临时产物清理需要本次准确清单获批；完整 Git 提交历史和所有正式版保留。

## 2026-10-01 维护记录

- 正式版保留 `v1.9.9` 和 `v2.0.0`；Latest 仍为 `v2.0.0`，保留历史正式版以支持显式回退。
- 当前测试版保留 `v2.0.1-beta.2`，包含 Windows、Mac、源码与对应校验文件。
- 已移除旧测试版 `v2.0.0-beta.1`、`v2.0.0-beta.2`、`v2.0.0-beta.3`、`v2.0.0-beta.4`、`v2.0.1-beta.1` 的 Release、安装资产与对应标签。因此历史文档中的这些 Release 下载链接不再可用。
- 完整 Git 提交历史、现有分支、正式版标签及最新测试版标签保留。未截断历史、改写提交或强制推送；删除旧测试标签后，原提交仍可由保留的云端引用访问。
- 安装包及校验文件以 [GitHub Releases](https://github.com/swy2018/Aurora-Audio-Studio/releases) 和官网实际下载入口为准；源码历史可通过提交哈希追溯。

Stable releases 1.9.9, 2.0.0 and 2.0.1 remain downloadable. Superseded Beta releases and their remote tags have been removed; complete commit history and existing branches are preserved without rewriting commit hashes. Stable and Beta channels currently select 2.0.1 on both platforms.
