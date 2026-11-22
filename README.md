# Mayoi Plus（真宵 Plus）

[简体中文](#简体中文) · [繁體中文](#繁體中文) · [English](#english) · [日本語](#日本語)

## 简体中文

适用于 Windows Steam《逆转裁判123 成步堂精选集》的非官方无障碍模组。它为菜单、剧情、法庭档案和调查添加屏幕阅读器朗读，并提供调查与谜题中的键盘操作辅助。游戏内按 F1 可查看当前界面的帮助。

Mayoi 是绫里真宵的名字。Plus 是一个小玩笑：除了召唤灵魂，真宵的灵力还可以帮玩家读菜单、找线索。

这个 Mod 不只是为了让盲人玩家能玩这款游戏，也希望他们能完整体验游戏内容。为此，我们对游戏交互做了一系列无障碍适配。

菜单、剧情对话、选项、庭审、法庭档案、调查、指证、科学搜查、鲁米诺喷洒，以及原本依赖视觉的多种小游戏和谜题，都加入了朗读、音效或键盘操作辅助。希望这些适配能缩小盲人玩家和明眼玩家之间的体验差异，让大家更方便地一起玩、一起聊。

只想安装游玩的话，请到 [Releases 发布页](https://github.com/1999luyi/mayoi-plus/releases) 下载最新的玩家安装 ZIP，不要下载 `Source code` 压缩包。解压后，将 ZIP 内的全部文件和文件夹复制到游戏根目录（`PWAAT.exe` 所在的文件夹）。如果已安装其他模组或加载器，请先检查是否冲突。

复制完成后，从 Steam 启动游戏即可。游戏设置中会新增无障碍选项，可以调整朗读偏好，也可以开关谜题交互的无障碍适配。建议全盲玩家保持谜题适配开启。

带听觉交互的谜题都有配套的可见界面，录制实况或直播时不会只剩声音和黑屏。部分谜题的操作方式与原版不同，建议在录制或直播时向观众说明安装了调整交互的 Mod。

我会持续改进这个 Mod。遇到问题欢迎提交 [Issue](https://github.com/1999luyi/mayoi-plus/issues)；有新点子或改进建议，也可以告诉我，或直接提交 Pull Request。审核通过后，我会合并改动并注明贡献者。

- 构建环境：PowerShell 7、.NET 10 SDK、自备 Steam 游戏（构建号 `20282248`）和 BepInEx `5.4.23.5` Mono x86。游戏程序集仅从本机安装引用，不随源码发布。
- 从仓库根目录运行下文的构建命令；`-BepInExPath` 指向包含 `BepInEx/core` 的目录。产物 `artifacts/release/Pwaat.Accessibility.dll` 放入游戏的 `BepInEx/plugins/Pwaat.Accessibility/`。升级时覆盖同名 DLL，不要另外保留一份。
- 读屏：玩家安装包内含 [boz700908/tolk](https://github.com/boz700908/tolk) 的定制版，通常不必更换。我用这版 Tolk 配合保益悦听做过测试；也可使用该版本支持的其他读屏器，或换用其他兼容的 x86 Tolk，模组不锁定文件哈希。如果从源码构建或需要更换 Tolk，请把 `Tolk.dll` 和对应依赖放入 `BepInEx/plugins/Pwaat.Accessibility/native/x86/`。Tolk 不随源码提供。
- 格式化：运行 `dotnet format whitespace ./src --folder`。仓库工作流检查格式，不需要游戏文件。

本模组免费提供，源码公开，原创部分采用[基于 MIT、附有禁止付费分发条款的许可](LICENSE)。可以使用、修改和免费分享源码或 DLL，但不能出售模组本体或改版，也不能设付费门槛让玩家才能下载。直播、实况和其他视频可以照常获得广告、赞助、订阅或打赏收入；不影响下载的自愿捐助也可以。再分发时须保留原版权声明和完整许可文本。发布改版时，也欢迎注明「基于 Mayoi Plus」并链接原仓库。此许可不改变 Capcom、Tolk 或其他第三方内容的授权，也不撤销旧版本已授予的权利。

## 繁體中文

適用於 Windows Steam 版《逆轉裁判123 成步堂精選集》的非官方無障礙模組。它為選單、劇情、法庭檔案和調查提供螢幕閱讀器朗讀，並支援調查與謎題的鍵盤操作。遊戲內按 F1 可查看目前畫面的說明。

Mayoi 是綾里真宵的名字。Plus 是個小玩笑：除了召喚靈魂，真宵的靈力還能幫玩家讀選單、找線索。

這個 Mod 不只是為了讓盲人玩家能玩這款遊戲，也希望他們能完整體驗遊戲內容。為此，我們針對遊戲操作加入了一系列無障礙設計。

選單、劇情對話、選項、庭審、法庭檔案、調查、指證、科學搜查、魯米諾噴灑，以及原本仰賴視覺的多種小遊戲和謎題，都加入了朗讀、音效或鍵盤操作輔助。希望這些調整能縮小盲人玩家與明眼玩家之間的體驗差異，讓大家更方便地一起玩、一起聊。

只想安裝遊玩的話，請到 [Releases 發布頁](https://github.com/1999luyi/mayoi-plus/releases) 下載最新的玩家安裝 ZIP，不要下載 `Source code` 壓縮檔。解壓縮後，把 ZIP 裡的所有檔案和資料夾複製到遊戲根目錄（`PWAAT.exe` 所在的資料夾）。如果已安裝其他模組或載入器，請先檢查是否會衝突。

複製完成後，從 Steam 啟動遊戲即可。遊戲設定會新增無障礙選項，可調整朗讀偏好，也可開關謎題操作的無障礙適配。建議全盲玩家保持謎題適配開啟。

有聽覺操作設計的謎題都有相應的可見介面，錄製實況或直播時不會只剩聲音和黑畫面。部分謎題的操作方式與原版不同，建議錄製或直播時向觀眾說明已安裝調整操作方式的 Mod。

我會持續改進這個 Mod。遇到問題歡迎提交 [Issue](https://github.com/1999luyi/mayoi-plus/issues)；有新點子或改進建議，也可以告訴我，或直接提交 Pull Request。審查通過後，我會合併變更並註明貢獻者。

- 建置環境：PowerShell 7、.NET 10 SDK、自備 Steam 遊戲（組建編號 `20282248`）和 BepInEx `5.4.23.5` Mono x86。遊戲組件僅從本機安裝引用，不隨原始碼發布。
- 從儲存庫根目錄執行下方的建置指令；`-BepInExPath` 指向包含 `BepInEx/core` 的資料夾。將建置產出的 `artifacts/release/Pwaat.Accessibility.dll` 放入遊戲的 `BepInEx/plugins/Pwaat.Accessibility/`。更新時覆蓋同名 DLL，不要另留一份。
- 螢幕閱讀器：玩家安裝包內含 [boz700908/tolk](https://github.com/boz700908/tolk) 的客製版，通常不必更換。我曾以這版 Tolk 搭配保益悅聽測試；也可使用該版本支援的其他螢幕閱讀器，或更換為其他相容的 x86 Tolk，模組不鎖定檔案雜湊。如果從原始碼建置或需要更換 Tolk，請把 `Tolk.dll` 和相應的相依檔案放入 `BepInEx/plugins/Pwaat.Accessibility/native/x86/`。Tolk 不隨原始碼提供。
- 格式化：執行 `dotnet format whitespace ./src --folder`。儲存庫的工作流程會檢查格式，不需要遊戲檔案。

本模組免費提供，原始碼公開，原創部分採用[以 MIT 為基礎、附有禁止付費散布條款的授權](LICENSE)。可以使用、修改及免費分享原始碼或 DLL，但不得販售模組本體或修改版，也不能要求玩家付費才能下載。直播、實況和其他影片可照常獲得廣告、贊助、訂閱或打賞收入；不影響下載的自願捐助也可以。重新散布時須保留原著作權聲明和完整授權條款。發布修改版時，也歡迎註明「基於 Mayoi Plus」並連結原儲存庫。此授權不改變 Capcom、Tolk 或其他第三方內容的授權，也不撤回舊版本已授予的權利。

## English

An unofficial accessibility mod for the Windows Steam edition of Phoenix Wright: Ace Attorney Trilogy. It adds screen-reader output for menus, dialogue, the Court Record, and investigations, plus keyboard assistance for investigations and puzzles. Press F1 in-game for help with the current screen.

Mayoi is Maya Fey's given name in Japanese. The “Plus” is a little joke: channeling spirits is one thing, but what if Maya could also help players read menus and find clues?

The goal isn't just to make the game playable for blind players. It's to let them experience the game in full, so the mod adapts how players get information and interact with it.

Menus, dialogue, choices, trials, the Court Record, investigations, pointing out details, forensic examinations, luminol spraying, and various minigames and puzzles that originally relied on sight now have speech, sound cues, or keyboard controls. The goal is to narrow the gap between blind and sighted players so they can share and discuss the game more easily.

If you just want to play, get the latest player installation ZIP from [Releases](https://github.com/1999luyi/mayoi-plus/releases), not the `Source code` archive. Extract it and copy every file and folder inside to the game directory containing `PWAAT.exe`. If you already use other mods or mod loaders, check for conflicts first.

Launch the game through Steam. The game's settings will include accessibility options for adjusting speech preferences and turning the puzzle adaptations on or off. If you're playing without sight, I recommend leaving the puzzle adaptations enabled.

Puzzles adapted for play by sound also have visible interfaces, so recordings and streams won't show a blank screen while those puzzles are played. Some puzzles work differently from the original; I recommend telling your viewers that you're using a mod that changes how those puzzles are played.

I'll keep improving this mod. Feel free to [open an issue](https://github.com/1999luyi/mayoi-plus/issues) if you run into a problem. Suggestions and pull requests are welcome. I'll review contributions and credit contributors when their changes are merged.

- Build prerequisites: PowerShell 7, the .NET 10 SDK, your own Steam installation (build `20282248`), and BepInEx `5.4.23.5` Mono x86. Game assemblies are referenced locally and are not distributed with the source.
- Run the build command below from the repository root. `-BepInExPath` is the folder containing `BepInEx/core`. Install `artifacts/release/Pwaat.Accessibility.dll` in the game's `BepInEx/plugins/Pwaat.Accessibility/` folder. Replace the existing DLL when upgrading; do not keep both copies.
- Speech: the player installation ZIP includes the custom build from [boz700908/tolk](https://github.com/boz700908/tolk), so you normally don't need to replace it. I tested this build with BoyPCReader. You can use another screen reader supported by that build, or switch to another compatible x86 Tolk build; the mod doesn't pin the file hash. If you're building from source or replacing Tolk, put `Tolk.dll` and its matching dependencies in `BepInEx/plugins/Pwaat.Accessibility/native/x86/`. Tolk is not bundled with the source.
- Formatting: run `dotnet format whitespace ./src --folder`. The repository workflow checks formatting without requiring game files.

The mod is free to download and its source is available. Original contributions are under an [MIT-based license with a No Paid Distribution condition](LICENSE). You may use, modify, and share the source or DLL for free, but may not sell the mod or modified versions or put access to the download behind a paywall. Livestreams and videos may be monetized with ads, sponsorships, subscriptions, or tips. Voluntary donations that do not gate downloads are also welcome. Keep the original copyright notice and complete license text when redistributing. When publishing a modified version, please credit Mayoi Plus and link to the original repository. This license does not change third-party terms or revoke rights already granted for earlier versions.

## 日本語

Windows 向け Steam 版『逆転裁判123 成歩堂セレクション』の非公式アクセシビリティ MOD です。メニュー、会話、法廷記録、調査の内容をスクリーンリーダーで読み上げ、調査やパズルのキーボード操作を補助します。ゲーム内で F1 キーを押すと、現在の画面のヘルプを確認できます。

Mayoi は綾里真宵の名前から。「Plus」には、霊を呼び出すだけでなく、メニューを読んだり手がかりを探したりするときにも真宵の霊力が役立つ、という遊び心を込めています。

この MOD は、目の見えないプレイヤーが単にゲームを進められるだけでなく、作品の内容をひと通り体験できることを目指しています。そのため、情報の伝え方や操作方法にさまざまな工夫を加えています。

メニュー、シナリオ中の会話、選択肢、法廷でのやり取り、法廷記録、調査、場所の指摘、科学捜査、ルミノール反応の検査に加え、もともと視覚に頼るミニゲームやパズルにも、読み上げや効果音、キーボード操作による補助を加えています。目の見える人と見えない人の体験の差を縮め、一緒に楽しんだり、ゲームについて話したりしやすくすることが目的です。

遊ぶだけなら、[Releases ページ](https://github.com/1999luyi/mayoi-plus/releases) から最新版のプレイヤー向けインストール用 ZIP をダウンロードしてください。`Source code` のアーカイブではありません。展開したすべてのファイルとフォルダーを、`PWAAT.exe` があるゲームフォルダーにコピーしてください。他の MOD や MOD ローダーを導入済みの場合は、先に競合の有無を確認してください。

コピーが終わったら、Steam からゲームを起動してください。ゲームの設定にアクセシビリティ項目が追加され、読み上げの設定や、パズルのアクセシビリティ対応のオン・オフを変更できます。画面を見ずにプレイする場合は、パズルの対応をオンのままにしておくことをおすすめします。

音で遊べるように調整したパズルには、見える画面も用意してあります。録画やライブ配信で、そのパズルの画面が真っ黒になる心配はありません。一部のパズルは原作と操作方法が異なるため、録画や配信では、操作を調整する MOD を使用していることを視聴者に伝えていただけると助かります。

この MOD は今後も改善していきます。不具合があれば [Issue](https://github.com/1999luyi/mayoi-plus/issues) を作成してください。アイデアや改善提案、Pull Request も歓迎します。内容を確認し、変更を取り込む際には貢献者のお名前を記載します。

- ビルドに必要なもの：PowerShell 7、.NET 10 SDK、各自の Steam 版ゲーム（ビルド `20282248`）、BepInEx `5.4.23.5` Mono x86。ゲームのアセンブリは各自のインストール先から参照し、ソースには同梱しません。
- リポジトリのルートで下記のコマンドを実行します。`-BepInExPath` には `BepInEx/core` を含むフォルダーを指定してください。生成された `artifacts/release/Pwaat.Accessibility.dll` をゲーム内の `BepInEx/plugins/Pwaat.Accessibility/` に配置します。更新時は既存の DLL を上書きし、複数のコピーを残さないでください。
- 読み上げ：プレイヤー向けインストール用 ZIP には [boz700908/tolk](https://github.com/boz700908/tolk) のカスタム版が同梱されているので、通常は入れ替える必要はありません。作者はこのビルドを BoyPCReader でテストしました。このビルドが対応する他のスクリーンリーダーや、互換性のある別の x86 版 Tolk も使用できます。この MOD はファイルハッシュを固定しません。ソースからビルドする場合や Tolk を入れ替える場合は、`Tolk.dll` と対応する依存ファイルを `BepInEx/plugins/Pwaat.Accessibility/native/x86/` に置いてください。Tolk はソースに同梱しません。
- 整形：`dotnet format whitespace ./src --folder` を実行します。リポジトリのワークフローはゲームファイルなしで書式を検査します。

この MOD は無料で提供し、ソースコードも公開しています。独自の部分には[有償配布の禁止を追加した MIT ベースのライセンス](LICENSE)を適用します。ソースや DLL は使用、改変、無償配布できますが、MOD 本体や改変版の販売、ダウンロードへの課金は禁止です。ライブ配信や動画での広告、スポンサー、購読、投げ銭による収益化は可能です。ダウンロードの条件としない任意の寄付も可能です。再配布時は元の著作権表示と完全なライセンス文を残してください。改変版を公開する際は Mayoi Plus をクレジットし、元リポジトリへのリンクを載せていただけると助かります。このライセンスは第三者のライセンスを変更せず、旧版ですでに付与された権利を取り消すものでもありません。

## 联系方式 / 聯絡方式 / Contact / 連絡先

### 简体中文联系方式

- 邮箱：[1909482450@qq.com](mailto:1909482450@qq.com)。海外用户如果无法发信到 QQ 邮箱，可以改用 [magicboy-luyi@foxmail.com](mailto:magicboy-luyi@foxmail.com) 或 [luyi1909482450@gmail.com](mailto:luyi1909482450@gmail.com)。
- Discord：`magical boy 卢毅#6383`。
- 中国用户如果方便使用 QQ，可以联系 QQ `1909482450`，也可以加入 QQ 群 `141922968` 或 `715681596`。加一个或两个都可以，群里会不定期分享各种原创游戏和无障碍内容。

### 繁體中文聯絡方式

- 電子郵件：[1909482450@qq.com](mailto:1909482450@qq.com)。海外使用者如果無法寄信到 QQ 信箱，可改寄 [magicboy-luyi@foxmail.com](mailto:magicboy-luyi@foxmail.com) 或 [luyi1909482450@gmail.com](mailto:luyi1909482450@gmail.com)。
- Discord：`magical boy 卢毅#6383`。
- 中國使用者若方便使用 QQ，可以聯絡 QQ `1909482450`，也可以加入 QQ 群 `141922968` 或 `715681596`。加入一個或兩個都可以，群內會不定期分享各種原創遊戲和無障礙內容。

### English contact details

- Email: [1909482450@qq.com](mailto:1909482450@qq.com). If you are outside China and email to the QQ address doesn't go through, try [magicboy-luyi@foxmail.com](mailto:magicboy-luyi@foxmail.com) or [luyi1909482450@gmail.com](mailto:luyi1909482450@gmail.com).
- Discord: `magical boy 卢毅#6383`.
- If you're in China and use QQ, you can contact me at QQ `1909482450` or join QQ group `141922968` or `715681596`. You're welcome to join either or both. The groups occasionally share original games and accessibility content.

### 日本語の連絡先

- メール：[1909482450@qq.com](mailto:1909482450@qq.com)。中国国外から QQ メールに送信できない場合は、[magicboy-luyi@foxmail.com](mailto:magicboy-luyi@foxmail.com) または [luyi1909482450@gmail.com](mailto:luyi1909482450@gmail.com) をお試しください。
- Discord：`magical boy 卢毅#6383`。
- 中国で QQ をお使いの方は、QQ `1909482450` にご連絡いただくか、QQ グループ `141922968` または `715681596` にご参加ください。どちらか一方でも、両方でも構いません。グループではオリジナルゲームやアクセシビリティ関連のコンテンツを不定期に共有しています。

## 构建命令 / 建置指令 / Build command / ビルドコマンド

```powershell
pwsh -NoProfile -File ./scripts/Build-Mod.ps1 -GamePath '<game directory>' -BepInExPath '<directory containing BepInEx/core>' -ReleaseDirectory ./artifacts/release
```

[第三方声明 / 第三方聲明 / Third-party notices / 第三者に関する表示](NOTICE.md)
