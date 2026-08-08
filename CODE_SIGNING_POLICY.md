# Code signing policy

[中文](#中文) | [English](#english)

## 中文

Aurora Audio Studio 的正式 Windows 安装包计划使用 SignPath Foundation 提供的开源代码签名服务。

项目已于 2026-08-08 提交申请，目前等待 SignPath Foundation 审核；在获批并完成自动签名流程前，发布包不会宣称已经签名。

免费代码签名由 [SignPath.io](https://about.signpath.io/) 提供，证书由 [SignPath Foundation](https://signpath.org/) 提供。

### 签名范围

- 只签署从公开仓库 <https://github.com/swy2018/Aurora-Audio-Studio> 的源代码和构建脚本生成的 Aurora Audio Studio 安装包。
- 不使用本项目的签名证书签署第三方模型、第三方工具或其他项目的二进制文件。
- 正式产物由仓库中的 GitHub Actions 工作流在 GitHub 托管的 Windows 运行器上构建。
- 每次正式签名请求都需要人工批准，并核对版本号、提交、构建记录和 SHA-256 摘要。

### 项目角色

- 提交者（Committer）：[swy2018](https://github.com/swy2018)
- 审核者（Reviewer）：[swy2018](https://github.com/swy2018) 负责审核外部贡献；单维护者直接提交会通过公开构建记录和签名前人工复核进行审计。
- 签名批准者（Approver）：[swy2018](https://github.com/swy2018)

项目维护者必须为 GitHub 和 SignPath 账户启用多重身份验证。角色或流程发生变化时，本政策会同步更新。

### 隐私

请参阅 [Aurora Audio Studio 隐私政策](PRIVACY.md)。

## English

Aurora Audio Studio plans to use the open-source code-signing service provided by SignPath Foundation for official Windows installers.

The project submitted its application on 2026-08-08 and is awaiting SignPath Foundation review. Release artifacts will not be represented as signed until approval and the automated signing flow are complete.

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

### Signing scope

- Only Aurora Audio Studio installers produced from the source code and build scripts in the public repository at <https://github.com/swy2018/Aurora-Audio-Studio> may be signed.
- The project certificate must not be used to sign third-party models, third-party tools, or binaries from other projects.
- Release artifacts are built by the repository's GitHub Actions workflow on GitHub-hosted Windows runners.
- Every production signing request requires manual approval and verification of the version, commit, build record, and SHA-256 digest.

### Project roles

- Committer: [swy2018](https://github.com/swy2018)
- Reviewer: [swy2018](https://github.com/swy2018) reviews external contributions; direct commits by the sole maintainer remain auditable through public build records and manual pre-signing review.
- Approver: [swy2018](https://github.com/swy2018)

Project maintainers must enable multi-factor authentication for both GitHub and SignPath accounts. This policy will be updated when project roles or procedures change.

### Privacy

See the [Aurora Audio Studio Privacy Policy](PRIVACY.md).
