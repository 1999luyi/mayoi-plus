# Mayoi Plus（真宵 Plus）

[简体中文](#简体中文) · [English](#english) · [日本語](#日本語)

## 简体中文

适用于 Windows Steam《逆转裁判123 成步堂精选集》的非官方无障碍模组。它为菜单、剧情、法庭档案和调查添加屏幕阅读器朗读，并提供调查与谜题中的键盘操作辅助。游戏内按 F1 可查看当前界面的帮助。

Mayoi 是绫里真宵的名字。Plus 是一个小玩笑：除了召唤灵魂，真宵的灵力还可以帮玩家读菜单、找线索。

- 构建环境：PowerShell 7、.NET 10 SDK、自备 Steam 游戏（构建号 `20282248`）和 BepInEx `5.4.23.5` Mono x86。游戏程序集仅从本机安装引用，不随源码发布。
- 从仓库根目录运行下文的构建命令；`-BepInExPath` 指向包含 `BepInEx/core` 的目录。产物 `artifacts/release/Pwaat.Accessibility.dll` 放入游戏的 `BepInEx/plugins/Pwaat.Accessibility/`。升级时覆盖同名 DLL，不要另外保留一份。
- 读屏：把兼容的 x86 `Tolk.dll` 和其对应依赖放在 `BepInEx/plugins/Pwaat.Accessibility/native/x86/`。模组不限制 Tolk 的文件哈希。作者此前用 [boz700908/tolk](https://github.com/boz700908/tolk) 的定制版本和保益悦听测试；其他构建和读屏器未逐一验证。Tolk 不随模组源码提供。
- 格式化：运行 `dotnet format whitespace ./src --folder`。仓库工作流检查格式，不需要游戏文件。

本模组原创代码采用 [MIT 许可证](LICENSE)。再分发源码或 DLL 时，请随附原版权声明和许可文本。发布修改版时，也请在下载页或发布说明中写明「基于 Mayoi Plus 的修改版」并链接 `LICENSE` 版权声明中所列的原仓库。这是署名请求，不是额外的许可证条款。Capcom、Tolk 和其他第三方内容不因本模组的 MIT 许可而获得授权。

## English

An unofficial accessibility mod for the Windows Steam edition of Phoenix Wright: Ace Attorney Trilogy. It adds screen-reader output for menus, dialogue, the Court Record, and investigations, plus keyboard assistance for investigations and puzzles. Press F1 in-game for help with the current screen.

Mayoi is Maya Fey's given name in Japanese. The “Plus” is a little joke: channeling spirits is one thing, but what if Maya could also help players read menus and find clues?

- Build prerequisites: PowerShell 7, the .NET 10 SDK, your own Steam installation (build `20282248`), and BepInEx `5.4.23.5` Mono x86. Game assemblies are referenced locally and are not distributed with the source.
- Run the build command below from the repository root. `-BepInExPath` is the folder containing `BepInEx/core`. Install `artifacts/release/Pwaat.Accessibility.dll` in the game's `BepInEx/plugins/Pwaat.Accessibility/` folder. Replace the existing DLL when upgrading; do not keep both copies.
- Speech: put a compatible x86 `Tolk.dll` and its matching dependencies in `BepInEx/plugins/Pwaat.Accessibility/native/x86/`. The mod does not pin the Tolk file hash. The author previously tested the custom build from [boz700908/tolk](https://github.com/boz700908/tolk) with the BoyPCReader screen reader. Other builds and screen readers have not been individually verified. Tolk is not bundled with the source.
- Formatting: run `dotnet format whitespace ./src --folder`. The repository workflow checks formatting without requiring game files.

The original mod code is under the [MIT License](LICENSE). Keep the original copyright and license notices when redistributing the source or DLL. When publishing a modified version, please identify it as based on Mayoi Plus and link to the original repository listed in the copyright notice on your download page or in your release notes. This is an attribution request, not an additional license term. The mod's MIT license does not cover Capcom, Tolk, or other third-party material.

## 日本語

Windows 向け Steam 版『逆転裁判123 成歩堂セレクション』の非公式アクセシビリティ MOD です。メニュー、会話、法廷記録、調査の内容をスクリーンリーダーで読み上げ、調査やパズルのキーボード操作を補助します。ゲーム内で F1 キーを押すと、現在の画面のヘルプを確認できます。

Mayoi は綾里真宵の名前から。「Plus」には、霊を呼び出すだけでなく、メニューを読んだり手がかりを探したりするときにも真宵の霊力が役立つ、という遊び心を込めています。

- ビルドに必要なもの：PowerShell 7、.NET 10 SDK、各自の Steam 版ゲーム（ビルド `20282248`）、BepInEx `5.4.23.5` Mono x86。ゲームのアセンブリは各自のインストール先から参照し、ソースには同梱しません。
- リポジトリのルートで下記のコマンドを実行します。`-BepInExPath` には `BepInEx/core` を含むフォルダーを指定してください。生成された `artifacts/release/Pwaat.Accessibility.dll` をゲーム内の `BepInEx/plugins/Pwaat.Accessibility/` に配置します。更新時は既存の DLL を上書きし、複数のコピーを残さないでください。
- 読み上げ：互換性のある x86 版 `Tolk.dll` と対応する依存ファイルを `BepInEx/plugins/Pwaat.Accessibility/native/x86/` に配置してください。この MOD は Tolk のファイルハッシュを固定しません。作者がこれまでに使用したのは [boz700908/tolk](https://github.com/boz700908/tolk) のカスタム版と、スクリーンリーダーの BoyPCReader です。他のビルドやスクリーンリーダーは個別に動作確認していません。Tolk はソースに同梱しません。
- 整形：`dotnet format whitespace ./src --folder` を実行します。リポジトリのワークフローはゲームファイルなしで書式を検査します。

この MOD 独自のコードは [MIT ライセンス](LICENSE) で公開します。ソースや DLL を再配布する際は、元の著作権表示とライセンス文を同梱してください。改変版を公開する際には、Mayoi Plus を元にした改変版であることを明記し、著作権表示に記載の元リポジトリへのリンクをダウンロードページやリリースノートに載せてください。これはクレジット表記のお願いであり、追加のライセンス条件ではありません。カプコン、Tolk などの第三者のコンテンツには、この MOD の MIT ライセンスは適用されません。

## 联系方式 / Contact / 連絡先

- 中文：如遇到读屏、键盘操作或安装问题，请发邮件至 [1909482450@qq.com](mailto:1909482450@qq.com)。
- English: For screen reader, keyboard, or installation issues, email [1909482450@qq.com](mailto:1909482450@qq.com).
- 日本語：読み上げ、キーボード操作、インストールに関する問題は、[1909482450@qq.com](mailto:1909482450@qq.com) までご連絡ください。

## 构建命令 / Build command / ビルドコマンド

```powershell
pwsh -NoProfile -File ./scripts/Build-Mod.ps1 -GamePath '<game directory>' -BepInExPath '<directory containing BepInEx/core>' -ReleaseDirectory ./artifacts/release
```

[Third-party notices / 第三方声明 / 第三者に関する表示](NOTICE.md)
