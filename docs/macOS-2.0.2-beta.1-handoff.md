# Aurora 2.0.2-beta.1：Mac 构建与验收交接

2026-10-09 更新：Mac 补包已完成，见[Mac 原生验收记录](validation-mac-2.0.2-beta.1.md)。以下保留构建前交接要求。

日期：2026-10-09。Windows 发布范围包含安装包与完整源码；本轮没有 Mac 安装包，官网下载仍使用已验收的 Mac 2.0.1。Windows 测试、共享 C# 或 WSL 测试不等于 Mac 实机通过。

## 获取相同源码

使用 Release 中的 `Aurora-Audio-Studio-2.0.2-beta.1-Source.zip`，并核对同名 `.sha256`。也可从一个全新目录克隆 `v2.0.2-beta.1` 标签，保留现有 Mac 工作区的未提交改动：

```bash
git clone --branch v2.0.2-beta.1 --single-branch \
  https://github.com/swy2018/Aurora-Audio-Studio.git Aurora-2.0.2-beta.1
cd Aurora-2.0.2-beta.1
git rev-parse HEAD
```

不要改写或移动已发布标签。源码版本是 2.0.2-beta.1，不表示已提供同版本 Mac 二进制。

## 本轮变化

- Mac 工具打包补齐可再分发的 Git、HTTPS helper、模板、CA 及非系统动态库。最终用户不应依赖 Homebrew、Xcode 或系统 Python。
- 管理环境首次安装/中断重试使用应用自带工具与隔离 Python；安装和维护共用真实推理模块导入检查。
- 同步命令取消需要终止并等待本次进程树退出。原生 Unix 的退出码、信号与取消回归必须在 Mac 再跑。
- Windows 的 CUDA 依赖、TensorFlow wheel 与 Basic Pitch 备用下载修复不是 Mac 安装实现，不能直接照搬 Windows 路径或二进制。

详细原因、已测样本与未验证项见 [空白环境修复记录](clean-install-2026-10-08.md) 和 [ADR 0005](decisions/0005-application-owned-bootstrap-tools.md)。

## 隔离本机验证

目标仍是 Apple Silicon / macOS 26+，未新增 Intel Mac 支持。先满足 [Mac 构建要求](macOS-release.md)；这些是开发机要求，不是最终用户要求。

```bash
aurora_qa_root="$(mktemp -d "$PWD/.aurora-mac-qa.XXXXXX")"
export AURORA_BUILD_ROOT="$aurora_qa_root/build"
export AURORA_TEST_DATA_ROOT="$aurora_qa_root/user-data"
unset AURORA_SIGN_IDENTITY
dotnet run --project work/audio-studio/AuroraAudioStudio.MacTests -c Release
bash work/audio-studio/tools/build-macos.sh
dotnet run --project work/audio-studio/AuroraAudioStudio.MacTests -c Release -- \
  --bootstrap-tools "$AURORA_BUILD_ROOT/Aurora Audio Studio.app/Contents/MacOS/Runtime" \
  "$AURORA_TEST_DATA_ROOT" whisper-small
```

这会生成 ad-hoc 本机验证应用，不覆盖 `/Applications` 或已有模型。首次安装测试前，隔离目录内不能已有 `Models`。工具与模型下载安装需要网络；测试保留文件，不自动删除旧目录。

必须继续确认：

1. 脱离 Homebrew / DYLD 路径时，工具、Git HTTPS 和动态库仍能运行。
2. 从 Finder 启动新应用，模型中心能完成首次安装、失败重试与取消，完成后进度结束；四语言操作入口可见。
3. 每个准备声明可用的模型实际处理短样本，输出能预览、导出并重新打开；记录模型、设备、输入、任务与结果，不以“已连接”代替生成验收。
4. 新用户/干净 Mac 验证缺少开发环境时的行为，确认权限提示、系统支持范围及已有应用/数据保护。

## 后续补包

Mac 实机通过后，再依用户授权执行 Developer ID 签名、Apple 公证、最终 DMG 验证与同名 SHA-256，流程见 [Mac 发布说明](macOS-release.md)。补到同一 Beta Release 时保留 Windows 与源码资产，不抢占 Stable Latest。更新官网下载资产快照并确认两平台各自指向真实可用版本。

回传完整提交 SHA、系统/硬件、签名及公证状态、安装日志、实际输出与失败项。凭据、私钥、模型、私人素材和全量运行缓存不得随源码或验收记录上传。清理仍须准确清单及批准。
